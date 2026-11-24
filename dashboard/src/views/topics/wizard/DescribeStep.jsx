import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { History, Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../../context/AuthContext.jsx';
import { AUDIENCE_LEVELS, REFINEMENT_GOALS } from '../../../utils/enums.js';
import DateCell from '../../../components/DateCell.jsx';
import FormField from '../../../components/FormField.jsx';
import KeyValueGrid from '../../../components/KeyValueGrid.jsx';

export const EMPTY_DESCRIBE = { seedText: '', goal: '', depth: '', horizon: '', known: '', modelEndpointId: '' };

export function startBody(form, t) {
  return {
    seedText: form.seedText.trim(),
    goal: form.goal ? t('wizard.describe.goals.' + form.goal) : null,
    depth: form.depth || null,
    horizon: form.horizon.trim() || null,
    known: form.known.trim() || null,
    modelEndpointId: form.modelEndpointId || null
  };
}

// Step 1. For a new topic: what you care about, why, depth, horizon, what you know, and the model. For a session that
// already exists: what was described.
export default function DescribeStep({ data, initialText = '', onStart, busy = false }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [form, setForm] = useState({ ...EMPTY_DESCRIBE, seedText: initialText });
  const [endpoints, setEndpoints] = useState([]);
  const [resumable, setResumable] = useState([]);

  useEffect(() => {
    if (data) return undefined;
    let cancelled = false;
    apiClient
      .listModelEndpoints({ pageSize: 100 })
      .then((r) => !cancelled && setEndpoints(((r && r.objects) || []).filter((e) => e.active !== false && e.supportsChat !== false)))
      .catch(() => {});
    // One-time requests keep their clarifying questions in a session too; those are not wizard sessions.
    Promise.all([apiClient.listRefinementSessions({ status: 'Active', pageSize: 25, sortBy: 'createdUtc', sortDirection: 'Descending' }), apiClient.listTopics({ isOneTime: true, pageSize: 1000 })])
      .then(([sessions, oneTime]) => {
        if (cancelled) return;
        const skip = new Set(((oneTime && oneTime.objects) || []).map((x) => x.id));
        setResumable(((sessions && sessions.objects) || []).filter((s) => !skip.has(s.topicId)).slice(0, 8));
      })
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [apiClient, data]);

  if (data && data.session) {
    const s = data.session;
    return (
      <div className="stack">
        <p className="muted">{t('wizard.describe.existing')}</p>
        <KeyValueGrid
          rows={[
            { key: 'seed', label: t('wizard.describe.seedText'), value: s.seedText },
            { key: 'goal', label: t('wizard.describe.goal'), value: s.goal },
            { key: 'depth', label: t('wizard.describe.depth'), value: s.depth },
            { key: 'horizon', label: t('wizard.describe.horizon'), value: s.horizonText },
            { key: 'known', label: t('wizard.describe.known'), value: s.known },
            { key: 'started', label: t('wizard.describe.started'), value: <DateCell value={s.createdUtc} /> }
          ]}
        />
      </div>
    );
  }

  const set = (patch) => setForm((f) => ({ ...f, ...patch }));
  return (
    <form
      className="stack"
      onSubmit={(e) => {
        e.preventDefault();
        if (form.seedText.trim()) onStart(startBody(form, t));
      }}
    >
      <FormField id="wiz-seed" label={t('wizard.describe.seedText')} help={t('wizard.describe.seedHelp')} wide>
        <textarea id="wiz-seed" rows={4} value={form.seedText} maxLength={4000} placeholder={t('wizard.describe.seedPlaceholder')} onChange={(e) => set({ seedText: e.target.value })} disabled={busy} required />
      </FormField>
      <div className="form-grid">
        <FormField id="wiz-goal" label={t('wizard.describe.goal')} help={t('wizard.describe.optional')}>
          <select id="wiz-goal" value={form.goal} onChange={(e) => set({ goal: e.target.value })} disabled={busy}>
            <option value="">{t('wizard.describe.notSaying')}</option>
            {REFINEMENT_GOALS.map((g) => (
              <option key={g} value={g}>
                {t('wizard.describe.goals.' + g)}
              </option>
            ))}
          </select>
        </FormField>
        <FormField id="wiz-depth" label={t('wizard.describe.depth')} help={t('wizard.describe.optional')}>
          <select id="wiz-depth" value={form.depth} onChange={(e) => set({ depth: e.target.value })} disabled={busy}>
            <option value="">{t('wizard.describe.notSaying')}</option>
            {AUDIENCE_LEVELS.map((a) => (
              <option key={a} value={a}>
                {t('enums.audienceLevel.' + a)}
              </option>
            ))}
          </select>
        </FormField>
        <FormField id="wiz-horizon" label={t('wizard.describe.horizon')} help={t('wizard.describe.horizonHelp')}>
          <input id="wiz-horizon" value={form.horizon} maxLength={128} onChange={(e) => set({ horizon: e.target.value })} disabled={busy} autoComplete="off" />
        </FormField>
        <FormField id="wiz-model" label={t('wizard.describe.model')} help={t('wizard.describe.modelHelp')}>
          <select id="wiz-model" value={form.modelEndpointId} onChange={(e) => set({ modelEndpointId: e.target.value })} disabled={busy}>
            <option value="">{t('wizard.describe.defaultModel')}</option>
            {endpoints.map((e) => (
              <option key={e.id} value={e.id}>
                {e.name + (e.model ? ' (' + e.model + ')' : '')}
              </option>
            ))}
          </select>
        </FormField>
        <FormField id="wiz-known" label={t('wizard.describe.known')} help={t('wizard.describe.optional')} wide>
          <textarea id="wiz-known" rows={2} value={form.known} maxLength={4000} onChange={(e) => set({ known: e.target.value })} disabled={busy} />
        </FormField>
      </div>
      <div className="form-actions start">
        <button type="submit" className="button primary" disabled={busy || !form.seedText.trim()}>
          <Sparkles size={14} aria-hidden="true" /> {busy ? t('wizard.describe.starting') : t('wizard.describe.start')}
        </button>
      </div>
      {resumable.length > 0 ? (
        <section className="resume-list">
          <h3>
            <History size={14} aria-hidden="true" /> {t('wizard.describe.resumeTitle')}
          </h3>
          <ul className="cta-list">
            {resumable.map((s) => (
              <li key={s.id}>
                <Link to={'/dashboard/topics/' + encodeURIComponent(s.topicId) + '/refine?session=' + encodeURIComponent(s.id)} className="truncate">
                  {s.seedText}
                </Link>
                <DateCell value={s.lastUpdateUtc || s.createdUtc} />
              </li>
            ))}
          </ul>
        </section>
      ) : null}
    </form>
  );
}

DescribeStep.propTypes = {
  data: PropTypes.object,
  initialText: PropTypes.string,
  onStart: PropTypes.func,
  busy: PropTypes.bool
};
