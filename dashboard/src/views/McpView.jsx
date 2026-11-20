import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { KeyRound, RotateCcw, ShieldAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useLocalStorage from '../hooks/useLocalStorage.js';
import { isHttpUrl, mcpSnippets, suggestMcpUrl } from '../utils/mcp.js';
import CopyButton from '../components/CopyButton.jsx';
import FormField from '../components/FormField.jsx';
import PageHeader from '../components/PageHeader.jsx';

function Snippet({ id, title, help = null, value }) {
  return (
    <section className="detail-section" data-testid={'mcp-' + id}>
      <div className="detail-section-header">
        <strong>{title}</strong>
        <CopyButton value={value} />
      </div>
      <div className="detail-section-body stack">
        {help ? <p className="muted small">{help}</p> : null}
        <pre className="code-block">{value}</pre>
      </div>
    </section>
  );
}

Snippet.propTypes = { id: PropTypes.string.isRequired, title: PropTypes.string.isRequired, help: PropTypes.string, value: PropTypes.string.isRequired };

// How to connect Claude Code or another MCP client to the My Morning News MCP server: the Streamable HTTP URL (kept
// per browser), an access key to create, and copyable commands. The page never shows the session token.
export default function McpView() {
  const { t } = useTranslation();
  const { serverUrl, tenantId, isTenantAdmin, apiClient } = useAuth();
  const [advertised, setAdvertised] = useState('');
  const suggested = isHttpUrl(advertised) ? advertised : suggestMcpUrl(serverUrl);
  const [stored, setStored] = useLocalStorage('mymorningnews.mcp.url', '');
  const [edited, setMcpUrl] = useState(null);
  const mcpUrl = edited ?? (stored || suggested);
  // The server advertises its MCP endpoint in client config (Rest.McpPublicUrl); older servers do not.
  useEffect(() => {
    let live = true;
    if (!apiClient) return undefined;
    apiClient
      .get('/v1.0/api/client-config')
      .then((config) => {
        if (live && config && typeof config.mcpUrl === 'string') setAdvertised(config.mcpUrl);
      })
      .catch(() => {});
    return () => {
      live = false;
    };
  }, [apiClient]);
  const change = (value) => {
    setMcpUrl(value);
    setStored(value === suggested ? '' : value);
  };
  const valid = isHttpUrl(mcpUrl);
  const snippets = mcpSnippets({ mcpUrl: valid ? mcpUrl : suggested, apiUrl: serverUrl, tenantId });
  const prompts = ['topics', 'briefing', 'review'];

  return (
    <div className="page">
      <PageHeader title={t('nav.items.mcp')} summary={t('routes.mcp.summary')} />
      <div className="notice notice-warning" data-testid="mcp-token-warning">
        <ShieldAlert size={14} aria-hidden="true" /> {t('mcp.tokenWarning')}
      </div>
      <section className="panel stack">
        <h2>{t('mcp.about.title')}</h2>
        <p>{t('mcp.about.body')}</p>
        <p className="muted small">{t('mcp.about.privileges')}</p>
        <FormField id="mcp-url" label={t('mcp.url.label')} help={valid ? t('mcp.url.help') : undefined} error={valid ? undefined : t('mcp.url.invalid')}>
          <span className="service-url">
            <input id="mcp-url" className="mono" value={mcpUrl} onChange={(e) => change(e.target.value)} spellCheck={false} autoComplete="off" />
            <CopyButton value={mcpUrl} labelKey="common.copyUrl" />
            {mcpUrl !== suggested ? (
              <button type="button" className="icon-button" onClick={() => change(suggested)} aria-label={t('mcp.url.reset')} title={t('mcp.url.reset')}>
                <RotateCcw size={14} aria-hidden="true" />
              </button>
            ) : null}
          </span>
        </FormField>
      </section>
      <section className="panel stack" data-testid="mcp-access-key">
        <h2>
          <KeyRound size={16} aria-hidden="true" /> {t('mcp.key.title')}
        </h2>
        <p>{t('mcp.key.body')}</p>
        <p className="muted small">{t('mcp.key.authMode')}</p>
        {isTenantAdmin ? (
          <div className="row-actions">
            <Link className="button primary small" to="/dashboard/admin/credentials">
              {t('mcp.key.create')}
            </Link>
          </div>
        ) : (
          <p className="muted small">{t('mcp.key.askAdmin')}</p>
        )}
      </section>
      <section className="panel stack">
        <h2>{t('mcp.connect.title')}</h2>
        <Snippet id="http" title={t('mcp.connect.claudeHttp')} help={t('mcp.connect.claudeHttpHelp')} value={snippets.claudeHttp} />
        <Snippet id="stdio" title={t('mcp.connect.claudeStdio')} help={t('mcp.connect.claudeStdioHelp')} value={snippets.claudeStdio} />
        <Snippet id="json" title={t('mcp.connect.desktop')} help={t('mcp.connect.desktopHelp')} value={snippets.desktopJson} />
        <Snippet id="docker" title={t('mcp.connect.run')} help={t('mcp.connect.runHelp')} value={snippets.docker} />
      </section>
      <section className="panel stack">
        <h2>{t('mcp.try.title')}</h2>
        <ul className="cta-list">
          {prompts.map((key) => (
            <li key={key}>{t('mcp.try.' + key)}</li>
          ))}
        </ul>
        <p className="muted small">{t('mcp.try.safety')}</p>
      </section>
    </div>
  );
}
