import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { ChevronDown, ChevronRight } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useFormat from '../hooks/useFormat.js';
import CopyButton from './CopyButton.jsx';
import CopyableId from './CopyableId.jsx';
import ErrorState from './ErrorState.jsx';
import KeyValueGrid from './KeyValueGrid.jsx';
import LoadingState from './LoadingState.jsx';
import Modal from './Modal.jsx';
import StatusBadge, { methodTone, statusTone } from './StatusBadge.jsx';

function Section({ title, copyValue, children, defaultOpen = true }) {
  const [open, setOpen] = useState(defaultOpen);
  return (
    <section className="detail-section">
      <div className="detail-section-header">
        <button type="button" className="section-toggle" onClick={() => setOpen((o) => !o)} aria-expanded={open}>
          {open ? <ChevronDown size={14} aria-hidden="true" /> : <ChevronRight size={14} aria-hidden="true" />}
          {title}
        </button>
        {copyValue !== undefined ? <CopyButton value={copyValue} /> : null}
      </div>
      {open ? <div className="detail-section-body">{children}</div> : null}
    </section>
  );
}

Section.propTypes = { title: PropTypes.string.isRequired, copyValue: PropTypes.string, children: PropTypes.node, defaultOpen: PropTypes.bool };

function prettyBody(body) {
  if (!body) return '';
  try {
    return JSON.stringify(JSON.parse(body), null, 2);
  } catch {
    return body;
  }
}

function HeaderTable({ headers }) {
  const { t } = useTranslation();
  const entries = Object.entries(headers || {});
  if (entries.length === 0) return <p className="muted">{t('requests.details.noHeaders')}</p>;
  return (
    <table className="kv-table">
      <tbody>
        {entries.map(([k, v]) => (
          <tr key={k}>
            <th scope="row" className="mono">
              {k}
            </th>
            <td className="mono">{v}</td>
          </tr>
        ))}
      </tbody>
    </table>
  );
}

HeaderTable.propTypes = { headers: PropTypes.object };

function Body({ body, truncated, bytes }) {
  const { t } = useTranslation();
  const f = useFormat();
  if (!body) return <p className="muted">{t('requests.details.noBody')}</p>;
  return (
    <>
      {truncated ? <div className="notice notice-warning">{t('requests.details.truncated', { size: f.bytes(bytes) })}</div> : null}
      <pre className="code-block">{prettyBody(body)}</pre>
    </>
  );
}

Body.propTypes = { body: PropTypes.string, truncated: PropTypes.bool, bytes: PropTypes.number };

export default function RequestDetailsModal({ entryId, onClose }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [entry, setEntry] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!entryId) return undefined;
    let cancelled = false;
    setEntry(null);
    setError(null);
    apiClient
      .getRequestHistoryEntry(entryId)
      .then((e) => !cancelled && setEntry(e))
      .catch((err) => !cancelled && setError(err));
    return () => {
      cancelled = true;
    };
  }, [entryId, apiClient]);

  return (
    <Modal open={Boolean(entryId)} title={t('requests.details.title')} subtitle={entryId ? <CopyableId value={entryId} /> : null} onClose={onClose} size="xlarge">
      {error ? <ErrorState error={error} /> : null}
      {!entry && !error ? <LoadingState /> : null}
      {entry ? (
        <>
          <Section title={t('requests.details.metadata')}>
            <KeyValueGrid
              rows={[
                { key: 'id', label: t('requests.columns.id'), value: <CopyableId value={entry.id} truncate={false} /> },
                { key: 'method', label: t('requests.columns.method'), value: <StatusBadge tone={methodTone(entry.method)} mono>{entry.method}</StatusBadge> },
                { key: 'path', label: t('requests.columns.path'), value: entry.path, mono: true },
                { key: 'route', label: t('requests.columns.route'), value: entry.routeTemplate, mono: true },
                { key: 'url', label: t('requests.columns.url'), value: entry.url, mono: true },
                { key: 'status', label: t('requests.columns.status'), value: <StatusBadge tone={statusTone(entry.statusCode)}>{String(entry.statusCode)}</StatusBadge> },
                { key: 'created', label: t('requests.columns.time'), value: f.dateTime(entry.createdUtc) },
                { key: 'completed', label: t('requests.columns.completed'), value: f.dateTime(entry.completedUtc) },
                { key: 'duration', label: t('requests.columns.duration'), value: f.duration(entry.durationMs) },
                { key: 'ip', label: t('requests.columns.sourceIp'), value: entry.sourceIp, mono: true },
                { key: 'principal', label: t('requests.columns.principal'), value: entry.principalName },
                { key: 'tenant', label: t('requests.columns.tenant'), value: entry.tenantId, mono: true }
              ]}
            />
          </Section>
          <Section title={t('requests.details.requestHeaders')} copyValue={JSON.stringify(entry.requestHeaders || {}, null, 2)}>
            <HeaderTable headers={entry.requestHeaders} />
          </Section>
          <Section title={t('requests.details.requestBody')} copyValue={entry.requestBody || ''}>
            <Body body={entry.requestBody} truncated={entry.requestBodyTruncated} bytes={entry.requestBodyBytes} />
          </Section>
          <Section title={t('requests.details.responseHeaders')} copyValue={JSON.stringify(entry.responseHeaders || {}, null, 2)}>
            <HeaderTable headers={entry.responseHeaders} />
          </Section>
          <Section title={t('requests.details.responseBody')} copyValue={entry.responseBody || ''}>
            <Body body={entry.responseBody} truncated={entry.responseBodyTruncated} bytes={entry.responseBodyBytes} />
          </Section>
          <Section title={t('requests.details.raw')} copyValue={JSON.stringify(entry, null, 2)} defaultOpen={false}>
            <pre className="code-block">{JSON.stringify(entry, null, 2)}</pre>
          </Section>
        </>
      ) : null}
    </Modal>
  );
}

RequestDetailsModal.propTypes = {
  entryId: PropTypes.string,
  onClose: PropTypes.func.isRequired
};
