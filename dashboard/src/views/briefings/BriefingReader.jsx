import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, Download, FileCode, FileText, Headphones, MessageSquare, Pin, PinOff, RefreshCw, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import useInterval from '../../hooks/useInterval.js';
import { citationAnchor, itemHref, matchSections, saveBlob, sectionAnchor, splitBriefingMarkdown } from '../../utils/briefing.js';
import { ACTIVE_BRIEFING_STATUSES } from '../../utils/enums.js';
import { describeError } from '../../utils/errors.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import DateCell from '../../components/DateCell.jsx';
import EmptyState from '../../components/EmptyState.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import BriefingMarkdown, { scrollToAnchor } from './BriefingMarkdown.jsx';
import useBriefingActions, { BriefingKindBadge, BriefingStatusBadge } from './briefingParts.jsx';

const POLL_MS = 3000;

// Placeholder for features whose backend arrives later. Marked with data-slot so the later phase can find it.
export function UpcomingSlot({ slot, icon: Icon, title, message, compact = false }) {
  return (
    <div className={'upcoming-slot' + (compact ? ' compact' : '')} data-slot={slot}>
      <Icon size={compact ? 13 : 16} aria-hidden="true" />
      <span>
        <strong>{title}</strong>
        {message ? <span className="muted small"> {message}</span> : null}
      </span>
    </div>
  );
}

UpcomingSlot.propTypes = { slot: PropTypes.string.isRequired, icon: PropTypes.elementType.isRequired, title: PropTypes.string.isRequired, message: PropTypes.string, compact: PropTypes.bool };

// The outline: one entry per section, with how many findings, predictions, and suggestions it carries.
function Outline({ chunks, counts }) {
  const { t } = useTranslation();
  if (chunks.length === 0) return null;
  return (
    <nav className="briefing-outline" aria-label={t('briefings.reader.outline')}>
      <span className="field-label">{t('briefings.reader.outline')}</span>
      <ol>
        {chunks.map((c) => (
          <li key={c.key}>
            <a
              href={'#' + sectionAnchor(c.key)}
              onClick={(e) => {
                e.preventDefault();
                scrollToAnchor(sectionAnchor(c.key));
              }}
            >
              <span className="truncate">{c.heading}</span>
              {c.section && counts.get(c.section.id) ? <span className="muted small">{counts.get(c.section.id)}</span> : null}
            </a>
          </li>
        ))}
      </ol>
    </nav>
  );
}

Outline.propTypes = { chunks: PropTypes.array.isRequired, counts: PropTypes.instanceOf(Map).isRequired };

// Findings, predictions, and suggestions the briefing used, grouped by section, with links to each.
function ItemList({ items, sections }) {
  const { t } = useTranslation();
  const groups = useMemo(() => {
    const bySection = new Map();
    for (const item of items) {
      if (!bySection.has(item.sectionId)) bySection.set(item.sectionId, []);
      bySection.get(item.sectionId).push(item);
    }
    const ordered = sections.filter((s) => bySection.has(s.id)).map((s) => ({ section: s, items: bySection.get(s.id) }));
    const known = new Set(sections.map((s) => s.id));
    const orphans = items.filter((i) => !known.has(i.sectionId));
    if (orphans.length > 0) ordered.push({ section: null, items: orphans });
    return ordered;
  }, [items, sections]);

  if (items.length === 0) return <p className="muted small">{t('briefings.reader.noItems')}</p>;
  return (
    <div className="stack">
      {groups.map((g) => (
        <div key={g.section ? g.section.id : 'other'}>
          <span className="field-label">{g.section ? g.section.heading : t('briefings.reader.otherItems')}</span>
          <ul className="briefing-items">
            {g.items.map((item) => {
              const href = itemHref(item);
              return (
                <li key={item.id}>
                  <StatusBadge tone={item.itemType === 'Finding' ? 'accent' : item.itemType === 'Prediction' ? 'info' : 'warning'}>{t('enums.briefingItemType.' + item.itemType)}</StatusBadge>
                  {item.citationNumber ? (
                    <a
                      href={'#' + citationAnchor(item.citationNumber)}
                      className="citation-link"
                      aria-label={t('briefings.reader.citation', { number: item.citationNumber })}
                      onClick={(e) => {
                        e.preventDefault();
                        scrollToAnchor(citationAnchor(item.citationNumber));
                      }}
                    >
                      {'[' + item.citationNumber + ']'}
                    </a>
                  ) : null}
                  <CopyableId value={item.itemId} />
                  {href ? (
                    <Link to={href} className="small">
                      {t('briefings.reader.open.' + item.itemType)}
                    </Link>
                  ) : null}
                </li>
              );
            })}
          </ul>
        </div>
      ))}
    </div>
  );
}

ItemList.propTypes = { items: PropTypes.array.isRequired, sections: PropTypes.array.isRequired };

function FileList({ files, onDownload }) {
  const { t } = useTranslation();
  const f = useFormat();
  if (files.length === 0) return <p className="muted small">{t('briefings.reader.noFiles')}</p>;
  return (
    <ul className="briefing-files">
      {files.map((file) => (
        <li key={file.id}>
          <span className="date-cell">
            <strong className="mono truncate">{file.fileName}</strong>
            <span className="muted small">
              {t('enums.artifactKind.' + file.kind)} {' · '} {f.bytes(file.bytes)}
            </span>
          </span>
          {file.deletedUtc ? (
            <StatusBadge tone="warning">{t('briefings.files.expired')}</StatusBadge>
          ) : file.pinned ? (
            <StatusBadge tone="accent">{t('briefings.files.pinned')}</StatusBadge>
          ) : file.expiresUtc ? (
            <span className="muted small">{t('briefings.files.expires', { date: f.date(file.expiresUtc) })}</span>
          ) : null}
          {!file.deletedUtc ? (
            <button type="button" className="icon-button" onClick={() => onDownload(file)} aria-label={t('briefings.files.download', { name: file.fileName })} title={t('briefings.files.download', { name: file.fileName })}>
              <Download size={14} aria-hidden="true" />
            </button>
          ) : null}
        </li>
      ))}
    </ul>
  );
}

FileList.propTypes = { files: PropTypes.array.isRequired, onDownload: PropTypes.func.isRequired };

// The briefing reader: sanitized Markdown with an outline, citations linked to Sources, the items it used, its
// stored files, downloads, pin, and Regenerate when the files passed retention. Polls while composing.
export default function BriefingReader({ briefingId }) {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const actions = useBriefingActions();
  const [detail, setDetail] = useState(null);
  const [loadError, setLoadError] = useState(null);
  const [busy, setBusy] = useState(null);
  const [deleting, setDeleting] = useState(false);

  const load = useCallback(async () => {
    try {
      const d = await apiClient.getBriefing(briefingId);
      setDetail(d);
      setLoadError(null);
    } catch (err) {
      setLoadError(err);
    }
  }, [apiClient, briefingId]);

  useEffect(() => {
    load();
  }, [load]);

  const briefing = detail ? detail.briefing : null;
  const active = Boolean(briefing && ACTIVE_BRIEFING_STATUSES.includes(briefing.status));
  useInterval(load, active ? POLL_MS : null);

  const sections = useMemo(() => (detail && Array.isArray(detail.sections) ? detail.sections.slice().sort((a, b) => a.sortOrder - b.sortOrder) : []), [detail]);
  const items = useMemo(() => (detail && Array.isArray(detail.items) ? detail.items : []), [detail]);
  const chunks = useMemo(() => (briefing ? matchSections(splitBriefingMarkdown(briefing.markdown).chunks, sections) : []), [briefing, sections]);
  const counts = useMemo(() => {
    const map = new Map();
    for (const i of items) map.set(i.sectionId, (map.get(i.sectionId) || 0) + 1);
    return map;
  }, [items]);

  if (loadError && !detail) {
    return (
      <div className="page">
        <Link to="/dashboard/briefings" className="link-button small back-link">
          <ArrowLeft size={14} aria-hidden="true" /> {t('briefings.reader.back')}
        </Link>
        <ErrorState error={loadError} onRetry={load} />
      </div>
    );
  }
  if (!detail) return <LoadingState />;

  const expired = Boolean(detail.filesExpired);
  const ready = briefing.status === 'Ready';

  async function run(key, fn) {
    setBusy(key);
    try {
      await fn();
    } finally {
      setBusy(null);
    }
  }

  async function download(format) {
    await run('download-' + format, async () => {
      const outcome = await actions.download(briefing, format);
      if (outcome === 'expired') setDetail((d) => ({ ...d, filesExpired: true }));
    });
  }

  async function downloadFile(file) {
    try {
      const result = await apiClient.getArtifactContent(file.id, { download: true });
      saveBlob(result.filename || file.fileName, result.blob);
    } catch (err) {
      addNotification(describeError(err, t).message, 'error', 8000);
    }
  }

  const subject = [
    detail.editionName ? (
      <Link key="edition" to={'/dashboard/editions/' + encodeURIComponent(briefing.editionId)}>
        {detail.editionName}
      </Link>
    ) : null,
    detail.topicName ? (
      <Link key="topic" to={'/dashboard/topics/' + encodeURIComponent(briefing.topicId)}>
        {detail.topicName}
      </Link>
    ) : null,
    briefing.runId ? (
      <Link key="run" to={'/dashboard/runs?run=' + encodeURIComponent(briefing.runId)}>
        {t('briefings.reader.openRun')}
      </Link>
    ) : null
  ].filter(Boolean);

  return (
    <div className="page page-wide">
      <Link to="/dashboard/briefings" className="link-button small back-link">
        <ArrowLeft size={14} aria-hidden="true" /> {t('briefings.reader.back')}
      </Link>
      <PageHeader
        title={briefing.title || t('briefings.untitled')}
        meta={
          <div className="prompt-meta">
            <BriefingKindBadge kind={briefing.kind} />
            <BriefingStatusBadge status={briefing.status} />
            {briefing.pinned ? <StatusBadge tone="accent">{t('briefings.pin.badge')}</StatusBadge> : null}
            {briefing.isDryRun ? <StatusBadge tone="warning">{t('briefings.dryRun')}</StatusBadge> : null}
            {subject.map((node, i) => (
              <span key={i} className="small">
                {node}
              </span>
            ))}
            {briefing.composedUtc ? <span className="muted small">{f.dateTime(briefing.composedUtc)}</span> : null}
            {ready ? <span className="muted small">{t('briefings.reader.stats', { minutes: briefing.readMinutes || 0, words: f.number(briefing.wordCount || 0), items: briefing.itemCount || 0 })}</span> : null}
          </div>
        }
        actions={
          <div className="row-actions">
            <button type="button" className="button secondary small" disabled={!ready || expired || busy !== null} onClick={() => download('md')}>
              <FileText size={14} aria-hidden="true" /> {t('briefings.download.md')}
            </button>
            <button type="button" className="button secondary small" disabled={!ready || expired || busy !== null} onClick={() => download('html')}>
              <FileCode size={14} aria-hidden="true" /> {t('briefings.download.html')}
            </button>
            <button
              type="button"
              className="button secondary small"
              disabled={busy !== null}
              aria-pressed={briefing.pinned}
              onClick={() =>
                run('pin', async () => {
                  const updated = await actions.setPinned(briefing, !briefing.pinned);
                  if (updated) await load();
                })
              }
            >
              {briefing.pinned ? <PinOff size={14} aria-hidden="true" /> : <Pin size={14} aria-hidden="true" />} {briefing.pinned ? t('briefings.pin.unpin') : t('briefings.pin.pin')}
            </button>
            <button type="button" className="button danger small" disabled={busy !== null} onClick={() => setDeleting(true)}>
              <Trash2 size={14} aria-hidden="true" /> {t('common.delete')}
            </button>
          </div>
        }
      />

      {briefing.status === 'Failed' ? (
        <div className="notice notice-danger" role="alert">
          <strong>{t('briefings.reader.failed')}</strong> {briefing.errorMessage || ''}
        </div>
      ) : null}
      {active ? (
        <div className="notice notice-info" role="status" data-testid="briefing-progress">
          {briefing.status === 'Queued' && briefing.researchDeadlineUtc
            ? t('briefings.progress.researching', { time: f.time(briefing.researchDeadlineUtc) })
            : t('briefings.progress.' + briefing.status)}
        </div>
      ) : null}
      {expired ? (
        <div className="notice notice-warning expired-notice" role="status">
          <span>
            <strong>{t('briefings.expired.title')}</strong> {t('briefings.expired.message')}
          </span>
          <button
            type="button"
            className="button primary small"
            disabled={busy !== null}
            onClick={() =>
              run('regenerate', async () => {
                const updated = await actions.regenerate(briefing);
                if (updated) await load();
              })
            }
          >
            <RefreshCw size={14} aria-hidden="true" /> {busy === 'regenerate' ? t('common.working') : t('briefings.expired.regenerate')}
          </button>
        </div>
      ) : null}

      <UpcomingSlot slot="audio-player" icon={Headphones} title={t('briefings.slots.audioTitle')} message={t('briefings.slots.audioMessage')} />

      <div className="briefing-layout">
        <aside className="briefing-aside">
          <Outline chunks={chunks} counts={counts} />
        </aside>
        <section className="panel briefing-body">
          {briefing.markdown ? (
            <BriefingMarkdown
              markdown={briefing.markdown}
              sections={sections}
              sectionExtra={(chunk) =>
                chunk.section && chunk.section.source !== 'citations' ? <UpcomingSlot slot="section-feedback" compact icon={MessageSquare} title={t('briefings.slots.feedbackTitle')} message={t('briefings.slots.feedbackMessage')} /> : null
              }
            />
          ) : (
            <EmptyState title={active ? t('briefings.progress.' + briefing.status) : t('briefings.reader.noContent')} />
          )}
        </section>
      </div>

      <div className="two-column">
        <section className="panel">
          <h2>{t('briefings.reader.items', { count: items.length })}</h2>
          <ItemList items={items} sections={sections} />
        </section>
        <section className="panel">
          <h2>{t('briefings.reader.files')}</h2>
          <FileList files={detail.files || []} onDownload={downloadFile} />
          {briefing.periodStartUtc || briefing.periodEndUtc ? (
            <p className="muted small">
              {t('briefings.reader.period')} <DateCell value={briefing.periodStartUtc} relative={false} /> {' - '} <DateCell value={briefing.periodEndUtc} relative={false} />
            </p>
          ) : null}
        </section>
      </div>

      <ConfirmModal
        open={deleting}
        title={t('briefings.delete.title')}
        message={t('briefings.delete.message', { title: briefing.title || '' })}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(false)}
        onConfirm={async () => {
          await apiClient.deleteBriefing(briefing.id);
          addNotification(t('briefings.delete.done'), 'success');
          navigate('/dashboard/briefings');
        }}
      />
    </div>
  );
}

BriefingReader.propTypes = { briefingId: PropTypes.string.isRequired };
