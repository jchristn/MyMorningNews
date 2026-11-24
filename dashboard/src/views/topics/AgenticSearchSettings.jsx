import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';

// Agentic search for one topic (opt-in, decision Q19): the research model searches and reads pages itself, and may
// call the MCP tools allowed here. Only tools switched on for their server can be allowed.
export default function AgenticSearchSettings({ topic, onTopicUpdated }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [enabled, setEnabled] = useState(Boolean(topic.agenticSearch));
  const [allowed, setAllowed] = useState(() => new Set(topic.mcpToolIds || []));
  const [tools, setTools] = useState(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    setEnabled(Boolean(topic.agenticSearch));
    setAllowed(new Set(topic.mcpToolIds || []));
  }, [topic]);

  useEffect(() => {
    let live = true;
    apiClient
      .listMcpServers({ pageSize: 100 })
      .then(async (page) => {
        const servers = (page && Array.isArray(page.objects) ? page.objects : []).filter((s) => s.enabled);
        const details = await Promise.all(servers.map((s) => apiClient.getMcpServer(s.id).catch(() => null)));
        const list = [];
        details.forEach((d) => {
          if (!d || !d.server || !Array.isArray(d.tools)) return;
          d.tools.filter((x) => x.enabled).forEach((x) => list.push({ id: x.id, name: x.name, description: x.description, server: d.server.name }));
        });
        if (live) setTools(list);
      })
      .catch(() => {
        if (live) setTools([]);
      });
    return () => {
      live = false;
    };
  }, [apiClient]);

  function toggle(id) {
    setAllowed((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  async function save() {
    setSaving(true);
    setError(null);
    try {
      onTopicUpdated(await apiClient.updateTopic(topic.id, { agenticSearch: enabled, mcpToolIds: [...allowed] }));
      addNotification(t('admin.saved'), 'success');
    } catch (err) {
      setError(err);
    } finally {
      setSaving(false);
    }
  }

  return (
    <section className="panel" aria-labelledby="agentic-heading">
      <h3 id="agentic-heading">{t('topics.agentic.title')}</h3>
      <p className="muted small">{t('topics.agentic.help')}</p>
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <label className="auth-check" htmlFor="agentic-enabled">
        <input id="agentic-enabled" type="checkbox" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />
        <span>{t('topics.agentic.enable')}</span>
      </label>
      {enabled ? (
        <div className="stack">
          <strong className="small">{t('topics.agentic.tools')}</strong>
          {tools === null ? <p className="muted small">{t('common.loading')}</p> : null}
          {tools && tools.length === 0 ? (
            <p className="muted small">
              {t('topics.agentic.noTools')} <Link to="/dashboard/mcp-servers">{t('nav.items.mcpServers')}</Link>
            </p>
          ) : null}
          {tools && tools.length > 0
            ? tools.map((x) => (
                <label key={x.id} className="auth-check small" htmlFor={'agentic-tool-' + x.id}>
                  <input id={'agentic-tool-' + x.id} type="checkbox" checked={allowed.has(x.id)} onChange={() => toggle(x.id)} />
                  <span>
                    <span className="mono">{x.server + ': ' + x.name}</span>
                    {x.description ? <span className="muted"> {x.description}</span> : null}
                  </span>
                </label>
              ))
            : null}
        </div>
      ) : null}
      <div className="form-actions">
        <button type="button" className="button primary" onClick={save} disabled={saving}>
          {saving ? t('common.saving') : t('common.save')}
        </button>
      </div>
    </section>
  );
}

AgenticSearchSettings.propTypes = { topic: PropTypes.object.isRequired, onTopicUpdated: PropTypes.func.isRequired };
