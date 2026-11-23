import { describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MEMBER_DETAILS } from '../test/fakeClient.js';
import { forbidden, page, renderRoute } from '../test/renderRoute.jsx';
import { RevealModal, REVEAL_SECONDS, vaultUpdateBody } from './VaultView.jsx';

const CREDENTIAL = {
  id: 'vlt_1',
  tenantId: 'default',
  name: 'qa-anthropic',
  kind: 'ApiKey',
  provider: 'Anthropic',
  secretPreview: '****abcd',
  active: true,
  createdUtc: '2026-11-19T10:00:00Z',
  lastUpdateUtc: '2026-11-19T10:00:00Z'
};

async function openActions(name) {
  const row = (await screen.findByText(name)).closest('tr');
  await userEvent.click(within(row).getByRole('button', { name: 'Actions' }));
}

describe('VaultView', () => {
  it('lists stored credentials with the secret preview only', async () => {
    const client = renderRoute('/dashboard/credentials-vault', { listVaultCredentials: vi.fn(async () => page([CREDENTIAL])) });
    expect(await screen.findByText('qa-anthropic')).toBeInTheDocument();
    expect(screen.getByText('****abcd')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Stored credentials' })).toBeInTheDocument();
    expect(client.listVaultCredentials).toHaveBeenCalled();
  });

  it('shows the empty state with an add action', async () => {
    renderRoute('/dashboard/credentials-vault', { listVaultCredentials: vi.fn(async () => page([])) });
    expect(await screen.findByText('No stored credentials')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /Add/ }).length).toBeGreaterThan(0);
  });

  it('confirms, reveals, and shows the secret after an audited reveal', async () => {
    const client = renderRoute('/dashboard/credentials-vault', {
      listVaultCredentials: vi.fn(async () => page([CREDENTIAL])),
      revealVaultCredential: vi.fn(async () => ({ id: 'vlt_1', name: 'qa-anthropic', secret: 'sk-secret-abcd', revealedUtc: '2026-11-19T10:00:00Z' }))
    });
    await openActions('qa-anthropic');
    await userEvent.click(screen.getByRole('menuitem', { name: 'Reveal' }));
    expect(screen.getByText(/Reveals are audited/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Reveal' }));
    expect(await screen.findByText('sk-secret-abcd')).toBeInTheDocument();
    expect(client.revealVaultCredential).toHaveBeenCalledWith('vlt_1');
    await userEvent.click(screen.getByRole('button', { name: 'Hide now' }));
    await waitFor(() => expect(screen.queryByText('sk-secret-abcd')).not.toBeInTheDocument());
  });

  it('shows a clear error when the server forbids the reveal', async () => {
    renderRoute('/dashboard/credentials-vault', { listVaultCredentials: vi.fn(async () => page([CREDENTIAL])), revealVaultCredential: vi.fn(forbidden) });
    await openActions('qa-anthropic');
    await userEvent.click(screen.getByRole('menuitem', { name: 'Reveal' }));
    await userEvent.click(screen.getByRole('button', { name: 'Reveal' }));
    expect(await screen.findByText('You do not have permission to do that.')).toBeInTheDocument();
  });

  it('lets members list but disables reveal and changes', async () => {
    renderRoute('/dashboard/credentials-vault', { getTokenDetails: async () => MEMBER_DETAILS, listVaultCredentials: vi.fn(async () => page([CREDENTIAL])) });
    expect(await screen.findByText(/requires a tenant administrator/)).toBeInTheDocument();
    await openActions('qa-anthropic');
    expect(screen.getByRole('menuitem', { name: 'Reveal (creator and administrators only)' })).toBeDisabled();
    expect(screen.getByRole('menuitem', { name: 'Delete' })).toBeDisabled();
    expect(screen.getByRole('menuitem', { name: 'Where used' })).toBeEnabled();
  });

  it('lets a member reveal a credential they created, and nothing else', async () => {
    const client = renderRoute('/dashboard/credentials-vault', {
      getTokenDetails: async () => MEMBER_DETAILS,
      listVaultCredentials: vi.fn(async () => page([{ ...CREDENTIAL, name: 'my-key', createdByUserId: MEMBER_DETAILS.userId }])),
      revealVaultCredential: vi.fn(async () => ({ id: 'vlt_1', name: 'my-key', secret: 'sk-mine-1234' }))
    });
    await openActions('my-key');
    expect(screen.getByRole('menuitem', { name: 'Delete' })).toBeDisabled();
    await userEvent.click(screen.getByRole('menuitem', { name: 'Reveal' }));
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Reveal' }));
    expect(await screen.findByText('sk-mine-1234')).toBeInTheDocument();
    expect(client.revealVaultCredential).toHaveBeenCalledWith('vlt_1');
  });
});

describe('RevealModal', () => {
  it('hides the secret automatically after the countdown', () => {
    vi.useFakeTimers();
    try {
      const onClose = vi.fn();
      render(<RevealModal revealed={{ id: 'vlt_1', name: 'k', secret: 'shh' }} onClose={onClose} />);
      expect(screen.getByText('shh')).toBeInTheDocument();
      expect(screen.getByText('Hides in 30 seconds')).toBeInTheDocument();
      act(() => {
        vi.advanceTimersByTime((REVEAL_SECONDS - 1) * 1000);
      });
      expect(onClose).not.toHaveBeenCalled();
      act(() => {
        vi.advanceTimersByTime(1500);
      });
      expect(onClose).toHaveBeenCalledTimes(1);
    } finally {
      vi.useRealTimers();
    }
  });
});

describe('vault update body', () => {
  const original = { ...CREDENTIAL, kind: 'CustomHeader', headerName: 'X-Key', description: 'old', expiresUtc: '2027-01-01T00:00:00.000Z' };
  const form = { name: ' qa-anthropic ', kind: 'CustomHeader', provider: 'Anthropic', headerName: 'X-Key', username: '', secret: '', description: 'old', expiresUtc: '2027-01-01T00:00:00.000Z', active: true };

  it('sends values that are set and clears nothing when nothing was emptied', () => {
    expect(vaultUpdateBody(form, original)).toEqual({ name: 'qa-anthropic', kind: 'CustomHeader', provider: 'Anthropic', active: true, headerName: 'X-Key', description: 'old', expiresUtc: '2027-01-01T00:00:00.000Z' });
  });

  it('names emptied values, and values the new kind no longer uses, in clearFields', () => {
    const body = vaultUpdateBody({ ...form, kind: 'ApiKey', description: '  ', expiresUtc: '' }, original);
    expect(body.clearFields).toEqual(['headerName', 'description', 'expiresUtc']);
    expect(body).not.toHaveProperty('expiresUtc');
    expect(body).not.toHaveProperty('headerName');
  });

  it('does not clear a value the credential never had', () => {
    expect(vaultUpdateBody({ ...form, expiresUtc: '', description: '' }, { ...original, expiresUtc: null, description: null }).clearFields).toBeUndefined();
  });
});
