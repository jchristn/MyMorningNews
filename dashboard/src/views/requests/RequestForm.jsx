import { useState } from 'react';
import PropTypes from 'prop-types';
import { Link, useNavigate } from 'react-router-dom';
import { Send, Zap } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { DELIVERABLES, DELIVERABLE_LENGTHS } from '../../utils/enums.js';
import { browserTimeZone, zonedLocalToUtc } from '../../utils/schedule.js';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import { GenerationPanel } from '../topics/wizard/WizardParts.jsx';

export const EMPTY_REQUEST = { requestText: '', deliverables: ['Report'], length: 'Standard', timing: 'now', runAtLocal: '', emailRecipients: '', publishToFeed: false, skipQuestions: false };

// The ResearchRequestCreateRequest for a form. Times are entered in the browser's time zone.
export function requestBody(form, timeZone = browserTimeZone()) {
  const runAtUtc = form.timing === 'at' ? zonedLocalToUtc(form.runAtLocal, timeZone) : null;
  return {
    requestText: form.requestText.trim(),
    deliverables: form.deliverables.length > 0 ? form.deliverables : ['Report'],
    length: form.length,
    runAtUtc,
    emailRecipients: form.deliverables.includes('Email')
      ? form.emailRecipients
          .split(/[,\s;]+/)
          .map((s) => s.trim())
          .filter(Boolean)
      : [],
    publishToFeed: form.deliverables.includes('Podcast') && form.publishToFeed,
    skipQuestions: Boolean(form.skipQuestions)
  };
}

// "Ask for anything once": text, deliverables, length, timing, recipients, feed, and "Just do it". On success the
// request is planned (and with Just do it, confirmed) and the caller navigates to it. compact is the Home box.
export default function RequestForm({ compact = false, initialText = '' }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient, identity } = useAuth();
  const [form, setForm] = useState({ ...EMPTY_REQUEST, requestText: initialText });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const set = (patch) => setForm((f) => ({ ...f, ...patch }));
  const toggle = (d) => set({ deliverables: form.deliverables.includes(d) ? form.deliverables.filter((x) => x !== d) : [...form.deliverables, d] });
  const id = (name) => (compact ? 'home-request-' : 'request-') + name;

  async function submit(skipQuestions) {
    const body = requestBody({ ...form, skipQuestions });
    if (!body.requestText) return;
    setBusy(true);
    setError(null);
    try {
      const created = await apiClient.createResearchRequest(body);
      let result = created;
      const planned = created.request.status === 'Clarifying' && created.topic && created.topic.brief;
      if (skipQuestions && planned) result = await apiClient.confirmResearchRequest(created.request.id, body.runAtUtc);
      navigate('/dashboard/one-time/' + encodeURIComponent(result.request.id));
    } catch (err) {
      setError(err);
      setBusy(false);
    }
  }

  return (
    <form
      className={'stack request-form' + (compact ? ' compact' : '')}
      onSubmit={(e) => {
        e.preventDefault();
        submit(form.skipQuestions);
      }}
    >
      {busy ? <GenerationPanel kind="request" /> : null}
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {error ? (
        <p className="muted small">
          {t('oneTime.form.failedHint')} <Link to="/dashboard/one-time">{t('oneTime.form.openList')}</Link>
        </p>
      ) : null}
      <FormField id={id('text')} label={t('oneTime.form.text')} help={compact ? undefined : t('oneTime.form.textHelp')} wide>
        <textarea id={id('text')} rows={compact ? 2 : 4} maxLength={4000} value={form.requestText} disabled={busy} placeholder={t('oneTime.form.placeholder')} onChange={(e) => set({ requestText: e.target.value })} required />
      </FormField>
      <fieldset className="inline-fieldset">
        <legend>{t('oneTime.form.deliverables')}</legend>
        <div className="checkbox-group">
          {DELIVERABLES.map((d) => (
            <label key={d} className="checkbox-row">
              <input type="checkbox" checked={form.deliverables.includes(d)} disabled={busy} onChange={() => toggle(d)} />
              <span>{t('enums.deliverable.' + d)}</span>
            </label>
          ))}
        </div>
      </fieldset>
      {!compact ? (
        <div className="form-grid">
          <FormField id={id('length')} label={t('oneTime.form.length')}>
            <select id={id('length')} value={form.length} disabled={busy} onChange={(e) => set({ length: e.target.value })}>
              {DELIVERABLE_LENGTHS.map((l) => (
                <option key={l} value={l}>
                  {t('enums.deliverableLength.' + l)}
                </option>
              ))}
            </select>
          </FormField>
          <FormField id={id('timing')} label={t('oneTime.form.timing')}>
            <select id={id('timing')} value={form.timing} disabled={busy} onChange={(e) => set({ timing: e.target.value })}>
              <option value="now">{t('oneTime.form.runNow')}</option>
              <option value="at">{t('oneTime.form.runAt')}</option>
            </select>
          </FormField>
          {form.timing === 'at' ? (
            <FormField id={id('at')} label={t('oneTime.form.when')} help={t('schedule.fields.localTimeHelp', { zone: browserTimeZone() })}>
              <input id={id('at')} type="datetime-local" value={form.runAtLocal} disabled={busy} onChange={(e) => set({ runAtLocal: e.target.value })} required />
            </FormField>
          ) : null}
          {form.deliverables.includes('Email') ? (
            <FormField id={id('recipients')} label={t('oneTime.form.recipients')} help={t('oneTime.form.recipientsHelp', { email: identity && identity.email ? identity.email : '' })} wide>
              <input id={id('recipients')} value={form.emailRecipients} disabled={busy} placeholder={identity && identity.email ? identity.email : ''} onChange={(e) => set({ emailRecipients: e.target.value })} autoComplete="off" />
            </FormField>
          ) : null}
          {form.deliverables.includes('Podcast') ? (
            <FormField id={id('feed')} label={t('oneTime.form.publishToFeed')} help={t('oneTime.form.publishToFeedHelp')}>
              <label className="checkbox-row">
                <input id={id('feed')} type="checkbox" checked={form.publishToFeed} disabled={busy} onChange={(e) => set({ publishToFeed: e.target.checked })} />
                <span>{form.publishToFeed ? t('common.yes') : t('common.no')}</span>
              </label>
            </FormField>
          ) : null}
          <FormField id={id('skip')} label={t('oneTime.form.justDoIt')} help={t('oneTime.form.justDoItHelp')}>
            <label className="checkbox-row">
              <input id={id('skip')} type="checkbox" checked={form.skipQuestions} disabled={busy} onChange={(e) => set({ skipQuestions: e.target.checked })} />
              <span>{form.skipQuestions ? t('common.yes') : t('common.no')}</span>
            </label>
          </FormField>
        </div>
      ) : null}
      <div className="row-actions">
        <button type="submit" className="button primary" disabled={busy || !form.requestText.trim() || (form.timing === 'at' && !form.runAtLocal)}>
          <Send size={14} aria-hidden="true" /> {form.skipQuestions ? t('oneTime.form.submitNow') : t('oneTime.form.submit')}
        </button>
        {compact ? (
          <>
            <button type="button" className="button secondary" disabled={busy || !form.requestText.trim()} onClick={() => submit(true)}>
              <Zap size={14} aria-hidden="true" /> {t('oneTime.form.justDoIt')}
            </button>
            <Link to="/dashboard/one-time/new" state={{ requestText: form.requestText }} className="link-button small">
              {t('oneTime.form.moreOptions')}
            </Link>
          </>
        ) : null}
      </div>
    </form>
  );
}

RequestForm.propTypes = { compact: PropTypes.bool, initialText: PropTypes.string };
