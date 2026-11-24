import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link, Route, Routes, useNavigate, useParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import ErrorState from '../components/ErrorState.jsx';
import FormField from '../components/FormField.jsx';
import LoadingState from '../components/LoadingState.jsx';
import MarkdownEditor from '../components/MarkdownEditor.jsx';
import PageHeader from '../components/PageHeader.jsx';
import ScheduleEditor from '../components/ScheduleEditor.jsx';
import TablePagination from '../components/TablePagination.jsx';
import { defaultScheduleForm, formFromSchedule, scheduleBody } from '../utils/schedule.js';
import HistoryFields from './topics/HistoryFields.jsx';

function CatalogAdminList() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [page, setPage] = useState(null);
  const [pageNumber, setPageNumber] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const [error, setError] = useState(null);
  const load = useCallback(async () => {
    try {
      setPage(await apiClient.listAdminCatalog({ pageNumber, pageSize }));
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, pageNumber, pageSize]);
  useEffect(() => {
    load();
  }, [load]);
  return (
    <div className="page">
      <PageHeader
        title={t('catalog.adminTitle')}
        summary={t('catalog.summary')}
        actions={
          <Link className="button primary" to="/dashboard/admin/catalog/new">
            {t('common.add')}
          </Link>
        }
      />
      <ErrorState error={error} onRetry={load} />
      {!page && !error ? <LoadingState /> : null}
      <section className="panel stack">
        {page?.objects.length === 0 ? <p className="muted">{t('catalog.empty')}</p> : null}
        <ul className="event-list">
          {page?.objects.map((entry) => (
            <li key={entry.id}>
              <Link to={'/dashboard/admin/catalog/' + entry.id}>{entry.title}</Link>
              <span className="muted small">{t('catalog.' + entry.status)}</span>
            </li>
          ))}
        </ul>
        {page ? (
          <TablePagination
            pageNumber={pageNumber}
            totalPages={page.totalPages}
            pageSize={pageSize}
            pageSizes={[25, 50, 100]}
            onPageChange={setPageNumber}
            onPageSizeChange={(size) => {
              setPageSize(size);
              setPageNumber(1);
            }}
          />
        ) : null}
      </section>
    </div>
  );
}

function MetadataForm({ publication, onSaved }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [form, setForm] = useState(
    () => publication || { title: '', description: '', category: 'General', language: 'en', status: 'Draft', freeEligible: false }
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const set = (patch) => setForm((current) => ({ ...current, ...patch }));
  const save = async (event) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      onSaved(publication ? await apiClient.updateCatalog(publication.id, form) : await apiClient.createCatalog(form));
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  };
  return (
    <form className="panel stack" onSubmit={save}>
      <ErrorState error={error} inline />
      <div className="form-grid">
        <FormField id="catalog-title" label={t('wizard.context.name')} wide>
          <input id="catalog-title" value={form.title} required maxLength={200} onChange={(e) => set({ title: e.target.value })} />
        </FormField>
        <FormField id="catalog-description" label={t('catalog.description')} wide>
          <textarea
            id="catalog-description"
            value={form.description || ''}
            maxLength={4000}
            rows={3}
            onChange={(e) => set({ description: e.target.value })}
          />
        </FormField>
        <FormField id="catalog-category" label={t('catalog.category')}>
          <input id="catalog-category" value={form.category} required maxLength={100} onChange={(e) => set({ category: e.target.value })} />
        </FormField>
        <FormField id="catalog-language" label={t('common.language')}>
          <input id="catalog-language" value={form.language} required maxLength={16} onChange={(e) => set({ language: e.target.value })} />
        </FormField>
        <FormField id="catalog-status" label={t('common.status')}>
          <select id="catalog-status" value={form.status} onChange={(e) => set({ status: e.target.value })}>
            {['Draft', 'Published', 'Retired', 'Retracted'].map((status) => (
              <option key={status} value={status}>
                {t('catalog.' + status)}
              </option>
            ))}
          </select>
        </FormField>
        <FormField id="catalog-free" label={t('catalog.freeEligible')}>
          <input id="catalog-free" type="checkbox" checked={form.freeEligible} onChange={(e) => set({ freeEligible: e.target.checked })} />
        </FormField>
      </div>
      <div>
        <button type="submit" className="button primary" disabled={busy}>
          {t(busy ? 'common.saving' : 'common.save')}
        </button>
      </div>
    </form>
  );
}
MetadataForm.propTypes = { publication: PropTypes.object, onSaved: PropTypes.func.isRequired };

function ResearchForm({ publication, research, onSaved }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const topic = research.topic;
  const [form, setForm] = useState(() => ({
    brief: topic.brief || topic.description || '',
    status: topic.status,
    audioEnabled: topic.audioEnabled,
    historyMode: topic.historyMode || 'Off',
    historyCount: topic.historyCount ?? 5,
    historySinceUtc: topic.historySinceUtc?.slice(0, 10) || '',
    historyBriefingId: topic.historyBriefingId || ''
  }));
  const [schedule, setSchedule] = useState(() => (research.schedule ? formFromSchedule(research.schedule) : defaultScheduleForm('UTC')));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [runId, setRunId] = useState(null);
  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const updated = await apiClient.saveCatalogResearch(publication.id, {
        topic: {
          ...form,
          historyCount: Number(form.historyCount),
          historySinceUtc:
            form.historyMode === 'SinceDate' && form.historySinceUtc ? new Date(form.historySinceUtc + 'T00:00:00Z').toISOString() : null,
          historyBriefingId: form.historyMode === 'SpecificIssue' ? form.historyBriefingId : null
        },
        schedule: scheduleBody(schedule)
      });
      onSaved(updated);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  };
  const run = async () => {
    setBusy(true);
    setError(null);
    try {
      setRunId((await apiClient.runCatalog(publication.id)).id);
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  };
  return (
    <section className="panel stack">
      <h2>{t('topics.workspace.brief')}</h2>
      <ErrorState error={error} inline />
      <MarkdownEditor
        id="catalog-research-brief"
        value={form.brief}
        onChange={(brief) => setForm({ ...form, brief })}
        label={t('wizard.context.brief')}
        disabled={busy}
      />
      <div className="form-grid">
        <HistoryFields topicId={topic.id} catalogPublicationId={publication.id} value={form} onChange={setForm} disabled={busy} />
        <FormField id="catalog-audio" label={t('topicAudio.label')}>
          <input
            id="catalog-audio"
            type="checkbox"
            checked={Boolean(form.audioEnabled)}
            onChange={(e) => setForm({ ...form, audioEnabled: e.target.checked })}
            disabled={busy}
          />
        </FormField>
        <FormField id="catalog-active" label={t('common.active')}>
          <input
            id="catalog-active"
            type="checkbox"
            checked={form.status === 'Active'}
            onChange={(e) => setForm({ ...form, status: e.target.checked ? 'Active' : 'Paused' })}
            disabled={busy}
          />
        </FormField>
      </div>
      <ScheduleEditor value={schedule} onChange={setSchedule} idPrefix="catalog-schedule" disabled={busy} />
      <div className="row-actions">
        <button type="button" className="button primary" onClick={save} disabled={busy}>
          {t(busy ? 'common.saving' : 'common.save')}
        </button>
        <button
          type="button"
          className="button secondary"
          onClick={run}
          disabled={busy || publication.status !== 'Published' || topic.status !== 'Active'}
        >
          {t('topics.actions.runNow')}
        </button>
        {runId ? (
          <span role="status" className="muted small">
            {t('common.working')}
          </span>
        ) : null}
      </div>
    </section>
  );
}
ResearchForm.propTypes = {
  publication: PropTypes.object.isRequired,
  research: PropTypes.object.isRequired,
  onSaved: PropTypes.func.isRequired
};

function CatalogEditor() {
  const { id } = useParams();
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const navigate = useNavigate();
  const [publication, setPublication] = useState(null);
  const [research, setResearch] = useState(null);
  const [error, setError] = useState(null);
  const load = useCallback(async () => {
    if (id === 'new') return;
    try {
      const [p, r] = await Promise.all([apiClient.getAdminCatalog(id), apiClient.getCatalogResearch(id)]);
      setPublication(p);
      setResearch(r);
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, id]);
  useEffect(() => {
    load();
  }, [load]);
  return (
    <div className="page">
      <PageHeader
        title={publication?.title || t('catalog.adminTitle')}
        actions={<Link to="/dashboard/admin/catalog">{t('catalog.adminTitle')}</Link>}
      />
      <ErrorState error={error} onRetry={load} />
      {!publication && id !== 'new' && !error ? <LoadingState /> : null}
      {publication || id === 'new' ? (
        <MetadataForm
          key={publication?.lastUpdateUtc || 'new'}
          publication={publication}
          onSaved={(next) => {
            setPublication(next);
            if (id === 'new') navigate('/dashboard/admin/catalog/' + next.id);
            else load();
          }}
        />
      ) : null}
      {publication && research ? (
        <ResearchForm key={research.topic.lastUpdateUtc} publication={publication} research={research} onSaved={setResearch} />
      ) : null}
    </div>
  );
}
export default function AdminCatalogView() {
  return (
    <Routes>
      <Route index element={<CatalogAdminList />} />
      <Route path=":id" element={<CatalogEditor />} />
    </Routes>
  );
}
