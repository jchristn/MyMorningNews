import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';

const SERVER = { id: 'mcp_1', name: 'Company wiki', url: 'https://mcp.example.com/mcp', transport: 'StreamableHttp', enabled: true, toolCount: 2, lastDiscoveredUtc: '2026-11-20T08:00:00Z', headerName: 'Authorization', headerPrefix: 'Bearer ' };
const TOOLS = [
  { id: 'mct_1', mcpServerId: 'mcp_1', name: 'search_wiki', description: 'Search the wiki', enabled: true },
  { id: 'mct_2', mcpServerId: 'mcp_1', name: 'delete_page', description: 'Delete a page', enabled: false }
];

describe('MCP servers', () => {
  it('lists servers and switches a tool on', async () => {
    const setMcpToolEnabled = vi.fn(async () => ({ ...TOOLS[1], enabled: true }));
    renderRoute('/dashboard/mcp-servers', {
      listMcpServers: vi.fn(async () => page([SERVER])),
      getMcpServer: vi.fn(async () => ({ server: SERVER, tools: TOOLS })),
      setMcpToolEnabled
    });
    await userEvent.click(await screen.findByText('Company wiki'));
    const table = await screen.findByRole('table', { name: 'Tools' });
    const row = within(table).getByText('delete_page').closest('tr');
    await userEvent.click(within(row).getByRole('checkbox'));
    expect(setMcpToolEnabled).toHaveBeenCalledWith('mcp_1', 'mct_2', true);
  });

  it('registers a server and discovers its tools', async () => {
    const createMcpServer = vi.fn(async (body) => ({ ...SERVER, ...body, id: 'mcp_2' }));
    const discoverMcpServer = vi.fn(async () => ({ server: SERVER, tools: TOOLS }));
    renderRoute('/dashboard/mcp-servers', { listMcpServers: vi.fn(async () => page([])), createMcpServer, discoverMcpServer });
    await userEvent.click((await screen.findAllByRole('button', { name: /Add/ }))[0]);
    await userEvent.type(screen.getByLabelText('Name'), 'Docs');
    await userEvent.type(screen.getByLabelText('URL'), 'https://docs.example.com/mcp');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(discoverMcpServer).toHaveBeenCalledWith('mcp_2'));
    expect(createMcpServer).toHaveBeenCalledWith(expect.objectContaining({ name: 'Docs', url: 'https://docs.example.com/mcp', transport: 'StreamableHttp' }));
  });
});

describe('Account card', () => {
  const METER = {
    planKey: 'free',
    planName: 'Free',
    monthToDateCostUsd: 2.7,
    monthlyCostCapUsd: 3,
    costUsedFraction: 0.9,
    resetUtc: '2026-12-01T00:00:00Z',
    topics: 1,
    topicsMax: 1,
    runsToday: 0,
    runsDailyMax: 2,
    scheduleMinIntervalMinutes: 1440,
    audioMinutes: 4,
    audioMinutesMax: 10,
    audioEnabled: true,
    customEndpoints: false
  };

  it('shows usage against the plan and deletes the account after typing the email', async () => {
    const deleteAccount = vi.fn(async () => ({ purgeAfterUtc: '2026-11-27T00:00:00Z' }));
    const client = renderRoute('/dashboard/profile', {
      getMe: vi.fn(async () => ({ id: 'usr_admin', email: 'admin@mymorningnews', firstName: 'Admin' })),
      getAccountUsage: vi.fn(async () => METER),
      deleteAccount
    });
    expect(await screen.findByText('Research budget this month')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Research budget this month: 90%' })).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Topics: 100%' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Delete my account' }));
    const dialog = await screen.findByRole('dialog');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Delete my account' }));
    expect(await within(dialog).findByText('The email does not match your account.')).toBeInTheDocument();
    expect(deleteAccount).not.toHaveBeenCalled();
    await userEvent.type(within(dialog).getByLabelText('Type your email to confirm'), 'admin@mymorningnews');
    await userEvent.click(within(dialog).getByRole('button', { name: 'Delete my account' }));
    await waitFor(() => expect(deleteAccount).toHaveBeenCalled());
    // The server already ended every session: the account-deleted page shows the date, and no sign-out request is sent
    // (it would fail and read as an expired session).
    const signedOut = await screen.findByTestId('account-deleted');
    expect(within(signedOut).getByText(/Your account will be deleted on/)).toBeInTheDocument();
    expect(within(signedOut).getByRole('link', { name: 'Sign in to keep my account' })).toHaveAttribute('href', '/');
    expect(screen.queryByText(/Your session expired/)).toBeNull();
    expect(client.logout).not.toHaveBeenCalled();
  });
});
