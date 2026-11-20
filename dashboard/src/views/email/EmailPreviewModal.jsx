import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';

// Rendered email in a sandboxed iframe (no scripts, no same-origin access, links open nowhere), with a plain text
// tab. load() resolves to { subject, recipient, html, text } (a run preview) or an outbox message with renderedHtml
// and renderedText; both shapes are accepted.
export default function EmailPreviewModal({ title, load, onClose }) {
  const { t } = useTranslation();
  const [mail, setMail] = useState(null);
  const [error, setError] = useState(null);
  const [tab, setTab] = useState('html');

  useEffect(() => {
    let live = true;
    load()
      .then((m) => live && setMail(m || {}))
      .catch((err) => live && setError(err));
    return () => {
      live = false;
    };
  }, [load]);

  const html = mail ? mail.html || mail.renderedHtml || '' : '';
  const text = mail ? mail.text || mail.renderedText || '' : '';
  return (
    <Modal open size="xlarge" title={title} subtitle={mail && mail.subject ? mail.subject : null} onClose={onClose}>
      <ErrorState error={error} inline />
      {!mail && !error ? <LoadingState /> : null}
      {mail ? (
        <div className="stack">
          {mail.recipient ? <p className="muted small">{t('email.preview.to', { recipient: mail.recipient })}</p> : null}
          <div className="tabs" role="tablist" aria-label={t('email.preview.format')}>
            {['html', 'text'].map((id) => (
              <button type="button" role="tab" key={id} aria-selected={tab === id} className={tab === id ? 'active' : ''} onClick={() => setTab(id)}>
                {t('email.preview.' + id)}
              </button>
            ))}
          </div>
          {tab === 'html' ? (
            html ? (
              <iframe className="email-preview-frame" title={t('email.preview.frame')} sandbox="" srcDoc={html} referrerPolicy="no-referrer" />
            ) : (
              <p className="muted">{t('email.preview.noHtml')}</p>
            )
          ) : (
            <pre className="email-preview-text">{text || t('email.preview.noText')}</pre>
          )}
        </div>
      ) : null}
    </Modal>
  );
}

EmailPreviewModal.propTypes = { title: PropTypes.string.isRequired, load: PropTypes.func.isRequired, onClose: PropTypes.func.isRequired };
