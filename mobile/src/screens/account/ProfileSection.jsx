import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { accountApi } from '@/api/account';
import { fieldErrors } from '@/api/errors';
import { ResourceView } from '@/components/states';
import { Banner, Button, Field, KeyValue, Card, Screen } from '@/components/ui';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';

/** Profile: `GET /v1.0/api/me` and `PUT /v1.0/api/me` (first name, last name, time zone, locale; email is read-only). */
export function ProfileSection() {
  const { client } = useAuth();
  const me = useResource(() => accountApi.getMe(client), [client]);
  return (
    <Screen testID="profile-section">
      <ResourceView resource={me}>{(user) => <ProfileForm key={user.id + (user.lastUpdateUtc || '')} user={user} />}</ResourceView>
    </Screen>
  );
}

function ProfileForm({ user }) {
  const { t } = useTranslation();
  const { auth, client } = useAuth();
  const [form, setForm] = useState({ firstName: user.firstName || '', lastName: user.lastName || '', timeZone: user.timeZone || '', locale: user.locale || '' });
  const [status, setStatus] = useState(null);
  const [errors, setErrors] = useState({});

  const save = async () => {
    setStatus('saving');
    setErrors({});
    try {
      const body = { firstName: form.firstName.trim(), lastName: form.lastName.trim(), timeZone: form.timeZone.trim() || null, locale: form.locale.trim() || null };
      const updated = await accountApi.updateMe(client, body);
      auth.setUser(updated && typeof updated === 'object' && updated.id ? updated : { ...user, ...body });
      setStatus('saved');
    } catch (error) {
      setErrors({ ...fieldErrors(error), form: error.message });
      setStatus('failed');
    }
  };
  const set = (key) => (value) => {
    setForm({ ...form, [key]: value });
    setStatus(null);
  };

  return (
    <>
      <Card>
        <KeyValue label={t('mobile.profile.email')} value={user.email} selectable />
        <KeyValue label={t('mobile.profile.tenant')} value={user.tenantId} selectable />
      </Card>
      <Field label={t('mobile.profile.firstName')} value={form.firstName} onChangeText={set('firstName')} autoComplete="given-name" error={errors.firstName} testID="profile-first-name" />
      <Field label={t('mobile.profile.lastName')} value={form.lastName} onChangeText={set('lastName')} autoComplete="family-name" error={errors.lastName} />
      <Field label={t('mobile.profile.timeZone')} hint={t('mobile.profile.timeZoneHint')} value={form.timeZone} onChangeText={set('timeZone')} autoCapitalize="none" autoCorrect={false} error={errors.timeZone} />
      <Field label={t('mobile.profile.locale')} hint={t('mobile.profile.localeHint')} value={form.locale} onChangeText={set('locale')} autoCapitalize="none" autoCorrect={false} error={errors.locale} />
      {status === 'saved' ? <Banner kind="success">{t('mobile.profile.saved')}</Banner> : null}
      {status === 'failed' && errors.form ? <Banner kind="danger">{errors.form}</Banner> : null}
      <Button title={t('mobile.common.save')} busy={status === 'saving'} onPress={() => void save()} testID="profile-save" />
    </>
  );
}
