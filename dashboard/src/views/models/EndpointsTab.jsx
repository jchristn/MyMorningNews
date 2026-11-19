import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Braces, CopyPlus, ListTree, Pencil, Play, Plus, ShieldCheck, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { pagedQuery } from '../../utils/api.js';
import { MODEL_PROVIDERS } from '../../utils/enums.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import DataTable from '../../components/DataTable.jsx';
import DateCell from '../../components/DateCell.jsx';
import JsonViewerModal from '../../components/JsonViewerModal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import EndpointFormModal from './EndpointFormModal.jsx';
import { ModelsListModal, TestModal, ValidateModal } from './EndpointToolsModals.jsx';

export function Capabilities({ endpoint }) {
  const { t } = useTranslation();
  return (
    <span className="row-actions">
      {endpoint.supportsChat ? <StatusBadge tone="info">{t('models.capabilities.chat')}</StatusBadge> : null}
      {endpoint.supportsTools === true ? <StatusBadge tone="success">{t('models.capabilities.tools')}</StatusBadge> : null}
      {endpoint.supportsTools === false && endpoint.supportsChat ? <StatusBadge tone="warning">{t('models.capabilities.noTools')}</StatusBadge> : null}
      {endpoint.supportsEmbeddings ? (
        <StatusBadge tone="accent">{endpoint.embeddingDimensions ? t('models.capabilities.embeddingsDims', { dims: endpoint.embeddingDimensions }) : t('models.capabilities.embeddings')}</StatusBadge>
      ) : null}
    </span>
  );
}

Capabilities.propTypes = { endpoint: PropTypes.object.isRequired };

export default function EndpointsTab({ canWrite, canExecute, focusId, onChanged }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [form, setForm] = useState(null);
  const [testing, setTesting] = useState(null);
  const [validating, setValidating] = useState(null);
  const [listing, setListing] = useState(null);
  const [json, setJson] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [refresh, setRefresh] = useState(0);

  const fetchPage = useCallback((q) => apiClient.listModelEndpoints(pagedQuery(q)), [apiClient]);
  const bump = useCallback(() => {
    setRefresh((n) => n + 1);
    if (onChanged) onChanged();
  }, [onChanged]);

  // Deep link from the vault's "where used" list opens the endpoint.
  useEffect(() => {
    if (!focusId) return;
    apiClient
      .getModelEndpoint(focusId)
      .then((e) => {
        if (canWrite) setForm({ mode: 'edit', endpoint: e });
        else setJson(e);
      })
      .catch(() => {});
  }, [apiClient, focusId, canWrite]);

  const price = (r) => {
    if (r.inputCostPerMTok === null || r.inputCostPerMTok === undefined) return <span className="muted">{t('models.columns.noPrice')}</span>;
    return t('models.columns.priceValue', { input: f.usd(r.inputCostPerMTok), output: f.usd(r.outputCostPerMTok || 0) });
  };

  const columns = [
    {
      key: 'name',
      header: t('models.columns.name'),
      sortKey: 'name',
      pinned: true,
      render: (r) => (
        <span className="date-cell">
          <strong>{r.name}</strong>
          {r.endpoint ? <span className="muted small mono truncate">{r.endpoint}</span> : null}
        </span>
      )
    },
    { key: 'provider', header: t('models.columns.provider'), filter: { type: 'select', param: 'provider', options: MODEL_PROVIDERS.map((p) => ({ value: p, label: t('enums.provider.' + p) })) }, render: (r) => t('enums.provider.' + r.provider) },
    {
      key: 'model',
      header: t('models.columns.model'),
      render: (r) => (
        <span className="date-cell">
          {r.model ? <span className="mono">{r.model}</span> : null}
          {r.embeddingModel ? <span className="mono muted small">{r.embeddingModel}</span> : null}
        </span>
      )
    },
    { key: 'capabilities', header: t('models.columns.capabilities'), render: (r) => <Capabilities endpoint={r} /> },
    { key: 'price', header: t('models.columns.price'), className: 'hide-narrow', render: price },
    { key: 'reasoningEffort', header: t('models.columns.reasoningEffort'), defaultHidden: true, render: (r) => (r.reasoningEffort ? t('enums.reasoningEffort.' + r.reasoningEffort) : '') },
    { key: 'credential', header: t('models.columns.credential'), defaultHidden: true, render: (r) => (r.credentialId ? <CopyableId value={r.credentialId} /> : <span className="muted">{t('common.none')}</span>) },
    { key: 'lastValidatedUtc', header: t('models.columns.lastValidated'), defaultHidden: true, render: (r) => <DateCell value={r.lastValidatedUtc} /> },
    { key: 'active', header: t('models.columns.status'), filter: { type: 'boolean', param: 'active' }, render: (r) => <StatusBadge tone={r.active ? 'success' : 'neutral'}>{r.active ? t('common.active') : t('common.inactive')}</StatusBadge> },
    { key: 'id', header: t('models.columns.id'), defaultHidden: true, className: 'cell-id', render: (r) => <CopyableId value={r.id} /> }
  ];

  const addButton = (
    <button type="button" className="button primary small" onClick={() => setForm({ mode: 'create', endpoint: null })} disabled={!canWrite} title={canWrite ? undefined : t('models.adminOnly')}>
      <Plus size={14} aria-hidden="true" /> {t('models.add')}
    </button>
  );

  return (
    <>
      <DataTable
        tableKey="model-endpoints"
        columns={columns}
        fetchPage={fetchPage}
        refreshSignal={refresh}
        defaultSort={{ key: 'name', direction: 'asc' }}
        onRowClick={canWrite ? (r) => setForm({ mode: 'edit', endpoint: r }) : (r) => setJson(r)}
        toolbar={addButton}
        rowActions={(r) => [
          { key: 'edit', label: t('common.edit'), icon: Pencil, disabled: !canWrite, onClick: () => setForm({ mode: 'edit', endpoint: r }) },
          { key: 'test', label: t('models.actions.test'), icon: Play, disabled: !canExecute, onClick: () => setTesting(r) },
          { key: 'validate', label: t('models.actions.validate'), icon: ShieldCheck, disabled: !canExecute, onClick: () => setValidating(r) },
          { key: 'models', label: t('models.actions.listModels'), icon: ListTree, disabled: !canExecute, onClick: () => setListing(r) },
          { key: 'dup', label: t('common.duplicate'), icon: CopyPlus, disabled: !canWrite, onClick: () => setForm({ mode: 'duplicate', endpoint: r }) },
          { key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) },
          { key: 'delete', label: t('common.delete'), icon: Trash2, danger: true, disabled: !canWrite, onClick: () => setDeleting(r) }
        ]}
        emptyTitle={t('models.empty.title')}
        emptyMessage={t('models.empty.message')}
        emptyAction={canWrite ? addButton : null}
      />
      <EndpointFormModal
        state={form}
        onClose={() => setForm(null)}
        onSaved={(saved, isEdit) => {
          setForm(null);
          addNotification(isEdit ? t('admin.saved') : t('models.created', { name: saved.name }), 'success');
          bump();
        }}
      />
      <TestModal endpoint={testing} onClose={() => setTesting(null)} />
      <ValidateModal endpoint={validating} onClose={() => setValidating(null)} onDone={bump} />
      <ModelsListModal
        endpoint={listing}
        canWrite={canWrite}
        onClose={() => setListing(null)}
        onUse={async (endpoint, model) => {
          const saved = await apiClient.updateModelEndpoint(endpoint.id, { model });
          setListing(saved);
          addNotification(t('models.list.used', { model }), 'success');
          bump();
        }}
      />
      <JsonViewerModal open={Boolean(json)} value={json} onClose={() => setJson(null)} />
      <ConfirmModal
        open={Boolean(deleting)}
        title={t('models.delete.title')}
        message={deleting ? t('models.delete.message', { name: deleting.name }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteModelEndpoint(deleting.id);
          addNotification(t('admin.deleted'), 'success');
          bump();
        }}
      />
    </>
  );
}

EndpointsTab.propTypes = {
  canWrite: PropTypes.bool,
  canExecute: PropTypes.bool,
  focusId: PropTypes.string,
  onChanged: PropTypes.func
};
