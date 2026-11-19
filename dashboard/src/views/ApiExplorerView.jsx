import { useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Play, Plus, Square, Trash2, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import useApiExplorer from '../hooks/useApiExplorer.js';
import useFormat from '../hooks/useFormat.js';
import { buildCodeSnippets, pathParameterNames } from '../utils/openApi.js';
import ConfirmModal from '../components/ConfirmModal.jsx';
import CopyButton from '../components/CopyButton.jsx';
import EmptyState from '../components/EmptyState.jsx';
import ErrorState from '../components/ErrorState.jsx';
import JsonTree from '../components/JsonTree.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge, { methodTone, statusTone } from '../components/StatusBadge.jsx';

function RowsEditor({ rows, onChange, keyLabel, valueLabel }) {
  const { t } = useTranslation();
  function set(index, field, value) {
    onChange(rows.map((r, i) => (i === index ? { ...r, [field]: value } : r)));
  }
  return (
    <div className="rows-editor">
      {rows.map((r, i) => (
        <div className="rows-editor-row" key={i}>
          <input value={r.key} onChange={(e) => set(i, 'key', e.target.value)} placeholder={keyLabel} aria-label={keyLabel} className="mono" />
          <input value={r.value} onChange={(e) => set(i, 'value', e.target.value)} placeholder={valueLabel} aria-label={valueLabel} className="mono" />
          <button type="button" className="icon-button" onClick={() => onChange(rows.length > 1 ? rows.filter((_, j) => j !== i) : [{ key: '', value: '' }])} aria-label={t('common.remove')} title={t('common.remove')}>
            <X size={14} aria-hidden="true" />
          </button>
        </div>
      ))}
      <button type="button" className="button secondary small" onClick={() => onChange([...rows, { key: '', value: '' }])}>
        <Plus size={14} aria-hidden="true" /> {t('common.add')}
      </button>
    </div>
  );
}

RowsEditor.propTypes = { rows: PropTypes.array.isRequired, onChange: PropTypes.func.isRequired, keyLabel: PropTypes.string.isRequired, valueLabel: PropTypes.string.isRequired };

function isDangerous(op) {
  return op && (op.method === 'DELETE' || op.path.includes('/bulk') || op.path.includes('/restart'));
}

export default function ApiExplorerView() {
  const { t } = useTranslation();
  const f = useFormat();
  const ex = useApiExplorer();
  const [search, setSearch] = useState('');
  const [tab, setTab] = useState('preview');
  const [snippet, setSnippet] = useState('curl');
  const [confirming, setConfirming] = useState(false);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    const ops = q ? ex.operations.filter((o) => (o.method + ' ' + o.path + ' ' + (o.operationId || '') + ' ' + o.summary).toLowerCase().includes(q)) : ex.operations;
    const groups = new Map();
    for (const o of ops) {
      if (!groups.has(o.tag)) groups.set(o.tag, []);
      groups.get(o.tag).push(o);
    }
    return Array.from(groups.entries());
  }, [ex.operations, search]);

  const snippets = ex.operation
    ? buildCodeSnippets({
        method: ex.operation.method,
        url: ex.resolvedUrl,
        headers: { Authorization: 'Bearer <token>', 'Content-Type': ex.body ? 'application/json' : undefined, ...ex.headerObject },
        body: ex.body
      })
    : null;

  let parsed = null;
  if (ex.response && ex.response.body) {
    try {
      parsed = JSON.parse(ex.response.body);
    } catch {
      parsed = null;
    }
  }

  function run() {
    if (isDangerous(ex.operation)) setConfirming(true);
    else ex.execute();
  }

  if (ex.specError) {
    return (
      <div className="page">
        <PageHeader title={t('nav.items.explorer')} summary={t('routes.explorer.summary')} />
        <ErrorState error={ex.specError} onRetry={ex.loadSpec} />
        <p className="muted">{t('explorer.specMissing')}</p>
      </div>
    );
  }

  return (
    <div className="page page-wide">
      <PageHeader title={t('nav.items.explorer')} summary={t('routes.explorer.summary')} />
      {!ex.spec ? <LoadingState /> : null}
      {ex.spec ? (
        <div className="explorer">
          <aside className="explorer-ops panel">
            <input type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('explorer.searchPlaceholder')} aria-label={t('explorer.search')} />
            <div className="explorer-op-list">
              {filtered.map(([tag, ops]) => (
                <div key={tag} className="explorer-group">
                  <div className="explorer-group-label">{tag}</div>
                  {ops.map((o) => (
                    <button type="button" key={o.id} className={'explorer-op' + (ex.operation && ex.operation.id === o.id ? ' active' : '')} onClick={() => ex.selectOperation(o)} title={o.summary}>
                      <StatusBadge tone={methodTone(o.method)} mono>
                        {o.method}
                      </StatusBadge>
                      <span className="mono truncate">{o.path}</span>
                    </button>
                  ))}
                </div>
              ))}
              {filtered.length === 0 ? <EmptyState title={t('explorer.noOperations')} /> : null}
            </div>
            {ex.history.length > 0 ? (
              <details className="explorer-history">
                <summary>{t('explorer.history', { count: ex.history.length })}</summary>
                {ex.history.map((h) => (
                  <div className="explorer-history-row" key={h.id}>
                    <button type="button" className="link-button" onClick={() => ex.loadHistory(h)}>
                      <StatusBadge tone={methodTone(h.method)} mono>
                        {h.method}
                      </StatusBadge>{' '}
                      <span className="mono truncate">{h.path}</span>
                    </button>
                    <StatusBadge tone={statusTone(h.status)}>{String(h.status)}</StatusBadge>
                    <button type="button" className="icon-button" onClick={() => ex.deleteHistory(h.id)} aria-label={t('common.delete')} title={t('common.delete')}>
                      <Trash2 size={12} aria-hidden="true" />
                    </button>
                  </div>
                ))}
              </details>
            ) : null}
          </aside>
          <section className="explorer-main">
            {!ex.operation ? (
              <section className="panel">
                <EmptyState title={t('explorer.pickOperation')} message={t('explorer.pickOperationMessage')} />
              </section>
            ) : (
              <>
                <section className="panel">
                  <div className="explorer-title">
                    <StatusBadge tone={methodTone(ex.operation.method)} mono>
                      {ex.operation.method}
                    </StatusBadge>
                    <code className="mono">{ex.operation.path}</code>
                  </div>
                  {ex.operation.summary ? <p className="muted">{ex.operation.summary}</p> : null}
                  <div className="resolved-url">
                    <span className="field-label">{t('explorer.url')}</span>
                    <code className="mono">{ex.resolvedUrl}</code>
                    <CopyButton value={ex.resolvedUrl} labelKey="common.copyUrl" />
                  </div>
                  {pathParameterNames(ex.operation.path).length > 0 ? (
                    <fieldset>
                      <legend>{t('explorer.pathParams')}</legend>
                      <div className="form-grid">
                        {pathParameterNames(ex.operation.path).map((name) => (
                          <label key={name} className="form-field">
                            <span className="field-label">
                              {name} <span className="required" aria-hidden="true">*</span>
                            </span>
                            <input className="mono" value={ex.pathParams[name] ?? ''} onChange={(e) => ex.setPathParams({ ...ex.pathParams, [name]: e.target.value })} required aria-required="true" />
                          </label>
                        ))}
                      </div>
                    </fieldset>
                  ) : null}
                  <fieldset>
                    <legend>{t('explorer.queryParams')}</legend>
                    <RowsEditor rows={ex.queryParams} onChange={ex.setQueryParams} keyLabel={t('explorer.name')} valueLabel={t('explorer.value')} />
                  </fieldset>
                  <fieldset>
                    <legend>{t('explorer.headers')}</legend>
                    <p className="muted small">{t('explorer.authInherited')}</p>
                    <RowsEditor rows={ex.headers} onChange={ex.setHeaders} keyLabel={t('explorer.name')} valueLabel={t('explorer.value')} />
                  </fieldset>
                  {ex.operation.method !== 'GET' && ex.operation.method !== 'HEAD' ? (
                    <fieldset>
                      <legend>{t('explorer.body')}</legend>
                      <textarea className="code-editor" rows={10} value={ex.body} onChange={(e) => ex.setBody(e.target.value)} spellCheck={false} aria-label={t('explorer.body')} />
                      <div className="editor-actions">
                        {ex.bodyError ? <span className="field-error">{t('explorer.invalidJson', { message: ex.bodyError })}</span> : <span className="muted small">{t('explorer.validJson')}</span>}
                        <button
                          type="button"
                          className="button secondary small"
                          disabled={Boolean(ex.bodyError) || !ex.body}
                          onClick={() => ex.setBody(JSON.stringify(JSON.parse(ex.body), null, 2))}
                        >
                          {t('explorer.format')}
                        </button>
                        <CopyButton value={ex.body} />
                      </div>
                    </fieldset>
                  ) : null}
                  <div className="form-actions">
                    {ex.running ? (
                      <button type="button" className="button secondary" onClick={ex.cancel}>
                        <Square size={14} aria-hidden="true" /> {t('explorer.cancel')}
                      </button>
                    ) : null}
                    <button type="button" className={isDangerous(ex.operation) ? 'button danger' : 'button primary'} onClick={run} disabled={ex.running || Boolean(ex.bodyError)}>
                      <Play size={14} aria-hidden="true" /> {ex.running ? t('explorer.running') : t('explorer.execute')}
                    </button>
                  </div>
                </section>
                <section className="panel">
                  <div className="tabs" role="tablist">
                    {['preview', 'body', 'headers', 'status', 'code'].map((id) => (
                      <button type="button" role="tab" key={id} aria-selected={tab === id} className={tab === id ? 'active' : ''} onClick={() => setTab(id)}>
                        {t('explorer.tabs.' + id)}
                      </button>
                    ))}
                  </div>
                  {tab === 'code' && snippets ? (
                    <div>
                      <div className="segmented">
                        {['curl', 'fetch', 'csharp'].map((s) => (
                          <button type="button" key={s} className={snippet === s ? 'active' : ''} onClick={() => setSnippet(s)} aria-pressed={snippet === s}>
                            {t('explorer.snippets.' + s)}
                          </button>
                        ))}
                        <CopyButton value={snippets[snippet]} />
                      </div>
                      <pre className="code-block">{snippets[snippet]}</pre>
                    </div>
                  ) : !ex.response ? (
                    <EmptyState title={t('explorer.noResponse')} message={t('explorer.noResponseMessage')} />
                  ) : (
                    <>
                      <div className="response-meta">
                        <StatusBadge tone={ex.response.error ? 'danger' : statusTone(ex.response.status)}>{String(ex.response.status) + ' ' + (ex.response.statusText || '')}</StatusBadge>
                        <span className="muted">{f.duration(ex.response.durationMs)}</span>
                        {ex.response.bytes !== undefined ? <span className="muted">{f.bytes(ex.response.bytes)}</span> : null}
                        {ex.response.streaming ? <span className="badge badge-info">{t('explorer.streaming')}</span> : null}
                        <CopyButton value={ex.response.body} labelKey="explorer.copyResponse" />
                      </div>
                      {tab === 'preview' ? parsed !== null ? <JsonTree value={parsed} /> : <pre className="code-block">{ex.response.body || t('explorer.emptyBody')}</pre> : null}
                      {tab === 'body' ? <pre className="code-block">{parsed !== null ? JSON.stringify(parsed, null, 2) : ex.response.body}</pre> : null}
                      {tab === 'headers' ? (
                        <table className="kv-table">
                          <tbody>
                            {Object.entries(ex.response.headers).map(([k, v]) => (
                              <tr key={k}>
                                <th scope="row" className="mono">
                                  {k}
                                </th>
                                <td className="mono">{v}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      ) : null}
                      {tab === 'status' ? (
                        <dl className="kv-grid">
                          <div className="kv-row">
                            <dt>{t('explorer.status')}</dt>
                            <dd>{String(ex.response.status)}</dd>
                          </div>
                          <div className="kv-row">
                            <dt>{t('explorer.duration')}</dt>
                            <dd>{f.duration(ex.response.durationMs)}</dd>
                          </div>
                        </dl>
                      ) : null}
                    </>
                  )}
                </section>
              </>
            )}
          </section>
        </div>
      ) : null}
      <ConfirmModal
        open={confirming}
        title={t('explorer.confirmTitle')}
        message={ex.operation ? t('explorer.confirmMessage', { method: ex.operation.method, url: ex.resolvedUrl }) : ''}
        confirmLabel={t('explorer.execute')}
        danger
        onClose={() => setConfirming(false)}
        onConfirm={() => ex.execute()}
      />
    </div>
  );
}
