// (c) 2026 Lattice AI LLC
// Local dependency simulation. Control routes are test fixtures, never production Stripe APIs.
const http = require('http');
const crypto = require('crypto');
let serial = Promise.resolve();
function read(req) {
  if (req.body !== undefined) return Promise.resolve(req.body);
  return new Promise((resolve, reject) => {
    let body = '';
    req.on('data', chunk => { body += chunk; });
    req.on('end', () => resolve(body));
    req.on('error', reject);
  });
}
const server = http.createServer(async (req, res) => {
  const incoming = await read(req);
  const url = new URL(req.url, 'http://127.0.0.1');
  const path = url.pathname;
  const reply = (status, body) => { res.writeHead(status, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(body)); };
  const task = async () => {
    const raw = typeof incoming === 'string' ? incoming : JSON.stringify(incoming || {});
    const form = typeof incoming === 'object' ? incoming : Object.fromEntries(new URLSearchParams(raw));
    const state = await pm.state.get('stripe.billing') || { sessions: {}, subscriptions: {}, coupons: {}, failAfterWrite: false, failures: false };
    if (path === '/__control' && req.method === 'POST') {
      const command = typeof incoming === 'object' ? incoming : JSON.parse(raw || '{}');
      if (command.reset) { await pm.state.set('stripe.billing', { sessions: {}, subscriptions: {}, coupons: {}, failAfterWrite: !!command.failAfterWrite, failures: false }); return reply(200, { reset: true }); }
      if (command.subscriptionId) Object.assign(state.subscriptions[command.subscriptionId], command.patch || {});
      if (command.failures !== undefined) state.failures = command.failures;
      await pm.state.set('stripe.billing', state);
      return reply(200, { updated: true });
    }
    if (path === '/__state') return reply(200, state);
    if (req.headers.authorization !== 'Bearer sk_test_simulation') return reply(401, { error: { type: 'authentication_error' } });
    if (state.failures) return reply(503, { error: { type: 'api_error' } });
    if (req.method === 'POST' && path === '/v1/coupons') {
      const key = req.headers['idempotency-key'];
      if (!key || !(Number(form.percent_off) > 0 && Number(form.percent_off) <= 100) || form.max_redemptions !== '1') return reply(400, { error: { type: 'invalid_request_error' } });
      if (state.coupons[key] && state.coupons[key].form !== raw) return reply(400, { error: { type: 'idempotency_error' } });
      state.coupons[key] ||= { id: 'coupon_' + crypto.createHash('sha256').update(key).digest('hex').slice(0, 18), form: raw };
      await pm.state.set('stripe.billing', state);
      return reply(200, state.coupons[key]);
    }
    if (req.method === 'POST' && path === '/v1/checkout/sessions') {
      const key = req.headers['idempotency-key'];
      if (!key || form.mode !== 'subscription' || form['line_items[0][quantity]'] !== '1' || !form['line_items[0][price]'] || !form['subscription_data[metadata][userId]'] || Object.keys(form).some(k => k.includes('trial'))) return reply(400, { error: { type: 'invalid_request_error' } });
      if (state.sessions[key]) {
        if (state.sessions[key].form !== raw) return reply(400, { error: { type: 'idempotency_error' } });
        return reply(200, state.sessions[key].response);
      }
      const suffix = crypto.createHash('sha256').update(key).digest('hex').slice(0, 18);
      const id = 'sub_' + suffix;
      const response = { id: 'cs_test_' + suffix, object: 'checkout.session', url: 'https://checkout.stripe.com/c/pay/cs_test_' + suffix, subscription: id, payment_status: 'unpaid', status: 'open', expires_at: Number(form.expires_at) };
      state.sessions[key] = { form: raw, response };
      state.subscriptions[id] = { id, object: 'subscription', customer: 'cus_' + suffix, livemode: false, status: 'incomplete', cancel_at_period_end: false, metadata: { userId: form['subscription_data[metadata][userId]'], signupCodeId: form['subscription_data[metadata][signupCodeId]'] }, items: { data: [{ price: { id: form['line_items[0][price]'] }, current_period_start: Math.floor(Date.now()/1000), current_period_end: Math.floor(Date.now()/1000) + 2592000 }] } };
      const fail = state.failAfterWrite; state.failAfterWrite = false;
      await pm.state.set('stripe.billing', state);
      return reply(fail ? 500 : 200, fail ? { error: { type: 'api_error' } } : response);
    }
    if (req.method === 'GET' && path.startsWith('/v1/subscriptions/')) {
      const value = state.subscriptions[path.split('/').pop()];
      if (!value) return reply(404, { error: { type: 'invalid_request_error', code: 'resource_missing' } });
      const suffix = value.id.slice(4);
      return reply(200, { ...value, latest_invoice: { id: 'in_' + suffix, customer: value.customer, livemode: false, status: value.invoiceStatus || 'paid', amount_due: value.zeroInvoice ? 0 : 1000, amount_paid: value.zeroInvoice ? 0 : 1000 } });
    }
    const subscription = Object.values(state.subscriptions).find(s => ['in_', 'pi_', 'ch_'].some(prefix => path.endsWith('/' + prefix + s.id.slice(4)) || [...url.searchParams.values()].includes(prefix + s.id.slice(4))));
    if (subscription) {
      const suffix = subscription.id.slice(4);
      const charge = { id: 'ch_' + suffix, object: 'charge', customer: subscription.customer, livemode: false, paid: true, captured: true, amount_captured: 1000, amount_refunded: subscription.refunded || 0, disputed: !!subscription.dispute };
      if (path === '/v1/invoice_payments') return reply(200, { data: [{ id: 'inpay_' + suffix, invoice: 'in_' + suffix, status: 'paid', amount_paid: 1000, payment: { type: 'payment_intent', payment_intent: 'pi_' + suffix } }], has_more: false });
      if (path.startsWith('/v1/payment_intents/')) return reply(200, { id: 'pi_' + suffix, status: 'succeeded', latest_charge: charge });
      if (path.startsWith('/v1/charges/')) return reply(200, charge);
      if (path === '/v1/disputes') return reply(200, { data: subscription.dispute ? [{ id: 'du_' + suffix, status: subscription.dispute, charge: charge.id }] : [], has_more: false });
    }
    if (req.method === 'POST' && path === '/v1/billing_portal/sessions') {
      if (!Object.values(state.subscriptions).some(s => s.customer === form.customer)) return reply(404, { error: { type: 'invalid_request_error' } });
      return reply(200, { id: 'bps_simulation', url: 'https://billing.stripe.com/p/session/simulation' });
    }
    return reply(501, { error: { type: 'simulation_not_implemented', path } });
  };
  serial = serial.then(task).catch(() => reply(500, { error: { type: 'simulation_error' } }));
});
server.listen(process.env.PORT || 12193);
