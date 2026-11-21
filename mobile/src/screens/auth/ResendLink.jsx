import React, { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { authApi } from '@/api/auth';
import { ApiError, isNetworkError } from '@/api/errors';
import { Banner, Button } from '@/components/ui';
import { useAuth } from '@/state/AuthContext';

/**
 * "Send a new link" for an unconfirmed email address: `POST /v1.0/public/verify-email/resend`. The server answers 202
 * whether or not the address has an unconfirmed account, so the confirmation is the same either way. Only a network
 * failure or a rate limit is reported as an error.
 */
export function ResendLink({ email, kind = 'secondary' }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [status, setStatus] = useState(null);
  const [error, setError] = useState(null);
  const address = String(email || '').trim();

  const resend = async () => {
    setStatus('sending');
    setError(null);
    try {
      await authApi.resendVerification(client, address);
      setStatus('sent');
    } catch (e) {
      if (isNetworkError(e)) setError(t('mobile.signIn.errorUnreachable'));
      else if (e instanceof ApiError && e.status === 429) setError(t('mobile.signIn.errorRateLimited'));
      else {
        setStatus('sent');
        return;
      }
      setStatus(null);
    }
  };

  if (status === 'sent') {
    return (
      <Banner kind="success" testID="resend-sent">
        {t('mobile.verify.resent')}
      </Banner>
    );
  }
  return (
    <>
      {error ? <Banner kind="danger">{error}</Banner> : null}
      <Button title={t('mobile.verify.resend')} kind={kind} icon="mail-outline" busy={status === 'sending'} disabled={!address.includes('@')} onPress={() => void resend()} testID="resend-link" />
    </>
  );
}
