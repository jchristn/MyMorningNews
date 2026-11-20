import { useState } from 'react';
import PropTypes from 'prop-types';
import { Download } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { toCsv, downloadText } from '../../utils/csv.js';
import { fieldErrors } from '../../utils/errors.js';
import { BATCH_CSV_COLUMNS, BATCH_MAX, COMPLIMENTARY_KEY, emptyPromoForm, normalizeCode, promoBody, promoFormFrom, validatePromoForm } from '../../utils/promo.js';
import CopyButton from '../../components/CopyButton.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import Modal from '../../components/Modal.jsx';

// The id of the Complimentary plan, the default for new codes, or the first plan.
export function defaultPlanId(plans) {
  const list = plans || [];
  const comp = list.find((p) => p.plan && p.plan.key === COMPLIMENTARY_KEY);
  return comp ? comp.plan.id : list[0] ? list[0].plan.id : '';
}

// Create, edit, or generate-in-bulk dialog for promotional codes. mode: create, edit, or batch. For edit, pass the
// PromoCodeStats being edited as stats. A batch shows its created codes afterwards with copy-all and CSV download.
export default function PromoCodeFormModal({ mode, plans, stats = null, onClose, onSaved }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const original = stats ? stats.code : null;
  const [form, setForm] = useState(() => (original ? promoFormFrom(original) : emptyPromoForm(defaultPlanId(plans), mode === 'batch')));
  const [touched, setTouched] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [created, setCreated] = useState(null);
  const usedCount = original ? original.redemptionCount || 0 : 0;
  const errors = validatePromoForm(form, { mode, usedCount });
  const server = fieldErrors(error);
  const shown = (field) => (touched && errors[field] ? t(errors[field], { used: usedCount, max: BATCH_MAX }) : server[field]);
  const set = (patch) => setForm((f) => ({ ...f, ...patch }));
  const id = (name) => 'promo-' + mode + '-' + name;

  async function save() {
    setTouched(true);
    if (Object.keys(errors).length > 0) return;
    setSaving(true);
    setError(null);
    try {
      const body = promoBody(form, { mode, original });
      if (mode === 'create') onSaved(await apiClient.createPromoCode(body));
      else if (mode === 'edit') onSaved(await apiClient.updatePromoCode(original.id, body));
      else {
        const result = await apiClient.generatePromoCodes(body);
        setCreated(Array.isArray(result) ? result : []);
        onSaved(result, { keepOpen: true });
      }
    } catch (err) {
      setError(err);
    } finally {
      setSaving(false);
    }
  }

  const title = mode === 'create' ? t('promo.create.title') : mode === 'edit' ? t('promo.edit.title', { code: original ? original.code : '' }) : t('promo.batch.title');

  if (created) {
    const allCodes = created.map((c) => c.code).join('\n');
    const batchId = created[0] ? created[0].batchId : '';
    return (
      <Modal
        open
        title={t('promo.batch.createdTitle', { count: created.length })}
        subtitle={batchId ? t('promo.batch.batchId', { id: batchId }) : null}
        onClose={onClose}
        footer={
          <>
            <button type="button" className="button secondary" onClick={() => downloadText('promo-codes-' + (batchId || 'batch') + '.csv', toCsv(created, BATCH_CSV_COLUMNS))}>
              <Download size={14} aria-hidden="true" /> {t('promo.batch.downloadCsv')}
            </button>
            <button type="button" className="button primary" onClick={onClose}>
              {t('common.close')}
            </button>
          </>
        }
      >
        <div className="row-actions">
          <span className="muted small">{t('promo.batch.copyAll')}</span>
          <CopyButton value={allCodes} labelKey="promo.batch.copyAll" />
        </div>
        <textarea className="mono promo-batch-list" readOnly value={allCodes} rows={Math.min(14, Math.max(3, created.length))} aria-label={t('promo.batch.codes')} />
      </Modal>
    );
  }

  return (
    <Modal
      open
      title={title}
      size="large"
      onClose={() => !saving && onClose()}
      footer={
        <>
          <button type="button" className="button secondary" onClick={onClose} disabled={saving}>
            {t('common.cancel')}
          </button>
          <button type="button" className="button primary" onClick={save} disabled={saving}>
            {saving ? t('common.saving') : mode === 'batch' ? t('promo.batch.generate') : t('common.save')}
          </button>
        </>
      }
    >
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <div className="form-grid">
        {mode === 'create' ? (
          <FormField id={id('code')} label={t('promo.fields.code')} help={t('promo.fields.codeHelp')} error={shown('code')}>
            <input id={id('code')} className="mono" value={form.code} onChange={(e) => set({ code: normalizeCode(e.target.value) })} maxLength={40} autoComplete="off" />
          </FormField>
        ) : null}
        {mode === 'edit' ? (
          <FormField id={id('code')} label={t('promo.fields.code')} help={t('promo.fields.codeLocked')}>
            <input id={id('code')} className="mono" value={form.code} disabled />
          </FormField>
        ) : null}
        {mode === 'batch' ? (
          <>
            <FormField id={id('prefix')} label={t('promo.fields.prefix')} help={t('promo.fields.prefixHelp')} error={shown('prefix')}>
              <input id={id('prefix')} className="mono" value={form.prefix} onChange={(e) => set({ prefix: normalizeCode(e.target.value) })} maxLength={16} autoComplete="off" />
            </FormField>
            <FormField id={id('count')} label={t('promo.fields.count')} help={t('promo.fields.countHelp', { max: BATCH_MAX })} error={shown('count')}>
              <input id={id('count')} type="number" min={1} max={BATCH_MAX} value={form.count} onChange={(e) => set({ count: e.target.value })} />
            </FormField>
          </>
        ) : null}
        <FormField id={id('plan')} label={t('promo.fields.plan')} error={shown('planId')}>
          <select id={id('plan')} value={form.planId} onChange={(e) => set({ planId: e.target.value })}>
            {(plans || []).length === 0 ? <option value="">{t('promo.fields.noPlans')}</option> : null}
            {(plans || []).map((p) => (
              <option key={p.plan.id} value={p.plan.id}>
                {p.plan.name}
              </option>
            ))}
          </select>
        </FormField>
        <FormField id={id('description')} label={t('promo.fields.description')} wide>
          <input id={id('description')} value={form.description} onChange={(e) => set({ description: e.target.value })} maxLength={500} />
        </FormField>
        <FormField id={id('duration')} label={t('promo.fields.duration')} help={t('promo.fields.durationHelp')} error={shown('durationDays')}>
          <input id={id('duration')} type="number" min={0} max={3650} value={form.durationDays} onChange={(e) => set({ durationDays: e.target.value })} />
        </FormField>
        <FormField
          id={id('max')}
          label={t('promo.fields.maxRedemptions')}
          help={mode === 'edit' && usedCount > 0 ? t('promo.fields.maxUsedHelp', { used: usedCount }) : t('promo.fields.maxHelp')}
          error={shown('maxRedemptions')}
        >
          <input id={id('max')} type="number" min={0} value={form.maxRedemptions} onChange={(e) => set({ maxRedemptions: e.target.value })} />
        </FormField>
        <FormField id={id('peruser')} label={t('promo.fields.perUserLimit')} help={t('promo.fields.perUserHelp')} error={shown('perUserLimit')}>
          <input id={id('peruser')} type="number" min={1} max={100} value={form.perUserLimit} onChange={(e) => set({ perUserLimit: e.target.value })} />
        </FormField>
        <FormField id={id('starts')} label={t('promo.fields.starts')} help={t('promo.fields.startsHelp')} error={shown('startsLocal')}>
          <input id={id('starts')} type="datetime-local" value={form.startsLocal} onChange={(e) => set({ startsLocal: e.target.value })} />
        </FormField>
        <FormField id={id('expires')} label={t('promo.fields.expires')} help={t('promo.fields.expiresHelp')} error={shown('expiresLocal')}>
          <input id={id('expires')} type="datetime-local" value={form.expiresLocal} onChange={(e) => set({ expiresLocal: e.target.value })} />
        </FormField>
        <FormField id={id('enabled')} label={t('promo.fields.enabled')}>
          <input id={id('enabled')} type="checkbox" checked={form.enabled} onChange={(e) => set({ enabled: e.target.checked })} />
        </FormField>
        <FormField id={id('newonly')} label={t('promo.fields.newAccountsOnly')} help={t('promo.fields.newAccountsOnlyHelp')}>
          <input id={id('newonly')} type="checkbox" checked={form.newAccountsOnly} onChange={(e) => set({ newAccountsOnly: e.target.checked })} />
        </FormField>
      </div>
    </Modal>
  );
}

PromoCodeFormModal.propTypes = {
  mode: PropTypes.oneOf(['create', 'edit', 'batch']).isRequired,
  plans: PropTypes.array,
  stats: PropTypes.object,
  onClose: PropTypes.func.isRequired,
  onSaved: PropTypes.func.isRequired
};
