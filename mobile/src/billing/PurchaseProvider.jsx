import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { AppState, Platform } from 'react-native';
import { billingApi } from '@/api/billing';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { getIap as nativeIap } from './native';
import { createPurchaseProcessor, productId, purchaseErrorKind, purchaseRequest } from './purchases';

const PurchaseContext = createContext(null);
// Native store connection is process-wide; serialize teardown and reconnect across accounts.
let connectionQueue = Promise.resolve();

/** Account-wide listeners survive leaving the billing screen. Changing server or account resets every binding. */
export function PurchaseProvider({ children, getIap = nativeIap, os = Platform.OS }) {
  const { client, state } = useAuth();
  const { prefs } = usePrefs();
  const key = state.status === 'signedIn' ? [prefs.serverUrl, state.tenantId, state.user?.id].join('|') : null;
  if (!key) return <PurchaseContext.Provider value={null}>{children}</PurchaseContext.Provider>;
  return <SessionPurchases key={key} client={client} getIap={getIap} os={os}>{children}</SessionPurchases>;
}

function SessionPurchases({ children, client, getIap, os }) {
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [event, setEvent] = useState(null);
  const [connection, setConnection] = useState({ status: 'loading', products: [] });
  const [busy, setBusy] = useState(false);
  const [retry, setRetry] = useState(0);
  const operation = useRef(null);
  const processor = useRef(null);
  const iap = useMemo(() => getIap(), [getIap]);
  const alive = useRef(true);
  const loadId = useRef(0);
  const refresh = useCallback(async () => {
    const id = ++loadId.current;
    const next = await billingApi.status(client);
    if (alive.current && id === loadId.current) { setData(next); setError(null); }
    return next;
  }, [client]);
  useEffect(() => {
    alive.current = true;
    const check = () => refresh().catch(e => { if (alive.current) setError(e); });
    check();
    const sub = AppState.addEventListener('change', state => { if (state === 'active') { check(); if (processor.current) void processor.current.recover().catch(e => { if (alive.current) setError(e); }); else setRetry(n => n + 1); } });
    return () => { alive.current = false; sub.remove(); };
  }, [refresh]);
  const available = Boolean(data && (os === 'ios' ? data.appleAvailable : os === 'android' && data.googleAvailable));
  const skuKey = JSON.stringify([...new Set((data?.offers || []).map(o => productId(o, os)).filter(Boolean))].sort());
  useEffect(() => {
    if (!available || !iap) return undefined;
    let active = true;
    let connected = false;
    let current;
    const subscriptions = [];
    const ids = JSON.parse(skuKey);
    async function connect() {
      try {
        await iap.initConnection(); connected = true;
        if (!active) return;
        current = createPurchaseProcessor({ client, iap, os, productIds: ids, refresh, emit: next => { if (active) { setEvent(next); if (!['verifying'].includes(next.kind) && operation.current !== 'restore') { operation.current = null; setBusy(false); } } } });
        processor.current = current;
        subscriptions.push(iap.purchaseUpdatedListener(p => { void current.process(p); }));
        subscriptions.push(iap.purchaseErrorListener(e => { if (active) { setEvent({ kind: purchaseErrorKind(e), error: e }); if (operation.current !== 'restore') { operation.current = null; setBusy(false); } } }));
        const products = ids.length ? await iap.fetchProducts({ skus: ids, type: 'subs' }) : [];
        if (active) setConnection({ status: 'ready', products: products || [], skuKey });
        if (active) await current.recover();
      } catch (e) { if (active) { setError(e); setConnection({ status: 'unavailable', products: [], skuKey }); } }
    }
    connectionQueue = connectionQueue.catch(() => {}).then(connect);
    return () => {
      active = false; current?.stop();
      if (processor.current === current) processor.current = null;
      for (const s of subscriptions) s.remove();
      connectionQueue = connectionQueue.catch(() => {}).then(async () => { if (connected) await iap.endConnection(); }).catch(() => {});
    };
  }, [available, iap, skuKey, client, os, refresh, retry]);

  async function buy(offer) {
    if (operation.current || connection.status !== 'ready' || connection.skuKey !== skuKey) return;
    operation.current = 'buy';
    setBusy(true); setError(null); setEvent(null);
    try {
      const latest = await refresh();
      const selected = latest.offers.find(o => o.key === offer.key);
      if (!latest.enabled || !selected || hasSubscription(latest.subscriptions)) throw new Error('A new subscription is not available. Refresh your plan or manage the existing subscription.');
      const product = connection.products.find(p => p.id === productId(selected, os));
      await iap.requestPurchase(purchaseRequest(selected, product, latest.purchaseAccountToken, os));
      // The store listener, not requestPurchase's return value, reports payment outcome.
    } catch (e) { setEvent({ kind: purchaseErrorKind(e), error: e }); if (operation.current !== 'restore') { operation.current = null; setBusy(false); } }
  }
  async function restore() {
    if (!processor.current || operation.current) return;
    operation.current = 'restore';
    setBusy(true); setError(null); setEvent(null);
    try {
      const results = await processor.current.restore();
      if (!results.some(r => r.error || r.pending)) setEvent({ kind: results.some(r => r.verified) ? 'verified' : 'empty' });
    } catch (e) { setError(e); }
    finally { operation.current = null; setBusy(false); }
  }
  const status = !data ? 'loading' : !available || !iap ? 'unavailable' : connection.skuKey === skuKey ? connection.status : 'loading';
  return <PurchaseContext.Provider value={{ data, error, event, busy, status, products: connection.skuKey === skuKey ? connection.products : [], os, refresh: async () => { const result = await refresh(); if (status !== 'ready') setRetry(n => n + 1); return result; }, buy, restore, manage: () => iap?.deepLinkToSubscriptions() }}>{children}</PurchaseContext.Provider>;
}

/** Block duplicate subscriptions while any provider has an unexpired or suspended contract. */
export function hasSubscription(subscriptions) {
  return (subscriptions || []).some(s => ['Active', 'Grace', 'Suspended'].includes(s.status) || (s.status === 'Canceled' && new Date(s.endsUtc) > new Date()));
}

export function usePurchases() { return useContext(PurchaseContext); }
