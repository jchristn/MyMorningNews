import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link, useNavigate } from 'react-router-dom';
import { GitBranchPlus } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import DateCell from '../../components/DateCell.jsx';
import EmptyState from '../../components/EmptyState.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import { OverrideModal } from '../prompts/PromptParts.jsx';

// The topic's prompt overrides: prompts this topic uses in place of the tenant's, each opening in the prompt editor.
// Every other prompt key uses the tenant version.
export default function TopicPrompts({ topicId }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const [items, setItems] = useState(null);
  const [error, setError] = useState(null);
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      const page = await apiClient.listPrompts({ topicId, pageSize: 100, sortBy: 'key', sortDirection: 'Ascending' });
      setItems(((page && page.objects) || []).filter((p) => p.topicId === topicId));
    } catch (err) {
      setError(err);
    }
  }, [apiClient, topicId]);

  useEffect(() => {
    load();
  }, [load]);

  const open = (id) => navigate('/dashboard/prompts/' + encodeURIComponent(id));
  const addButton = (
    <button type="button" className="button primary small" onClick={() => setCreating(true)}>
      <GitBranchPlus size={14} aria-hidden="true" /> {t('topics.prompts.add')}
    </button>
  );

  return (
    <div className="stack" data-testid="topic-prompts">
      <div className="section-heading">
        <p className="muted small">{t('topics.prompts.help')}</p>
        {addButton}
      </div>
      <ErrorState error={error} inline onRetry={load} />
      {items === null && !error ? <LoadingState /> : null}
      {items && items.length === 0 ? <EmptyState title={t('topics.prompts.emptyTitle')} message={t('topics.prompts.emptyMessage')} /> : null}
      {items && items.length > 0 ? (
        <ul className="event-list">
          {items.map((p) => (
            <li key={p.id}>
              <Link to={'/dashboard/prompts/' + encodeURIComponent(p.id)} className="mono">
                {p.key}
              </Link>
              <span className="muted small">{t('enums.task.' + p.role)}</span>
              <StatusBadge tone="info">{t('prompts.versionLabel', { version: p.currentVersion })}</StatusBadge>
              <DateCell value={p.lastUpdateUtc} />
            </li>
          ))}
        </ul>
      ) : null}
      <p className="small">
        <Link to="/dashboard/prompts">{t('topics.prompts.allPrompts')}</Link>
      </p>
      <OverrideModal open={creating} fixedTopicId={topicId} onClose={() => setCreating(false)} onCreated={(created) => open(created.template.id)} />
    </div>
  );
}

TopicPrompts.propTypes = { topicId: PropTypes.string.isRequired };
