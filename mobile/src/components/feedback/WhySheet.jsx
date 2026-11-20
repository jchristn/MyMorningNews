import React, { useState } from 'react';
import { KeyboardAvoidingView, Modal, Platform, Pressable, ScrollView, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { ACTION_ICONS, actionKey, actionOf, actionsFor } from '@/lib/feedback';
import { useTheme } from '@/theme/ThemeContext';
import { radius, spacing } from '@/theme/palette';
import { ButtonRow, ChipGroup } from '../Chips';
import { Banner, Button, Field, T } from '../ui';

/** The longest reason the server keeps (FeedbackService.MaxReasonLength); longer text is cut. */
export const MAX_REASON_LENGTH = 2000;

/**
 * The "why" sheet (plan section 35.3): pick or change the rating, say why, save, or retract the current rating. The
 * same choices as the email feedback page. `onSave({ action, reason })` and `onRetract()` return promises; the sheet
 * stays open with the error when one fails.
 */
export function WhySheet({ visible, targetType, current, title, onSave, onRetract, onClose }) {
  const p = useTheme();
  const { t } = useTranslation();
  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose} supportedOrientations={['portrait', 'landscape']}>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={{ flex: 1, justifyContent: 'flex-end' }}>
        <Pressable style={{ flex: 1 }} onPress={onClose} accessibilityRole="button" accessibilityLabel={t('mobile.common.close')} />
        <View testID="why-sheet" style={{ backgroundColor: p.surface, borderTopLeftRadius: radius.lg, borderTopRightRadius: radius.lg, borderColor: p.border, borderWidth: 1, maxHeight: '85%' }}>
          {/* The form mounts each time the sheet opens, so it starts from the current rating. */}
          {visible ? <WhyForm targetType={targetType} current={current} title={title} onSave={onSave} onRetract={onRetract} onClose={onClose} /> : null}
        </View>
      </KeyboardAvoidingView>
    </Modal>
  );
}

function WhyForm({ targetType, current, title, onSave, onRetract, onClose }) {
  const { t } = useTranslation();
  const [action, setAction] = useState(actionOf(current));
  const [reason, setReason] = useState((current && current.reason) || '');
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);

  const run = async (kind, fn) => {
    setBusy(kind);
    setError(null);
    try {
      await fn();
      onClose();
    } catch (e) {
      setError(e && e.message ? e.message : t('mobile.feedback.failed'));
    } finally {
      setBusy(null);
    }
  };

  const options = actionsFor(targetType).map((a) => ({ value: a, label: t('mobile.feedback.actions.' + actionKey(a)), icon: ACTION_ICONS[a], testID: 'why-action-' + a }));
  return (
    <ScrollView contentContainerStyle={{ padding: spacing.lg, gap: spacing.md, width: '100%', maxWidth: 640, alignSelf: 'center' }} keyboardShouldPersistTaps="handled">
      <T variant="title">{t('mobile.feedback.whyTitle')}</T>
      {title ? <T variant="muted" numberOfLines={3}>{title}</T> : null}
      <ChipGroup options={options} value={action} onChange={setAction} accessibilityLabel={t('mobile.feedback.rating')} />
      <Field
        label={t('mobile.feedback.why')}
        hint={t('mobile.feedback.whyHint')}
        value={reason}
        onChangeText={(text) => setReason(text.slice(0, MAX_REASON_LENGTH))}
        multiline
        maxLength={MAX_REASON_LENGTH}
        testID="why-reason"
      />
      {error ? <Banner kind="danger">{error}</Banner> : null}
      <ButtonRow>
        <Button title={t('mobile.feedback.save')} busy={busy === 'save'} disabled={!action && !reason.trim()} onPress={() => void run('save', () => onSave({ action, reason: reason.trim() }))} testID="why-save" />
        {current && onRetract ? <Button title={t('mobile.feedback.retract')} kind="danger" busy={busy === 'retract'} onPress={() => void run('retract', onRetract)} testID="why-retract" /> : null}
        <Button title={t('mobile.common.cancel')} kind="ghost" onPress={onClose} testID="why-cancel" />
      </ButtonRow>
    </ScrollView>
  );
}
