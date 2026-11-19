import { useEffect, useLayoutEffect, useRef, useState } from 'react';
import { createPortal } from 'react-dom';
import PropTypes from 'prop-types';
import { Ellipsis } from 'lucide-react';
import { useTranslation } from 'react-i18next';

// Row action menu, portaled to the body so table overflow never clips it.
export default function ActionMenu({ actions }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState({ top: 0, left: 0 });
  const buttonRef = useRef(null);
  const menuRef = useRef(null);

  useLayoutEffect(() => {
    if (!open || !buttonRef.current) return;
    const rect = buttonRef.current.getBoundingClientRect();
    const width = 200;
    const left = Math.max(8, Math.min(rect.right - width, window.innerWidth - width - 8));
    const estimated = actions.length * 36 + 12;
    const top = rect.bottom + estimated > window.innerHeight ? Math.max(8, rect.top - estimated) : rect.bottom + 4;
    setPosition({ top, left });
  }, [open, actions.length]);

  useEffect(() => {
    if (!open) return undefined;
    function onDocument(event) {
      if (menuRef.current && !menuRef.current.contains(event.target) && !buttonRef.current.contains(event.target)) setOpen(false);
    }
    function onKey(event) {
      if (event.key === 'Escape') setOpen(false);
    }
    function onScroll() {
      setOpen(false);
    }
    document.addEventListener('mousedown', onDocument);
    document.addEventListener('keydown', onKey);
    window.addEventListener('scroll', onScroll, true);
    return () => {
      document.removeEventListener('mousedown', onDocument);
      document.removeEventListener('keydown', onKey);
      window.removeEventListener('scroll', onScroll, true);
    };
  }, [open]);

  const visible = actions.filter((a) => !a.hidden);
  if (visible.length === 0) return null;

  return (
    <>
      <button
        type="button"
        ref={buttonRef}
        className="icon-button"
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={t('table.actions')}
        title={t('table.actions')}
        data-ignore-row-click="true"
        onClick={(e) => {
          e.stopPropagation();
          setOpen((o) => !o);
        }}
      >
        <Ellipsis size={16} aria-hidden="true" />
      </button>
      {open
        ? createPortal(
            <div className="action-menu" role="menu" ref={menuRef} style={{ top: position.top, left: position.left }}>
              {visible.map((action) => (
                <button
                  type="button"
                  role="menuitem"
                  key={action.key}
                  className={action.danger ? 'danger' : ''}
                  disabled={action.disabled}
                  onClick={(e) => {
                    e.stopPropagation();
                    setOpen(false);
                    action.onClick();
                  }}
                >
                  {action.icon ? <action.icon size={14} aria-hidden="true" /> : null}
                  {action.label}
                </button>
              ))}
            </div>,
            document.body
          )
        : null}
    </>
  );
}

ActionMenu.propTypes = {
  actions: PropTypes.arrayOf(
    PropTypes.shape({
      key: PropTypes.string.isRequired,
      label: PropTypes.string.isRequired,
      onClick: PropTypes.func.isRequired,
      icon: PropTypes.elementType,
      danger: PropTypes.bool,
      disabled: PropTypes.bool,
      hidden: PropTypes.bool
    })
  ).isRequired
};
