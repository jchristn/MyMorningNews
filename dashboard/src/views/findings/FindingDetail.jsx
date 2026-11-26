import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { BadgeCheck, ExternalLink, Eye, EyeOff, Pin, PinOff, Search, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { DIMENSION_WEIGHT_TONES, EVIDENCE_ROLE_TONES } from '../../utils/enums.js';
import ScoreBreakdownChart from '../../charts/ScoreBreakdownChart.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import DateCell from '../../components/DateCell.jsx';
import EmptyState from '../../components/EmptyState.jsx';
import FeedbackControl from '../../components/FeedbackControl.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import EvidenceModal from './EvidenceModal.jsx';
import { ScoreBadge, TierBadge, scoreComponents } from './findingParts.jsx';
import useFindingActions from './useFindingActions.jsx';

// Load one finding with its evidence, dimension, and topic name. Returns { detail, error, reload, setFinding }, where
// setFinding replaces the finding after a pin or hide without reloading the evidence.
export function useFindingDetail(findingId) {
  const { apiClient } = useAuth();
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState(null);
  const load = useCallback(async () => {
    if (!findingId) return;
    setError(null);
    try {
      setDetail(await apiClient.getFinding(findingId));
    } catch (err) {
      setError(err);
    }
  }, [apiClient, findingId]);
  useEffect(() => {
    setDetail(null);
    load();
  }, [load]);
  const setFinding = useCallback((finding) => setDetail((current) => (current ? { ...current, finding: { ...current.finding, ...finding } } : current)), []);
  return { detail, error, reload: load, clearError: () => setError(null), setFinding };
}

function Section({ title, children }) {
  return (
    <section className="detail-section">
      <div className="detail-section-header">
        <strong>{title}</strong>
      </div>
      <div className="detail-section-body">{children}</div>
    </section>
  );
}

Section.propTypes = { title: PropTypes.string.isRequired, children: PropTypes.node };

function EvidenceItem({ view, onOpen }) {
  const { t } = useTranslation();
  const e = view.evidence || {};
  const label = view.sourceTitle || view.sourceDomain || view.sourceUrl || t('findings.evidence.unknownSource');
  return (
    <li className="evidence-item">
      {e.id && onOpen ? (
        <button type="button" className="evidence-quote-button" onClick={() => onOpen(view)} title={t('findings.evidence.open')}>
          <blockquote className="evidence-quote">{e.quote}</blockquote>
        </button>
      ) : (
        <blockquote className="evidence-quote">{e.quote}</blockquote>
      )}
      <div className="evidence-meta">
        {view.role ? <StatusBadge tone={EVIDENCE_ROLE_TONES[view.role] || 'neutral'}>{t('enums.evidenceRole.' + view.role, { defaultValue: view.role })}</StatusBadge> : null}
        {e.verified ? (
          <StatusBadge tone="success">
            <BadgeCheck size={12} aria-hidden="true" /> {t('findings.evidence.verified')}
          </StatusBadge>
        ) : (
          <StatusBadge tone="warning">{t('findings.evidence.unverified')}</StatusBadge>
        )}
        {view.sourceUrl ? (
          <a href={view.sourceUrl} target="_blank" rel="noopener noreferrer" className="evidence-source">
            {label} <ExternalLink size={12} aria-hidden="true" />
          </a>
        ) : (
          <span className="evidence-source">{label}</span>
        )}
        {view.sourceDomain && view.sourceTitle ? <span className="muted small">{view.sourceDomain}</span> : null}
        {view.sourcePublishedUtc ? <DateCell value={view.sourcePublishedUtc} relative={false} /> : null}
        {e.id && onOpen ? (
          <button type="button" className="link-button small" onClick={() => onOpen(view)}>
            <Search size={12} aria-hidden="true" /> {t('findings.evidence.open')}
          </button>
        ) : null}
      </div>
    </li>
  );
}

EvidenceItem.propTypes = { view: PropTypes.object.isRequired, onOpen: PropTypes.func };

// Everything about one finding: badges, pin, hide, and delete, summary, the score breakdown, rationale, evidence
// quotes with source links (each opens the quote in context), the dimension, and provenance. onChanged receives the
// updated finding after a pin or hide; onDeleted runs after a delete.
export default function FindingDetail({ detail, onChanged, onDeleted }) {
  const { t } = useTranslation();
  const f = useFormat();
  const finding = detail.finding;
  const dimension = detail.dimension;
  const evidence = detail.evidence || [];
  const [openEvidence, setOpenEvidence] = useState(null);
  const actions = useFindingActions({ onChanged, onDeleted });
  const working = actions.busy === finding.id;
  return (
    <div className="stack" data-testid="finding-detail">
      <div className="result-grid">
        {finding.pinned ? (
          <StatusBadge tone="accent">
            <Pin size={12} aria-hidden="true" /> {t('findings.pinnedBadge')}
          </StatusBadge>
        ) : null}
        {finding.hidden ? <StatusBadge tone="neutral">{t('findings.hiddenBadge')}</StatusBadge> : null}
        <TierBadge tier={finding.tier} />
        <ScoreBadge finding={finding} />
        <StatusBadge>{t('enums.findingCategory.' + finding.category, { defaultValue: finding.category })}</StatusBadge>
        {finding.isDuplicate ? <StatusBadge tone="warning">{t('findings.duplicate')}</StatusBadge> : null}
        {finding.isUpdateOfFindingId ? <StatusBadge tone="info">{t('findings.update')}</StatusBadge> : null}
        {finding.isUpdateOfFindingId && finding.isCorrection ? <StatusBadge tone="warning">{t('findings.correction')}</StatusBadge> : null}
        {finding.isDryRun ? <StatusBadge tone="info">{t('runs.dryRun')}</StatusBadge> : null}
      </div>
      <h3 className="finding-title">{finding.title}</h3>
      <div className="row-actions finding-actions" data-testid="finding-actions">
        <button type="button" className="button secondary small" disabled={working} onClick={() => actions.togglePin(finding)}>
          {finding.pinned ? <PinOff size={14} aria-hidden="true" /> : <Pin size={14} aria-hidden="true" />} {finding.pinned ? t('findings.actions.unpin') : t('findings.actions.pin')}
        </button>
        <button type="button" className="button secondary small" disabled={working} onClick={() => actions.toggleHidden(finding)}>
          {finding.hidden ? <Eye size={14} aria-hidden="true" /> : <EyeOff size={14} aria-hidden="true" />} {finding.hidden ? t('findings.actions.unhide') : t('findings.actions.hide')}
        </button>
        <button type="button" className="button danger small" disabled={working} onClick={() => actions.requestDelete(finding)}>
          <Trash2 size={14} aria-hidden="true" /> {t('common.delete')}
        </button>
      </div>
      {finding.hidden ? <p className="muted small">{t('findings.hiddenHelp')}</p> : null}
      {finding.summary ? <p className="finding-summary">{finding.summary}</p> : null}
      <div className="feedback-row">
        <span className="muted small">{t('feedbackControl.isRelevant')}</span>
        <FeedbackControl targetType="Finding" targetId={finding.id} />
      </div>
      {finding.whyItMatters ? (
        <Section title={t('findings.detail.whyItMatters')}>
          <p className="finding-text">{finding.whyItMatters}</p>
        </Section>
      ) : null}
      <Section title={t('findings.detail.scoreBreakdown')}>
        <ScoreBreakdownChart
          components={scoreComponents(finding, t)}
          composite={Number(finding.relevanceScore) || 0}
          compositeLabel={t('findings.components.composite')}
          ariaLabel={t('findings.detail.scoreChartAria', { score: f.number(finding.relevanceScore, { maximumFractionDigits: 1 }) })}
        />
        <p className="muted small">{t('findings.detail.scoreHelp')}</p>
      </Section>
      <Section title={t('findings.detail.rationale')}>
        {finding.relevanceRationale ? <p className="finding-text">{finding.relevanceRationale}</p> : <p className="muted">{t('findings.detail.noRationale')}</p>}
      </Section>
      <Section title={t('findings.detail.evidence', { count: evidence.length })}>
        {evidence.length > 0 ? (
          <ol className="evidence-list">
            {evidence.map((v) => (
              <EvidenceItem key={(v.evidence && v.evidence.id) || v.sourceUrl} view={v} onOpen={setOpenEvidence} />
            ))}
          </ol>
        ) : (
          <EmptyState title={t('findings.evidence.emptyTitle')} message={t('findings.evidence.emptyMessage')} />
        )}
      </Section>
      <Section title={t('findings.detail.dimension')}>
        {dimension ? (
          <div className="stack">
            <span className="row-actions">
              <strong>{dimension.name}</strong>
              <StatusBadge tone={DIMENSION_WEIGHT_TONES[dimension.weight]}>{t('enums.dimensionWeight.' + dimension.weight)}</StatusBadge>
            </span>
            {dimension.description ? <span className="muted small">{dimension.description}</span> : null}
          </div>
        ) : (
          <p className="muted">{t('findings.detail.noDimension')}</p>
        )}
      </Section>
      <KeyValueGrid
        rows={[
          { key: 'topic', label: t('findings.columns.topic'), value: <Link to={'/dashboard/topics/' + encodeURIComponent(finding.topicId) + '?tab=findings'}>{detail.topicName || finding.topicId}</Link> },
          { key: 'run', label: t('findings.detail.run'), value: <Link to={'/dashboard/runs?run=' + encodeURIComponent(finding.runId)}>{finding.runId}</Link>, mono: true },
          { key: 'confidence', label: t('findings.detail.confidence'), value: f.percent(finding.confidence, 0) },
          { key: 'rank', label: t('findings.detail.rank'), value: finding.rank ? f.number(finding.rank) : null },
          { key: 'published', label: t('findings.columns.published'), value: <DateCell value={finding.publishedUtc} /> },
          { key: 'created', label: t('findings.columns.found'), value: <DateCell value={finding.createdUtc} /> },
          { key: 'duplicateOf', label: t('findings.detail.duplicateOf'), value: finding.duplicateOfFindingId ? <Link to={'/dashboard/findings/' + encodeURIComponent(finding.duplicateOfFindingId)}>{finding.duplicateOfFindingId}</Link> : null, hidden: !finding.duplicateOfFindingId },
          { key: 'updateOf', label: t('findings.detail.updateOf'), value: finding.isUpdateOfFindingId ? <Link to={'/dashboard/findings/' + encodeURIComponent(finding.isUpdateOfFindingId)}>{finding.isUpdateOfFindingId}</Link> : null, hidden: !finding.isUpdateOfFindingId },
          { key: 'id', label: t('findings.columns.id'), value: <CopyableId value={finding.id} /> }
        ]}
      />
      {actions.modals}
      {openEvidence ? <EvidenceModal evidenceId={openEvidence.evidence.id} role={openEvidence.role || null} onClose={() => setOpenEvidence(null)} /> : null}
    </div>
  );
}

FindingDetail.propTypes = {
  detail: PropTypes.shape({ finding: PropTypes.object.isRequired, evidence: PropTypes.array, dimension: PropTypes.object, topicName: PropTypes.string }).isRequired,
  onChanged: PropTypes.func,
  onDeleted: PropTypes.func
};

// Loading and error handling around FindingDetail for a drawer or page. onDeleted runs after the finding is deleted.
export function FindingDetailBody({ findingId, onDeleted }) {
  const { detail, error, reload, clearError, setFinding } = useFindingDetail(findingId);
  return (
    <>
      <ErrorState error={error} inline onRetry={reload} onDismiss={clearError} />
      {!detail && !error ? <LoadingState /> : null}
      {detail ? <FindingDetail detail={detail} onChanged={setFinding} onDeleted={onDeleted} /> : null}
    </>
  );
}

FindingDetailBody.propTypes = { findingId: PropTypes.string.isRequired, onDeleted: PropTypes.func };
