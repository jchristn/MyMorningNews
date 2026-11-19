import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Check, CircleHelp, SkipForward } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import ErrorState from './ErrorState.jsx';
import LoadingState from './LoadingState.jsx';
import StatusBadge from './StatusBadge.jsx';

const SCALE = [1, 2, 3, 4, 5];

// The answer body for a question kind from local selections. Returns null when there is nothing to send yet.
export function answerBody(kind, { selectedIds = [], text = '' } = {}) {
  const trimmed = String(text || '').trim();
  if (kind === 'SingleChoice' || kind === 'MultiChoice') {
    if (selectedIds.length === 0 && !trimmed) return null;
    return { selectedOptionIds: kind === 'SingleChoice' ? selectedIds.slice(0, 1) : selectedIds, answerText: trimmed || null };
  }
  if (!trimmed) return null;
  return { answerText: trimmed };
}

// One clarifying question of any kind (single choice, multi choice, scale 1 to 5, yes or no, free text) with its
// one-line rationale and a Skip control. Choices save on click; text saves on blur or with Save.
// onAnswer(body) resolves to the updated { question, options }.
export default function QuestionCard({ item, onAnswer, disabled = false, index }) {
  const { t } = useTranslation();
  const { question } = item;
  const options = [...(item.options || [])].sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
  const [selectedIds, setSelectedIds] = useState(() => options.filter((o) => o.selected).map((o) => o.id));
  const [text, setText] = useState(question.answerText || '');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [hint, setHint] = useState(null);

  useEffect(() => {
    setSelectedIds(options.filter((o) => o.selected).map((o) => o.id));
    setText(question.answerText || '');
    // Resync only when the server copy changes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [question.lastUpdateUtc, question.id]);

  async function send(body) {
    setSaving(true);
    setError(null);
    setHint(null);
    try {
      await onAnswer(body);
    } catch (err) {
      setError(err);
    } finally {
      setSaving(false);
    }
  }

  function choose(nextIds, nextText = text) {
    setSelectedIds(nextIds);
    const body = answerBody(question.kind, { selectedIds: nextIds, text: nextText });
    if (body) send(body);
    else setHint(t('questions.pickOne'));
  }

  function saveText() {
    if ((question.answerText || '') === text.trim() && question.answered) return;
    const body = answerBody(question.kind, { selectedIds, text });
    if (body) send(body);
  }

  const name = 'q-' + question.id;
  const locked = disabled || saving;
  let control = null;
  if (question.kind === 'SingleChoice' || question.kind === 'MultiChoice') {
    const multi = question.kind === 'MultiChoice';
    control = (
      <>
        <div className="option-list" role={multi ? 'group' : 'radiogroup'} aria-label={question.questionText}>
          {options.map((o) => {
            const on = selectedIds.includes(o.id);
            return (
              <label key={o.id} className={'option-item' + (on ? ' selected' : '')}>
                <input
                  type={multi ? 'checkbox' : 'radio'}
                  name={name}
                  checked={on}
                  disabled={locked}
                  onChange={() => choose(multi ? (on ? selectedIds.filter((x) => x !== o.id) : [...selectedIds, o.id]) : [o.id])}
                />
                <span>{o.label}</span>
              </label>
            );
          })}
        </div>
        <input
          type="text"
          className="option-note"
          value={text}
          disabled={locked}
          placeholder={t('questions.somethingElse')}
          aria-label={t('questions.somethingElse')}
          onChange={(e) => setText(e.target.value)}
          onBlur={saveText}
        />
      </>
    );
  } else if (question.kind === 'Scale') {
    const low = options[0] ? options[0].label : t('questions.scaleLow');
    const high = options.length > 1 ? options[options.length - 1].label : t('questions.scaleHigh');
    control = (
      <div className="scale-control">
        <span className="muted small">{low}</span>
        <div className="segmented" role="radiogroup" aria-label={question.questionText}>
          {SCALE.map((n) => (
            <button
              type="button"
              key={n}
              role="radio"
              aria-checked={text === String(n)}
              className={text === String(n) ? 'active' : ''}
              disabled={locked}
              onClick={() => {
                setText(String(n));
                send({ answerText: String(n) });
              }}
            >
              {n}
            </button>
          ))}
        </div>
        <span className="muted small">{high}</span>
      </div>
    );
  } else if (question.kind === 'YesNo') {
    const value = String(text || '').toLowerCase();
    control = (
      <div className="segmented" role="radiogroup" aria-label={question.questionText}>
        {['yes', 'no'].map((v) => (
          <button
            type="button"
            key={v}
            role="radio"
            aria-checked={value === v}
            className={value === v ? 'active' : ''}
            disabled={locked}
            onClick={() => {
              setText(v);
              send({ answerText: v });
            }}
          >
            {v === 'yes' ? t('common.yes') : t('common.no')}
          </button>
        ))}
      </div>
    );
  } else {
    control = (
      <div className="free-text">
        <textarea rows={3} value={text} disabled={locked} aria-label={question.questionText} onChange={(e) => setText(e.target.value)} onBlur={saveText} />
        <button type="button" className="button secondary small" disabled={locked || !text.trim()} onClick={saveText}>
          {t('questions.save')}
        </button>
      </div>
    );
  }

  return (
    <article className={'question-card' + (question.skipped ? ' skipped' : '') + (question.answered ? ' answered' : '')} data-testid={'question-' + question.id}>
      <header className="question-header">
        <div className="question-text">
          <strong>
            {index !== undefined ? <span className="muted">{index + 1}. </span> : null}
            {question.questionText}
          </strong>
          {question.rationale ? (
            <span className="question-rationale muted small">
              <CircleHelp size={12} aria-hidden="true" /> {t('questions.why', { reason: question.rationale })}
            </span>
          ) : null}
        </div>
        <div className="question-state">
          {saving ? <LoadingState compact label={t('common.saving')} /> : null}
          {!saving && question.answered ? (
            <StatusBadge tone="success">
              <Check size={12} aria-hidden="true" /> {t('questions.answered')}
            </StatusBadge>
          ) : null}
          {!saving && question.skipped ? <StatusBadge>{t('questions.skipped')}</StatusBadge> : null}
          <StatusBadge tone="neutral">{t('enums.questionKind.' + question.kind)}</StatusBadge>
        </div>
      </header>
      <div className={question.skipped ? 'question-body dimmed' : 'question-body'}>{control}</div>
      <footer className="question-footer">
        {hint ? <span className="muted small">{hint}</span> : <span />}
        {!question.skipped ? (
          <button type="button" className="link-button small" disabled={locked} onClick={() => send({ skipped: true })}>
            <SkipForward size={12} aria-hidden="true" /> {t('questions.skip')}
          </button>
        ) : (
          <span className="muted small">{t('questions.skippedHint')}</span>
        )}
      </footer>
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
    </article>
  );
}

QuestionCard.propTypes = {
  item: PropTypes.shape({
    question: PropTypes.shape({
      id: PropTypes.string.isRequired,
      questionText: PropTypes.string.isRequired,
      rationale: PropTypes.string,
      kind: PropTypes.string.isRequired,
      answerText: PropTypes.string,
      answered: PropTypes.bool,
      skipped: PropTypes.bool,
      lastUpdateUtc: PropTypes.string
    }).isRequired,
    options: PropTypes.array
  }).isRequired,
  onAnswer: PropTypes.func.isRequired,
  disabled: PropTypes.bool,
  index: PropTypes.number
};
