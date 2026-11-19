import { useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Columns3 } from 'lucide-react';
import { useTranslation } from 'react-i18next';

// Select Columns control. Pinned columns are always shown and cannot be unchecked.
export default function ColumnChooser({ columns, visibleKeys, onChange }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const ref = useRef(null);

  useEffect(() => {
    if (!open) return undefined;
    function onDocument(e) {
      if (ref.current && !ref.current.contains(e.target)) setOpen(false);
    }
    function onKey(e) {
      if (e.key === 'Escape') setOpen(false);
    }
    document.addEventListener('mousedown', onDocument);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('mousedown', onDocument);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  function toggle(key) {
    const next = visibleKeys.includes(key) ? visibleKeys.filter((k) => k !== key) : [...visibleKeys, key];
    onChange(next);
  }

  return (
    <div className="column-chooser" ref={ref}>
      <button type="button" className="button secondary small" onClick={() => setOpen((o) => !o)} aria-expanded={open} aria-haspopup="true">
        <Columns3 size={14} aria-hidden="true" /> {t('table.selectColumns')}
      </button>
      {open ? (
        <div className="column-chooser-popover" role="group" aria-label={t('table.selectColumns')}>
          {columns.map((c) => (
            <label key={c.key} className="checkbox-row">
              <input type="checkbox" checked={c.pinned || visibleKeys.includes(c.key)} disabled={c.pinned} onChange={() => toggle(c.key)} />
              <span>{c.header}</span>
            </label>
          ))}
        </div>
      ) : null}
    </div>
  );
}

ColumnChooser.propTypes = {
  columns: PropTypes.arrayOf(PropTypes.shape({ key: PropTypes.string.isRequired, header: PropTypes.string.isRequired, pinned: PropTypes.bool })).isRequired,
  visibleKeys: PropTypes.arrayOf(PropTypes.string).isRequired,
  onChange: PropTypes.func.isRequired
};
