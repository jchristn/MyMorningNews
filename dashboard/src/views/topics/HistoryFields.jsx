import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import { HISTORY_BOUNDS } from '../../utils/comparison.js';

export const HISTORY_MODES = ['Off', 'PreviousIssue', 'RecentIssues', 'SinceDate', 'SpecificIssue'];

// "Compare with past briefings": the mode, count, start date, pinned issue, and (showBudget) the comparison budget.
// The pinned issue is chosen from the topic's ready briefings, or from editionId's when given (an edition compares
// with its own earlier issues, and its Off keeps the topics' own research comparisons). children render after the
// fields (a baseline preview).
export default function HistoryFields({ topicId = null, editionId = null, value, onChange, disabled, catalogPublicationId = null, showBudget = false, idPrefix = 'ctx-history', children = null }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [issues, setIssues] = useState([]);
  const [error, setError] = useState(null);
  const edition = Boolean(editionId);
  useEffect(() => {
    if (value.historyMode !== 'SpecificIssue') return;
    let live = true;
    const query = { ...(edition ? { editionId } : { topicId }), status: 'Ready', pageSize: 100 };
    const listing = catalogPublicationId ? apiClient.listAdminCatalogIssues(catalogPublicationId, query) : apiClient.listBriefings(query);
    listing
      .then((page) => {
        if (live) {
          setIssues(page.objects || []);
          setError(null);
        }
      })
      .catch((err) => {
        if (live) setError(err);
      });
    return () => {
      live = false;
    };
  }, [apiClient, topicId, editionId, edition, value.historyMode, catalogPublicationId]);
  const set = (patch) => onChange({ ...value, ...patch });
  return (
    <>
      <FormField id={idPrefix} label={t('topicHistory.title')} help={edition ? t('topicHistory.editionHelp') : t('topicHistory.help')} wide>
        <select id={idPrefix} value={value.historyMode} disabled={disabled} onChange={(e) => set({ historyMode: e.target.value })}>
          {HISTORY_MODES.map((mode) => (
            <option key={mode} value={mode}>
              {edition && mode === 'Off' ? t('topicHistory.editionOff') : t('topicHistory.' + mode)}
            </option>
          ))}
        </select>
      </FormField>
      {['RecentIssues', 'SinceDate'].includes(value.historyMode) ? (
        <FormField id={idPrefix + '-count'} label={t('topicHistory.count')}>
          <input
            id={idPrefix + '-count'}
            type="number"
            min={HISTORY_BOUNDS.count.min}
            max={HISTORY_BOUNDS.count.max}
            value={value.historyCount}
            disabled={disabled}
            onChange={(e) => set({ historyCount: e.target.value })}
          />
        </FormField>
      ) : null}
      {value.historyMode === 'SinceDate' ? (
        <FormField id={idPrefix + '-date'} label={t('topicHistory.date')}>
          <input
            id={idPrefix + '-date'}
            type="date"
            max={new Date().toISOString().slice(0, 10)}
            value={value.historySinceUtc}
            disabled={disabled}
            onChange={(e) => set({ historySinceUtc: e.target.value })}
          />
        </FormField>
      ) : null}
      {value.historyMode === 'SpecificIssue' ? (
        <FormField id={idPrefix + '-issue'} label={t('topicHistory.issue')} wide>
          <select
            id={idPrefix + '-issue'}
            value={value.historyBriefingId}
            disabled={disabled}
            onChange={(e) => set({ historyBriefingId: e.target.value })}
          >
            <option value="">{t('topicHistory.choose')}</option>
            {issues.map((issue) => (
              <option key={issue.id} value={issue.id}>
                {issue.title} ({issue.createdUtc?.slice(0, 10)})
              </option>
            ))}
          </select>
          <ErrorState error={error} inline />
        </FormField>
      ) : null}
      {showBudget && value.historyMode !== 'Off' ? (
        <FormField id={idPrefix + '-budget'} label={t('topicHistory.budget')} help={t('topicHistory.budgetHelp', HISTORY_BOUNDS.budget)}>
          <input
            id={idPrefix + '-budget'}
            type="number"
            min={HISTORY_BOUNDS.budget.min}
            max={HISTORY_BOUNDS.budget.max}
            step={256}
            value={value.historyTokenBudget}
            disabled={disabled}
            onChange={(e) => set({ historyTokenBudget: e.target.value })}
          />
        </FormField>
      ) : null}
      {value.historyMode !== 'Off' ? <p className="muted small form-field wide">{t('topicHistory.firstRun')}</p> : null}
      {edition && value.historyMode === 'Off' ? <p className="muted small form-field wide">{t('topicHistory.editionOffHelp')}</p> : null}
      {children}
    </>
  );
}
HistoryFields.propTypes = {
  topicId: PropTypes.string,
  editionId: PropTypes.string,
  showBudget: PropTypes.bool,
  idPrefix: PropTypes.string,
  children: PropTypes.node,
  value: PropTypes.object.isRequired,
  onChange: PropTypes.func.isRequired,
  disabled: PropTypes.bool,
  catalogPublicationId: PropTypes.string
};
