// Connection snippets for the My Morning News MCP server (MCP_API.md). The snippets carry placeholders for the
// access key and secret; they never include a session token.
export const DEFAULT_MCP_URL = 'http://127.0.0.1:12105/mcp';
export const DEFAULT_API_PORT = '12100';
export const DEFAULT_MCP_PORT = '12105';
export const MCP_SERVER_NAME = 'mymorningnews';
export const ACCESS_KEY_PLACEHOLDER = '<access key>';
export const SECRET_KEY_PLACEHOLDER = '<secret key>';
export const MCP_URL_PLACEHOLDER = '<MCP server URL>';
export const SOURCE_PATH_PLACEHOLDER = '/path/to/MyMorningNews';

const LOOPBACK = ['127.0.0.1', '[::1]', 'localhost'];

// The Streamable HTTP URL to suggest for a REST API base URL when the server's client config publishes none (mcpUrl
// absent or null). A loopback API is a local install, whose MCP server listens on its own loopback port. For any other
// API the address is unknown: a loopback URL would point at the reader's own machine, so the answer is '' and the page
// asks for the address instead.
export function suggestMcpUrl(apiUrl) {
  try {
    const url = new URL(String(apiUrl || ''));
    if (LOOPBACK.includes(url.hostname)) return DEFAULT_MCP_URL;
    if (url.protocol === 'http:' || url.protocol === 'https:') return '';
  } catch {
    // Not a URL; use the default.
  }
  return DEFAULT_MCP_URL;
}

// Whether a value looks like an http or https URL.
export function isHttpUrl(value) {
  try {
    const url = new URL(String(value || ''));
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

// Every snippet for one MCP URL, REST API URL, and tenant: { docker, claudeHttp, claudeStdio, desktopJson }.
export function mcpSnippets({ mcpUrl = DEFAULT_MCP_URL, apiUrl = 'http://127.0.0.1:' + DEFAULT_API_PORT, tenantId = 'default' } = {}) {
  const tenant = tenantId || 'default';
  const api = String(apiUrl || '').replace(/\/+$/, '') || 'http://127.0.0.1:' + DEFAULT_API_PORT;
  const docker = 'cd docker && docker compose --profile mcp up -d';
  const claudeHttp = [
    'claude mcp add --transport http ' + MCP_SERVER_NAME + ' ' + mcpUrl + ' \\',
    '  --header "x-access-key: ' + ACCESS_KEY_PLACEHOLDER + '" \\',
    '  --header "x-secret-key: ' + SECRET_KEY_PLACEHOLDER + '" \\',
    '  --header "x-tenant-guid: ' + tenant + '"'
  ].join('\n');
  const claudeStdio = [
    'claude mcp add ' + MCP_SERVER_NAME + ' \\',
    '  --env MYMORNINGNEWS_BASE_URL=' + api + ' \\',
    '  --env MYMORNINGNEWS_ACCESS_KEY="' + ACCESS_KEY_PLACEHOLDER + '" \\',
    '  --env MYMORNINGNEWS_SECRET_KEY="' + SECRET_KEY_PLACEHOLDER + '" \\',
    '  --env MYMORNINGNEWS_TENANT_ID=' + tenant + ' \\',
    '  -- dotnet run --project ' + SOURCE_PATH_PLACEHOLDER + '/src/MyMorningNews.McpServer -f net10.0 -- --stdio'
  ].join('\n');
  const desktopJson = JSON.stringify(
    {
      mcpServers: {
        [MCP_SERVER_NAME]: {
          command: 'dotnet',
          args: ['run', '--project', SOURCE_PATH_PLACEHOLDER + '/src/MyMorningNews.McpServer', '-f', 'net10.0', '--', '--stdio'],
          env: {
            MYMORNINGNEWS_BASE_URL: api,
            MYMORNINGNEWS_ACCESS_KEY: ACCESS_KEY_PLACEHOLDER,
            MYMORNINGNEWS_SECRET_KEY: SECRET_KEY_PLACEHOLDER,
            MYMORNINGNEWS_TENANT_ID: tenant
          }
        }
      }
    },
    null,
    2
  );
  return { docker, claudeHttp, claudeStdio, desktopJson };
}
