import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Plus, RotateCcw, Save, Trash2, TriangleAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { MODEL_TASKS, REASONING_EFFORTS, STRUCTURED_TASKS } from '../../utils/enums.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import { endpointName } from './useModelEndpoints.js';

function draftFrom(assignment) {
  return {
    modelEndpointId: assignment ? assignment.modelEndpointId : '',
    modelOverride: assignment && assignment.modelOverride ? assignment.modelOverride : '',
    reasoningEffort: assignment && assignment.reasoningEffort ? assignment.reasoningEffort : ''
  };
}

function sameDraft(a, b) {
  return a.modelEndpointId === b.modelEndpointId && a.modelOverride.trim() === b.modelOverride.trim() && a.reasoningEffort === b.reasoningEffort;
}

export function eligibleEndpoints(endpoints, task) {
  return endpoints.filter((e) => (task === 'Embedding' ? e.supportsEmbeddings : e.supportsChat));
}

function EndpointSelect({ id, task, endpoints, value, onChange, disabled, label }) {
  const { t } = useTranslation();
  const options = eligibleEndpoints(endpoints, task);
  return (
    <select id={id} value={value} onChange={(e) => onChange(e.target.value)} disabled={disabled} aria-label={label}>
      {!value ? <option value="">{t('models.assignments.pickEndpoint')}</option> : null}
      {options.map((e) => (
        <option key={e.id} value={e.id}>
          {e.name + (e.active ? '' : ' (' + t('common.inactive') + ')')}
        </option>
      ))}
      {value && !options.some((e) => e.id === value) ? <option value={value}>{endpointName(endpoints, value)}</option> : null}
    </select>
  );
}

EndpointSelect.propTypes = {
  id: PropTypes.string,
  task: PropTypes.string.isRequired,
  endpoints: PropTypes.array.isRequired,
  value: PropTypes.string.isRequired,
  onChange: PropTypes.func.isRequired,
  disabled: PropTypes.bool,
  label: PropTypes.string.isRequired
};

function EffortSelect({ id, value, onChange, disabled, label }) {
  const { t } = useTranslation();
  return (
    <select id={id} value={value} onChange={(e) => onChange(e.target.value)} disabled={disabled} aria-label={label}>
      <option value="">{t('models.assignments.endpointDefault')}</option>
      {REASONING_EFFORTS.map((r) => (
        <option key={r} value={r}>
          {t('enums.reasoningEffort.' + r)}
        </option>
      ))}
    </select>
  );
}

EffortSelect.propTypes = { id: PropTypes.string, value: PropTypes.string.isRequired, onChange: PropTypes.func.isRequired, disabled: PropTypes.bool, label: PropTypes.string.isRequired };

const EMPTY_OVERRIDE = { task: 'Synthesis', topicId: '', modelEndpointId: '', modelOverride: '', reasoningEffort: '' };

export default function AssignmentsTab({ endpoints, canWrite }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [assignments, setAssignments] = useState(null);
  const [error, setError] = useState(null);
  const [drafts, setDrafts] = useState({});
  const [rowErrors, setRowErrors] = useState({});
  const [saving, setSaving] = useState(null);
  const [override, setOverride] = useState(EMPTY_OVERRIDE);
  const [overrideError, setOverrideError] = useState(null);
  const [addingOverride, setAddingOverride] = useState(false);
  const [deleting, setDeleting] = useState(null);

  const load = useCallback(async () => {
    try {
      const page = await apiClient.listModelAssignments({ pageSize: 1000 });
      const items = page && page.items ? page.items : [];
      setAssignments(items);
      const next = {};
      for (const task of MODEL_TASKS) next[task] = draftFrom(items.find((a) => a.task === task && !a.topicId));
      setDrafts(next);
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient]);

  useEffect(() => {
    load();
  }, [load]);

  const defaults = useMemo(() => {
    const map = {};
    for (const a of assignments || []) if (!a.topicId) map[a.task] = a;
    return map;
  }, [assignments]);
  const overrides = (assignments || []).filter((a) => a.topicId).sort((a, b) => (a.topicId + a.task).localeCompare(b.topicId + b.task));

  function update(task, patch) {
    setDrafts((d) => ({ ...d, [task]: { ...d[task], ...patch } }));
  }

  async function saveRow(task) {
    const draft = drafts[task];
    setSaving(task);
    setRowErrors((e) => ({ ...e, [task]: null }));
    try {
      await apiClient.upsertModelAssignment({ task, modelEndpointId: draft.modelEndpointId, modelOverride: draft.modelOverride.trim() || null, reasoningEffort: draft.reasoningEffort || null });
      addNotification(t('models.assignments.saved', { role: t('enums.task.' + task) }), 'success');
      await load();
    } catch (err) {
      setRowErrors((e) => ({ ...e, [task]: err }));
    } finally {
      setSaving(null);
    }
  }

  async function addOverride() {
    setAddingOverride(true);
    setOverrideError(null);
    try {
      await apiClient.upsertModelAssignment({
        task: override.task,
        topicId: override.topicId.trim(),
        modelEndpointId: override.modelEndpointId,
        modelOverride: override.modelOverride.trim() || null,
        reasoningEffort: override.reasoningEffort || null
      });
      addNotification(t('models.assignments.overrideAdded'), 'success');
      setOverride(EMPTY_OVERRIDE);
      await load();
    } catch (err) {
      setOverrideError(err);
    } finally {
      setAddingOverride(false);
    }
  }

  if (error && !assignments) return <ErrorState error={error} onRetry={load} />;
  if (!assignments) return <LoadingState />;

  const toolWarning = (task, endpointId) => {
    const e = endpoints.find((x) => x.id === endpointId);
    return STRUCTURED_TASKS.includes(task) && e && e.supportsTools === false;
  };

  return (
    <div className="stack">
      <p className="muted">{t('models.assignments.intro')}</p>
      <div className="table-scroll">
        <table className="assign-table">
          <thead>
            <tr>
              <th scope="col">{t('models.assignments.role')}</th>
              <th scope="col">{t('models.assignments.endpoint')}</th>
              <th scope="col">{t('models.assignments.modelOverride')}</th>
              <th scope="col">{t('models.assignments.reasoningEffort')}</th>
              <th scope="col">
                <span className="sr-only">{t('table.actions')}</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {MODEL_TASKS.map((task) => {
              const draft = drafts[task] || draftFrom(null);
              const original = draftFrom(defaults[task]);
              const dirty = !sameDraft(draft, original);
              const endpoint = endpoints.find((e) => e.id === draft.modelEndpointId);
              const roleLabel = t('enums.task.' + task);
              return (
                <tr key={task} data-testid={'assignment-' + task}>
                  <td>
                    <span className="assign-role">
                      <strong>{roleLabel}</strong>
                      <span className="muted small">{t('models.assignments.uses.' + task)}</span>
                      {toolWarning(task, draft.modelEndpointId) ? (
                        <StatusBadge tone="warning" title={t('models.assignments.noToolsHelp')}>
                          <TriangleAlert size={12} aria-hidden="true" /> {t('models.assignments.noTools')}
                        </StatusBadge>
                      ) : null}
                      {rowErrors[task] ? <ErrorState error={rowErrors[task]} inline onDismiss={() => setRowErrors((e) => ({ ...e, [task]: null }))} /> : null}
                    </span>
                  </td>
                  <td>
                    <EndpointSelect task={task} endpoints={endpoints} value={draft.modelEndpointId} onChange={(v) => update(task, { modelEndpointId: v })} disabled={!canWrite} label={t('models.assignments.endpointFor', { role: roleLabel })} />
                  </td>
                  <td>
                    <input
                      className="mono"
                      value={draft.modelOverride}
                      onChange={(e) => update(task, { modelOverride: e.target.value })}
                      placeholder={endpoint ? (task === 'Embedding' ? endpoint.embeddingModel : endpoint.model) || '' : ''}
                      disabled={!canWrite}
                      aria-label={t('models.assignments.modelOverrideFor', { role: roleLabel })}
                    />
                  </td>
                  <td>
                    {task === 'Embedding' ? (
                      <span className="muted">{t('models.assignments.notApplicable')}</span>
                    ) : (
                      <EffortSelect value={draft.reasoningEffort} onChange={(v) => update(task, { reasoningEffort: v })} disabled={!canWrite} label={t('models.assignments.effortFor', { role: roleLabel })} />
                    )}
                  </td>
                  <td className="align-right">
                    {canWrite ? (
                      <span className="row-actions">
                        <button type="button" className="button primary small" onClick={() => saveRow(task)} disabled={!dirty || !draft.modelEndpointId || saving === task}>
                          <Save size={14} aria-hidden="true" /> {saving === task ? t('common.saving') : t('common.save')}
                        </button>
                        <button type="button" className="icon-button" onClick={() => update(task, original)} disabled={!dirty} aria-label={t('models.assignments.undo')} title={t('models.assignments.undo')}>
                          <RotateCcw size={14} aria-hidden="true" />
                        </button>
                      </span>
                    ) : null}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>

      <div className="section-title">
        <h3>{t('models.assignments.overridesTitle')}</h3>
      </div>
      <p className="muted small">{t('models.assignments.overridesIntro')}</p>
      {overrides.length === 0 ? <p className="muted">{t('models.assignments.noOverrides')}</p> : null}
      {overrides.length > 0 ? (
        <div className="table-scroll">
          <table className="assign-table">
            <thead>
              <tr>
                <th scope="col">{t('models.assignments.topic')}</th>
                <th scope="col">{t('models.assignments.role')}</th>
                <th scope="col">{t('models.assignments.endpoint')}</th>
                <th scope="col">{t('models.assignments.modelOverride')}</th>
                <th scope="col">{t('models.assignments.reasoningEffort')}</th>
                <th scope="col">
                  <span className="sr-only">{t('table.actions')}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {overrides.map((a) => (
                <tr key={a.id}>
                  <td>
                    <CopyableId value={a.topicId} />
                  </td>
                  <td>{t('enums.task.' + a.task)}</td>
                  <td>{endpointName(endpoints, a.modelEndpointId)}</td>
                  <td className="mono">{a.modelOverride || ''}</td>
                  <td>{a.reasoningEffort ? t('enums.reasoningEffort.' + a.reasoningEffort) : ''}</td>
                  <td className="align-right">
                    {canWrite ? (
                      <button type="button" className="icon-button" onClick={() => setDeleting(a)} aria-label={t('common.delete')} title={t('common.delete')}>
                        <Trash2 size={14} aria-hidden="true" />
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      {canWrite ? (
        <fieldset className="form-field wide">
          <legend className="field-label">{t('models.assignments.addOverride')}</legend>
          <ErrorState error={overrideError} inline onDismiss={() => setOverrideError(null)} />
          <div className="form-grid">
            <FormField id="ovr-topic" label={t('models.assignments.topicId')} help={t('models.assignments.topicIdHelp')}>
              <input id="ovr-topic" className="mono" value={override.topicId} onChange={(e) => setOverride({ ...override, topicId: e.target.value })} autoComplete="off" />
            </FormField>
            <FormField id="ovr-task" label={t('models.assignments.role')}>
              <select id="ovr-task" value={override.task} onChange={(e) => setOverride({ ...override, task: e.target.value, modelEndpointId: '' })}>
                {MODEL_TASKS.map((task) => (
                  <option key={task} value={task}>
                    {t('enums.task.' + task)}
                  </option>
                ))}
              </select>
            </FormField>
            <FormField id="ovr-endpoint" label={t('models.assignments.endpoint')}>
              <EndpointSelect id="ovr-endpoint" task={override.task} endpoints={endpoints} value={override.modelEndpointId} onChange={(v) => setOverride({ ...override, modelEndpointId: v })} label={t('models.assignments.endpoint')} />
            </FormField>
            <FormField id="ovr-model" label={t('models.assignments.modelOverride')}>
              <input id="ovr-model" className="mono" value={override.modelOverride} onChange={(e) => setOverride({ ...override, modelOverride: e.target.value })} autoComplete="off" />
            </FormField>
            {override.task !== 'Embedding' ? (
              <FormField id="ovr-effort" label={t('models.assignments.reasoningEffort')}>
                <EffortSelect id="ovr-effort" value={override.reasoningEffort} onChange={(v) => setOverride({ ...override, reasoningEffort: v })} label={t('models.assignments.reasoningEffort')} />
              </FormField>
            ) : null}
          </div>
          <div className="form-actions">
            <button type="button" className="button secondary" onClick={addOverride} disabled={addingOverride || !override.topicId.trim() || !override.modelEndpointId}>
              <Plus size={14} aria-hidden="true" /> {addingOverride ? t('common.working') : t('models.assignments.addOverride')}
            </button>
          </div>
        </fieldset>
      ) : null}

      <ConfirmModal
        open={Boolean(deleting)}
        title={t('models.assignments.deleteTitle')}
        message={deleting ? t('models.assignments.deleteMessage', { role: t('enums.task.' + deleting.task), topic: deleting.topicId }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteModelAssignment(deleting.id);
          addNotification(t('admin.deleted'), 'success');
          await load();
        }}
      />
    </div>
  );
}

AssignmentsTab.propTypes = {
  endpoints: PropTypes.array.isRequired,
  canWrite: PropTypes.bool
};
