import { useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useLocale } from '../i18n/index.js';
import { formatNumber, formatPercent } from '../i18n/formatters.js';
import ChartTooltip from './ChartTooltip.jsx';

// Ranked table with an inline bar per row (for example cost by user with each user's cost share). Rows are
// focusable and show a tooltip; click or Enter calls onRowClick.
export default function RankedBarTable({ rows, labelHeader, valueHeader, ariaLabel, formatValue, extraColumns = [], onRowClick, color = 'var(--chart-series-1)', showShare = true }) {
  const { t } = useTranslation();
  const { locale } = useLocale();
  const [tip, setTip] = useState(null);
  const fmt = formatValue || ((v) => formatNumber(v, locale));
  const max = Math.max(0, ...rows.map((r) => Number(r.value) || 0)) || 1;

  function show(point, row, rank) {
    setTip({
      x: point.clientX,
      y: point.clientY,
      title: '#' + rank + ' ' + row.label,
      lines: [
        { label: valueHeader, value: fmt(row.value || 0), color },
        ...(showShare ? [{ label: t('charts.share'), value: formatPercent(row.share || 0, locale, 1) }] : []),
        ...extraColumns.map((c) => ({ label: c.header, value: String(c.text ? c.text(row) : '') }))
      ]
    });
  }

  return (
    <div className="table-scroll ranked-table-frame">
      <table className="ranked-table" aria-label={ariaLabel}>
        <thead>
          <tr>
            <th scope="col" className="rank-cell">
              <span className="sr-only">{t('charts.rank')}</span>
            </th>
            <th scope="col">{labelHeader}</th>
            <th scope="col" className="bar-cell">
              {valueHeader}
            </th>
            {showShare ? (
              <th scope="col" className="align-right">
                {t('charts.share')}
              </th>
            ) : null}
            {extraColumns.map((c) => (
              <th scope="col" key={c.key} className={'align-right ' + (c.className || '')}>
                {c.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => {
            const pct = Math.max(0, Math.min(100, ((Number(row.value) || 0) / max) * 100));
            return (
              <tr
                key={row.key}
                className={onRowClick ? 'clickable' : ''}
                tabIndex={0}
                aria-label={'#' + (index + 1) + ' ' + row.label + ': ' + fmt(row.value || 0)}
                onMouseMove={(e) => show(e, row, index + 1)}
                onMouseLeave={() => setTip(null)}
                onFocus={(e) => {
                  const r = e.currentTarget.getBoundingClientRect();
                  show({ clientX: r.left + 120, clientY: r.top }, row, index + 1);
                }}
                onBlur={() => setTip(null)}
                onClick={() => onRowClick && onRowClick(row)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter' && onRowClick) onRowClick(row);
                }}
              >
                <td className="rank-cell muted">{formatNumber(index + 1, locale)}</td>
                <td className="ranked-label">
                  <span className="truncate" title={row.label}>
                    {row.label}
                  </span>
                  {row.sublabel ? <span className="muted small truncate">{row.sublabel}</span> : null}
                </td>
                <td className="bar-cell">
                  <span className="ranked-bar">
                    <span className="hbar-track">
                      <span className="hbar-fill" style={{ width: pct + '%', background: row.color || color }} />
                    </span>
                    <span className="ranked-value">{fmt(row.value || 0)}</span>
                  </span>
                </td>
                {showShare ? <td className="align-right">{formatPercent(row.share || 0, locale, 1)}</td> : null}
                {extraColumns.map((c) => (
                  <td key={c.key} className={'align-right ' + (c.className || '')}>
                    {c.render ? c.render(row) : c.text ? c.text(row) : ''}
                  </td>
                ))}
              </tr>
            );
          })}
        </tbody>
      </table>
      <ChartTooltip tip={tip} />
    </div>
  );
}

RankedBarTable.propTypes = {
  rows: PropTypes.arrayOf(
    PropTypes.shape({ key: PropTypes.string.isRequired, label: PropTypes.string.isRequired, sublabel: PropTypes.string, value: PropTypes.number, share: PropTypes.number, color: PropTypes.string })
  ).isRequired,
  labelHeader: PropTypes.string.isRequired,
  valueHeader: PropTypes.string.isRequired,
  ariaLabel: PropTypes.string.isRequired,
  formatValue: PropTypes.func,
  extraColumns: PropTypes.arrayOf(PropTypes.shape({ key: PropTypes.string.isRequired, header: PropTypes.string.isRequired, render: PropTypes.func, text: PropTypes.func, className: PropTypes.string })),
  onRowClick: PropTypes.func,
  color: PropTypes.string,
  showShare: PropTypes.bool
};
