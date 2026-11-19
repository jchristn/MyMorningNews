import { useEffect, useRef } from 'react';
import { createPortal } from 'react-dom';
import PropTypes from 'prop-types';
import { X } from 'lucide-react';
import { useTranslation } from 'react-i18next';

const FOCUSABLE = 'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';
let openCount = 0;

// Modal dialog: portaled, closes on Escape and backdrop click (unless dismissible is false), traps focus, locks body
// scroll. The header and footer stay visible while the body scrolls. drawer docks the dialog to the inline end as a
// full-height panel for detail views.
export default function Modal({ open, title, subtitle, onClose, size = 'medium', children, footer = null, dismissible = true, headerExtra = null, drawer = false }) {
  const { t } = useTranslation();
  const dialogRef = useRef(null);
  const previous = useRef(null);

  useEffect(() => {
    if (!open) return undefined;
    previous.current = document.activeElement;
    openCount += 1;
    document.body.classList.add('modal-open');
    const dialog = dialogRef.current;
    const first = dialog ? dialog.querySelector(FOCUSABLE) : null;
    if (first) first.focus();
    else if (dialog) dialog.focus();
    return () => {
      openCount = Math.max(0, openCount - 1);
      if (openCount === 0) document.body.classList.remove('modal-open');
      if (previous.current && previous.current.focus) previous.current.focus();
    };
  }, [open]);

  if (!open) return null;

  function onKeyDown(event) {
    if (event.key === 'Escape' && dismissible) {
      event.stopPropagation();
      onClose();
      return;
    }
    if (event.key !== 'Tab') return;
    const nodes = dialogRef.current ? Array.from(dialogRef.current.querySelectorAll(FOCUSABLE)) : [];
    if (nodes.length === 0) return;
    const first = nodes[0];
    const last = nodes[nodes.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }

  return createPortal(
    <div className={drawer ? 'modal-backdrop drawer-backdrop' : 'modal-backdrop'} onMouseDown={(e) => dismissible && e.target === e.currentTarget && onClose()}>
      <div className={drawer ? 'modal modal-drawer' : 'modal modal-' + size} role="dialog" aria-modal="true" aria-label={title} ref={dialogRef} tabIndex={-1} onKeyDown={onKeyDown}>
        <div className="modal-header">
          <div className="modal-title">
            <h2>{title}</h2>
            {subtitle ? <div className="modal-subtitle muted">{subtitle}</div> : null}
          </div>
          {headerExtra}
          {dismissible ? (
            <button type="button" className="icon-button" onClick={onClose} aria-label={t('common.close')} title={t('common.close')}>
              <X size={18} aria-hidden="true" />
            </button>
          ) : null}
        </div>
        <div className="modal-body">{children}</div>
        {footer ? <div className="modal-footer">{footer}</div> : null}
      </div>
    </div>,
    document.body
  );
}

Modal.propTypes = {
  open: PropTypes.bool.isRequired,
  title: PropTypes.string.isRequired,
  subtitle: PropTypes.node,
  onClose: PropTypes.func.isRequired,
  size: PropTypes.oneOf(['small', 'medium', 'large', 'xlarge']),
  children: PropTypes.node,
  footer: PropTypes.node,
  dismissible: PropTypes.bool,
  headerExtra: PropTypes.node,
  drawer: PropTypes.bool
};
