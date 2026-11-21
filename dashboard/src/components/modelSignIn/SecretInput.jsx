import { useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { FileCheck, FileUp } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import PasswordInput from '../PasswordInput.jsx';
import { secretLabel } from './signInCatalog.js';
import { authFileName } from '../../utils/modelAuth.js';

// The largest secret the server accepts (SetupConnectRequest.Secret is limited to 65536 characters).
export const MAX_SECRET_BYTES = 65536;

function FieldMessage({ id, error, help }) {
  if (error) {
    return (
      <span id={id} className="field-error" role="alert">
        {error}
      </span>
    );
  }
  return help ? (
    <span id={id} className="field-help muted">
      {help}
    </span>
  ) : null;
}

FieldMessage.propTypes = { id: PropTypes.string, error: PropTypes.string, help: PropTypes.string };

// A sign-in file: chosen or dropped, read in the browser as text, and never shown. The name and size confirm which
// file it was. "Paste the contents instead" covers files the picker cannot reach.
function FileSecret({ id, provider, value, onChange, disabled, error, help }) {
  const { t } = useTranslation();
  const f = useFormat();
  const inputRef = useRef(null);
  const [file, setFile] = useState(null);
  const [pasting, setPasting] = useState(false);
  const [dragging, setDragging] = useState(false);
  const [readError, setReadError] = useState(null);
  const fileName = authFileName(provider) || '';
  const label = secretLabel(t, provider, 'AuthFile');
  const messageId = id + '-message';
  // A value set from outside (cleared after a save, or typed in paste mode) no longer matches the chosen file.
  const chosen = file && value ? file : null;

  function read(picked) {
    setReadError(null);
    if (!picked) return;
    if (picked.size > MAX_SECRET_BYTES) {
      setReadError(t('setup.field.fileTooLarge'));
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      const text = typeof reader.result === 'string' ? reader.result : '';
      setFile({ name: picked.name, size: picked.size });
      onChange(text);
    };
    reader.onerror = () => setReadError(t('setup.field.fileReadError'));
    reader.readAsText(picked);
  }

  function onDrop(event) {
    event.preventDefault();
    setDragging(false);
    if (disabled) return;
    read(event.dataTransfer && event.dataTransfer.files ? event.dataTransfer.files[0] : null);
  }

  function switchMode() {
    setPasting((p) => !p);
    setFile(null);
    setReadError(null);
    onChange('');
  }

  const shownError = error || readError;

  if (pasting) {
    return (
      <div className={'form-field' + (shownError ? ' has-error' : '')}>
        <label htmlFor={id} className="field-label">
          {t('setup.field.pasteLabel', { file: fileName })}
        </label>
        <textarea
          id={id}
          className="mono secret-paste"
          rows={5}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          autoComplete="off"
          spellCheck={false}
          disabled={disabled}
          aria-describedby={messageId}
        />
        <FieldMessage id={messageId} error={shownError} help={help} />
        <button type="button" className="link-button small" onClick={switchMode} disabled={disabled}>
          {t('setup.field.chooseInstead')}
        </button>
      </div>
    );
  }

  return (
    <div className={'form-field' + (shownError ? ' has-error' : '')}>
      <span className="field-label" id={id + '-label'}>
        {label}
      </span>
      <div
        className={'file-drop' + (dragging ? ' dragging' : '') + (chosen ? ' has-file' : '')}
        onDragOver={(e) => {
          e.preventDefault();
          if (!disabled) setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={onDrop}
        data-testid="file-drop"
      >
        {chosen ? (
          <>
            <FileCheck size={20} aria-hidden="true" className="file-drop-icon ok" />
            <span className="file-drop-text" aria-live="polite">
              <strong className="mono">{chosen.name}</strong> <span className="muted small">{f.bytes(chosen.size)}</span>
            </span>
          </>
        ) : (
          <>
            <FileUp size={20} aria-hidden="true" className="file-drop-icon" />
            <span className="file-drop-text">{t('setup.field.drop', { file: fileName })}</span>
          </>
        )}
        <button
          type="button"
          id={id}
          className="button secondary small"
          onClick={() => inputRef.current && inputRef.current.click()}
          disabled={disabled}
          aria-describedby={id + '-label ' + messageId}
        >
          {chosen ? t('setup.field.replace') : t('setup.field.chooseFile')}
        </button>
        <input
          ref={inputRef}
          type="file"
          className="sr-only"
          tabIndex={-1}
          aria-hidden="true"
          accept=".json,application/json"
          onChange={(e) => {
            read(e.target.files ? e.target.files[0] : null);
            e.target.value = '';
          }}
          disabled={disabled}
          data-testid={id + '-file'}
        />
      </div>
      <FieldMessage id={messageId} error={shownError} help={help || t('setup.field.fileHelp')} />
      <button type="button" className="link-button small" onClick={switchMode} disabled={disabled}>
        {t('setup.field.pasteInstead')}
      </button>
    </div>
  );
}

FileSecret.propTypes = {
  id: PropTypes.string.isRequired,
  provider: PropTypes.string.isRequired,
  value: PropTypes.string.isRequired,
  onChange: PropTypes.func.isRequired,
  disabled: PropTypes.bool,
  error: PropTypes.string,
  help: PropTypes.string
};

// The secret a sign-in method needs: a password field with show and hide for an API key or token, or a file for a
// sign-in file. Nothing is sent until the form is submitted.
export default function SecretInput({ id, provider, method, value, onChange, disabled = false, error = null, help = null }) {
  const { t } = useTranslation();
  if (method === 'AuthFile')
    return (
      <FileSecret
        id={id}
        provider={provider}
        value={value}
        onChange={onChange}
        disabled={disabled}
        error={error || undefined}
        help={help || undefined}
      />
    );
  const messageId = id + '-message';
  return (
    <div className={'form-field' + (error ? ' has-error' : '')}>
      <label htmlFor={id} className="field-label">
        {secretLabel(t, provider, method)}
      </label>
      <PasswordInput id={id} value={value} onChange={onChange} autoComplete="off" disabled={disabled} describedBy={messageId} />
      <FieldMessage id={messageId} error={error || undefined} help={help || undefined} />
    </div>
  );
}

SecretInput.propTypes = {
  id: PropTypes.string.isRequired,
  provider: PropTypes.string.isRequired,
  method: PropTypes.string.isRequired,
  value: PropTypes.string.isRequired,
  onChange: PropTypes.func.isRequired,
  disabled: PropTypes.bool,
  error: PropTypes.string,
  help: PropTypes.string
};
