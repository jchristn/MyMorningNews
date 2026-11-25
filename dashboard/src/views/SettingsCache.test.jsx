import { expect, it, vi } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderRoute } from '../test/renderRoute.jsx';

it('edits nested crawl cache settings while keeping masked credentials out of password inputs', async () => {
  const settings = { crawlCache: { enabled: false, storage: { directory: '/data/crawl-cache', s3SecretKey: '********' } } };
  const fields = [
    { path: 'crawlCache.enabled', group: 'crawlCache', name: 'enabled', type: 'boolean' },
    { path: 'crawlCache.storage.directory', group: 'crawlCache', name: 'directory', type: 'string', restartRequired: true },
    { path: 'crawlCache.storage.s3SecretKey', group: 'crawlCache', name: 's3SecretKey', type: 'string', secret: true, isSet: true, restartRequired: true }
  ];
  const updateSettings = vi.fn(async () => ({ changed: ['crawlCache.enabled', 'crawlCache.storage.directory'], appliedLive: ['crawlCache.enabled'], pendingRestart: ['crawlCache.storage.directory'] }));
  renderRoute('/dashboard/settings', { getSettings: vi.fn(async () => ({ settings, fields, pendingRestart: [] })), updateSettings });
  expect(await screen.findByText('Crawl cache', { selector: 'legend' })).toBeInTheDocument();
  const secret = document.getElementById('setting-crawlCache.storage.s3SecretKey');
  expect(secret).toHaveAttribute('type', 'password');
  expect(secret).toHaveValue('');
  await userEvent.click(document.getElementById('setting-crawlCache.enabled'));
  const directory = document.getElementById('setting-crawlCache.storage.directory');
  await userEvent.clear(directory);
  await userEvent.type(directory, '/data/cache-new');
  await userEvent.click(screen.getByRole('button', { name: 'Save' }));
  await waitFor(() => expect(updateSettings).toHaveBeenCalledWith({ crawlCache: { enabled: true, storage: { directory: '/data/cache-new', s3SecretKey: '********' } } }));
});
