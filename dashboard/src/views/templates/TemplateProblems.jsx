import PropTypes from 'prop-types';
import { CircleAlert, CircleCheck, TriangleAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';

// Problems from a 400 TemplateInvalid response, whose details are keyed "line N" (a repeat on one line is keyed
// "line N (k)"). Keys that carry no line number are kept with line 0.
export function problemsFromDetails(details) {
  if (!details || typeof details !== 'object') return [];
  return Object.entries(details).map(([key, message]) => {
    const m = /^line\s+(\d+)/i.exec(key);
    return { line: m ? Number(m[1]) : 0, message: String(message), isWarning: false };
  });
}

// Errors first within a line, then by line; duplicates (same line and message) are dropped.
export function sortProblems(problems) {
  const seen = new Set();
  return (problems || [])
    .filter((p) => {
      const key = p.line + '|' + p.message + '|' + Boolean(p.isWarning);
      if (seen.has(key)) return false;
      seen.add(key);
      return true;
    })
    .slice()
    .sort((a, b) => (a.line || 0) - (b.line || 0) || Number(Boolean(a.isWarning)) - Number(Boolean(b.isWarning)));
}

// The validation result for a template: a summary line, then each error and warning with its line number. Clicking a
// line number calls onJump(line) so the editor can select it. checking shows that a newer validation is on the way.
export default function TemplateProblems({ problems = [], checking = false, onJump = null, title = null }) {
  const { t } = useTranslation();
  const list = sortProblems(problems);
  const errors = list.filter((p) => !p.isWarning).length;
  const warnings = list.length - errors;
  return (
    <div className="template-problems" data-testid="template-problems">
      <div className={'template-problems-summary ' + (errors > 0 ? 'has-errors' : warnings > 0 ? 'has-warnings' : 'is-valid')} role="status" aria-live="polite">
        {errors > 0 ? <CircleAlert size={14} aria-hidden="true" /> : warnings > 0 ? <TriangleAlert size={14} aria-hidden="true" /> : <CircleCheck size={14} aria-hidden="true" />}
        <span>
          {title ? <strong>{title} </strong> : null}
          {errors === 0 && warnings === 0 ? t('templates.problems.none') : [errors > 0 ? t('templates.problems.errors', { count: errors }) : null, warnings > 0 ? t('templates.problems.warnings', { count: warnings }) : null].filter(Boolean).join(', ')}
        </span>
        {checking ? <span className="muted small">{t('templates.problems.checking')}</span> : null}
      </div>
      {list.length > 0 ? (
        <ul className="template-problem-list">
          {list.map((p, i) => (
            <li key={i} className={p.isWarning ? 'warning' : 'error'}>
              {p.line > 0 && onJump ? (
                <button type="button" className="link-button mono small" onClick={() => onJump(p.line)} aria-label={t('templates.problems.jump', { line: p.line })}>
                  {t('templates.problems.line', { line: p.line })}
                </button>
              ) : (
                <span className="mono small">{p.line > 0 ? t('templates.problems.line', { line: p.line }) : t('templates.problems.general')}</span>
              )}
              <span className="problem-kind small">{p.isWarning ? t('templates.problems.warning') : t('templates.problems.error')}</span>
              <span className="problem-message">{p.message}</span>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}

TemplateProblems.propTypes = {
  problems: PropTypes.arrayOf(PropTypes.shape({ line: PropTypes.number, message: PropTypes.string.isRequired, isWarning: PropTypes.bool })),
  checking: PropTypes.bool,
  onJump: PropTypes.func,
  title: PropTypes.string
};
