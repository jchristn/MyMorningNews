import React, { useState } from 'react';
import { View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { providerApi } from '@/api/billing';
import { ResourceView } from '@/components/states';
import { Banner, Button, Card, Choice, Field, Screen, T } from '@/components/ui';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { spacing } from '@/theme/palette';

const draft = (kind, current) => ({ kind, provider: current?.provider || (kind === 'Chat' ? 'Anthropic' : 'OpenAi'), model: current?.model || '', endpoint: current?.endpoint || '', voice: current?.voice || '', apiKey: '' });

/** Private keys stay in this form's memory until submission; no device storage or response contains a key. */
export function ProvidersSection() {
  const { client } = useAuth();
  const resource = useResource(() => providerApi.list(client), [client]);
  return <Screen testID="providers-section"><ResourceView resource={resource}>{data => <ProvidersForm data={data} onData={resource.setData} />}</ResourceView></Screen>;
}

function ProvidersForm({ data, onData }) {
  const { client } = useAuth();
  const { t } = useTranslation();
  const p = key => t('mobile.personalProviders.' + key);
  const [form, setForm] = useState(() => draft('Chat', data.connections?.find(c => c.kind === 'Chat')));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [message, setMessage] = useState(null);
  const [confirm, setConfirm] = useState(false);
  const connection = data.connections?.find(c => c.kind === form.kind);
  const personal = data.plan?.fundingMode === 'Personal';
  const ready = data.connections?.some(c => c.kind === 'Chat' && c.active && c.validatedUtc);
  const options = form.kind === 'Speech' ? ['OpenAi', 'OpenAiCompatible', ...(data.localSpeechAvailable ? ['Local'] : [])] : form.kind === 'Embedding' ? ['OpenAi', 'Gemini', 'OpenAiCompatible'] : ['Anthropic', 'OpenAi', 'Gemini', 'XAi', 'OpenAiCompatible'];
  const set = key => value => { setForm(f => ({ ...f, [key]: value })); setMessage(null); };
  async function act(action, success) {
    if (busy) return;
    setBusy(true); setError(null); setMessage(null);
    try { await action(); onData(await providerApi.list(client)); setMessage(success); }
    catch (e) { setError(e); }
    finally { setBusy(false); }
  }
  return <>
    <T variant="muted">{p('summary')}</T>
    <Card>
      <T variant="title">{personal ? p('byok') : data.plan?.plan?.name}</T><T>{p('fundingHelp')}</T>
      <Button title={personal ? p('useAssigned') : p('useByok')} busy={busy} disabled={!personal && !ready} onPress={() => void act(() => providerApi.funding(client, personal ? 'Platform' : 'Personal'), p('fundingSaved'))} testID="provider-funding" />
    </Card>
    <Choice accessibilityLabel={p('kind')} value={form.kind} options={['Chat', 'Embedding', 'Speech'].map(kind => ({ value: kind, label: p(kind === 'Chat' ? 'chat' : kind === 'Embedding' ? 'embedding' : 'speech') }))} onChange={kind => { if (!busy) { setForm(draft(kind, data.connections?.find(c => c.kind === kind))); setConfirm(false); setMessage(null); } }} />
    <Card>
      {connection ? <T>{connection.secretLast4 ? '•••• ' + connection.secretLast4 + ' · ' : ''}{connection.validatedUtc ? p('verified') : p('unverified')}</T> : null}
      {form.kind === 'Speech' ? <T variant="small">{p('speechHelp')}</T> : null}
      <Choice accessibilityLabel={p('provider')} value={form.provider} options={options.map(value => ({ value, label: value === 'Local' ? p('local') : value === 'OpenAi' ? 'OpenAI / Codex API' : value === 'OpenAiCompatible' ? 'OpenAI compatible' : value }))} onChange={provider => { if (!busy) setForm(f => ({ ...f, provider, apiKey: '' })); }} />
      {form.provider !== 'Local' ? <>
        <Field label={p('model')} value={form.model} onChangeText={set('model')} editable={!busy} maxLength={256} autoCapitalize="none" autoCorrect={false} testID="provider-model" />
        <Field label={p('endpoint')} value={form.endpoint} onChangeText={set('endpoint')} editable={!busy} keyboardType="url" autoCapitalize="none" autoCorrect={false} testID="provider-endpoint" />
        {form.kind === 'Speech' ? <Field label={p('voice')} value={form.voice} onChangeText={set('voice')} editable={!busy} maxLength={128} autoCapitalize="none" autoCorrect={false} /> : null}
        <Field label={p('key')} hint={p('keyHelp')} value={form.apiKey} onChangeText={set('apiKey')} editable={!busy} secureTextEntry autoComplete="off" autoCapitalize="none" autoCorrect={false} testID="provider-key" />
      </> : null}
      <Button title={t('mobile.common.save')} busy={busy} disabled={form.provider !== 'Local' && (!form.model.trim() || (form.kind === 'Speech' && !form.voice.trim()))} onPress={() => void act(async () => {
        await providerApi.save(client, { ...form, apiKey: form.apiKey || null, endpoint: form.endpoint || null, voice: form.voice || null }); setForm(f => ({ ...f, apiKey: '' }));
      }, p('saved'))} testID="provider-save" />
      {connection ? <Button title={p('test')} kind="secondary" busy={busy} onPress={() => void act(() => providerApi.test(client, connection.id), p('tested'))} testID="provider-test" /> : null}
      {connection && !confirm ? <Button title={p('remove')} kind="danger" disabled={busy} onPress={() => setConfirm(true)} /> : null}
      {connection && confirm ? <View style={{ gap: spacing.sm }}><T>{p('removeHelp')}</T><Button title={t('mobile.common.delete')} kind="danger" busy={busy} onPress={() => void act(async () => { await providerApi.remove(client, connection.id); setForm(draft(form.kind)); setConfirm(false); }, null)} /><Button title={t('mobile.common.cancel')} kind="ghost" disabled={busy} onPress={() => setConfirm(false)} /></View> : null}
    </Card>
    {error ? <Banner kind="danger">{error.message}</Banner> : null}
    {message ? <Banner kind="success">{message}</Banner> : null}
  </>;
}
