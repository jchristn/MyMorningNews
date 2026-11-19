import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

const ATTRIBUTES = ['tier', 'status', 'group', 'limit', 'order', 'topic', 'dimension'];
const SOURCE_TONES = { findings: 'accent', predictions: 'info', suggestions: 'warning', citations: 'success', 'run-summaries': 'neutral', none: 'neutral' };

// The parsed template: front matter (name, title, audio) and each section with its directive attributes and the
// instruction passed to the composer. onJump(line) selects the section's heading in the editor.
export default function TemplateOutline({ validation, onJump = null }) {
  const { t } = useTranslation();
  if (!validation) return <p className="muted small">{t('templates.outline.pending')}</p>;
  const sections = validation.sections || [];
  const audio = validation.audio || null;
  return (
    <div className="stack template-outline" data-testid="template-outline">
      <KeyValueGrid
        rows={[
          { key: 'name', label: t('templates.outline.name'), value: validation.name },
          { key: 'title', label: t('templates.outline.title'), value: validation.title ? <code>{validation.title}</code> : null },
          {
            key: 'audio',
            label: t('templates.outline.audio'),
            value: audio ? (audio.enabled ? t('templates.outline.audioOn', { format: audio.format || 'narrator', minutes: audio.targetMinutes || 0 }) : t('templates.outline.audioOff')) : null
          },
          { key: 'intro', label: t('templates.outline.intro'), value: audio && audio.intro ? audio.intro : null, hidden: !(audio && audio.intro) }
        ]}
      />
      {sections.length === 0 ? <p className="muted small">{t('templates.outline.noSections')}</p> : null}
      <ol className="template-sections">
        {sections.map((s) => {
          const d = s.directive || {};
          const source = d.source || 'none';
          return (
            <li key={s.key + ':' + s.line}>
              <div className="template-section-head">
                {onJump && s.line ? (
                  <button type="button" className="link-button" onClick={() => onJump(s.line)} aria-label={t('templates.outline.jump', { heading: s.heading, line: s.line })}>
                    <strong>{s.heading}</strong>
                  </button>
                ) : (
                  <strong>{s.heading}</strong>
                )}
                {s.line ? <span className="muted small mono">{t('templates.problems.line', { line: s.line })}</span> : null}
              </div>
              <div className="chip-list">
                <StatusBadge tone={SOURCE_TONES[source] || 'neutral'} mono>
                  {t('templates.outline.source', { source })}
                </StatusBadge>
                {ATTRIBUTES.filter((a) => d[a] !== undefined && d[a] !== null && d[a] !== '' && !(a === 'limit' && source === 'none')).map((a) => (
                  <code key={a}>{a + '=' + d[a]}</code>
                ))}
              </div>
              {s.instruction ? <p className="muted small template-instruction">{s.instruction}</p> : null}
            </li>
          );
        })}
      </ol>
    </div>
  );
}

TemplateOutline.propTypes = { validation: PropTypes.object, onJump: PropTypes.func };
