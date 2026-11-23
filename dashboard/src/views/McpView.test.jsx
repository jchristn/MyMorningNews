import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MEMBER_DETAILS } from '../test/fakeClient.js';
import { renderRoute } from '../test/renderRoute.jsx';
import { DEFAULT_MCP_URL, MCP_URL_PLACEHOLDER, isHttpUrl, mcpSnippets, suggestMcpUrl } from '../utils/mcp.js';

describe('MCP snippets', () => {
  it('suggests the default Streamable HTTP URL for a loopback API and otherwise', () => {
    expect(DEFAULT_MCP_URL).toBe('http://127.0.0.1:12105/mcp');
    expect(suggestMcpUrl('http://127.0.0.1:12100')).toBe('http://127.0.0.1:12105/mcp');
    expect(suggestMcpUrl('http://localhost:8000')).toBe(DEFAULT_MCP_URL);
    // A public API with no advertised MCP address: unknown, never a loopback guess.
    expect(suggestMcpUrl('https://api.example.test')).toBe('');
    expect(suggestMcpUrl('not a url')).toBe(DEFAULT_MCP_URL);
    expect(isHttpUrl('https://mcp.example.test/mcp')).toBe(true);
    expect(isHttpUrl('ftp://x')).toBe(false);
    expect(isHttpUrl('')).toBe(false);
  });

  it('builds Claude Code and JSON snippets with access key placeholders and the tenant', () => {
    const s = mcpSnippets({ mcpUrl: 'http://127.0.0.1:12105/mcp', apiUrl: 'http://127.0.0.1:12100/', tenantId: 'ten_1' });
    expect(s.claudeHttp).toContain('claude mcp add --transport http mymorningnews http://127.0.0.1:12105/mcp');
    expect(s.claudeHttp).toContain('--header "x-access-key: <access key>"');
    expect(s.claudeHttp).toContain('--header "x-secret-key: <secret key>"');
    expect(s.claudeHttp).toContain('--header "x-tenant-guid: ten_1"');
    expect(s.claudeStdio).toContain('--env MYMORNINGNEWS_BASE_URL=http://127.0.0.1:12100 ');
    expect(s.claudeStdio).toContain('--env MYMORNINGNEWS_TENANT_ID=ten_1');
    expect(s.claudeStdio).toContain('src/MyMorningNews.McpServer -f net10.0 -- --stdio');
    const json = JSON.parse(s.desktopJson);
    expect(json.mcpServers.mymorningnews.env).toEqual({ MYMORNINGNEWS_BASE_URL: 'http://127.0.0.1:12100', MYMORNINGNEWS_ACCESS_KEY: '<access key>', MYMORNINGNEWS_SECRET_KEY: '<secret key>', MYMORNINGNEWS_TENANT_ID: 'ten_1' });
    expect(s.docker).toBe('cd docker && docker compose --profile mcp up -d');
    for (const text of Object.values(s)) expect(text).not.toContain('Bearer');
  });
});

describe('McpView', () => {
  it('shows the URL, copyable commands, and a link to create an access key, never the session token', async () => {
    renderRoute('/dashboard/mcp');
    expect(await screen.findByRole('heading', { name: 'MCP client', level: 1 })).toBeInTheDocument();
    expect(screen.getByLabelText('Streamable HTTP URL')).toHaveValue('http://127.0.0.1:12105/mcp');
    expect(screen.getByTestId('mcp-token-warning')).toHaveTextContent('Never paste your session token');
    const http = screen.getByTestId('mcp-http');
    expect(http).toHaveTextContent('claude mcp add --transport http mymorningnews http://127.0.0.1:12105/mcp');
    expect(http).toHaveTextContent('x-tenant-guid: default');
    expect(within(http).getByRole('button', { name: 'Copy' })).toBeInTheDocument();
    expect(screen.getByTestId('mcp-json')).toHaveTextContent('"mcpServers"');
    expect(screen.getByRole('link', { name: 'Create an access key' })).toHaveAttribute('href', '/dashboard/admin/credentials');
    expect(document.body).not.toHaveTextContent('tok_1');
  });

  it('follows an edited URL in the commands, flags an invalid one, and resets', async () => {
    renderRoute('/dashboard/mcp');
    const input = await screen.findByLabelText('Streamable HTTP URL');
    await userEvent.clear(input);
    await userEvent.type(input, 'https://mcp.example.test/mcp');
    await waitFor(() => expect(screen.getByTestId('mcp-http')).toHaveTextContent('https://mcp.example.test/mcp'));
    await userEvent.clear(input);
    await userEvent.type(input, 'nope');
    expect(screen.getByRole('alert')).toHaveTextContent('Enter an http or https URL.');
    expect(screen.getByTestId('mcp-http')).toHaveTextContent('http://127.0.0.1:12105/mcp');
    await userEvent.click(screen.getByRole('button', { name: 'Reset to the suggested URL' }));
    expect(screen.getByLabelText('Streamable HTTP URL')).toHaveValue('http://127.0.0.1:12105/mcp');
  });

  it('uses the MCP address the server publishes in client config', async () => {
    renderRoute('/dashboard/mcp', { get: vi.fn(async () => ({ mcpUrl: 'https://app.example.test/mcp' })) });
    await waitFor(() => expect(screen.getByLabelText('Streamable HTTP URL')).toHaveValue('https://app.example.test/mcp'));
    expect(screen.getByTestId('mcp-http')).toHaveTextContent('claude mcp add --transport http mymorningnews https://app.example.test/mcp');
  });

  it('asks for the address, with no loopback guess, when a public server publishes none', async () => {
    window.localStorage.setItem('mymorningnews.serverUrl', 'https://app.example.test');
    try {
      renderRoute('/dashboard/mcp', { get: vi.fn(async () => ({ productName: 'My Morning News', mcpUrl: null })) });
      const input = await screen.findByLabelText('Streamable HTTP URL');
      expect(input).toHaveValue('');
      expect(screen.getByRole('alert')).toHaveTextContent('Enter an http or https URL.');
      expect(screen.getByTestId('mcp-http')).toHaveTextContent('claude mcp add --transport http mymorningnews ' + MCP_URL_PLACEHOLDER);
      expect(document.body).not.toHaveTextContent('127.0.0.1:12105');
    } finally {
      window.localStorage.removeItem('mymorningnews.serverUrl');
    }
  });

  it('tells members to ask an administrator for an access key', async () => {
    renderRoute('/dashboard/mcp', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS) });
    const section = await screen.findByTestId('mcp-access-key');
    await waitFor(() => expect(within(section).getByText(/Ask yours for an access key/)).toBeInTheDocument());
    expect(within(section).queryByRole('link', { name: 'Create an access key' })).toBeNull();
  });
});
