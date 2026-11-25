import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { AppProvider } from '../../context/AppContext.jsx';
import { AuthProvider, STORAGE_KEYS } from '../../context/AuthContext.jsx';
import { ThemeProvider } from '../../context/ThemeContext.jsx';
import { fakeClient } from '../../test/fakeClient.js';
import PromoteTopicModal from './PromoteTopicModal.jsx';

function renderModal(overrides) {
  window.localStorage.setItem(STORAGE_KEYS.token, 'tok_1');
  const client = fakeClient(overrides);
  const onClose = vi.fn();
  render(
    <ThemeProvider>
      <AuthProvider clientFactory={() => client}>
        <AppProvider>
          <MemoryRouter>
            <PromoteTopicModal topic={{ id: 'tpc_1', name: 'AI chips', description: 'Accelerators', language: 'en' }} onClose={onClose} />
          </MemoryRouter>
        </AppProvider>
      </AuthProvider>
    </ThemeProvider>
  );
  return { client, onClose };
}

describe('PromoteTopicModal', () => {
  it('copies the topic into the catalog with the chosen fields', async () => {
    const { client } = renderModal({ promoteTopicToCatalog: vi.fn(async (id, body) => ({ id: 'pub_1', title: body.title, status: body.status })) });
    const title = await screen.findByLabelText('Title');
    expect(title).toHaveValue('AI chips');
    await userEvent.clear(screen.getByLabelText('Category'));
    await userEvent.type(screen.getByLabelText('Category'), 'Technology');
    await userEvent.click(screen.getByLabelText('Publish now (otherwise it is saved as a draft)'));
    await userEvent.click(screen.getByRole('button', { name: 'Create system briefing' }));
    await waitFor(() => expect(client.promoteTopicToCatalog).toHaveBeenCalled());
    const [id, body] = client.promoteTopicToCatalog.mock.calls[0];
    expect(id).toBe('tpc_1');
    expect(body).toMatchObject({ title: 'AI chips', description: 'Accelerators', category: 'Technology', language: 'en', freeEligible: true, status: 'Draft' });
    expect(await screen.findByRole('link', { name: 'Open in catalog' })).toHaveAttribute('href', '/dashboard/admin/catalog/pub_1');
  });

  it('needs a title', async () => {
    const { client } = renderModal({});
    await userEvent.clear(await screen.findByLabelText('Title'));
    expect(screen.getByRole('button', { name: 'Create system briefing' })).toBeDisabled();
    expect(client.promoteTopicToCatalog).not.toHaveBeenCalled();
  });
});
