import { useCallback, useEffect, useImperativeHandle, useMemo, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Pause, Play, RotateCcw, RotateCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import {
  PLAYBACK_SPEEDS,
  SKIP_BACK_SECONDS,
  SKIP_FORWARD_SECONDS,
  chapterIndexAt,
  chapterTicks,
  clampTime,
  formatClock,
  normalizeChapters,
  progressFraction,
  timeAtOffset
} from '../utils/audio.js';
import ErrorState from './ErrorState.jsx';
import LoadingState from './LoadingState.jsx';

const KEY_STEP_SECONDS = 5;

// The progress bar: hand-rolled SVG with the played share filled and a tick where each chapter starts. It is a
// slider for keyboards and screen readers; a click seeks to the point under the pointer. Time runs left to right in
// every locale, as it does on media controls.
function ProgressBar({ time, duration, ticks, label, valueText, onSeek }) {
  const ref = useRef(null);
  const fraction = progressFraction(time, duration);
  const pct = (fraction * 100).toFixed(3) + '%';

  function seekFromPointer(event) {
    const el = ref.current;
    if (!el || duration <= 0) return;
    const rect = el.getBoundingClientRect();
    onSeek(timeAtOffset(event.clientX - rect.left, rect.width, duration));
  }

  function onKeyDown(event) {
    const moves = { ArrowLeft: -KEY_STEP_SECONDS, ArrowDown: -KEY_STEP_SECONDS, ArrowRight: KEY_STEP_SECONDS, ArrowUp: KEY_STEP_SECONDS, PageDown: -SKIP_FORWARD_SECONDS, PageUp: SKIP_FORWARD_SECONDS };
    if (event.key in moves) onSeek(clampTime(time + moves[event.key], duration));
    else if (event.key === 'Home') onSeek(0);
    else if (event.key === 'End' && duration > 0) onSeek(duration);
    else return;
    event.preventDefault();
  }

  return (
    <div
      ref={ref}
      className="audio-progress"
      role="slider"
      tabIndex={0}
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={Math.round(duration)}
      aria-valuenow={Math.round(time)}
      aria-valuetext={valueText}
      onClick={seekFromPointer}
      onKeyDown={onKeyDown}
      data-testid="audio-progress"
    >
      <svg width="100%" height="20" aria-hidden="true" focusable="false">
        <rect className="audio-progress-track" x="0" y="7" width="100%" height="6" rx="3" />
        <rect className="audio-progress-fill" x="0" y="7" width={pct} height="6" rx="3" />
        {ticks.map((tick) => (
          <rect key={tick.key} className="audio-progress-tick" x={(tick.fraction * 100).toFixed(3) + '%'} y="4" width="2" height="12" transform="translate(-1 0)" data-fraction={tick.fraction.toFixed(4)}>
            <title>{tick.title}</title>
          </rect>
        ))}
        <circle className="audio-progress-thumb" cx={pct} cy="10" r="6" />
      </svg>
    </div>
  );
}

ProgressBar.propTypes = {
  time: PropTypes.number.isRequired,
  duration: PropTypes.number.isRequired,
  ticks: PropTypes.array.isRequired,
  label: PropTypes.string.isRequired,
  valueText: PropTypes.string,
  onSeek: PropTypes.func.isRequired
};

// Plays an episode stored as an artifact. The audio is fetched with the session's bearer token into a blob URL (the
// audio element cannot send headers), which is revoked on unmount. Custom controls drive the native audio element:
// play and pause, back 15 s and forward 30 s, speed, and an SVG progress bar with chapter ticks. The chapter list
// highlights the chapter playing and seeks on click. onSectionChange receives the briefingSectionId of the chapter
// playing (or null) whenever it changes, so a reader can highlight that section. apiRef receives
// { seekTo(seconds), seekToSection(sectionId), play(), pause() }. compact hides the chapter list.
export default function AudioPlayer({ artifactId, chapters = [], durationSeconds = 0, bytes = 0, title = '', compact = false, onSectionChange = null, apiRef = null, autoPlay = false }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const audioRef = useRef(null);
  const [src, setSrc] = useState(null);
  const [loadError, setLoadError] = useState(null);
  const [attempt, setAttempt] = useState(0);
  const [time, setTime] = useState(0);
  const [mediaDuration, setMediaDuration] = useState(0);
  const [playing, setPlaying] = useState(false);
  const [speed, setSpeed] = useState(1);
  const pendingPlay = useRef(Boolean(autoPlay));

  useEffect(() => {
    if (!artifactId) return undefined;
    const controller = typeof AbortController !== 'undefined' ? new AbortController() : null;
    let url = null;
    let cancelled = false;
    setSrc(null);
    setLoadError(null);
    apiClient
      .getArtifactContent(artifactId, { signal: controller ? controller.signal : undefined })
      .then((result) => {
        if (cancelled) return;
        url = URL.createObjectURL(result.blob);
        setSrc(url);
      })
      .catch((err) => {
        if (!cancelled && !(err && err.name === 'AbortError')) setLoadError(err);
      });
    return () => {
      cancelled = true;
      if (controller) controller.abort();
      if (url) URL.revokeObjectURL(url);
    };
  }, [apiClient, artifactId, attempt]);

  const duration = mediaDuration > 0 ? mediaDuration : Math.max(0, Number(durationSeconds) || 0);
  const list = useMemo(() => normalizeChapters(chapters, duration), [chapters, duration]);
  const ticks = useMemo(() => chapterTicks(list, duration), [list, duration]);
  const currentIndex = chapterIndexAt(list, time);
  const current = currentIndex >= 0 ? list[currentIndex] : null;
  const currentSectionId = current && current.briefingSectionId ? current.briefingSectionId : null;

  const sectionCallback = useRef(onSectionChange);
  useEffect(() => {
    sectionCallback.current = onSectionChange;
  }, [onSectionChange]);
  useEffect(() => {
    if (sectionCallback.current) sectionCallback.current(currentSectionId);
  }, [currentSectionId]);
  useEffect(
    () => () => {
      if (sectionCallback.current) sectionCallback.current(null);
    },
    []
  );

  const play = useCallback(() => {
    const audio = audioRef.current;
    if (!audio || !src) {
      pendingPlay.current = true;
      return;
    }
    const result = audio.play();
    if (result && typeof result.catch === 'function') result.catch(() => setPlaying(false));
  }, [src]);

  const pause = useCallback(() => {
    if (audioRef.current) audioRef.current.pause();
  }, []);

  const seekTo = useCallback(
    (seconds) => {
      const next = clampTime(seconds, duration);
      if (audioRef.current) audioRef.current.currentTime = next;
      setTime(next);
    },
    [duration]
  );

  const seekToSection = useCallback(
    (sectionId) => {
      const chapter = list.find((c) => c.briefingSectionId === sectionId);
      if (!chapter) return false;
      seekTo(chapter.startSeconds);
      play();
      return true;
    },
    [list, seekTo, play]
  );

  useImperativeHandle(apiRef, () => ({ seekTo, seekToSection, play, pause }), [seekTo, seekToSection, play, pause]);

  useEffect(() => {
    if (audioRef.current) audioRef.current.playbackRate = speed;
  }, [speed, src]);

  function onLoadedMetadata(event) {
    const d = Number(event.currentTarget.duration);
    if (Number.isFinite(d) && d > 0) setMediaDuration(d);
    event.currentTarget.playbackRate = speed;
    if (pendingPlay.current) {
      pendingPlay.current = false;
      play();
    }
  }

  const sizeLabel = bytes > 0 ? f.bytes(bytes) : null;
  if (loadError) {
    return (
      <div className={'audio-player' + (compact ? ' compact' : '')} data-testid="audio-player">
        <ErrorState error={loadError} inline onRetry={() => setAttempt((n) => n + 1)} />
      </div>
    );
  }

  const valueText = t('audio.player.position', { time: formatClock(time), duration: formatClock(duration) }) + (current ? ', ' + current.title : '');

  return (
    <div className={'audio-player' + (compact ? ' compact' : '')} data-testid="audio-player">
      {src ? (
        <audio
          ref={audioRef}
          src={src}
          preload="metadata"
          onLoadedMetadata={onLoadedMetadata}
          onDurationChange={onLoadedMetadata}
          onTimeUpdate={(e) => setTime(Number(e.currentTarget.currentTime) || 0)}
          onPlay={() => setPlaying(true)}
          onPause={() => setPlaying(false)}
          onEnded={() => setPlaying(false)}
          data-testid="audio-element"
        />
      ) : null}
      {!src ? (
        <LoadingState compact label={sizeLabel ? t('audio.player.loadingSize', { size: sizeLabel }) : t('audio.player.loading')} />
      ) : (
        <>
          <div className="audio-controls">
            <button type="button" className="icon-button" onClick={() => seekTo(time - SKIP_BACK_SECONDS)} aria-label={t('audio.player.back', { seconds: SKIP_BACK_SECONDS })} title={t('audio.player.back', { seconds: SKIP_BACK_SECONDS })}>
              <RotateCcw size={16} aria-hidden="true" />
              <span className="audio-skip-label" aria-hidden="true">
                {SKIP_BACK_SECONDS}
              </span>
            </button>
            <button type="button" className="button primary audio-play" onClick={playing ? pause : play} aria-label={playing ? t('audio.player.pause') : t('audio.player.play')} title={playing ? t('audio.player.pause') : t('audio.player.play')}>
              {playing ? <Pause size={16} aria-hidden="true" /> : <Play size={16} aria-hidden="true" />}
            </button>
            <button type="button" className="icon-button" onClick={() => seekTo(time + SKIP_FORWARD_SECONDS)} aria-label={t('audio.player.forward', { seconds: SKIP_FORWARD_SECONDS })} title={t('audio.player.forward', { seconds: SKIP_FORWARD_SECONDS })}>
              <RotateCw size={16} aria-hidden="true" />
              <span className="audio-skip-label" aria-hidden="true">
                {SKIP_FORWARD_SECONDS}
              </span>
            </button>
            <div className="audio-timeline">
              {title || current ? (
                <div className="audio-now muted small truncate" aria-live="polite">
                  {current ? t('audio.player.nowPlaying', { chapter: current.title }) : title}
                </div>
              ) : null}
              <ProgressBar time={time} duration={duration} ticks={ticks} label={t('audio.player.seek')} valueText={valueText} onSeek={seekTo} />
            </div>
            <span className="audio-clock mono small" data-testid="audio-clock">
              {formatClock(time) + ' / ' + formatClock(duration)}
            </span>
            <select className="audio-speed" value={String(speed)} onChange={(e) => setSpeed(Number(e.target.value))} aria-label={t('audio.player.speed')} title={t('audio.player.speed')}>
              {PLAYBACK_SPEEDS.map((s) => (
                <option key={s} value={String(s)}>
                  {t('audio.player.speedOption', { speed: f.number(s, { minimumFractionDigits: 0, maximumFractionDigits: 2 }) })}
                </option>
              ))}
            </select>
          </div>
          {!compact && list.length > 0 ? (
            <ol className="audio-chapters" aria-label={t('audio.player.chapters')}>
              {list.map((c, i) => (
                <li key={c.id || i}>
                  <button type="button" className={'audio-chapter' + (i === currentIndex ? ' current' : '')} aria-current={i === currentIndex ? 'true' : undefined} onClick={() => {
                      seekTo(c.startSeconds);
                      play();
                    }}
                  >
                    <span className="mono small muted">{formatClock(c.startSeconds)}</span>
                    <span className="truncate">{c.title}</span>
                  </button>
                </li>
              ))}
            </ol>
          ) : null}
        </>
      )}
    </div>
  );
}

AudioPlayer.propTypes = {
  artifactId: PropTypes.string.isRequired,
  chapters: PropTypes.array,
  durationSeconds: PropTypes.number,
  bytes: PropTypes.number,
  title: PropTypes.string,
  compact: PropTypes.bool,
  onSectionChange: PropTypes.func,
  apiRef: PropTypes.oneOfType([PropTypes.func, PropTypes.shape({ current: PropTypes.any })]),
  autoPlay: PropTypes.bool
};
