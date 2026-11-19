import React, { useState } from 'react';
import { File, Paths } from 'expo-file-system';
import * as Sharing from 'expo-sharing';
import { useTranslation } from 'react-i18next';
import { accountApi } from '@/api/account';
import { isNotAvailable } from '@/api/errors';
import { routes } from '@/api/routes';
import { Banner, Button, Field, Screen, T } from '@/components/ui';
import { useAuth } from '@/state/AuthContext';
import { DeleteOnWebButton } from './InfoSections';
import { useSignOut } from './useSignOut';

/** The word the user types to confirm deletion. Not translated, so it is the same in every language. */
export const DELETE_CONFIRMATION = 'DELETE';

/**
 * Delete account (plan section 33.1, contract E): `POST /v1.0/api/account/delete`. The server signs out every session,
 * keeps a 7-day grace period (signing in again cancels it), and emails a confirmation. Until the route exists the
 * screen says so and offers the web page.
 */
export function DeleteSection() {
  const { t } = useTranslation();
  const { client } = useAuth();
  const signOut = useSignOut();
  const [confirm, setConfirm] = useState('');
  const [password, setPassword] = useState('');
  const [status, setStatus] = useState(null);
  const [error, setError] = useState(null);

  const remove = async () => {
    setStatus('deleting');
    setError(null);
    try {
      await accountApi.deleteAccount(client, { confirm: true, password: password || undefined });
      setStatus('deleted');
      await signOut();
    } catch (e) {
      if (isNotAvailable(e)) setStatus('unavailable');
      else {
        setError(e.message);
        setStatus(null);
      }
    }
  };

  return (
    <Screen testID="delete-section">
      <T variant="title">{t('mobile.delete.title')}</T>
      <T variant="muted">{t('mobile.delete.body')}</T>
      {status === 'unavailable' ? (
        <>
          <Banner kind="info" title={t('mobile.states.notAvailableTitle')}>
            {t('mobile.delete.notAvailable')}
          </Banner>
          <DeleteOnWebButton />
        </>
      ) : (
        <>
          <Field label={t('mobile.delete.password')} value={password} onChangeText={setPassword} secureTextEntry autoComplete="current-password" />
          <Field label={t('mobile.delete.confirmLabel', { word: DELETE_CONFIRMATION })} value={confirm} onChangeText={setConfirm} autoCapitalize="characters" autoCorrect={false} testID="delete-confirm" />
          {error ? <Banner kind="danger">{error}</Banner> : null}
          {status === 'deleted' ? <Banner kind="success">{t('mobile.delete.done')}</Banner> : null}
          <Button title={t('mobile.delete.submit')} kind="danger" busy={status === 'deleting'} disabled={confirm.trim() !== DELETE_CONFIRMATION} onPress={() => void remove()} testID="delete-submit" />
          <DeleteOnWebButton />
        </>
      )}
    </Screen>
  );
}

/**
 * Export data (contract E): `GET /v1.0/api/account/export` returns a ZIP of topics, briefings, findings, feedback, and
 * predictions. The app downloads it and opens the share sheet. 404 until the server supports it.
 */
export function ExportSection() {
  const { t } = useTranslation();
  const { auth, client } = useAuth();
  const [status, setStatus] = useState(null);
  const [error, setError] = useState(null);

  const run = async () => {
    setStatus('exporting');
    setError(null);
    try {
      const token = await auth.getAccessToken();
      const target = new File(Paths.cache, 'mymorningnews-export.zip');
      if (target.exists) target.delete();
      let file;
      try {
        file = await File.downloadFileAsync(client.url(routes.accountExport), target, { headers: token ? { Authorization: 'Bearer ' + token } : {}, idempotent: true });
      } catch (e) {
        // The download API reports HTTP failures as errors; confirm whether the route exists before giving up.
        try {
          await accountApi.exportAccount(client);
        } catch (probe) {
          if (isNotAvailable(probe)) {
            setStatus('unavailable');
            return;
          }
        }
        throw e;
      }
      setStatus('ready');
      if (await Sharing.isAvailableAsync()) await Sharing.shareAsync(file.uri, { mimeType: 'application/zip', dialogTitle: t('mobile.export.title') });
    } catch (e) {
      setError(e.message);
      setStatus(null);
    }
  };

  return (
    <Screen testID="export-section">
      <T variant="muted">{t('mobile.export.body')}</T>
      {status === 'unavailable' ? (
        <Banner kind="info" title={t('mobile.states.notAvailableTitle')}>
          {t('mobile.export.notAvailable')}
        </Banner>
      ) : null}
      {status === 'ready' ? <Banner kind="success">{t('mobile.export.ready')}</Banner> : null}
      {error ? <Banner kind="danger">{error}</Banner> : null}
      <Button title={t('mobile.export.submit')} icon="download-outline" busy={status === 'exporting'} onPress={() => void run()} />
    </Screen>
  );
}
