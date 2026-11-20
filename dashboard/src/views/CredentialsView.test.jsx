import { describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { page, renderRoute } from '../test/renderRoute.jsx';

const CREDENTIAL = {
  id: 'crd_1',
  tenantId: 'default',
  userId: 'usr_admin',
  name: 'Signed script',
  accessKey: 'ak_123',
  secretKeyPreview: 'secret_****abcd',
  authMode: 'SignedRequest',
  scopes: [],
  resourceIds: [],
  isProtected: false,
  active: true,
  createdUtc: '2026-11-18T10:00:00Z'
};

describe('CredentialsView auth mode', () => {
  it('shows each credential auth mode in the table', async () => {
    renderRoute('/dashboard/admin/credentials', { listCredentials: vi.fn(async () => page([CREDENTIAL, { ...CREDENTIAL, id: 'crd_2', name: 'Legacy key', authMode: undefined }])) });
    const row = (await screen.findByText('Signed script')).closest('tr');
    expect(within(row).getByText('Signed requests')).toBeInTheDocument();
    expect(within(screen.getByText('Legacy key').closest('tr')).getByText('Direct header')).toBeInTheDocument();
  });

  it('creates a credential with the chosen auth mode and explains each mode', async () => {
    const client = renderRoute('/dashboard/admin/credentials', {
      listCredentials: vi.fn(async () => page([])),
      createCredential: vi.fn(async (tenantId, body) => ({ credential: { ...CREDENTIAL, ...body, accessKey: 'ak_new' }, secretKey: 'sk_new' }))
    });
    await userEvent.click(await screen.findByRole('button', { name: /Add/ }));
    const select = screen.getByLabelText('Auth mode');
    expect(select).toHaveValue('DirectHeader');
    expect(screen.getByText(/The default\. Accepts the access key and secret as headers/)).toBeInTheDocument();
    expect([...select.querySelectorAll('option')].map((o) => o.value)).toEqual(['DirectHeader', 'SignedRequest', 'SessionExchange', 'Hybrid']);
    await userEvent.selectOptions(select, 'SessionExchange');
    expect(screen.getByText(/Works only through POST \/v1\.0\/token/)).toBeInTheDocument();
    await userEvent.selectOptions(select, 'SignedRequest');
    expect(screen.getByText(/Accepts only HMAC-signed requests/)).toBeInTheDocument();
    await userEvent.type(screen.getByLabelText('Name'), 'Signed script');
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.createCredential).toHaveBeenCalledWith('default', expect.objectContaining({ name: 'Signed script', authMode: 'SignedRequest' })));
    expect(await screen.findByText('sk_new')).toBeInTheDocument();
  });

  it('shows the auth mode read-only when editing, and does not send it', async () => {
    const client = renderRoute('/dashboard/admin/credentials', { listCredentials: vi.fn(async () => page([CREDENTIAL])), updateCredential: vi.fn(async () => CREDENTIAL) });
    await userEvent.click(await screen.findByText('Signed script'));
    const select = await screen.findByLabelText('Auth mode');
    expect(select).toHaveValue('SignedRequest');
    expect(select).toBeDisabled();
    expect(screen.getByText(/Set when the credential is created/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Save' }));
    await waitFor(() => expect(client.updateCredential).toHaveBeenCalled());
    expect(client.updateCredential.mock.calls[0][2]).not.toHaveProperty('authMode');
  });
});
