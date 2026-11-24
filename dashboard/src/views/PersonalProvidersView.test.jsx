import { expect, it, vi } from 'vitest';
import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderRoute } from '../test/renderRoute.jsx';

it('requires a verified model for BYOK and clears the submitted key', async () => {
  let connection = null;
  const client = renderRoute('/dashboard/account/providers', {
    getMyProviders: vi.fn(async () => ({ connections: connection ? [connection] : [], plan: { plan: { name: 'Catalog only' }, fundingMode: 'Platform' }, localSpeechAvailable: false })),
    saveMyProvider: vi.fn(async (body) => { connection = { id: 'pco_1', ...body, apiKey: undefined, active: true, secretLast4: '1234', validatedUtc: null }; return connection; }),
    testMyProvider: vi.fn(async () => { connection.validatedUtc = new Date().toISOString(); }),
  });
  expect(await screen.findByRole('button', { name: 'Use my own keys' })).toBeDisabled();
  fireEvent.change(screen.getByLabelText('Model ID'), { target: { value: 'my-model' } });
  fireEvent.change(screen.getByLabelText('API key'), { target: { value: 'private-key-1234' } });
  await userEvent.click(screen.getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(client.saveMyProvider).toHaveBeenCalledWith(expect.objectContaining({ provider: 'Anthropic', model: 'my-model', apiKey: 'private-key-1234' })));
  expect(screen.getByLabelText('API key')).toHaveValue('');
  expect(screen.getByRole('button', { name: 'Use my own keys' })).toBeDisabled();
  await userEvent.click(await screen.findByRole('button', { name: 'Test saved connection' }));
  await waitFor(() => expect(screen.getByRole('button', { name: 'Use my own keys' })).toBeEnabled());
  expect(client.testMyProvider).toHaveBeenCalledWith('pco_1');
});

it('offers local speech only when the server enables it', async () => {
  renderRoute('/dashboard/account/providers', { getMyProviders: vi.fn(async () => ({ connections: [], plan: { plan: { name: 'BYOK' }, fundingMode: 'Personal' }, localSpeechAvailable: false })) });
  await screen.findByRole('button', { name: 'Use assigned plan' });
  await userEvent.click(screen.getByRole('button', { name: 'Speech' }));
  expect(screen.queryByRole('option', { name: 'Local speech engine' })).not.toBeInTheDocument();
  expect(screen.getByLabelText('Voice ID')).toBeInTheDocument();
});
