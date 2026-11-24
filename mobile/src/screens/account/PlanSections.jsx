import React, { useState } from 'react';
import { RefreshControl, View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { accountApi, planApi } from '@/api/account';
import { ResourceView } from '@/components/states';
import { Badge, Banner, Button, Card, Field, KeyValue, Screen, SectionHeader, T } from '@/components/ui';
import { formatCost, formatDate, formatNumber } from '@/lib/format';
import { isUnlimited, isWellFormedCode, normalizeCode, planFeatureRows, promoReason } from '@/lib/promo';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';

/** A plan feature value for display: unlimited, yes or no, a cost, minutes, or a count. */
export function featureValue(row, t, locale) {
  if (isUnlimited(row.value)) return t('mobile.plan.unlimited');
  if (row.kind === 'flag') return row.value === 'true' ? t('mobile.common.yes') : t('mobile.common.no');
  const n = Number(row.value);
  if (!Number.isFinite(n)) return row.value;
  if (row.kind === 'cost') return formatCost(n, locale);
  if (row.kind === 'minutes') return t('mobile.plan.minutes', { count: n });
  return formatNumber(n, locale);
}

function PlanCard({ data }) {
  const { t, i18n } = useTranslation();
  const plan = data.plan || {};
  const source = data.assignment && data.assignment.source;
  return (
    <Card testID="plan-current">
      <T variant="label">{t('mobile.plan.current')}</T>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm, flexWrap: 'wrap' }}>
        <T variant="title" style={{ flexShrink: 1 }}>{plan.name || plan.key || t('mobile.plan.unknown')}</T>
        {source && source !== 'Default' ? <Badge label={t('mobile.plan.sources.' + source)} tone="primary" /> : null}
      </View>
      {plan.description ? <T variant="muted">{plan.description}</T> : null}
      <T variant="small" testID="plan-ends">
        {data.endsUtc ? t('mobile.plan.endsOn', { date: formatDate(data.endsUtc, i18n.language) }) : t('mobile.plan.noEnd')}
      </T>
    </Card>
  );
}

/** One usage row: "used of limit", or the count alone when the plan has no limit. */
export function usageValue(used, max, format, t) {
  const u = format(used);
  return max === null || max === undefined ? t('mobile.usage.noLimit', { used: u }) : t('mobile.usage.ofLimit', { used: u, max: format(max) });
}

/** This month's usage against the plan (`GET /v1.0/api/account/usage`). Hidden when the server does not offer it. */
export function UsageCard() {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const resource = useResource(() => accountApi.usageMeter(client).catch(() => null), [client]);
  const m = resource.data;
  if (!m || !m.planKey) return null;
  const cost = (v) => formatCost(Number(v) || 0, i18n.language);
  const count = (v) => formatNumber(Number(v) || 0, i18n.language);
  return (
    <>
      <SectionHeader title={t('mobile.usage.title')} />
      <Card testID="plan-usage">
        <KeyValue label={t('mobile.usage.cost')} value={usageValue(m.monthToDateCostUsd, m.monthlyCostCapUsd, cost, t)} />
        <KeyValue label={t('mobile.usage.topics')} value={usageValue(m.topics, m.topicsMax, count, t)} />
        <KeyValue label={t('mobile.usage.runsToday')} value={usageValue(m.runsToday, m.runsDailyMax, count, t)} />
        {m.systemSubscriptions !== undefined ? <KeyValue label={t('mobile.dailyUsage.systemSubscriptions')} value={usageValue(m.systemSubscriptions, m.systemSubscriptionsMax, count, t)} /> : null}
        {m.audioEnabled ? m.audioGeneratedSecondsToday !== undefined ? <>
          <KeyValue label={t('mobile.dailyUsage.audioToday')} value={usageValue(m.audioGeneratedSecondsToday / 60, m.audioDailySecondsMax == null ? null : m.audioDailySecondsMax / 60, count, t)} />
          <T variant="small">{t('mobile.dailyUsage.audioReserved', { minutes: count(m.audioReservedSecondsToday / 60), date: formatDate(m.audioResetUtc, i18n.language) })}</T>
        </> : <KeyValue label={t('mobile.usage.audio')} value={usageValue(m.audioMinutes, m.audioMinutesMax, count, t)} /> : null}
        <T variant="small">{t('mobile.usage.resets', { date: formatDate(m.resetUtc, i18n.language) })}</T>
      </Card>
    </>
  );
}

/** The redeem form: one code field, friendly text for every refusal reason, and the new plan on success. */
export function RedeemForm({ initialCode, onRedeemed }) {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [code, setCode] = useState(normalizeCode(initialCode));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [done, setDone] = useState(null);

  const redeem = async () => {
    setBusy(true);
    setError(null);
    setDone(null);
    try {
      const plan = await planApi.redeem(client, normalizeCode(code));
      setDone(plan);
      setCode('');
      if (onRedeemed) onRedeemed(plan);
    } catch (e) {
      const reason = promoReason(e);
      setError(reason ? t('mobile.promo.reasons.' + reason) : e.message);
    } finally {
      setBusy(false);
    }
  };

  const typed = normalizeCode(code);
  const malformed = typed.length > 0 && !isWellFormedCode(typed);
  return (
    <View style={{ gap: spacing.sm }} testID="redeem-form">
      <Field
        label={t('mobile.promo.code')}
        hint={t('mobile.promo.codeHint')}
        value={code}
        onChangeText={(text) => {
          setCode(text.toUpperCase());
          setError(null);
        }}
        autoCapitalize="characters"
        autoCorrect={false}
        autoComplete="off"
        maxLength={48}
        error={malformed ? t('mobile.promo.malformed') : undefined}
        testID="redeem-code"
        onSubmitEditing={() => (isWellFormedCode(typed) ? void redeem() : undefined)}
        returnKeyType="done"
      />
      {error ? (
        <Banner kind="danger" testID="redeem-error">
          {error}
        </Banner>
      ) : null}
      {done ? (
        <Banner kind="success" title={t('mobile.promo.redeemedTitle')} testID="redeem-done">
          {done.endsUtc ? t('mobile.promo.redeemedUntil', { plan: (done.plan && done.plan.name) || '', date: formatDate(done.endsUtc, i18n.language) }) : t('mobile.promo.redeemed', { plan: (done.plan && done.plan.name) || '' })}
        </Banner>
      ) : null}
      <Button title={t('mobile.promo.redeem')} icon="gift-outline" busy={busy} disabled={!isWellFormedCode(typed)} onPress={() => void redeem()} testID="redeem-submit" />
    </View>
  );
}

/**
 * Plan (plan section 33.2): `GET /v1.0/api/account/plan` shows the effective plan, its limits, when it ends, and the
 * plans that start later, with the redeem form under it.
 */
export function PlanSection() {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const resource = useResource(() => planApi.get(client), [client]);
  return (
    <Screen testID="plan-section" refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <ResourceView resource={resource} feature={t('mobile.plan.title')}>
        {(data) => (
          <>
            <PlanCard data={data} />
            <UsageCard />
            <SectionHeader title={t('mobile.plan.limits')} />
            <Card testID="plan-features">
              {planFeatureRows(data.features).filter(row => row.key !== 'topics.max' || data.features['customTopics.max'] === undefined).map((row) => (
                <KeyValue key={row.key} label={row.label ? t('mobile.plan.features.' + row.label) : t('plans.featureNames.' + row.key.replaceAll('.', '_'), { defaultValue: row.key })} value={featureValue(row, t, i18n.language)} />
              ))}
            </Card>
            {Array.isArray(data.upcoming) && data.upcoming.length ? (
              <>
                <SectionHeader title={t('mobile.plan.upcoming')} />
                <Card testID="plan-upcoming">
                  {data.upcoming.map((a) => (
                    <View key={a.id || a.startsUtc} style={{ gap: 2 }}>
                      <T>{a.planName || t('mobile.plan.sources.' + (a.source || 'Promo'))}</T>
                      <T variant="small">
                        {a.endsUtc
                          ? t('mobile.plan.period', { from: formatDate(a.startsUtc, i18n.language), to: formatDate(a.endsUtc, i18n.language) })
                          : t('mobile.plan.startsOn', { date: formatDate(a.startsUtc, i18n.language) })}
                      </T>
                    </View>
                  ))}
                </Card>
              </>
            ) : null}
          </>
        )}
      </ResourceView>
      <Button title={t('mobile.account.sections.billing')} kind="secondary" onPress={() => router.push('/account/billing')} />
      <Button title={t('mobile.account.sections.providers')} kind="secondary" onPress={() => router.push('/account/providers')} />
      <SectionHeader title={t('mobile.promo.title')} />
      <T variant="muted">{t('mobile.promo.intro')}</T>
      <RedeemForm onRedeemed={(plan) => resource.setData(plan)} />
    </Screen>
  );
}

/**
 * Redeem a code (`POST /v1.0/api/promo-codes/redeem`). Opened from Account or from a link that carries the code
 * (`mymorningnews://redeem?code=...`): the code is filled in and the user confirms.
 */
export function RedeemSection() {
  const { t } = useTranslation();
  const params = useLocalSearchParams();
  const initial = typeof params.code === 'string' ? params.code : '';
  const [plan, setPlan] = useState(null);
  return (
    <Screen testID="redeem-section">
      <T variant="muted">{t('mobile.promo.intro')}</T>
      <RedeemForm initialCode={initial} onRedeemed={setPlan} />
      {plan ? <Button title={t('mobile.promo.seePlan')} kind="secondary" icon="ribbon-outline" onPress={() => router.push('/account/plan')} testID="redeem-see-plan" /> : null}
    </Screen>
  );
}
