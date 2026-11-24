import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';

export const HISTORY_MODES = ['Off', 'PreviousIssue', 'RecentIssues', 'SinceDate', 'SpecificIssue'];

export default function HistoryFields({ topicId, value, onChange, disabled, catalogPublicationId = null }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [issues, setIssues] = useState([]);
  const [error, setError] = useState(null);
  useEffect(() => {
    if (value.historyMode !== 'SpecificIssue') return;
    let live = true;
    const query = { topicId, status: 'Ready', pageSize: 100 };
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
  }, [apiClient, topicId, value.historyMode, catalogPublicationId]);
  const set = (patch) => onChange({ ...value, ...patch });
  return (
    <>
      <FormField id="ctx-history" label={t('topicHistory.title')} help={t('topicHistory.help')} wide>
        <select id="ctx-history" value={value.historyMode} disabled={disabled} onChange={(e) => set({ historyMode: e.target.value })}>
          {HISTORY_MODES.map((mode) => (
            <option key={mode} value={mode}>
              {t('topicHistory.' + mode)}
            </option>
          ))}
        </select>
      </FormField>
      {['RecentIssues', 'SinceDate'].includes(value.historyMode) ? (
        <FormField id="ctx-history-count" label={t('topicHistory.count')}>
          <input
            id="ctx-history-count"
            type="number"
            min={1}
            max={20}
            value={value.historyCount}
            disabled={disabled}
            onChange={(e) => set({ historyCount: e.target.value })}
          />
        </FormField>
      ) : null}
      {value.historyMode === 'SinceDate' ? (
        <FormField id="ctx-history-date" label={t('topicHistory.date')}>
          <input
            id="ctx-history-date"
            type="date"
            max={new Date().toISOString().slice(0, 10)}
            value={value.historySinceUtc}
            disabled={disabled}
            onChange={(e) => set({ historySinceUtc: e.target.value })}
          />
        </FormField>
      ) : null}
      {value.historyMode === 'SpecificIssue' ? (
        <FormField id="ctx-history-issue" label={t('topicHistory.issue')} wide>
          <select
            id="ctx-history-issue"
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
      {value.historyMode !== 'Off' ? <p className="muted small form-field wide">{t('topicHistory.firstRun')}</p> : null}
    </>
  );
}
HistoryFields.propTypes = {
  topicId: PropTypes.string.isRequired,
  value: PropTypes.object.isRequired,
  onChange: PropTypes.func.isRequired,
  disabled: PropTypes.bool,
  catalogPublicationId: PropTypes.string
};
