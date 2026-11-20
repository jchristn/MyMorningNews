import React, { useMemo, useRef, useState } from 'react';
import { ActivityIndicator, RefreshControl, View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { findingsApi, predictionsApi, briefingsApi } from '@/api/research';
import { ButtonRow, ChipGroup } from '@/components/Chips';
import { FeedbackBar } from '@/components/FeedbackBar';
import { RatingControl, useFeedbackMap } from '@/components/feedback/RatingControl';
import { BriefingAudio } from '@/components/player/BriefingAudio';
import { Markdown } from '@/components/Markdown';
import { ResourceView } from '@/components/states';
import { Banner, Button, Card, ListRow, Screen, SectionHeader, T } from '@/components/ui';
import { shareBriefingFile } from '@/lib/briefingFiles';
import { BRIEFING_STATUS_TONES, PENDING_BRIEFING_STATUSES } from '@/lib/enums';
import { formatDateTime } from '@/lib/format';
import { parseBriefing } from '@/lib/markdown';
import { usePlayer } from '@/player/PlayerContext';
import { playingHeadingId } from '@/player/tracks';
import { useAuth } from '@/state/AuthContext';
import { usePolling } from '@/state/usePolling';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { PENDING_AUDIO_STATUSES } from '../common/items';
import { DetailHeader } from '../common/DetailScreen';

/**
 * Titles for the items a briefing links to. The detail carries them in `itemTitles` (item ID to title); servers that
 * do not get the findings and predictions read one by one (at most `limit`).
 */
export async function loadItemTitles(client, items, limit = 20, known) {
  const titles = { ...(known && typeof known === 'object' ? known : {}) };
  const wanted = (items || []).filter((i) => (i.itemType === 'Finding' || i.itemType === 'Prediction') && !titles[i.itemId]).slice(0, limit);
  await Promise.all(
    wanted.map(async (item) => {
      try {
        if (item.itemType === 'Finding') titles[item.itemId] = (await findingsApi.get(client, item.itemId)).finding.title;
        else titles[item.itemId] = (await predictionsApi.get(client, item.itemId)).prediction.statement;
      } catch {
        // The item may have been deleted; the row keeps its generic label.
      }
    }),
  );
  return titles;
}

/** Where a briefing item opens. */
export function itemHref(item) {
  if (item.itemType === 'Finding') return '/today/findings/' + encodeURIComponent(item.itemId);
  if (item.itemType === 'Prediction') return '/today/predictions/' + encodeURIComponent(item.itemId);
  return '/today/suggestions';
}

function LinkedItems({ detail }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const items = useMemo(() => (detail.items || []).slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)), [detail.items]);
  const titles = useResource(() => loadItemTitles(client, items, 20, detail.itemTitles), [client, items, detail.itemTitles]);
  const ratings = useFeedbackMap('Finding');
  if (items.length === 0) return null;
  const sections = (detail.sections || []).slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
  const groups = sections.map((s) => ({ section: s, items: items.filter((i) => i.sectionId === s.id) })).filter((g) => g.items.length > 0);
  const loose = items.filter((i) => !sections.some((s) => s.id === i.sectionId));
  if (loose.length) groups.push({ section: { id: 'other', heading: t('mobile.reader.otherItems') }, items: loose });
  const icons = { Finding: 'search-outline', Prediction: 'trending-up-outline', Suggestion: 'bulb-outline' };
  return (
    <>
      <SectionHeader title={t('mobile.reader.inThisBriefing')} />
      {groups.map((g) => (
        <Card key={g.section.id} testID={'items-' + g.section.id}>
          <T variant="label">{g.section.heading}</T>
          {g.items.map((item) => {
            const title = (titles.data && titles.data[item.itemId]) || t('mobile.reader.itemTypes.' + item.itemType);
            const cited = item.citationNumber ? t('mobile.reader.citedAs', { number: item.citationNumber }) : '';
            return (
              <View key={item.id} style={{ gap: spacing.xs }}>
                <ListRow icon={icons[item.itemType] || 'document-outline'} title={title} detail={cited} onPress={() => router.push(itemHref(item))} testID={'item-' + item.itemId} />
                {item.itemType === 'Finding' && ratings.available ? (
                  <RatingControl compact targetType="Finding" targetId={item.itemId} title={title} current={ratings.map[item.itemId] || null} onChange={(f) => ratings.set(item.itemId, f)} />
                ) : null}
              </View>
            );
          })}
        </Card>
      ))}
    </>
  );
}

function FileActions({ detail, onChanged }) {
  const { t } = useTranslation();
  const { auth, client } = useAuth();
  const [busy, setBusy] = useState(null);
  const [message, setMessage] = useState(null);
  const b = detail.briefing;
  const ready = b.status === 'Ready';

  const share = async (format) => {
    setBusy(format);
    setMessage(null);
    try {
      const token = await auth.getAccessToken();
      const result = await shareBriefingFile({ client, token, detail, format });
      if (!result.shared) setMessage({ kind: 'info', text: t('mobile.reader.savedTo', { path: result.uri }) });
    } catch (e) {
      setMessage({ kind: 'danger', text: e && e.code === 'ArtifactExpired' ? t('mobile.reader.expired') : e.message });
    } finally {
      setBusy(null);
    }
  };
  const pin = async () => {
    setBusy('pin');
    setMessage(null);
    try {
      const updated = await briefingsApi.pin(client, b.id, !b.pinned);
      onChanged((d) => ({ ...d, briefing: { ...d.briefing, pinned: updated && typeof updated.pinned === 'boolean' ? updated.pinned : !b.pinned } }));
    } catch (e) {
      setMessage({ kind: 'danger', text: e.message });
    } finally {
      setBusy(null);
    }
  };
  const regenerate = async () => {
    setBusy('regenerate');
    setMessage(null);
    try {
      await briefingsApi.regenerate(client, b.id);
      onChanged(await briefingsApi.get(client, b.id));
      setMessage({ kind: 'success', text: t('mobile.reader.regenerated') });
    } catch (e) {
      setMessage({ kind: 'danger', text: e.message });
    } finally {
      setBusy(null);
    }
  };

  return (
    <View style={{ gap: spacing.sm }}>
      <ButtonRow>
        <Button title={b.pinned ? t('mobile.reader.unpin') : t('mobile.reader.pin')} icon={b.pinned ? 'bookmark' : 'bookmark-outline'} kind="secondary" busy={busy === 'pin'} onPress={() => void pin()} testID="briefing-pin" />
        <Button title={t('mobile.reader.shareMarkdown')} icon="document-text-outline" kind="secondary" busy={busy === 'md'} disabled={!ready || detail.filesExpired} onPress={() => void share('md')} testID="briefing-share-md" />
        <Button title={t('mobile.reader.shareHtml')} icon="code-slash-outline" kind="secondary" busy={busy === 'html'} disabled={!ready || detail.filesExpired} onPress={() => void share('html')} testID="briefing-share-html" />
      </ButtonRow>
      {ready && detail.filesExpired ? (
        <Banner kind="warning" title={t('mobile.reader.expiredTitle')}>
          <View style={{ gap: spacing.sm }}>
            <T variant="small">{t('mobile.reader.expired')}</T>
            <Button title={t('mobile.reader.regenerate')} icon="refresh" busy={busy === 'regenerate'} onPress={() => void regenerate()} testID="briefing-regenerate" />
          </View>
        </Banner>
      ) : null}
      {message ? <Banner kind={message.kind}>{message.text}</Banner> : null}
    </View>
  );
}

/**
 * The briefing reader (plan section 21.3): title and status, audio (make, listen, download, chapters), section
 * navigation, the Markdown rendered with native components with the section being read aloud marked, numbered
 * citations that jump to the Sources section, the items it links to with inline ratings on findings, pin, and share or
 * save the `.md` and `.html` files. A briefing that is still being composed, or whose audio is being made, polls.
 */
export function BriefingDetail({ id }) {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const resource = useResource(() => briefingsApi.get(client, id), [client, id]);
  const scroll = useRef(null);
  const anchors = useRef({});
  const bodyY = useRef(0);
  const offset = useRef(0);
  const [highlight, setHighlight] = useState(null);
  const [returnTo, setReturnTo] = useState(null);
  const detail = resource.data;
  const briefing = detail && detail.briefing;
  const pending = Boolean(briefing && PENDING_BRIEFING_STATUSES.includes(briefing.status));
  const audioPending = Boolean(detail && detail.audio && PENDING_AUDIO_STATUSES.includes(detail.audio.status));
  usePolling(resource.poll, pending || audioPending);
  const { state: player } = usePlayer();

  const citationsHeadings = useMemo(() => ((detail && detail.sections) || []).filter((s) => s.source === 'citations').map((s) => s.heading), [detail]);
  const parsed = useMemo(
    () => (briefing ? parseBriefing(briefing.markdown || '', { skipTitle: briefing.title, sourcesHeadings: citationsHeadings.length ? citationsHeadings : undefined }) : null),
    [briefing, citationsHeadings],
  );

  const scrollTo = (y) => {
    if (scroll.current && typeof scroll.current.scrollTo === 'function') scroll.current.scrollTo({ y: Math.max(0, bodyY.current + y - spacing.md), animated: true });
  };
  const jumpToHeading = (headingId) => scrollTo(anchors.current[headingId] || 0);
  const onCite = (n) => {
    const source = parsed && parsed.sources[n];
    if (!source) return;
    setReturnTo(offset.current);
    setHighlight({ block: source.blockIndex, item: source.itemIndex, n });
    scrollTo((anchors.current['blk:' + source.blockIndex] || 0) + (anchors.current['item:' + source.blockIndex + ':' + source.itemIndex] || 0));
  };
  const goBack = () => {
    if (scroll.current && typeof scroll.current.scrollTo === 'function') scroll.current.scrollTo({ y: returnTo || 0, animated: true });
    setReturnTo(null);
    setHighlight(null);
  };

  const playingHeading = parsed && briefing ? playingHeadingId(player, briefing.id, detail.sections, parsed.blocks) : null;
  const navOptions = parsed ? parsed.outline.map((h) => ({ value: h.id, label: h.text, testID: 'nav-' + h.id })) : [];

  return (
    <View style={{ flex: 1, backgroundColor: p.bg }}>
      <Screen
        testID="briefing-detail"
        scrollRef={scroll}
        onScroll={(e) => (offset.current = e.nativeEvent.contentOffset.y)}
        refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}
      >
        <ResourceView resource={resource} feature={t('mobile.features.briefings')}>
          {() => (
            <>
              <DetailHeader
                title={briefing.title}
                subtitle={[detail.editionName || detail.topicName, briefing.composedUtc ? formatDateTime(briefing.composedUtc, i18n.language) : null, briefing.readMinutes ? t('mobile.reader.readMinutes', { count: briefing.readMinutes }) : null]
                  .filter(Boolean)
                  .join(' · ')}
                badges={[
                  { label: t('mobile.enums.briefingStatus.' + briefing.status), tone: BRIEFING_STATUS_TONES[briefing.status] || 'neutral' },
                  { label: t('mobile.enums.briefingKind.' + briefing.kind), tone: 'neutral' },
                  briefing.pinned ? { label: t('mobile.reader.pinned'), tone: 'primary' } : null,
                ]}
              />
              {pending ? (
                <Banner kind="info" title={t('mobile.reader.composingTitle')} testID="briefing-pending">
                  <View style={{ flexDirection: 'row', gap: spacing.sm, alignItems: 'center' }}>
                    <ActivityIndicator color={p.primary} />
                    <T variant="small">{t('mobile.reader.composingBody')}</T>
                  </View>
                </Banner>
              ) : null}
              {briefing.status === 'Failed' ? <Banner kind="danger" title={t('mobile.reader.failedTitle')}>{briefing.errorMessage || t('mobile.states.errorBody')}</Banner> : null}
              <BriefingAudio detail={detail} onChanged={resource.setData} />
              <FileActions detail={detail} onChanged={resource.setData} />
              {navOptions.length > 1 ? (
                <View style={{ gap: spacing.xs }}>
                  <T variant="label">{t('mobile.reader.sections')}</T>
                  <ChipGroup scroll options={navOptions} value={null} onChange={jumpToHeading} accessibilityLabel={t('mobile.reader.sections')} testID="section-nav" />
                </View>
              ) : null}
              <View onLayout={(e) => (bodyY.current = e.nativeEvent.layout.y)}>
                {parsed && parsed.blocks.length ? <Markdown blocks={parsed.blocks} onCite={onCite} onAnchor={(key, y) => (anchors.current[key] = y)} highlight={highlight} activeHeading={playingHeading} testID="briefing-body" /> : !pending ? <T variant="muted">{t('mobile.reader.empty')}</T> : null}
              </View>
              {highlight && parsed.sources[highlight.n] ? (
                <Card testID="cited-source">
                  <T variant="label">{t('mobile.reader.citation', { number: highlight.n })}</T>
                  <T selectable>{parsed.sources[highlight.n].text}</T>
                  {(detail.items || [])
                    .filter((i) => i.citationNumber === highlight.n)
                    .map((i) => (
                      <ListRow key={i.id} icon="search-outline" title={t('mobile.reader.openCited', { type: t('mobile.reader.itemTypes.' + i.itemType) })} onPress={() => router.push(itemHref(i))} />
                    ))}
                </Card>
              ) : null}
              <LinkedItems detail={detail} />
              {briefing.status === 'Ready' ? <FeedbackBar targetType="Briefing" targetId={briefing.id} title={briefing.title} /> : null}
            </>
          )}
        </ResourceView>
      </Screen>
      {returnTo !== null ? (
        <View style={{ position: 'absolute', bottom: spacing.lg, alignSelf: 'center' }}>
          <Button title={t('mobile.reader.backToText')} icon="arrow-up" onPress={goBack} testID="back-to-text" />
        </View>
      ) : null}
    </View>
  );
}
