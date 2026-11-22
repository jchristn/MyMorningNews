import { useCallback, useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { LoaderCircle, Play, Square } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { useApp } from '../../context/AppContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import { ApiError } from '../../utils/api.js';

// What identifies a voice in the list: its voice profile, or the speech server's voice ID when it has no profile yet.
export function voiceKey(voice) {
  return voice.voiceProfileId ? 'profile:' + voice.voiceProfileId : 'voice:' + voice.voice;
}

// The name to show for a voice: the voice profile's own name, else the readable label of the speech server's voice.
export function voiceName(voice) {
  if (!voice) return '';
  return voice.name || voice.label || voice.voice || '';
}

// The voice that reads a topic's briefings aloud. Lists the workspace's voice profiles and the speech server's other
// voices; Play speaks a short sample of one voice at a time, and Use this voice saves the choice on the topic right
// away (choosing a speech server voice makes a voice profile for it). Use the workspace default clears it.
export default function TopicVoice({ topic, onTopicUpdated, disabled = false }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [data, setData] = useState(null);
  const [loadError, setLoadError] = useState(null);
  const [error, setError] = useState(null);
  const [saving, setSaving] = useState(null);
  const [loadingKey, setLoadingKey] = useState(null);
  const [playing, setPlaying] = useState(null);
  const urlRef = useRef(null);
  const requestRef = useRef(0);

  const load = useCallback(() => {
    setLoadError(null);
    return Promise.resolve()
      .then(() => apiClient.getAvailableVoices())
      .then((r) => setData({ enabled: !r || r.enabled !== false, defaultVoiceProfileId: r && r.defaultVoiceProfileId ? r.defaultVoiceProfileId : null, voices: r && Array.isArray(r.voices) ? r.voices : [] }))
      .catch((err) => setLoadError(err));
  }, [apiClient]);

  useEffect(() => {
    load();
  }, [load]);

  const stop = useCallback(() => {
    requestRef.current += 1;
    if (urlRef.current) URL.revokeObjectURL(urlRef.current);
    urlRef.current = null;
    setPlaying(null);
    setLoadingKey(null);
  }, []);

  useEffect(() => stop, [stop]);

  async function play(voice) {
    const key = voiceKey(voice);
    stop();
    const id = requestRef.current;
    setError(null);
    setLoadingKey(key);
    try {
      const result = await apiClient.previewVoice(voice.voiceProfileId ? { voiceProfileId: voice.voiceProfileId } : { voice: voice.voice }, undefined);
      if (id !== requestRef.current) return;
      const url = URL.createObjectURL(result.blob);
      urlRef.current = url;
      setPlaying({ key, url });
    } catch (err) {
      if (id !== requestRef.current) return;
      if (err instanceof ApiError && err.status === 502) setError(new ApiError(502, err.body, t('voices.test.ttsError', { message: err.message || '' })));
      else if (err instanceof ApiError && err.status === 409) setError(new ApiError(409, err.body, t('topicVoice.speechOff')));
      else setError(err);
    } finally {
      if (id === requestRef.current) setLoadingKey(null);
    }
  }

  async function choose(voice) {
    const key = voice ? voiceKey(voice) : 'default';
    setSaving(key);
    setError(null);
    try {
      const body = !voice ? { voiceProfileId: '' } : voice.voiceProfileId ? { voiceProfileId: voice.voiceProfileId } : { voice: voice.voice };
      const updated = await apiClient.updateTopic(topic.id, body);
      onTopicUpdated(updated);
      addNotification(voice ? t('topicVoice.saved', { voice: voiceName(voice) }) : t('topicVoice.savedDefault'), 'success');
      // A speech server voice now has a voice profile: read the list again so it shows as one.
      if (voice && !voice.voiceProfileId) await load();
    } catch (err) {
      setError(err);
    } finally {
      setSaving(null);
    }
  }

  if (loadError) {
    return (
      <div className="stack-tight" data-testid="topic-voice">
        <ErrorState error={loadError} inline />
        <div>
          <button type="button" className="button secondary small" onClick={load}>
            {t('common.retry')}
          </button>
        </div>
      </div>
    );
  }
  if (!data) return <LoadingState compact />;

  const current = topic.voiceProfileId ? data.voices.find((v) => v.voiceProfileId === topic.voiceProfileId) || null : null;
  const fallback = data.voices.find((v) => v.isDefault) || null;
  const busy = disabled || saving !== null;
  let currentText;
  if (current) currentText = t('topicVoice.current', { voice: voiceName(current) });
  else if (fallback) currentText = t('topicVoice.usingDefault', { voice: voiceName(fallback) });
  else currentText = t('topicVoice.usingDefaultUnknown');

  return (
    <div className="stack-tight" data-testid="topic-voice">
      <p className="muted small">{t('topicVoice.help')}</p>
      {!data.enabled ? <div className="notice notice-warning">{t('topicVoice.speechOff')}</div> : null}
      <p className="voice-picker-current" data-testid="topic-voice-current">
        <strong>{currentText}</strong>{' '}
        {topic.voiceProfileId ? (
          <button type="button" className="link-button small" disabled={busy} onClick={() => choose(null)}>
            {saving === 'default' ? t('common.saving') : t('topicVoice.useDefault')}
          </button>
        ) : null}
      </p>
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {data.voices.length === 0 ? <p className="muted small">{t('topicVoice.empty')}</p> : null}
      <ul className="voice-list" aria-label={t('topicVoice.listLabel')}>
        {data.voices.map((voice) => {
          const key = voiceKey(voice);
          const name = voiceName(voice);
          const isCurrent = Boolean(current && voiceKey(current) === key);
          const isPlaying = Boolean(playing && playing.key === key);
          const isLoading = loadingKey === key;
          return (
            <li key={key} className={'voice-row' + (isCurrent ? ' current' : '')} data-testid={'voice-' + key}>
              <span className="voice-row-name">
                <span dir="auto">{name}</span>
                <span className="muted small" dir="auto">
                  {[voice.label && voice.label !== name ? voice.label : null, voice.voice !== name ? voice.voice : null, voice.isDefault ? t('topicVoice.workspaceDefault') : null, isCurrent ? t('topicVoice.inUse') : null]
                    .filter(Boolean)
                    .join(' · ')}
                </span>
              </span>
              <span className="voice-row-actions">
                <button
                  type="button"
                  className="button secondary small"
                  disabled={isLoading}
                  aria-label={isPlaying ? t('topicVoice.stop', { voice: name }) : t('topicVoice.play', { voice: name })}
                  aria-busy={isLoading || undefined}
                  onClick={() => (isPlaying ? stop() : play(voice))}
                >
                  {isLoading ? <LoaderCircle size={14} className="spin" aria-hidden="true" /> : isPlaying ? <Square size={14} aria-hidden="true" /> : <Play size={14} aria-hidden="true" />}{' '}
                  {isLoading ? t('topicVoice.loadingShort') : isPlaying ? t('topicVoice.stopShort') : t('topicVoice.playShort')}
                </button>
                <button type="button" className="button secondary small" disabled={busy || isCurrent} aria-label={t('topicVoice.useNamed', { voice: name })} onClick={() => choose(voice)}>
                  {saving === key ? t('common.saving') : t('topicVoice.use')}
                </button>
              </span>
            </li>
          );
        })}
      </ul>
      {playing ? <audio key={playing.url} src={playing.url} autoPlay onEnded={stop} onError={stop} data-testid="voice-preview-audio" aria-label={t('topicVoice.sampleLabel')} /> : null}
    </div>
  );
}

TopicVoice.propTypes = {
  topic: PropTypes.object.isRequired,
  onTopicUpdated: PropTypes.func.isRequired,
  disabled: PropTypes.bool
};
