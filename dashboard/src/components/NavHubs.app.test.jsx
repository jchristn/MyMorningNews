import { describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MEMBER_DETAILS, READY_SETUP } from '../test/fakeClient.js';
import { renderRoute } from '../test/renderRoute.jsx';

// The hub navigation inside the whole app: the tab strip sits above the page, and the setup gate still redirects.
const NOT_READY = { ...READY_SETUP, ready: false };

describe('hub navigation in the app', () => {
  it('puts the hub tab strip above the page and switches pages from it', async () => {
    renderRoute('/dashboard/explorer');
    expect(await screen.findByRole('heading', { name: 'API explorer', level: 1 })).toBeInTheDocument();
    const strip = screen.getByRole('navigation', { name: 'Observability pages' });
    expect(within(strip).getByRole('link', { name: 'API explorer' })).toHaveAttribute('aria-current', 'page');
    const heading = screen.getByRole('heading', { name: 'API explorer', level: 1 });
    expect(strip.compareDocumentPosition(heading) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    await userEvent.click(within(strip).getByRole('link', { name: 'Health' }));
    expect(await screen.findByRole('heading', { name: 'Health', level: 1 })).toBeInTheDocument();
    expect(within(screen.getByRole('navigation', { name: 'Observability pages' })).getByRole('link', { name: 'Health' })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });

  it('keeps the old page titles as the page headings behind the shorter tab labels', async () => {
    renderRoute('/dashboard/requests');
    expect(await screen.findByRole('heading', { name: 'Request history', level: 1 })).toBeInTheDocument();
    expect(within(screen.getByRole('navigation', { name: 'Observability pages' })).getByRole('link', { name: 'Requests' })).toHaveAttribute(
      'aria-current',
      'page'
    );
  });

  it('sends a dimmed tab to the Setup screen while setup is not finished', async () => {
    renderRoute('/dashboard/models', { getSetupStatus: vi.fn(async () => NOT_READY) });
    expect(await screen.findByRole('heading', { name: 'Models', level: 1 })).toBeInTheDocument();
    const strip = screen.getByRole('navigation', { name: 'Configuration pages' });
    await userEvent.click(within(strip).getByRole('link', { name: 'Prompts' }));
    expect(await screen.findByRole('heading', { name: 'Set up My Morning News', level: 1 })).toBeInTheDocument();
  });

  it('shows a member the Administration hub with only the read-only user directory and no tab strip', async () => {
    renderRoute('/dashboard/admin/users', { getTokenDetails: vi.fn(async () => MEMBER_DETAILS) });
    expect(await screen.findByRole('heading', { name: 'Users', level: 1 })).toBeInTheDocument();
    expect(screen.queryByTestId('hub-tabs')).not.toBeInTheDocument();
    expect(within(screen.getByRole('navigation', { name: 'Main navigation' })).getByRole('link', { name: 'Administration' })).toHaveClass(
      'active'
    );
  });
});
