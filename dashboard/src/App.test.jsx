import { describe, expect, it } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { AppRoutes } from './App.jsx';
import { AppProvider } from './context/AppContext.jsx';
import { AuthProvider, STORAGE_KEYS } from './context/AuthContext.jsx';
import { ThemeProvider } from './context/ThemeContext.jsx';
import { MEMBER_DETAILS, fakeClient } from './test/fakeClient.js';

function Where() {
  const location = useLocation();
  return <output data-testid="location">{location.pathname}</output>;
}

function renderApp(path, client = fakeClient(), { signedIn = false } = {}) {
  if (signedIn) window.localStorage.setItem(STORAGE_KEYS.token, 'tok_1');
  render(
    <ThemeProvider>
      <AuthProvider clientFactory={() => client}>
        <AppProvider>
          <MemoryRouter initialEntries={[path]}>
            <AppRoutes />
            <Where />
          </MemoryRouter>
        </AppProvider>
      </AuthProvider>
    </ThemeProvider>
  );
  return client;
}

const location = () => screen.getByTestId('location').textContent;

describe('routing guards', () => {
  it('sends signed-out visitors to the login page', async () => {
    renderApp('/dashboard/settings');
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument();
    expect(location()).toBe('/');
    expect(screen.getByAltText('My Morning News')).toBeInTheDocument();
  });

  it('signs in and returns to the page the visitor asked for', async () => {
    const client = renderApp('/dashboard/server-info');
    await screen.findByRole('button', { name: 'Sign in' });
    await userEvent.type(screen.getByLabelText('Email'), 'admin@mymorningnews');
    await userEvent.type(screen.getByLabelText('Password', { selector: 'input' }), 'password');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await waitFor(() => expect(location()).toBe('/dashboard/server-info'));
    expect(client.login).toHaveBeenCalledWith('admin@mymorningnews', 'password', 'default');
  });

  it('shows an error and stays on the login page when sign-in fails', async () => {
    const client = fakeClient({
      login: async () => {
        throw Object.assign(new Error('bad'), { status: 401, body: { error: 'Unauthorized' } });
      }
    });
    renderApp('/', client);
    await userEvent.type(await screen.findByLabelText('Email'), 'x@y');
    await userEvent.type(screen.getByLabelText('Password', { selector: 'input' }), 'nope');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    expect(await screen.findByRole('alert')).toBeInTheDocument();
    expect(location()).toBe('/');
  });

  it('restores a stored session and lands on home', async () => {
    renderApp('/', fakeClient(), { signedIn: true });
    await waitFor(() => expect(location()).toBe('/dashboard/home'));
    expect(await screen.findByRole('navigation', { name: 'Main navigation' })).toBeInTheDocument();
  });

  it('redirects the legacy aliases', async () => {
    renderApp('/dashboard/request-history', fakeClient(), { signedIn: true });
    await waitFor(() => expect(location()).toBe('/dashboard/requests'));
  });

  it('redirects the API explorer alias', async () => {
    renderApp('/dashboard/api-explorer', fakeClient(), { signedIn: true });
    await waitFor(() => expect(location()).toBe('/dashboard/explorer'));
  });

  it('opens pages that used to be placeholders', async () => {
    renderApp('/dashboard/search-providers', fakeClient(), { signedIn: true });
    expect(await screen.findByText('No search providers')).toBeInTheDocument();
  });

  it('blocks members from admin-only pages', async () => {
    renderApp('/dashboard/settings', fakeClient({ getTokenDetails: async () => MEMBER_DETAILS }), { signedIn: true });
    expect(await screen.findByText('Access denied')).toBeInTheDocument();
    // The Settings hub stays, opening the first page a member can use; the Server settings tab is not offered.
    expect(screen.queryByRole('link', { name: 'Server settings' })).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Settings' })).toHaveAttribute('href', '/dashboard/profile');
  });

  it('clears a stored session the server rejects', async () => {
    const client = fakeClient({
      getTokenDetails: async () => {
        throw Object.assign(new Error('expired'), { status: 401 });
      }
    });
    renderApp('/dashboard/home', client, { signedIn: true });
    expect(await screen.findByRole('button', { name: 'Sign in' })).toBeInTheDocument();
    expect(window.localStorage.getItem(STORAGE_KEYS.token)).toBeNull();
  });
});
