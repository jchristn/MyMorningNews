import React from 'react';
import { View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { spacing } from '@/theme/palette';
import { RatingControl, useCurrentFeedback } from './feedback/RatingControl';
import { Banner, T } from './ui';

/**
 * Feedback on a detail screen (plan section 35.3, "feedback everywhere"): the question for the target type, the
 * rating chips with the current rating selected (`GET /v1.0/api/feedback?targetType=&targetId=`), and the "why"
 * sheet. Ratings post to `POST /v1.0/api/feedback` with `channel: "Mobile"`, the same API as the email pages.
 */
export function FeedbackBar({ targetType, targetId, title }) {
  const { t } = useTranslation();
  const current = useCurrentFeedback(targetType, targetId);
  if (current.status === 'notAvailable') return <Banner kind="info">{t('mobile.feedback.notAvailable')}</Banner>;
  return (
    <View style={{ gap: spacing.sm }} testID={'feedback-' + targetType}>
      <T variant="label">{t('mobile.feedback.questions.' + targetType)}</T>
      {current.status === 'loading' ? null : <RatingControl targetType={targetType} targetId={targetId} title={title} current={current.data} onChange={current.setData} testID={'feedback-bar'} />}
    </View>
  );
}
