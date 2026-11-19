import { useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import MarkdownView from './MarkdownView.jsx';

// Monospace Markdown editor with a Write and Preview toggle.
export default function MarkdownEditor({ id, value, onChange, rows = 12, disabled = false, label }) {
  const { t } = useTranslation();
  const [mode, setMode] = useState('write');
  return (
    <div className="markdown-editor">
      <div className="segmented" role="group" aria-label={label || t('markdown.mode')}>
        <button type="button" className={mode === 'write' ? 'active' : ''} aria-pressed={mode === 'write'} onClick={() => setMode('write')}>
          {t('markdown.write')}
        </button>
        <button type="button" className={mode === 'preview' ? 'active' : ''} aria-pressed={mode === 'preview'} onClick={() => setMode('preview')}>
          {t('markdown.preview')}
        </button>
      </div>
      {mode === 'write' ? (
        <textarea id={id} className="code-editor" rows={rows} value={value} disabled={disabled} onChange={(e) => onChange(e.target.value)} spellCheck="true" aria-label={label} />
      ) : (
        <div className="markdown-preview">{value ? <MarkdownView source={value} /> : <p className="muted small">{t('markdown.empty')}</p>}</div>
      )}
    </div>
  );
}

MarkdownEditor.propTypes = {
  id: PropTypes.string.isRequired,
  value: PropTypes.string.isRequired,
  onChange: PropTypes.func.isRequired,
  rows: PropTypes.number,
  disabled: PropTypes.bool,
  label: PropTypes.string
};
