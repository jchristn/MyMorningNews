#!/usr/bin/env python3
"""Provision only My Morning News Stripe resources; never changes account defaults.

Supply STRIPE_SECRET_KEY in the environment. --output is a private, untracked JSON
file containing provider IDs and the webhook secret. The webhook is created disabled
and must be enabled after the receiving deployment passes its checks.
(c) 2026 Lattice AI LLC
"""
import argparse
import json
import os
from pathlib import Path
import requests

APP = 'mymorningnews'
VERSION = '2025-09-30.clover'


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True)
    parser.add_argument('--origin', default='https://app.mymorningnews.ai')
    args = parser.parse_args()
    output = Path(args.output).expanduser().resolve()
    repository = Path(__file__).resolve().parents[2]
    if output.is_relative_to(repository):
        raise SystemExit('Private output must be outside the repository.')
    key = os.environ['STRIPE_SECRET_KEY']
    if not key.startswith(('sk_test_', 'sk_live_', 'rk_test_', 'rk_live_')):
        raise SystemExit('A Stripe server key is required.')
    session = requests.Session()
    session.auth = (key, '')
    session.headers['Stripe-Version'] = VERSION

    def call(method, path, data=None, identity=None):
        headers = {'Idempotency-Key': APP + ':platform-v1:' + identity} if identity else {}
        response = session.request(method, 'https://api.stripe.com/v1/' + path, params=data if method == 'GET' else None,
                                   data=data if method != 'GET' else None, headers=headers, timeout=40)
        if not response.ok:
            error = response.json().get('error', {})
            raise RuntimeError(f"Stripe {method} {path}: HTTP {response.status_code}; {error.get('type')} {error.get('param')} {error.get('message')}")
        return response.json()

    def all_rows(path):
        rows = []
        query = {'limit': 100}
        while True:
            page = call('GET', path, query)
            rows.extend(page['data'])
            if not page.get('has_more'):
                return rows
            query['starting_after'] = page['data'][-1]['id']

    result = json.loads(output.read_text()) if output.exists() else {}
    result['environment'] = 'Production' if '_live_' in key else 'Sandbox'
    result.setdefault('offers', {})

    def save():
        output.parent.mkdir(parents=True, exist_ok=True)
        descriptor = os.open(output, os.O_WRONLY | os.O_CREAT | os.O_TRUNC, 0o600)
        os.chmod(output, 0o600)
        with os.fdopen(descriptor, 'w') as file:
            json.dump(result, file, indent=2)
            file.write('\n')

    products = all_rows('products')
    prices = all_rows('prices')
    packages = []
    for count, monthly, yearly, audio in [(5, 1000, 10000, 30), (20, 3000, 30000, 100)]:
        plan = f'paid-{count}'
        matches = [p for p in products if p.get('metadata', {}).get('app') == APP and p.get('metadata', {}).get('planKey') == plan]
        if len(matches) > 1:
            raise RuntimeError('Duplicate My Morning News products require reconciliation.')
        product = matches[0] if matches else call('POST', 'products', {
            'name': f'My Morning News {count}', 'description': f'{count} custom briefings, system catalog subscriptions, and {audio} generated audio minutes per UTC day.',
            'metadata[app]': APP, 'metadata[planKey]': plan}, 'product:' + plan)
        if not product['active']:
            raise RuntimeError('Existing product is archived; refusing to reactivate it implicitly.')
        product_prices = []
        for interval, amount in [('month', monthly), ('year', yearly)]:
            offer = plan + '-' + interval
            lookup = APP + '_' + offer.replace('-', '_') + '_usd_v1'
            matching = [p for p in prices if p.get('lookup_key') == lookup]
            price = matching[0] if matching else call('POST', 'prices', {
                'product': product['id'], 'currency': 'usd', 'unit_amount': amount,
                'recurring[interval]': interval, 'lookup_key': lookup,
                'metadata[app]': APP, 'metadata[offerKey]': offer}, 'price:' + offer)
            if not price['active'] or price['unit_amount'] != amount or price['product'] != product['id'] or price['currency'] != 'usd' or price['recurring']['interval'] != interval:
                raise RuntimeError('Existing price differs from the approved offer.')
            result['offers'][offer] = {'productId': product['id'], 'priceId': price['id'], 'usdCents': amount, 'interval': interval, 'planKey': plan}
            product_prices.append(price['id'])
            save()
        packages.append((product['id'], product_prices))

    configs = all_rows('billing_portal/configurations')
    config = next((c for c in configs if c.get('metadata', {}).get('app') == APP), None)
    if config is None:
        data = {'business_profile[headline]': 'Manage your My Morning News subscription',
                'business_profile[privacy_policy_url]': 'https://mymorningnews.ai/privacy',
                'business_profile[terms_of_service_url]': 'https://mymorningnews.ai/terms',
                'default_return_url': args.origin.rstrip('/') + '/dashboard/account/billing',
                'features[invoice_history][enabled]': 'true', 'features[payment_method_update][enabled]': 'true',
                'features[subscription_cancel][enabled]': 'true', 'features[subscription_cancel][mode]': 'at_period_end',
                'features[subscription_update][enabled]': 'true', 'features[subscription_update][default_allowed_updates][0]': 'price',
                'features[subscription_update][proration_behavior]': 'always_invoice', 'metadata[app]': APP}
        for index, (product, amounts) in enumerate(packages):
            data[f'features[subscription_update][products][{index}][product]'] = product
            for price_index, price in enumerate(amounts):
                data[f'features[subscription_update][products][{index}][prices][{price_index}]'] = price
        config = call('POST', 'billing_portal/configurations', data, 'portal')
    elif config.get('default_return_url') != args.origin.rstrip('/') + '/dashboard/account/billing':
        config = call('POST', 'billing_portal/configurations/' + config['id'], {'default_return_url': args.origin.rstrip('/') + '/dashboard/account/billing'})
    result['portalConfigurationId'] = config['id']
    save()
    webhook_url = args.origin.rstrip('/') + '/v1.0/public/billing/stripe'
    endpoint = next((w for w in all_rows('webhook_endpoints') if w['url'] == webhook_url), None)
    if endpoint is None:
        events = ['checkout.session.completed', 'checkout.session.expired', 'customer.subscription.created', 'customer.subscription.updated',
                  'customer.subscription.deleted', 'invoice.paid', 'invoice.payment_failed', 'charge.refunded',
                  'charge.dispute.created', 'charge.dispute.updated', 'charge.dispute.closed', 'charge.dispute.funds_reinstated']
        data = {'url': webhook_url, 'api_version': VERSION, 'description': 'My Morning News verified subscription lifecycle', 'metadata[app]': APP}
        data.update({f'enabled_events[{i}]': event for i, event in enumerate(events)})
        endpoint = call('POST', 'webhook_endpoints', data, 'webhook')
        result['webhookSecret'] = endpoint['secret']
        result['webhookId'] = endpoint['id']
        save()
        call('POST', 'webhook_endpoints/' + endpoint['id'], {'disabled': 'true'})
    result['webhookId'] = endpoint['id']
    if not result.get('webhookSecret'):
        raise RuntimeError('Existing endpoint secret is not in the private output file; recover it from Stripe before deployment.')
    save()
    print(f"{result['environment']}: verified 2 products, 4 prices, application-specific portal and webhook. Private configuration saved.")


if __name__ == '__main__':
    main()
