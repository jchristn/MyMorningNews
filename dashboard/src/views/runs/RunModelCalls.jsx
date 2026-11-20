import { useCallback } from 'react';
import PropTypes from 'prop-types';
import { useNavigate } from 'react-router-dom';
import { Eye } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useCanReadCalls from '../../hooks/useCanReadCalls.js';
import useFormat from '../../hooks/useFormat.js';
import { pagedQuery } from '../../utils/api.js';
import DataTable from '../../components/DataTable.jsx';
import { llmCallColumns } from '../models/callColumns.jsx';

// The model calls one run made, oldest first: task, model, prompt key, tokens, cost, duration, and outcome. Callers
// who may read the model call log open a call's full record there.
export default function RunModelCalls({ runId }) {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const { apiClient } = useAuth();
  const canReadCalls = useCanReadCalls();
  const fetchPage = useCallback((q) => apiClient.listRunLlmCalls(runId, pagedQuery(q)), [apiClient, runId]);
  const open = (r) => navigate('/dashboard/models?tab=calls&call=' + encodeURIComponent(r.id));
  return (
    <div data-testid="run-llm-calls">
      <DataTable
        tableKey="run-llm-calls"
        columns={llmCallColumns({ t, f, promptKeyHidden: false })}
        fetchPage={fetchPage}
        defaultSort={{ key: 'createdUtc', direction: 'asc' }}
        defaultPageSize={25}
        onRowClick={canReadCalls ? open : undefined}
        rowActions={canReadCalls ? (r) => [{ key: 'view', label: t('runs.calls.openCall'), icon: Eye, onClick: () => open(r) }] : undefined}
        emptyTitle={t('runs.calls.emptyTitle')}
        emptyMessage={t('runs.calls.emptyMessage')}
      />
    </div>
  );
}

RunModelCalls.propTypes = { runId: PropTypes.string.isRequired };
