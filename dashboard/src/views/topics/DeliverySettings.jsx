import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Mail, Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { useApp } from '../../context/AppContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import TopicAudio from './TopicAudio.jsx';
import TopicVoice from './TopicVoice.jsx';

export function emailOptionsFrom(topic) {
  return {
    emailOnRun: topic ? topic.emailOnRun !== false : true,
    emailWhenEmpty: topic ? Boolean(topic.emailWhenEmpty) : false,
    maxEmailFindings: topic && topic.maxEmailFindings ? topic.maxEmailFindings : 10
  };
}

// Email recipients (saved as you add or remove them), the topic's email options (saved with Save), automatic audio
// (saved when switched), and the voice that reads its briefings aloud (saved when chosen).
export default function DeliverySettings({ topic, recipients, onTopicUpdated, onRecipientsChanged, disabled = false, showSave = true, optionsRef }) {
  const { t } = useTranslation();
  const { apiClient, identity } = useAuth();
  const { addNotification } = useApp();
  const [options, setOptions] = useState(() => emailOptionsFrom(topic));
  const [email, setEmail] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const myEmail = identity && identity.email ? identity.email : null;
  const hasMe = myEmail && recipients.some((r) => r.email.toLowerCase() === myEmail.toLowerCase());

  useEffect(() => {
    setOptions(emailOptionsFrom(topic));
  }, [topic]);

  useEffect(() => {
    if (optionsRef) optionsRef.current = options;
  }, [options, optionsRef]);

  async function add(address) {
    const value = String(address || '').trim();
    if (!value) return;
    setBusy(true);
    setError(null);
    try {
      const created = await apiClient.addTopicRecipient(topic.id, { email: value, active: true });
      onRecipientsChanged([...recipients.filter((r) => r.id !== created.id), created]);
      setEmail('');
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  async function remove(r) {
    setBusy(true);
    setError(null);
    try {
      await apiClient.deleteTopicRecipient(topic.id, r.id);
      onRecipientsChanged(recipients.filter((x) => x.id !== r.id));
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  async function save() {
    setBusy(true);
    setError(null);
    try {
      onTopicUpdated(await apiClient.updateTopic(topic.id, { ...options, maxEmailFindings: Math.max(1, Math.min(50, Number(options.maxEmailFindings) || 10)) }));
      addNotification(t('delivery.saved'), 'success');
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="stack delivery-settings">
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <div className="form-grid">
        <FormField id="delivery-email-on-run" label={t('delivery.emailOnRun')} help={t('delivery.emailOnRunHelp')}>
          <label className="checkbox-row">
            <input id="delivery-email-on-run" type="checkbox" checked={options.emailOnRun} disabled={disabled} onChange={(e) => setOptions({ ...options, emailOnRun: e.target.checked })} />
            <span>{options.emailOnRun ? t('common.yes') : t('common.no')}</span>
          </label>
        </FormField>
        <FormField id="delivery-email-empty" label={t('delivery.emailWhenEmpty')} help={t('delivery.emailWhenEmptyHelp')}>
          <label className="checkbox-row">
            <input id="delivery-email-empty" type="checkbox" checked={options.emailWhenEmpty} disabled={disabled} onChange={(e) => setOptions({ ...options, emailWhenEmpty: e.target.checked })} />
            <span>{options.emailWhenEmpty ? t('common.yes') : t('common.no')}</span>
          </label>
        </FormField>
        <FormField id="delivery-max" label={t('delivery.maxFindings')} help={t('delivery.maxFindingsHelp')}>
          <input id="delivery-max" type="number" min={1} max={50} value={options.maxEmailFindings} disabled={disabled} onChange={(e) => setOptions({ ...options, maxEmailFindings: e.target.value })} />
        </FormField>
      </div>
      {showSave ? (
        <div>
          <button type="button" className="button primary small" disabled={disabled || busy} onClick={save}>
            {busy ? t('common.saving') : t('delivery.saveOptions')}
          </button>
        </div>
      ) : null}

      <fieldset>
        <legend>{t('delivery.recipients')}</legend>
        <p className="muted small">{t('delivery.recipientsHelp')}</p>
        {recipients.length === 0 ? <p className="muted small">{t('delivery.noRecipients')}</p> : null}
        <ul className="recipient-list">
          {recipients.map((r) => (
            <li key={r.id}>
              <Mail size={14} aria-hidden="true" />
              <span className="truncate">{r.email}</span>
              {myEmail && r.email.toLowerCase() === myEmail.toLowerCase() ? <span className="muted small">{t('delivery.you')}</span> : null}
              <button type="button" className="icon-button" disabled={disabled || busy} aria-label={t('delivery.removeRecipient', { email: r.email })} title={t('common.remove')} onClick={() => remove(r)}>
                <Trash2 size={14} aria-hidden="true" />
              </button>
            </li>
          ))}
        </ul>
        <form
          className="input-with-button"
          onSubmit={(e) => {
            e.preventDefault();
            add(email);
          }}
        >
          <input type="email" value={email} disabled={disabled} aria-label={t('delivery.addRecipient')} placeholder={t('delivery.emailPlaceholder')} onChange={(e) => setEmail(e.target.value)} autoComplete="off" />
          <button type="submit" className="button secondary small" disabled={disabled || busy || !email.trim()}>
            <Plus size={14} aria-hidden="true" /> {t('common.add')}
          </button>
          {myEmail && !hasMe ? (
            <button type="button" className="link-button small" disabled={disabled || busy} onClick={() => add(myEmail)}>
              {t('delivery.addMe')}
            </button>
          ) : null}
        </form>
      </fieldset>

      <fieldset>
        <legend>{t('topicAudio.title')}</legend>
        <TopicAudio topic={topic} onTopicUpdated={onTopicUpdated} disabled={disabled} idPrefix="delivery-audio" />
      </fieldset>

      <fieldset>
        <legend>{t('topicVoice.title')}</legend>
        <TopicVoice topic={topic} onTopicUpdated={onTopicUpdated} disabled={disabled} />
      </fieldset>
    </div>
  );
}

DeliverySettings.propTypes = {
  topic: PropTypes.object.isRequired,
  recipients: PropTypes.array.isRequired,
  onTopicUpdated: PropTypes.func.isRequired,
  onRecipientsChanged: PropTypes.func.isRequired,
  disabled: PropTypes.bool,
  showSave: PropTypes.bool,
  optionsRef: PropTypes.object
};
