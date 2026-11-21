import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { CircleAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import useFormat from '../hooks/useFormat.js';

const WIDTH = 300;

// A small marker beside a cost when some model calls had no price, with an explanation that opens on click, Enter, or
// Space: an endpoint without prices (set them on the Models page), or an agent CLI on a subscription (the cost is
// included in the subscription). The explanation stays in the DOM next to the marker (fixed position, so table and
// drawer overflow do not clip it) so keyboard focus reaches its link inside dialogs. Escape or a click outside closes
// it. unpriced is the number of calls without a price, total the number of calls; all says none had a price.
export default function UnpricedNote({ unpriced, total, all = false }) {
  const { t } = useTranslation();
  const f = useFormat();
  const id = useId();
  const [open, setOpen] = useState(false);
  const [position, setPosition] = useState({ top: 0, left: 0 });
  const buttonRef = useRef(null);
  const wrapRef = useRef(null);
  const title = all ? t('costs.unpriced.allTitle') : t('costs.unpriced.someTitle');

  useLayoutEffect(() => {
    if (!open || !buttonRef.current) return;
    const rect = buttonRef.current.getBoundingClientRect();
    const rtl = document.documentElement.dir === 'rtl';
    const left = Math.max(8, Math.min(rtl ? rect.right - WIDTH : rect.left, window.innerWidth - WIDTH - 8));
    const estimated = 210;
    const top = rect.bottom + estimated > window.innerHeight ? Math.max(8, rect.top - estimated - 4) : rect.bottom + 4;
    setPosition({ top, left });
  }, [open]);

  useEffect(() => {
    if (!open) return undefined;
    function onDocument(event) {
      if (wrapRef.current && !wrapRef.current.contains(event.target)) setOpen(false);
    }
    function onScroll(event) {
      if (wrapRef.current && wrapRef.current.contains(event.target)) return;
      setOpen(false);
    }
    document.addEventListener('mousedown', onDocument);
    window.addEventListener('scroll', onScroll, true);
    return () => {
      document.removeEventListener('mousedown', onDocument);
      window.removeEventListener('scroll', onScroll, true);
    };
  }, [open]);

  return (
    <span
      className="unpriced-note"
      ref={wrapRef}
      data-ignore-row-click="true"
      onKeyDown={(e) => {
        if (e.key === 'Escape' && open) {
          e.stopPropagation();
          setOpen(false);
          if (buttonRef.current) buttonRef.current.focus();
        }
      }}
    >
      <button
        type="button"
        ref={buttonRef}
        className="unpriced-marker"
        aria-expanded={open}
        aria-controls={id}
        aria-label={title}
        title={title}
        data-testid="unpriced-marker"
        onClick={(e) => {
          e.stopPropagation();
          setOpen((o) => !o);
        }}
      >
        <CircleAlert size={13} aria-hidden="true" />
      </button>
      {open ? (
        <span className="cost-popover" id={id} role="group" aria-label={title} style={{ top: position.top, left: position.left, width: WIDTH }} data-testid="unpriced-help">
          <strong>{title}</strong>
          {total > 0 ? <span className="small">{t('costs.unpriced.count', { count: f.number(unpriced), total: f.number(total) })}</span> : null}
          <span className="small">
            {t('costs.unpriced.endpoint')}{' '}
            <Link to="/dashboard/models" onClick={() => setOpen(false)}>
              {t('costs.unpriced.setPrices')}
            </Link>
          </span>
          <span className="small">{t('costs.unpriced.subscription')}</span>
          {!all ? <span className="small muted">{t('costs.unpriced.partial')}</span> : null}
        </span>
      ) : null}
    </span>
  );
}

UnpricedNote.propTypes = { unpriced: PropTypes.number, total: PropTypes.number, all: PropTypes.bool };
