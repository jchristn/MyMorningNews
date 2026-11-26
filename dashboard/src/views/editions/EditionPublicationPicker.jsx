import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { ArrowDown, ArrowUp, Plus, Save, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { ApiError } from '../../utils/api.js';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import { moveItem } from './EditionTopicPicker.jsx';

// The most system briefings one edition may include (the server rejects more).
export const MAX_EDITION_PUBLICATIONS = 20;

// Subscriptions read per page. The server filters to the caller's subscriptions (subscribed=true), searches title and
// description, and sorts by title; "Show more" reads the next page.
export const SUBSCRIPTION_PAGE_SIZE = 25;

// Delay before a typed search is sent.
const SEARCH_DELAY_MS = 250;

// "System briefings in this edition": catalog subscriptions whose issues the edition adds under "From your system
// briefings", in order. links are EditionResponse.publications; an unavailable entry (retracted, unsubscribed, or
// no longer allowed by the plan) is skipped at composition and can be removed here. onSaved receives the edition
// detail the server returns.
export default function EditionPublicationPicker({ editionId, links, onSaved, disabled = false }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const initial = useMemo(
    () =>
      (Array.isArray(links) ? links : [])
        .slice()
        .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
        .map((l) => l.publicationId),
    [links]
  );
  const linkById = useMemo(() => new Map((Array.isArray(links) ? links : []).map((l) => [l.publicationId, l])), [links]);
  const [chosen, setChosen] = useState(initial);
  const [catalog, setCatalog] = useState(null);
  const [pageInfo, setPageInfo] = useState({ pageNumber: 0, totalPages: 0, totalRecords: 0 });
  const [search, setSearch] = useState('');
  const [query, setQuery] = useState('');
  const [loadingMore, setLoadingMore] = useState(false);
  const [seen, setSeen] = useState(() => new Map());
  const [loadError, setLoadError] = useState(null);
  const requestRef = useRef(0);
  const [adding, setAdding] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    setChosen(initial);
  }, [initial]);

  useEffect(() => {
    const handle = setTimeout(() => setQuery(search.trim()), SEARCH_DELAY_MS);
    return () => clearTimeout(handle);
  }, [search]);

  // Reads one page of the caller's subscriptions. Page 1 replaces the list; later pages append. A response for an
  // older search is dropped.
  const fetchPage = useCallback(
    async (pageNumber) => {
      const request = ++requestRef.current;
      setLoadError(null);
      if (pageNumber > 1) setLoadingMore(true);
      try {
        const page = await apiClient.enumerateCatalog({
          subscribed: true,
          search: query || null,
          sortBy: 'title',
          sortDirection: 'Ascending',
          pageNumber,
          pageSize: SUBSCRIPTION_PAGE_SIZE
        });
        if (request !== requestRef.current) return;
        const objects = page && Array.isArray(page.objects) ? page.objects : [];
        setCatalog((current) => (pageNumber > 1 && current ? [...current, ...objects.filter((o) => !current.some((c) => c.id === o.id))] : objects));
        setSeen((current) => {
          const next = new Map(current);
          objects.forEach((o) => next.set(o.id, o));
          return next;
        });
        setPageInfo({ pageNumber: page?.pageNumber || pageNumber, totalPages: page?.totalPages || 0, totalRecords: page?.totalRecords || 0 });
      } catch (err) {
        if (request !== requestRef.current) return;
        setLoadError(err);
        if (pageNumber === 1) setCatalog([]);
      } finally {
        if (request === requestRef.current) setLoadingMore(false);
      }
    },
    [apiClient, query]
  );

  const load = useCallback(() => fetchPage(1), [fetchPage]);

  useEffect(() => {
    load();
  }, [load]);

  const subscribed = seen;
  const hasMore = pageInfo.pageNumber > 0 && pageInfo.pageNumber < pageInfo.totalPages;
  const available = (catalog || []).filter((entry) => !entry.suspended && !chosen.includes(entry.id));
  const dirty = chosen.length !== initial.length || chosen.some((id, i) => id !== initial[i]);
  const full = chosen.length >= MAX_EDITION_PUBLICATIONS;
  const capReached = error instanceof ApiError && error.status === 402;

  const nameOf = (id) => {
    const entry = subscribed.get(id);
    if (entry && entry.title) return entry.title;
    const link = linkById.get(id);
    return link && link.title ? link.title : id;
  };
  // A saved entry is unavailable when the server says so; a newly added one when its subscription is suspended.
  const unavailable = (id) => {
    const link = linkById.get(id);
    if (link) return link.available === false;
    const entry = subscribed.get(id);
    return Boolean(entry && entry.suspended);
  };

  async function save() {
    setSaving(true);
    setError(null);
    try {
      const described = await apiClient.setEditionPublications(editionId, chosen);
      addNotification(t('editions.publications.saved'), 'success');
      if (onSaved) onSaved(described);
    } catch (err) {
      setError(err);
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="stack edition-publications">
      <p className="muted small">{t('editions.publications.help', { max: MAX_EDITION_PUBLICATIONS })}</p>
      {catalog === null ? <LoadingState /> : null}
      <ErrorState error={loadError} inline onRetry={load} />
      {chosen.length === 0 ? <p className="muted small">{t('editions.publications.none')}</p> : null}
      {chosen.length > 0 ? (
        <ol className="ordered-picker" aria-label={t('editions.publications.chosen')}>
          {chosen.map((id, i) => {
            const name = nameOf(id);
            const missing = unavailable(id);
            return (
              <li key={id}>
                <span className="ordered-index muted small">{i + 1}</span>
                <span className="truncate" dir="auto">
                  {missing ? name : <Link to={'/dashboard/catalog/' + encodeURIComponent(id) + '/issues'}>{name}</Link>}
                </span>
                {missing ? <StatusBadge tone="warning">{t('editions.publications.unavailable')}</StatusBadge> : <span />}
                <span className="row-actions">
                  <button
                    type="button"
                    className="icon-button"
                    disabled={disabled || i === 0}
                    onClick={() => setChosen((l) => moveItem(l, i, -1))}
                    aria-label={t('editions.topics.moveUp', { name })}
                    title={t('editions.topics.moveUp', { name })}
                  >
                    <ArrowUp size={14} aria-hidden="true" />
                  </button>
                  <button
                    type="button"
                    className="icon-button"
                    disabled={disabled || i === chosen.length - 1}
                    onClick={() => setChosen((l) => moveItem(l, i, 1))}
                    aria-label={t('editions.topics.moveDown', { name })}
                    title={t('editions.topics.moveDown', { name })}
                  >
                    <ArrowDown size={14} aria-hidden="true" />
                  </button>
                  <button
                    type="button"
                    className="icon-button"
                    disabled={disabled}
                    onClick={() => setChosen((l) => l.filter((x) => x !== id))}
                    aria-label={t('editions.topics.remove', { name })}
                    title={t('editions.topics.remove', { name })}
                  >
                    <X size={14} aria-hidden="true" />
                  </button>
                </span>
              </li>
            );
          })}
        </ol>
      ) : null}
      {chosen.some(unavailable) ? <p className="muted small">{t('editions.publications.unavailableHelp')}</p> : null}
      {catalog !== null && catalog.length === 0 && !loadError && !query ? (
        <p className="muted small">
          {t('editions.publications.noSubscriptions')} <Link to="/dashboard/catalog">{t('editions.publications.browse')}</Link>
        </p>
      ) : null}
      {catalog !== null && (catalog.length > 0 || query) ? (
        <input
          type="search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          disabled={disabled}
          placeholder={t('editions.publications.searchPlaceholder')}
          aria-label={t('editions.publications.searchLabel')}
        />
      ) : null}
      {catalog !== null && query && catalog.length === 0 && !loadError ? <p className="muted small">{t('editions.publications.noMatches')}</p> : null}
      {available.length > 0 ? (
        <div className="row-actions">
          <select
            value={adding}
            onChange={(e) => setAdding(e.target.value)}
            disabled={disabled || full}
            aria-label={t('editions.publications.addLabel')}
          >
            <option value="">{t('editions.publications.pick')}</option>
            {available.map((entry) => (
              <option key={entry.id} value={entry.id}>
                {entry.title}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="button secondary small"
            disabled={disabled || !adding || full}
            onClick={() => {
              setChosen((l) => [...l, adding]);
              setAdding('');
            }}
          >
            <Plus size={14} aria-hidden="true" /> {t('editions.publications.add')}
          </button>
        </div>
      ) : null}
      {hasMore ? (
        <div className="row-actions">
          <span className="muted small">{t('editions.publications.showing', { shown: catalog.length, total: pageInfo.totalRecords })}</span>
          <button type="button" className="button secondary small" disabled={loadingMore} onClick={() => fetchPage(pageInfo.pageNumber + 1)}>
            {loadingMore ? t('common.loading') : t('editions.publications.more')}
          </button>
        </div>
      ) : null}
      {full ? <p className="muted small">{t('editions.publications.full', { max: MAX_EDITION_PUBLICATIONS })}</p> : null}
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {capReached ? (
        <p className="small notice notice-warning">
          {t('editions.publications.capReached')} <Link to="/dashboard/account/billing">{t('billing.title')}</Link>
        </p>
      ) : null}
      <div className="form-actions">
        <button type="button" className="button primary" disabled={disabled || saving || !dirty} onClick={save}>
          <Save size={14} aria-hidden="true" /> {saving ? t('common.saving') : t('editions.publications.save')}
        </button>
      </div>
    </div>
  );
}

EditionPublicationPicker.propTypes = {
  editionId: PropTypes.string.isRequired,
  links: PropTypes.array,
  onSaved: PropTypes.func,
  disabled: PropTypes.bool
};
