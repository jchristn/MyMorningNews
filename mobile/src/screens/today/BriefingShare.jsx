import React, { useState } from 'react';
import { Share, View } from 'react-native';
import * as Clipboard from 'expo-clipboard';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { briefingsApi } from '@/api/research';
import { ButtonRow, ChipGroup } from '@/components/Chips';
import { Badge, Banner, Button, Card, SwitchRow, T } from '@/components/ui';
import { formatDateTime } from '@/lib/format';
import { SHARE_MIN_DAYS, shareDayOptions, shareStatus } from '@/lib/sharing';
import { useAuth } from '@/state/AuthContext';
import { useServer } from '@/state/ServerContext';
import { useTheme } from '@/theme/ThemeContext';
import { radius, spacing } from '@/theme/palette';

const STATUS_TONES = { Live: 'success', Expired: 'warning', Revoked: 'neutral' };

/** True unless the server says public sharing is turned off (client config `sharingEnabled: false`). */
export function useSharingEnabled() {
  const { config } = useServer();
  return !config || config.sharingEnabled !== false;
}

/** One public link shown once after creating a share: the URL, the native share sheet, and Copy. */
function LinkBlock({ kind, label, url, title }) {
  const p = useTheme();
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState(null);
  const share = async () => {
    setError(null);
    try {
      await Share.share({ message: t('mobile.share.shareMessage', { title }) + '\n' + url, url, title });
    } catch (e) {
      setError(e.message);
    }
  };
  const copy = async () => {
    await Clipboard.setStringAsync(url);
    setCopied(true);
  };
  return (
    <View style={{ gap: spacing.xs }}>
      <T variant="label">{label}</T>
      <View style={{ backgroundColor: p.surfaceAlt, borderRadius: radius.md, padding: spacing.md }}>
        <T variant="mono" selectable testID={'share-url-' + kind}>
          {url}
        </T>
      </View>
      <ButtonRow>
        <Button title={kind === 'read' ? t('mobile.share.shareRead') : t('mobile.share.shareListen')} icon="share-outline" onPress={() => void share()} testID={'share-send-' + kind} />
        <Button title={copied ? t('mobile.share.copied') : t('mobile.share.copy')} icon={copied ? 'checkmark' : 'copy-outline'} kind="secondary" onPress={() => void copy()} testID={'share-copy-' + kind} accessibilityLabel={(copied ? t('mobile.share.copied') : t('mobile.share.copy')) + ': ' + label} />
      </ButtonRow>
      {error ? <Banner kind="danger">{error}</Banner> : null}
    </View>
  );
}

/**
 * Public sharing for one briefing (plan section 36): create a share that lasts 1 to 14 days, hand its reading link or
 * listening link to the native share sheet (the links need no sign-in and are shown once), and see or revoke the
 * briefing's existing shares with their expiry and how often they were opened. Hidden when the server has sharing
 * turned off or the briefing is not ready. Existing shares load when the panel is opened.
 */
export function BriefingShare({ detail }) {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const { config } = useServer();
  const enabled = useSharingEnabled();
  const options = shareDayOptions(config);
  const [open, setOpen] = useState(false);
  const [days, setDays] = useState(options.initial);
  const [showName, setShowName] = useState(false);
  const [shares, setShares] = useState(null);
  const [created, setCreated] = useState(null);
  const [busy, setBusy] = useState(null);
  const [confirm, setConfirm] = useState(null);
  const [error, setError] = useState(null);
  const briefing = detail.briefing;
  if (!enabled || briefing.status !== 'Ready') return null;

  const load = async () => {
    try {
      setShares(listItems(await briefingsApi.shares(client, briefing.id, { pageSize: 100 })));
    } catch (e) {
      setError(e.message);
    }
  };
  const toggle = async () => {
    const next = !open;
    setOpen(next);
    setError(null);
    if (next) await load();
  };
  const create = async () => {
    setBusy('create');
    setError(null);
    try {
      setCreated(await briefingsApi.share(client, briefing.id, { expiresInDays: days, showOwnerName: showName }));
      await load();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };
  const revoke = async (share, live) => {
    if (confirm !== share.id) {
      setConfirm(share.id);
      return;
    }
    setBusy(share.id);
    setError(null);
    try {
      await briefingsApi.revokeShare(client, briefing.id, share.id, { purge: !live });
      if (created && created.share && created.share.id === share.id) setCreated(null);
      setConfirm(null);
      await load();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };
  const when = (value) => formatDateTime(value, i18n.language);

  return (
    <View style={{ gap: spacing.sm }} testID="briefing-share">
      <Button title={t('mobile.share.open')} icon="share-social-outline" kind="secondary" onPress={() => void toggle()} testID="briefing-share-open" accessibilityHint={t('mobile.share.about')} />
      {open ? (
        <Card testID="briefing-share-panel">
          <T variant="label">{t('mobile.share.title')}</T>
          <T variant="small">{t('mobile.share.about')}</T>
          {created ? (
            <View style={{ gap: spacing.sm }} testID="share-created">
              <T variant="label">{t('mobile.share.newLinks')}</T>
              <LinkBlock kind="read" label={t('mobile.share.readLink')} url={created.readUrl} title={briefing.title} />
              <LinkBlock kind="listen" label={t('mobile.share.listenLink')} url={created.listenUrl} title={briefing.title} />
              <T variant="small" testID="share-expires">
                {t('mobile.share.expiresOn', { date: when(created.share.expiresUtc) })}
              </T>
              {created.audioReady ? null : <T variant="small">{t('mobile.share.noAudio')}</T>}
              <T variant="small">{t('mobile.share.shownOnce')}</T>
            </View>
          ) : null}
          <View style={{ gap: spacing.xs }}>
            <T variant="label">{t('mobile.share.days')}</T>
            <ChipGroup scroll options={options.days.map((d) => ({ value: d, label: String(d), testID: 'share-days-' + d }))} value={days} onChange={setDays} accessibilityLabel={t('mobile.share.days')} testID="share-days" />
            <T variant="small">{t('mobile.share.daysHelp', { min: SHARE_MIN_DAYS, max: options.max })}</T>
          </View>
          <SwitchRow title={t('mobile.share.showName')} detail={t('mobile.share.showNameHelp')} value={showName} onValueChange={setShowName} testID="share-show-name" />
          <Button title={created ? t('mobile.share.createAnother') : t('mobile.share.create')} icon="link-outline" busy={busy === 'create'} onPress={() => void create()} testID="share-create" />
          {error ? <Banner kind="danger">{error}</Banner> : null}
          <T variant="label">{t('mobile.share.existing')}</T>
          {shares !== null && shares.length === 0 ? <T variant="small">{t('mobile.share.none')}</T> : null}
          {(shares || []).map((s) => {
            const status = shareStatus(s);
            const live = status === 'Live';
            const line = status === 'Revoked' ? t('mobile.share.revokedOn', { date: when(s.revokedUtc) }) : live ? t('mobile.share.expiresOn', { date: when(s.expiresUtc) }) : t('mobile.share.expiredOn', { date: when(s.expiresUtc) });
            const opened = t('mobile.share.views', { count: s.viewCount || 0 }) + (s.lastViewedUtc ? ' · ' + t('mobile.share.lastOpened', { date: when(s.lastViewedUtc) }) : '');
            const action = live ? t('mobile.share.revoke') : t('mobile.share.remove');
            return (
              <View key={s.id} style={{ gap: spacing.xs }} testID={'share-' + s.id}>
                <View style={{ flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: spacing.sm }}>
                  <Badge label={t('mobile.share.status.' + status)} tone={STATUS_TONES[status]} />
                  <T variant="small">{line}</T>
                </View>
                <T variant="small">{opened}</T>
                <Button title={confirm === s.id ? t('mobile.share.confirm') : action} kind={confirm === s.id ? 'danger' : 'ghost'} busy={busy === s.id} onPress={() => void revoke(s, live)} testID={'share-revoke-' + s.id} accessibilityLabel={action + ': ' + line} />
              </View>
            );
          })}
        </Card>
      ) : null}
    </View>
  );
}
