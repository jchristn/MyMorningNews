import React, { useState } from 'react';
import { Modal, Pressable, ScrollView, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { ButtonRow } from '@/components/Chips';
import { Banner, Button, T } from '@/components/ui';
import { FINDING_ACTION_ICONS, findingActions, performFindingAction } from '@/lib/findings';
import { useAuth } from '@/state/AuthContext';
import { useTheme } from '@/theme/ThemeContext';
import { radius, spacing } from '@/theme/palette';

/**
 * Pin, hide, and delete one finding. `run(action)` calls the server and hands the change (`{ pinned, hidden }` or
 * `{ deleted: true }`) to `onDone`. Delete asks first: the first press arms it, the second deletes (the same two-step
 * confirmation as learned preferences and podcast feeds).
 */
function useFindingAction(finding, onDone) {
  const { client } = useAuth();
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const run = async (action) => {
    if (action === 'delete' && !confirmDelete) {
      setConfirmDelete(true);
      return;
    }
    setBusy(action);
    setError(null);
    try {
      const change = await performFindingAction(client, finding, action);
      setConfirmDelete(false);
      setBusy(null);
      onDone(change);
    } catch (e) {
      setError(e && e.message ? e.message : String(e));
      setBusy(null);
    }
  };
  return { busy, error, confirmDelete, cancelDelete: () => setConfirmDelete(false), run };
}

function ActionButtons({ finding, onDone, testPrefix, extra }) {
  const { t } = useTranslation();
  const action = useFindingAction(finding, onDone);
  return (
    <View style={{ gap: spacing.sm }}>
      <ButtonRow>
        {findingActions(finding).map((a) => {
          const arming = a === 'delete' && action.confirmDelete;
          return (
            <Button
              key={a}
              title={arming ? t('mobile.findings.confirmDelete') : t('mobile.findings.actions.' + a)}
              icon={FINDING_ACTION_ICONS[a]}
              kind={a === 'delete' ? (arming ? 'danger' : 'ghost') : 'secondary'}
              busy={action.busy === a}
              disabled={Boolean(action.busy) && action.busy !== a}
              onPress={() => void action.run(a)}
              testID={testPrefix + a}
            />
          );
        })}
        {action.confirmDelete ? <Button title={t('mobile.common.cancel')} kind="ghost" onPress={action.cancelDelete} testID={testPrefix + 'cancel-delete'} /> : null}
        {extra}
      </ButtonRow>
      {action.confirmDelete ? <T variant="small">{t('mobile.findings.deleteNote')}</T> : null}
      {action.error ? <Banner kind="danger">{action.error}</Banner> : null}
    </View>
  );
}

/** The finding detail's action row: Pin or Unpin, Hide or Unhide, and Delete with a confirmation. */
export function FindingActionBar({ finding, onDone }) {
  return <ActionButtons finding={finding} onDone={onDone} testPrefix="finding-" />;
}

/**
 * The actions menu a finding row opens (a bottom sheet like the why sheet): the same actions as the detail.
 * `onDone(change)` runs after an action succeeds, and the sheet closes.
 */
export function FindingActionsSheet({ finding, onDone, onClose }) {
  const p = useTheme();
  const { t } = useTranslation();
  const visible = Boolean(finding);
  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose} supportedOrientations={['portrait', 'landscape']}>
      <View style={{ flex: 1, justifyContent: 'flex-end' }}>
        <Pressable style={{ flex: 1 }} onPress={onClose} accessibilityRole="button" accessibilityLabel={t('mobile.common.close')} />
        <View testID="finding-actions-sheet" style={{ backgroundColor: p.surface, borderTopLeftRadius: radius.lg, borderTopRightRadius: radius.lg, borderColor: p.border, borderWidth: 1, maxHeight: '85%' }}>
          {visible ? (
            <ScrollView contentContainerStyle={{ padding: spacing.lg, gap: spacing.md, width: '100%', maxWidth: 640, alignSelf: 'center' }}>
              <T variant="title">{t('mobile.findings.actionsTitle')}</T>
              <T variant="muted" numberOfLines={3}>
                {finding.title}
              </T>
              <ActionButtons
                key={finding.id}
                finding={finding}
                testPrefix="sheet-"
                onDone={(change) => {
                  onDone(finding.id, change);
                  onClose();
                }}
                extra={<Button title={t('mobile.common.close')} kind="ghost" onPress={onClose} testID="sheet-close" />}
              />
            </ScrollView>
          ) : null}
        </View>
      </View>
    </Modal>
  );
}
