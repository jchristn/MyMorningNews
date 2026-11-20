import React, { useState } from 'react';
import { RefreshControl } from 'react-native';
import { useTranslation } from 'react-i18next';
import { emailPreferencesApi } from '@/api/account';
import { ResourceView } from '@/components/states';
import { Banner, Card, KeyValue, Screen, SwitchRow, T } from '@/components/ui';
import { useAuth } from '@/state/AuthContext';
import { useServer } from '@/state/ServerContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';

/** The email categories a member can turn on or off (EmailPreferencesRequest). */
export const EMAIL_CATEGORIES = ['runEmails', 'briefingEmails', 'failureEmails'];

/**
 * Email preferences (plan section 23.7): `GET/PUT /v1.0/api/email-preferences`. Run digests, briefings, and run
 * failure emails on or off for the account's address; each switch saves at once. Per-topic unsubscribes made from
 * email links are counted here.
 */
export function EmailSection() {
  const p = useTheme();
  const { t } = useTranslation();
  const { client } = useAuth();
  const { config } = useServer();
  const resource = useResource(() => emailPreferencesApi.get(client), [client]);
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  const off = Boolean(config && config.emailEnabled === false);

  const toggle = async (key, value) => {
    setBusy(key);
    setError(null);
    const before = resource.data;
    resource.setData((d) => ({ ...d, [key]: value }));
    try {
      const updated = await emailPreferencesApi.update(client, { [key]: value });
      if (updated && typeof updated === 'object') resource.setData(updated);
    } catch (e) {
      resource.setData(before);
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };

  return (
    <Screen testID="email-section" refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <T variant="muted">{t('mobile.email.intro')}</T>
      {off ? <Banner kind="info">{t('mobile.email.serverOff')}</Banner> : null}
      <ResourceView resource={resource} feature={t('mobile.email.title')}>
        {(prefs) => (
          <>
            <Card>
              <KeyValue label={t('mobile.email.address')} value={prefs.address || t('mobile.email.noAddress')} selectable />
            </Card>
            <Card>
              {EMAIL_CATEGORIES.map((key) => (
                <SwitchRow key={key} testID={'email-' + key} title={t('mobile.email.categories.' + key)} detail={t('mobile.email.details.' + key)} value={Boolean(prefs[key])} disabled={busy === key || !prefs.address} onValueChange={(v) => void toggle(key, v)} />
              ))}
            </Card>
            {Array.isArray(prefs.unsubscribedTopicIds) && prefs.unsubscribedTopicIds.length ? (
              <T variant="small" testID="email-topics-off">
                {t('mobile.email.topicsOff', { count: prefs.unsubscribedTopicIds.length })}
              </T>
            ) : null}
            {error ? <Banner kind="danger">{error}</Banner> : null}
          </>
        )}
      </ResourceView>
    </Screen>
  );
}
