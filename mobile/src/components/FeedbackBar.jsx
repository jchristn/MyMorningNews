import React, { useState } from 'react';
import { View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { feedbackApi } from '@/api/research';
import { isNotAvailable } from '@/api/errors';
import { useAuth } from '@/state/AuthContext';
import { spacing } from '@/theme/palette';
import { Banner, Button, Field, T } from './ui';

/**
 * Relevant or not relevant, with an optional "why" (plan section 35.3, "feedback everywhere"). Posts to
 * `POST /v1.0/api/feedback` with `{ targetType, targetId, rating, reason, channel: 'mobile' }`, the same API as the
 * email feedback pages. Until Phase 10 the route is missing and the bar says so.
 */
export function FeedbackBar({ targetType, targetId }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [rating, setRating] = useState(null);
  const [reason, setReason] = useState('');
  const [status, setStatus] = useState('idle');

  const send = async () => {
    setStatus('sending');
    try {
      await feedbackApi.submit(client, { targetType, targetId, rating, reason: reason.trim() || undefined, channel: 'mobile' });
      setStatus('sent');
    } catch (error) {
      setStatus(isNotAvailable(error) ? 'unavailable' : 'failed');
    }
  };

  if (status === 'sent') return <Banner kind="success">{t('mobile.feedback.thanks')}</Banner>;
  if (status === 'unavailable') return <Banner kind="info">{t('mobile.feedback.notAvailable')}</Banner>;
  return (
    <View style={{ gap: spacing.sm }}>
      <T variant="label">{t('mobile.feedback.question')}</T>
      <View style={{ flexDirection: 'row', gap: spacing.sm }}>
        <Button title={t('mobile.feedback.relevant')} icon="thumbs-up-outline" kind={rating === 'Relevant' ? 'primary' : 'secondary'} onPress={() => setRating('Relevant')} style={{ flex: 1 }} />
        <Button title={t('mobile.feedback.notRelevant')} icon="thumbs-down-outline" kind={rating === 'NotRelevant' ? 'primary' : 'secondary'} onPress={() => setRating('NotRelevant')} style={{ flex: 1 }} />
      </View>
      {rating ? (
        <>
          <Field label={t('mobile.feedback.why')} hint={t('mobile.feedback.whyHint')} value={reason} onChangeText={setReason} multiline />
          {status === 'failed' ? <Banner kind="danger">{t('mobile.feedback.failed')}</Banner> : null}
          <Button title={t('mobile.feedback.send')} busy={status === 'sending'} onPress={() => void send()} />
        </>
      ) : null}
    </View>
  );
}
