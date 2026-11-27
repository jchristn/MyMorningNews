import React, { useState } from 'react';
import { Linking, RefreshControl, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { hasSubscription, usePurchases } from '@/billing/PurchaseProvider';
import { capturedDiscount, productId, storePrice, webCheckoutPolicy } from '@/billing/purchases';
import { LoadingState } from '@/components/states';
import { Banner, Button, Card, Choice, Screen, T } from '@/components/ui';
import { useServer } from '@/state/ServerContext';
import { spacing } from '@/theme/palette';

/** Native purchases, restore and store management. Access and usage always come from the server. */
export function BillingSection() {
  const { t } = useTranslation();
  const billing = usePurchases();
  const { links } = useServer();
  const [interval, setInterval] = useState('month');
  const [localError, setLocalError] = useState(null);
  const b = key => t('mobile.billing.' + key);
  if (!billing) return <LoadingState />;
  const { data, products, os, storefront, status, busy, event, error } = billing;
  const policy = webCheckoutPolicy(os, storefront);
  const active = hasSubscription(data?.subscriptions);
  const nativeProvider = os === 'ios' ? 'Apple' : 'Google';
  const perform = async action => { setLocalError(null); try { await action(); } catch (e) { setLocalError(e); } };
  return <Screen testID="billing-section" refreshControl={<RefreshControl refreshing={false} onRefresh={() => void perform(billing.refresh)} />}>
    <T variant="muted">{b('summary')}</T>
    {error || localError ? <Banner kind="danger">{(localError || error).message}</Banner> : null}
    {!data && !error ? <LoadingState /> : null}
    {data ? <>
      <Card><T variant="title">{data.plan?.plan?.name}</T>{!data.plan?.subscriptionRequired ? <T variant="muted">{b('nonBilled')}</T> : null}</Card>
      {data.subscriptions?.map(sub => <Card key={sub.id}>
        <T>{sub.provider} · {t('mobile.nativeBilling.status.' + sub.status, { defaultValue: sub.status })}</T>
        {sub.provider === nativeProvider ? <Button kind="secondary" title={b('manage')} disabled={status !== 'ready'} onPress={() => void perform(billing.manage)} /> : <T variant="small">{t('mobile.billing.manageStore', { provider: sub.provider })}</T>}
      </Card>)}
      {event && event.kind !== 'cancelled' ? <Banner kind={event.kind === 'error' ? 'danger' : event.kind === 'verified' ? 'success' : 'info'} testID="billing-event">
        {event.kind === 'error' ? event.error?.message : t('mobile.nativeBilling.' + (event.kind === 'webCheckout' ? 'webCheckoutOpened' : event.kind))}
      </Banner> : null}
      {!data.enabled ? <T variant="muted">{b('unavailable')}</T> : null}
      {status === 'unavailable' ? <T variant="muted">{t('mobile.nativeBilling.unavailable')}</T> : null}
      <Choice accessibilityLabel={b('interval')} value={interval} onChange={setInterval} options={[{ value: 'month', label: b('monthly') }, { value: 'year', label: b('yearly') }]} />
      {data.offers?.filter(o => String(o.interval).toLowerCase() === interval).map(offer => {
        const product = products.find(p => p.id === productId(offer, os));
        const price = storePrice(product, offer, os);
        // A captured discount must not silently become a full-price store purchase. Where the storefront allows it,
        // the discount is redeemed through web checkout instead (APP_STORE_SUBMISSION.md section 3.1).
        const discount = capturedDiscount(data.signupBenefit, offer);
        const percent = discount ? Number(data.signupBenefit.discountPercent) : 0;
        const web = discount && data.enabled && offer.webAvailable;
        return <Card key={offer.key} testID={'billing-offer-' + offer.key}>
          <T variant="title">{offer.planName}</T>
          <T variant="heading">{price || t('mobile.nativeBilling.priceUnavailable')} {price ? b(interval === 'year' ? 'perYear' : 'perMonth') : ''}</T>
          <T>{t('mobile.billing.customTopics', { count: offer.customTopics === 'unlimited' ? t('mobile.plan.unlimited') : offer.customTopics })}</T>
          <T>{t('mobile.billing.systemTopics', { count: offer.systemSubscriptions === 'unlimited' ? t('mobile.plan.unlimited') : offer.systemSubscriptions })}</T>
          <T>{t('mobile.billing.audioDaily', { minutes: offer.dailyAudioSeconds === 'unlimited' ? t('mobile.plan.unlimited') : Number(offer.dailyAudioSeconds) / 60 })}</T>
          {!discount ? <Button title={t('mobile.nativeBilling.subscribe')} busy={busy} disabled={!data.enabled || status !== 'ready' || !price || active} onPress={() => void billing.buy(offer)} testID={'billing-buy-' + offer.key} />
            : web && policy === 'link' ? <>
              <T variant="small">{t('mobile.nativeBilling.webDiscountLink', { percent })}</T>
              <Button title={t('mobile.nativeBilling.webCheckout', { percent })} busy={busy} disabled={active} onPress={() => void billing.webCheckout(offer)} testID={'billing-web-' + offer.key} />
            </>
            : web && policy === 'inform' ? <T variant="small" testID={'billing-web-info-' + offer.key}>{t('mobile.nativeBilling.webDiscountInfo', { percent })}</T>
            : <T variant="small" testID={'billing-discount-' + offer.key}>{t('mobile.nativeBilling.offerUnavailable')}</T>}
        </Card>;
      })}
      <T variant="small">{b('terms')}</T>
      <Button title={t('mobile.nativeBilling.restore')} kind="secondary" busy={busy} disabled={status !== 'ready'} onPress={() => void billing.restore()} testID="billing-restore" />
      <Button title={t('mobile.common.retry')} kind="ghost" onPress={() => void perform(billing.refresh)} />
      <View style={{ gap: spacing.sm }}>
        <Button title={t('mobile.legal.terms')} kind="ghost" onPress={() => void perform(() => Linking.openURL(links.terms))} />
        <Button title={t('mobile.legal.privacy')} kind="ghost" onPress={() => void perform(() => Linking.openURL(links.privacy))} />
      </View>
    </> : null}
  </Screen>;
}
