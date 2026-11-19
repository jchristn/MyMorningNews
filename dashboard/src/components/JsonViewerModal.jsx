import { useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import Modal from './Modal.jsx';
import JsonTree from './JsonTree.jsx';
import CopyButton from './CopyButton.jsx';

export default function JsonViewerModal({ open, title, value, onClose }) {
  const { t } = useTranslation();
  const [raw, setRaw] = useState(false);
  const text = JSON.stringify(value, null, 2);
  return (
    <Modal
      open={open}
      title={title || t('json.title')}
      onClose={onClose}
      size="large"
      headerExtra={
        <div className="segmented" role="group" aria-label={t('json.view')}>
          <button type="button" className={!raw ? 'active' : ''} onClick={() => setRaw(false)} aria-pressed={!raw}>
            {t('json.tree')}
          </button>
          <button type="button" className={raw ? 'active' : ''} onClick={() => setRaw(true)} aria-pressed={raw}>
            {t('json.raw')}
          </button>
          <CopyButton value={text} labelKey="json.copy" />
        </div>
      }
    >
      {raw ? <pre className="code-block">{text}</pre> : <JsonTree value={value} />}
    </Modal>
  );
}

JsonViewerModal.propTypes = {
  open: PropTypes.bool.isRequired,
  title: PropTypes.string,
  value: PropTypes.any,
  onClose: PropTypes.func.isRequired
};
