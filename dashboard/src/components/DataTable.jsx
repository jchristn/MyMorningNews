import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { ArrowDown, ArrowUp, ArrowUpDown, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import useDebounce from '../hooks/useDebounce.js';
import useLocalStorage from '../hooks/useLocalStorage.js';
import { useLocale } from '../i18n/index.js';
import { formatNumber } from '../i18n/formatters.js';
import ActionMenu from './ActionMenu.jsx';
import ColumnChooser from './ColumnChooser.jsx';
import EmptyState from './EmptyState.jsx';
import ErrorState from './ErrorState.jsx';
import LoadingState from './LoadingState.jsx';
import TablePagination from './TablePagination.jsx';

const IGNORE_SELECTOR = 'a, button, input, select, textarea, label, [data-ignore-row-click]';

// Server-side table: the backend sorts, filters, and pages. fetchPage receives
// { pageNumber, pageSize, sortBy, sortDirection: 'asc' | 'desc', filters } and returns an EnumerationResult.
export default function DataTable({
  tableKey,
  columns,
  fetchPage,
  rowKey = (row) => row.id,
  onRowClick,
  rowActions,
  toolbar = null,
  pageSizes = [10, 25, 50, 100],
  defaultPageSize = 25,
  defaultSort = null,
  extraFilters = null,
  refreshSignal = 0,
  selectable = false,
  bulkActions = null,
  emptyTitle,
  emptyMessage,
  filteredEmptyMessage,
  emptyAction = null,
  onLoaded
}) {
  const { t } = useTranslation();
  const { locale } = useLocale();
  const [pageSize, setPageSize] = useLocalStorage('mymorningnews.table.' + tableKey + '.pageSize', defaultPageSize);
  const defaultVisible = useMemo(() => columns.filter((c) => !c.defaultHidden).map((c) => c.key), [columns]);
  const [visibleKeys, setVisibleKeys] = useLocalStorage('mymorningnews.table.' + tableKey + '.columns', defaultVisible);
  const [pageNumber, setPageNumber] = useState(1);
  const [sort, setSort] = useState(defaultSort);
  const [filterInputs, setFilterInputs] = useState({});
  const debouncedFilters = useDebounce(filterInputs, 300);
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selected, setSelected] = useState(() => new Set());
  const requestId = useRef(0);

  const effectivePageSize = pageSizes.includes(pageSize) ? pageSize : defaultPageSize;
  const shown = columns.filter((c) => c.pinned || visibleKeys.includes(c.key));
  const hasFilterRow = columns.some((c) => c.filter);
  const extraKey = JSON.stringify(extraFilters || {});

  const filters = useMemo(() => {
    const merged = { ...(extraFilters || {}) };
    for (const [param, value] of Object.entries(debouncedFilters)) {
      if (value !== '' && value !== null && value !== undefined) merged[param] = value;
    }
    return merged;
    // extraKey captures extraFilters by value.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [debouncedFilters, extraKey]);

  const filtersKey = JSON.stringify(filters);
  const isFiltered = Object.keys(debouncedFilters).some((k) => debouncedFilters[k] !== '' && debouncedFilters[k] !== undefined);

  // Reset to page one whenever filters, sort, or page size change.
  useEffect(() => {
    setPageNumber(1);
  }, [filtersKey, sort, effectivePageSize]);

  const load = useCallback(async () => {
    const id = ++requestId.current;
    setLoading(true);
    setError(null);
    try {
      const data = await fetchPage({
        pageNumber,
        pageSize: effectivePageSize,
        sortBy: sort ? sort.key : undefined,
        sortDirection: sort ? sort.direction : undefined,
        filters
      });
      if (id !== requestId.current) return;
      setResult(data);
      setSelected(new Set());
      if (onLoaded) onLoaded(data);
    } catch (err) {
      if (id !== requestId.current) return;
      setError(err);
    } finally {
      if (id === requestId.current) setLoading(false);
    }
    // filtersKey stands in for filters.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fetchPage, pageNumber, effectivePageSize, sort, filtersKey]);

  useEffect(() => {
    load();
  }, [load, refreshSignal]);

  function toggleSort(column) {
    if (!column.sortKey) return;
    setSort((current) => {
      if (!current || current.key !== column.sortKey) return { key: column.sortKey, direction: 'asc' };
      if (current.direction === 'asc') return { key: column.sortKey, direction: 'desc' };
      return defaultSort;
    });
  }

  function onRow(event, row) {
    if (!onRowClick) return;
    if (event.target.closest(IGNORE_SELECTOR)) return;
    onRowClick(row);
  }

  const items = result ? result.objects || [] : [];
  const total = result ? result.totalRecords || 0 : 0;
  const totalPages = result ? Math.max(1, result.totalPages || 1) : 1;
  const from = total === 0 ? 0 : (pageNumber - 1) * effectivePageSize + 1;
  const to = Math.min(total, pageNumber * effectivePageSize);
  const selectedRows = items.filter((row) => selected.has(rowKey(row)));
  const allSelected = items.length > 0 && selectedRows.length === items.length;

  function toggleAll() {
    setSelected(allSelected ? new Set() : new Set(items.map(rowKey)));
  }

  function toggleOne(row) {
    setSelected((current) => {
      const next = new Set(current);
      const key = rowKey(row);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  return (
    <div className="data-table">
      <div className="table-toolbar">
        <div className="table-toolbar-left">
          <span className="table-count" aria-live="polite">
            {t('table.showing', { from: formatNumber(from, locale), to: formatNumber(to, locale), total: formatNumber(total, locale) })}
          </span>
          {selectable && selectedRows.length > 0 && !bulkActions ? <span className="badge badge-accent">{t('table.selected', { count: selectedRows.length })}</span> : null}
        </div>
        <div className="table-toolbar-right">
          <TablePagination
            pageNumber={pageNumber}
            totalPages={totalPages}
            pageSize={effectivePageSize}
            pageSizes={pageSizes}
            onPageChange={setPageNumber}
            onPageSizeChange={setPageSize}
            disabled={loading && !result}
          />
          <button type="button" className="icon-button" onClick={load} aria-label={t('common.refresh')} title={t('common.refresh')}>
            <RefreshCw size={16} className={loading ? 'spin' : ''} aria-hidden="true" />
          </button>
          <ColumnChooser columns={columns} visibleKeys={visibleKeys} onChange={setVisibleKeys} />
          {toolbar}
        </div>
      </div>
      {selectable && selectedRows.length > 0 && bulkActions ? (
        <div className="bulk-bar">
          <span>{t('table.selected', { count: selectedRows.length })}</span>
          {bulkActions(selectedRows, () => setSelected(new Set()))}
          <button type="button" className="button secondary small" onClick={() => setSelected(new Set())}>
            {t('table.clearSelection')}
          </button>
        </div>
      ) : null}
      <ErrorState error={error} inline onRetry={load} onDismiss={() => setError(null)} />
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              {selectable ? (
                <th className="select-cell">
                  <input type="checkbox" checked={allSelected} onChange={toggleAll} aria-label={t('table.selectAll')} />
                </th>
              ) : null}
              {shown.map((c) => {
                const active = sort && sort.key === c.sortKey;
                const ariaSort = active ? (sort.direction === 'asc' ? 'ascending' : 'descending') : c.sortKey ? 'none' : undefined;
                return (
                  <th key={c.key} className={(c.align === 'right' ? 'align-right ' : '') + (c.className || '')} aria-sort={ariaSort} scope="col">
                    {c.sortKey ? (
                      <button type="button" className="sort-button" onClick={() => toggleSort(c)}>
                        {c.header}
                        {active ? (
                          sort.direction === 'asc' ? <ArrowUp size={12} aria-hidden="true" /> : <ArrowDown size={12} aria-hidden="true" />
                        ) : (
                          <ArrowUpDown size={12} className="muted" aria-hidden="true" />
                        )}
                      </button>
                    ) : (
                      c.header
                    )}
                  </th>
                );
              })}
              {rowActions ? <th className="actions-cell"><span className="sr-only">{t('table.actions')}</span></th> : null}
            </tr>
            {hasFilterRow ? (
              <tr className="filter-row">
                {selectable ? <th /> : null}
                {shown.map((c) => (
                  <th key={c.key}>
                    {c.filter ? (
                      <ColumnFilter filter={c.filter} header={c.header} value={filterInputs[c.filter.param] ?? ''} onChange={(v) => setFilterInputs((f) => ({ ...f, [c.filter.param]: v }))} />
                    ) : null}
                  </th>
                ))}
                {rowActions ? <th /> : null}
              </tr>
            ) : null}
          </thead>
          <tbody>
            {items.map((row) => (
              <tr key={rowKey(row)} className={onRowClick ? 'clickable' : ''} onClick={(e) => onRow(e, row)}>
                {selectable ? (
                  <td className="select-cell">
                    <input type="checkbox" checked={selected.has(rowKey(row))} onChange={() => toggleOne(row)} aria-label={t('table.selectRow')} />
                  </td>
                ) : null}
                {shown.map((c) => (
                  <td key={c.key} className={(c.align === 'right' ? 'align-right ' : '') + (c.className || '')}>
                    {c.render ? c.render(row) : row[c.key] ?? ''}
                  </td>
                ))}
                {rowActions ? (
                  <td className="actions-cell">
                    <ActionMenu actions={rowActions(row)} />
                  </td>
                ) : null}
              </tr>
            ))}
          </tbody>
        </table>
        {loading && !result ? <LoadingState /> : null}
        {!loading && !error && result && items.length === 0 ? (
          <EmptyState
            title={isFiltered ? t('table.noMatchesTitle') : emptyTitle || t('table.emptyTitle')}
            message={isFiltered ? filteredEmptyMessage || t('table.noMatchesMessage') : emptyMessage}
            action={isFiltered ? null : emptyAction}
          />
        ) : null}
      </div>
    </div>
  );
}

function ColumnFilter({ filter, header, value, onChange }) {
  const { t } = useTranslation();
  const label = t('table.filterBy', { column: header });
  if (filter.type === 'select' || filter.type === 'boolean') {
    const options = filter.type === 'boolean' ? [{ value: 'true', label: t('common.yes') }, { value: 'false', label: t('common.no') }] : filter.options || [];
    return (
      <select value={value} onChange={(e) => onChange(e.target.value)} aria-label={label} data-ignore-row-click="true">
        <option value="">{t('table.any')}</option>
        {options.map((o) => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </select>
    );
  }
  return (
    <input
      type={filter.type === 'number' ? 'number' : 'search'}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      placeholder={filter.placeholder || label}
      aria-label={label}
      data-ignore-row-click="true"
    />
  );
}

ColumnFilter.propTypes = {
  filter: PropTypes.shape({ type: PropTypes.string, param: PropTypes.string.isRequired, options: PropTypes.array, placeholder: PropTypes.string }).isRequired,
  header: PropTypes.string.isRequired,
  value: PropTypes.oneOfType([PropTypes.string, PropTypes.number]),
  onChange: PropTypes.func.isRequired
};

DataTable.propTypes = {
  tableKey: PropTypes.string.isRequired,
  columns: PropTypes.arrayOf(
    PropTypes.shape({
      key: PropTypes.string.isRequired,
      header: PropTypes.string.isRequired,
      render: PropTypes.func,
      sortKey: PropTypes.string,
      filter: PropTypes.object,
      pinned: PropTypes.bool,
      defaultHidden: PropTypes.bool,
      align: PropTypes.string,
      className: PropTypes.string
    })
  ).isRequired,
  fetchPage: PropTypes.func.isRequired,
  rowKey: PropTypes.func,
  onRowClick: PropTypes.func,
  rowActions: PropTypes.func,
  toolbar: PropTypes.node,
  pageSizes: PropTypes.arrayOf(PropTypes.number),
  defaultPageSize: PropTypes.number,
  defaultSort: PropTypes.shape({ key: PropTypes.string, direction: PropTypes.oneOf(['asc', 'desc']) }),
  extraFilters: PropTypes.object,
  refreshSignal: PropTypes.number,
  selectable: PropTypes.bool,
  bulkActions: PropTypes.func,
  emptyTitle: PropTypes.string,
  emptyMessage: PropTypes.string,
  filteredEmptyMessage: PropTypes.string,
  emptyAction: PropTypes.node,
  onLoaded: PropTypes.func
};
