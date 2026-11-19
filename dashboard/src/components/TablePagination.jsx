import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useLocale } from '../i18n/index.js';
import { formatNumber } from '../i18n/formatters.js';

export default function TablePagination({ pageNumber, totalPages, pageSize, pageSizes, onPageChange, onPageSizeChange, disabled = false }) {
  const { t } = useTranslation();
  const { locale } = useLocale();
  const pages = Math.max(1, totalPages || 1);
  const [jump, setJump] = useState(String(pageNumber));

  useEffect(() => {
    setJump(String(pageNumber));
  }, [pageNumber]);

  function go(page) {
    const next = Math.min(pages, Math.max(1, page));
    if (next !== pageNumber) onPageChange(next);
  }

  return (
    <div className="table-pagination">
      <label className="page-size">
        <span>{t('table.pageSize')}</span>
        <select value={pageSize} onChange={(e) => onPageSizeChange(Number(e.target.value))} disabled={disabled} aria-label={t('table.pageSize')}>
          {pageSizes.map((size) => (
            <option key={size} value={size}>
              {formatNumber(size, locale)}
            </option>
          ))}
        </select>
      </label>
      <button type="button" className="icon-button" onClick={() => go(1)} disabled={disabled || pageNumber <= 1} aria-label={t('table.firstPage')} title={t('table.firstPage')}>
        <ChevronsLeft size={16} aria-hidden="true" />
      </button>
      <button type="button" className="icon-button" onClick={() => go(pageNumber - 1)} disabled={disabled || pageNumber <= 1} aria-label={t('table.previousPage')} title={t('table.previousPage')}>
        <ChevronLeft size={16} aria-hidden="true" />
      </button>
      <form
        className="page-jump"
        onSubmit={(e) => {
          e.preventDefault();
          const n = Number(jump);
          if (Number.isFinite(n)) go(Math.round(n));
        }}
      >
        <input
          type="number"
          min={1}
          max={pages}
          value={jump}
          onChange={(e) => setJump(e.target.value)}
          onBlur={() => {
            const n = Number(jump);
            if (Number.isFinite(n)) go(Math.round(n));
          }}
          aria-label={t('table.jumpToPage')}
          disabled={disabled}
        />
        <span className="muted">{t('table.ofPages', { total: pages })}</span>
      </form>
      <button type="button" className="icon-button" onClick={() => go(pageNumber + 1)} disabled={disabled || pageNumber >= pages} aria-label={t('table.nextPage')} title={t('table.nextPage')}>
        <ChevronRight size={16} aria-hidden="true" />
      </button>
      <button type="button" className="icon-button" onClick={() => go(pages)} disabled={disabled || pageNumber >= pages} aria-label={t('table.lastPage')} title={t('table.lastPage')}>
        <ChevronsRight size={16} aria-hidden="true" />
      </button>
    </div>
  );
}

TablePagination.propTypes = {
  pageNumber: PropTypes.number.isRequired,
  totalPages: PropTypes.number,
  pageSize: PropTypes.number.isRequired,
  pageSizes: PropTypes.arrayOf(PropTypes.number).isRequired,
  onPageChange: PropTypes.func.isRequired,
  onPageSizeChange: PropTypes.func.isRequired,
  disabled: PropTypes.bool
};
