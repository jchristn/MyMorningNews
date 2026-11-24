import React, { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { briefingsApi } from '@/api/research';
import { Banner, Button, Card, T } from '@/components/ui';
import { useAuth } from '@/state/AuthContext';

/** A deliberate tap forwards the visible failure details through the server's support outbox. */
export function BriefingSupport({ id }) {
  const { client } = useAuth();
  const { t } = useTranslation();
  const sending = useRef(false);
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);
  const [error, setError] = useState(null);
  const send = async () => {
    if (sending.current || sent) return;
    sending.current = true;
    setBusy(true);
    setError(null);
    try {
      await briefingsApi.support(client, id);
      setSent(true);
    } catch (e) {
      setError(e.message);
    } finally {
      sending.current = false;
      setBusy(false);
    }
  };
  return <Card>
    <T>{t('mobile.supportReport.help')}</T>
    {sent ? <Banner kind="success">{t('mobile.supportReport.queued')}</Banner> :
      <Button title={t('mobile.supportReport.action')} onPress={send} disabled={busy} busy={busy} testID="briefing-support" />}
    {error ? <Banner kind="danger">{error}</Banner> : null}
  </Card>;
}
