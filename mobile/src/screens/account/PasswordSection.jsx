import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { accountApi } from '@/api/account';
import { fieldErrors } from '@/api/errors';
import { Banner, Button, Field, Screen, T } from '@/components/ui';
import { useAuth } from '@/state/AuthContext';

/** The server's minimum password length (UserRoutes.MinimumPasswordLength). */
export const MIN_PASSWORD_LENGTH = 8;

/** Client checks before `PUT /v1.0/api/me/password`; the server checks again. Returns an i18n key or null. */
export function passwordProblem(current, next, confirm) {
  if (!current) return 'mobile.password.errorCurrent';
  if (!next || next.length < MIN_PASSWORD_LENGTH) return 'mobile.password.errorShort';
  if (next !== confirm) return 'mobile.password.errorMismatch';
  if (next === current) return 'mobile.password.errorSame';
  return null;
}

export function PasswordSection() {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [status, setStatus] = useState(null);
  const [error, setError] = useState(null);

  const save = async () => {
    const problem = passwordProblem(current, next, confirm);
    if (problem) {
      setError(t(problem, { min: MIN_PASSWORD_LENGTH }));
      return;
    }
    setStatus('saving');
    setError(null);
    try {
      await accountApi.changePassword(client, current, next);
      setStatus('saved');
      setCurrent('');
      setNext('');
      setConfirm('');
    } catch (e) {
      const fields = fieldErrors(e);
      setError(fields.currentPassword ? t('mobile.password.errorWrongCurrent') : e.message);
      setStatus(null);
    }
  };

  return (
    <Screen testID="password-section">
      <T variant="muted">{t('mobile.password.intro')}</T>
      <Field label={t('mobile.password.current')} value={current} onChangeText={setCurrent} secureTextEntry autoComplete="current-password" textContentType="password" />
      <Field label={t('mobile.password.new')} value={next} onChangeText={setNext} secureTextEntry autoComplete="new-password" textContentType="newPassword" hint={t('mobile.password.rule', { min: MIN_PASSWORD_LENGTH })} />
      <Field label={t('mobile.password.confirm')} value={confirm} onChangeText={setConfirm} secureTextEntry autoComplete="new-password" textContentType="newPassword" />
      {error ? <Banner kind="danger">{error}</Banner> : null}
      {status === 'saved' ? <Banner kind="success">{t('mobile.password.saved')}</Banner> : null}
      <Button title={t('mobile.password.submit')} busy={status === 'saving'} onPress={() => void save()} />
    </Screen>
  );
}
