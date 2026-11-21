import React from 'react';
import { ActivityIndicator, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTranslation } from 'react-i18next';
import { isModelError, isNetworkError, isSessionExpired, isSetupRequired } from '@/api/errors';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { Banner, Button, T } from './ui';

function Centered({ children, testID }) {
  return (
    <View testID={testID} style={{ alignItems: 'center', justifyContent: 'center', gap: spacing.md, paddingVertical: spacing.xxl, paddingHorizontal: spacing.lg }}>
      {children}
    </View>
  );
}

export function LoadingState({ label }) {
  const p = useTheme();
  const { t } = useTranslation();
  return (
    <Centered testID="state-loading">
      <ActivityIndicator color={p.primary} accessibilityLabel={label || t('mobile.states.loading')} />
      <T variant="muted">{label || t('mobile.states.loading')}</T>
    </Centered>
  );
}

/** Empty list or first-run state, with an optional call to action. */
export function EmptyState({ icon = 'sunny-outline', title, body, actionLabel, onAction }) {
  const p = useTheme();
  return (
    <Centered testID="state-empty">
      <Ionicons name={icon} size={40} color={p.muted} />
      <T variant="title" style={{ textAlign: 'center' }}>
        {title}
      </T>
      {body ? (
        <T variant="muted" style={{ textAlign: 'center' }}>
          {body}
        </T>
      ) : null}
      {actionLabel && onAction ? <Button title={actionLabel} onPress={onAction} /> : null}
    </Centered>
  );
}

/** The route does not exist on this server yet (404): it arrives with a later server update. */
export function NotAvailableState({ feature }) {
  const p = useTheme();
  const { t } = useTranslation();
  return (
    <Centered testID="state-not-available">
      <Ionicons name="time-outline" size={40} color={p.muted} />
      <T variant="title" style={{ textAlign: 'center' }}>
        {t('mobile.states.notAvailableTitle')}
      </T>
      <T variant="muted" style={{ textAlign: 'center' }}>
        {feature ? t('mobile.states.notAvailableFeature', { feature }) : t('mobile.states.notAvailableBody')}
      </T>
    </Centered>
  );
}

/** The item does not exist (deleted, or a link to something from another account or server). */
export function NotFoundState() {
  const { t } = useTranslation();
  return <EmptyState icon="help-circle-outline" title={t('mobile.states.notFoundTitle')} body={t('mobile.states.notFoundBody')} />;
}

/** Error with Retry. Network and session errors get their own wording. */
export function ErrorState({ error, onRetry }) {
  const p = useTheme();
  const { t } = useTranslation();
  const title = isNetworkError(error) ? t('mobile.states.offlineTitle') : isSessionExpired(error) ? t('mobile.states.sessionTitle') : t('mobile.states.errorTitle');
  const body = isNetworkError(error) ? t('mobile.states.offlineBody') : error && error.message ? error.message : t('mobile.states.errorBody');
  return (
    <Centered testID="state-error">
      <Ionicons name="alert-circle-outline" size={40} color={p.danger} />
      <T variant="title" style={{ textAlign: 'center' }}>
        {title}
      </T>
      <T variant="muted" style={{ textAlign: 'center' }}>
        {body}
      </T>
      {onRetry ? <Button title={t('mobile.common.retry')} kind="secondary" onPress={onRetry} /> : null}
    </Centered>
  );
}

/**
 * An action that failed, inline. A model-backed step that failed because no model is set up (502 ModelError) says so
 * and points at the manual path (`manualHint`) instead of reading as a crash; other errors show their message.
 */
export function ActionError({ error, onRetry, manualHint, testID = 'action-error' }) {
  const { t } = useTranslation();
  if (!error) return null;
  // 409 SetupRequired: the server's message says what is missing; the setup gate re-checks and takes over the screen.
  if (isSetupRequired(error)) {
    return (
      <Banner kind="warning" title={t('mobile.setup.requiredTitle')} testID={testID}>
        <View style={{ gap: spacing.sm }}>
          {error.message ? <T variant="small">{error.message}</T> : null}
          <T variant="small">{t('mobile.setup.requiredHint')}</T>
        </View>
      </Banner>
    );
  }
  const model = isModelError(error);
  const offline = isNetworkError(error);
  return (
    <Banner kind={model ? 'warning' : 'danger'} title={model ? t('mobile.states.modelTitle') : offline ? t('mobile.states.offlineTitle') : t('mobile.states.errorTitle')} testID={testID}>
      <View style={{ gap: spacing.sm }}>
        <T variant="small">{offline ? t('mobile.states.offlineBody') : model ? t('mobile.states.modelBody') : error.message || t('mobile.states.errorBody')}</T>
        {model && error.message ? <T variant="small">{error.message}</T> : null}
        {model && manualHint ? <T variant="small">{manualHint}</T> : null}
        {onRetry ? <Button title={t('mobile.common.retry')} kind="secondary" onPress={onRetry} testID={testID + '-retry'} /> : null}
      </View>
    </Banner>
  );
}

/**
 * Render a `useResource` result: loading, error with retry, not available yet, empty, or the children.
 * `isEmpty(data)` decides when to show `empty` (an EmptyState element).
 */
export function ResourceView({ resource, feature, isEmpty, empty, children }) {
  if (resource.status === 'loading') return <LoadingState />;
  if (resource.status === 'notAvailable') return <NotAvailableState feature={feature} />;
  if (resource.status === 'notFound') return <NotFoundState />;
  if (resource.status === 'error' && !resource.data) return <ErrorState error={resource.error} onRetry={resource.reload} />;
  if (isEmpty && isEmpty(resource.data) && empty) return empty;
  return typeof children === 'function' ? children(resource.data) : children;
}
