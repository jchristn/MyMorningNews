import { useCallback, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import { useSetup } from '../context/SetupContext.jsx';
import ConfirmModal from '../components/ConfirmModal.jsx';
import ErrorState from '../components/ErrorState.jsx';
import FormField from '../components/FormField.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import PasswordInput from '../components/PasswordInput.jsx';
import StatusBadge from '../components/StatusBadge.jsx';

const blank = (kind) => ({ kind, provider: kind === 'Chat' ? 'Anthropic' : 'OpenAi', model: '', endpoint: '', voice: '', apiKey: '' });
const edit = (current, kind) => current ? { kind, provider: current.provider, model: current.model, endpoint: current.endpoint || '', voice: current.voice || '', apiKey: '' } : blank(kind);
export default function PersonalProvidersView() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const setup = useSetup();
  const [data, setData] = useState(null);
  const [form, setForm] = useState(blank('Chat'));
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState('');
  const [removing, setRemoving] = useState(null);
  const initialized = useRef(false);
  const load = useCallback(async () => {
    const next = await apiClient.getMyProviders();
    setData(next); setError(null);
    if (!initialized.current) { setForm(edit(next.connections?.find(c => c.kind === 'Chat'), 'Chat')); initialized.current = true; }
  }, [apiClient]);
  useEffect(() => { load().catch(setError); }, [load]);
  const connection = data?.connections?.find(c => c.kind === form.kind);
  const label = (kind) => kind === 'Chat' ? t('personalProviders.chat') : kind === 'Embedding' ? t('personalProviders.embedding') : t('personalProviders.speech');
  function choose(kind) {
    const current = data?.connections?.find(c => c.kind === kind);
    setForm(edit(current, kind));
    setMessage('');
  }
  async function act(action, success) {
    setBusy(true); setError(null); setMessage('');
    try { await action(); await load(); setMessage(success); await setup?.refresh(); }
    catch (err) { setError(err); }
    finally { setBusy(false); }
  }
  async function save(event) {
    event.preventDefault();
    await act(async () => { await apiClient.saveMyProvider({ ...form, apiKey: form.apiKey || null, endpoint: form.endpoint || null, voice: form.voice || null }); setForm(f => ({ ...f, apiKey: '' })); }, t('personalProviders.saved'));
  }
  const personal = data?.plan?.fundingMode === 'Personal';
  const chatReady = data?.connections?.some(c => c.kind === 'Chat' && c.active && c.validatedUtc);
  const providers = form.kind === 'Speech' ? ['OpenAi', 'OpenAiCompatible', ...(data?.localSpeechAvailable ? ['Local'] : [])] : form.kind === 'Embedding' ? ['OpenAi', 'Gemini', 'OpenAiCompatible'] : ['Anthropic', 'OpenAi', 'Gemini', 'XAi', 'OpenAiCompatible'];
  return <div className="page stack">
    <PageHeader title={t('personalProviders.title')} summary={t('personalProviders.summary')} />
    <ErrorState error={error} inline onRetry={() => load().catch(setError)} />
    {!data && !error ? <LoadingState /> : null}
    {data ? <>
      <section className="panel">
        <div className="section-title"><h2>{data.plan?.plan?.name}</h2><StatusBadge tone={personal ? 'success' : 'neutral'}>{personal ? t('personalProviders.byok') : t('personalProviders.assigned')}</StatusBadge></div>
        <p>{t('personalProviders.fundingHelp')}</p>
        <button className="button primary" disabled={busy || (!personal && !chatReady)} onClick={() => act(() => apiClient.setMyFunding(personal ? 'Platform' : 'Personal'), t('personalProviders.fundingSaved'))}>
          {personal ? t('personalProviders.useAssigned') : t('personalProviders.useByok')}
        </button>
      </section>
      <div className="row-actions" role="group" aria-label={t('personalProviders.kind')}>
        {['Chat', 'Embedding', 'Speech'].map(kind => <button key={kind} className={'button ' + (kind === form.kind ? 'primary' : 'secondary')} aria-pressed={kind === form.kind} disabled={busy} onClick={() => choose(kind)}>{label(kind)}</button>)}
      </div>
      <form className="panel" onSubmit={save}>
        <h2>{label(form.kind)}</h2>
        {connection ? <p className="provider-key-status">{connection.secretLast4 ? '•••• ' + connection.secretLast4 + ' · ' : ''}{connection.validatedUtc ? t('personalProviders.verified') : t('personalProviders.unverified')}</p> : null}
        {form.kind === 'Speech' ? <p className="muted">{t('personalProviders.speechHelp')}</p> : null}
        <div className="form-grid">
          <FormField id="personal-provider" label={t('personalProviders.provider')}>
            <select id="personal-provider" value={form.provider} disabled={busy} onChange={e => setForm(f => ({ ...f, provider: e.target.value, apiKey: '' }))}>
              {providers.map(p => <option key={p} value={p}>{p === 'Local' ? t('personalProviders.local') : p === 'OpenAi' ? 'OpenAI / Codex API' : p === 'OpenAiCompatible' ? 'OpenAI compatible' : p}</option>)}
            </select>
          </FormField>
          {form.provider !== 'Local' ? <>
            <FormField id="personal-model" label={t('personalProviders.model')}><input id="personal-model" required maxLength={256} value={form.model} disabled={busy} onChange={e => setForm(f => ({ ...f, model: e.target.value }))} /></FormField>
            <FormField id="personal-url" label={t('personalProviders.endpoint')}><input id="personal-url" type="url" value={form.endpoint} disabled={busy} onChange={e => setForm(f => ({ ...f, endpoint: e.target.value }))} /></FormField>
            {form.kind === 'Speech' ? <FormField id="personal-voice" label={t('personalProviders.voice')}><input id="personal-voice" required maxLength={128} value={form.voice} disabled={busy} onChange={e => setForm(f => ({ ...f, voice: e.target.value }))} /></FormField> : null}
            <FormField id="personal-key" label={t('personalProviders.key')} help={t('personalProviders.keyHelp')} wide><PasswordInput id="personal-key" value={form.apiKey} autoComplete="new-password" disabled={busy} onChange={value => setForm(f => ({ ...f, apiKey: value }))} /></FormField>
          </> : null}
        </div>
        <div className="row-actions">
          <button type="submit" className="button primary" disabled={busy}>{busy ? t('common.saving') : t('common.save')}</button>
          {connection ? <button type="button" className="button secondary" disabled={busy} onClick={() => act(() => apiClient.testMyProvider(connection.id), t('personalProviders.tested'))}>{t('personalProviders.test')}</button> : null}
          {connection ? <button type="button" className="button danger" disabled={busy} onClick={() => setRemoving(connection)}>{t('common.delete')}</button> : null}
        </div>
        {message ? <p role="status">{message}</p> : null}
      </form>
    </> : null}
    <ConfirmModal open={!!removing} title={t('personalProviders.remove')} message={t('personalProviders.removeHelp')} danger onClose={() => setRemoving(null)} onConfirm={async () => { await apiClient.deleteMyProvider(removing.id); setForm(blank(removing.kind)); await load(); await setup?.refresh(); }} />
  </div>;
}
