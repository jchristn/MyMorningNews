import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { AppRoutes } from '../../App.jsx';
import { AppProvider } from '../../context/AppContext.jsx';
import { AuthProvider } from '../../context/AuthContext.jsx';
import { ThemeProvider } from '../../context/ThemeContext.jsx';
import { ApiError } from '../../utils/api.js';
import { fakeClient } from '../../test/fakeClient.js';

const OPEN = { signupEnabled: true, requireEmailVerification: true, minimumAge: 18, termsVersion: '2026-11-20', turnstileSiteKey: null };

function Where() {
  const location = useLocation();
  return <output data-testid="location">{location.pathname}</output>;
}

function renderSignedOut(path, overrides = {}) {
  window.localStorage.clear();
  const client = fakeClient({ getClientConfig: vi.fn(async () => OPEN), ...overrides });
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

describe('sign-up', () => {
  it('needs the terms and age boxes, then shows check your email', async () => {
    const signUp = vi.fn(async () => ({ verificationRequired: true }));
    renderSignedOut('/signup?code=LAUNCH-2026', { signUp });
    await userEvent.type(await screen.findByLabelText('Email'), 'new@example.com');
    await userEvent.type(screen.getByLabelText('Password', { selector: 'input' }), 'a long passphrase');
    const create = screen.getByRole('button', { name: /Create account/ });
    expect(create).toBeDisabled();
    await userEvent.click(screen.getByLabelText(/I accept the/));
    await userEvent.click(screen.getByLabelText('I am at least 18 years old.'));
    expect(create).toBeEnabled();
    await userEvent.click(create);
    expect(await screen.findByText('Check your email')).toBeInTheDocument();
    expect(signUp).toHaveBeenCalledWith(expect.objectContaining({ email: 'new@example.com', acceptTerms: true, confirmAge: true, promoCode: 'LAUNCH-2026' }));
  });

  it('says sign-up is closed when the server does not allow it', async () => {
    renderSignedOut('/signup', { getClientConfig: vi.fn(async () => ({ ...OPEN, signupEnabled: false })) });
    expect(await screen.findByText(/Sign-up is not open on this server/)).toBeInTheDocument();
  });
});

describe('sign-in', () => {
  it('links to sign-up when it is open and signs in without a tenant', async () => {
    const client = renderSignedOut('/');
    expect(await screen.findByRole('link', { name: 'Create an account' })).toBeInTheDocument();
    await waitFor(() => expect(screen.getByLabelText('Tenant')).toHaveValue(''));
    await userEvent.type(screen.getByLabelText('Email'), 'reader@example.com');
    await userEvent.type(screen.getByLabelText('Password', { selector: 'input' }), 'a long passphrase');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await waitFor(() => expect(screen.getByTestId('location').textContent).toBe('/dashboard/home'));
    expect(client.login).toHaveBeenCalledWith('reader@example.com', 'a long passphrase', '');
  });

  it('offers a tenant list when the email is in several', async () => {
    let calls = 0;
    const login = vi.fn(async () => {
      calls += 1;
      if (calls === 1) throw new ApiError(409, { tenants: [{ id: 'ten_a', name: 'Alpha' }, { id: 'ten_b', name: 'Beta' }] });
      return { token: 'tok_1', tenantId: 'ten_b' };
    });
    renderSignedOut('/', { login });
    await userEvent.type(await screen.findByLabelText('Email'), 'reader@example.com');
    await userEvent.type(screen.getByLabelText('Password', { selector: 'input' }), 'a long passphrase');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    const choice = await screen.findByLabelText('Workspace');
    await userEvent.selectOptions(choice, 'ten_b');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await waitFor(() => expect(login).toHaveBeenLastCalledWith('reader@example.com', 'a long passphrase', 'ten_b'));
  });

  it('offers a new confirmation link when the email is not verified', async () => {
    const resendVerification = vi.fn(async () => ({}));
    const login = vi.fn(async () => {
      throw new ApiError(403, { error: 'EmailNotVerified', message: 'Confirm your email address first.' });
    });
    renderSignedOut('/', { login, resendVerification });
    await userEvent.type(await screen.findByLabelText('Email'), 'reader@example.com');
    await userEvent.type(screen.getByLabelText('Password', { selector: 'input' }), 'a long passphrase');
    await userEvent.click(screen.getByRole('button', { name: 'Sign in' }));
    await userEvent.click(await screen.findByRole('button', { name: 'Send a new link' }));
    expect(resendVerification).toHaveBeenCalledWith('reader@example.com');
    expect(await screen.findByText(/a new link is on its way/)).toBeInTheDocument();
  });
});

describe('email and password links', () => {
  it('verifies the email from the link', async () => {
    const verifyEmail = vi.fn(async () => ({ verified: true, email: 'reader@example.com' }));
    renderSignedOut('/verify?token=abc', { verifyEmail });
    expect(await screen.findByText('Your email is confirmed. You can sign in now.')).toBeInTheDocument();
    expect(verifyEmail).toHaveBeenCalledTimes(1);
    expect(verifyEmail).toHaveBeenCalledWith('abc');
  });

  it('offers a new link when verification fails', async () => {
    renderSignedOut('/verify?token=old', { verifyEmail: vi.fn(async () => Promise.reject(new ApiError(400, { error: 'BadRequest' }))) });
    expect(await screen.findByText(/not valid or has expired/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Send a new link' })).toBeDisabled();
  });

  it('sends a reset link and sets a new password', async () => {
    const forgotPassword = vi.fn(async () => ({}));
    renderSignedOut('/forgot-password', { forgotPassword });
    await userEvent.type(await screen.findByLabelText('Email'), 'reader@example.com');
    await userEvent.click(screen.getByRole('button', { name: 'Send reset link' }));
    expect(await screen.findByText(/a reset link is on its way/)).toBeInTheDocument();
    expect(forgotPassword).toHaveBeenCalledWith('reader@example.com');
  });

  it('resets the password from the link', async () => {
    const resetPassword = vi.fn(async () => ({ reset: true }));
    renderSignedOut('/reset-password?token=tok', { resetPassword });
    await userEvent.type(await screen.findByLabelText('New password', { selector: 'input' }), 'another passphrase');
    await userEvent.type(screen.getByLabelText('Confirm new password', { selector: 'input' }), 'another passphrase');
    await userEvent.click(screen.getByRole('button', { name: 'Save new password' }));
    expect(await screen.findByText(/Your password is changed/)).toBeInTheDocument();
    expect(resetPassword).toHaveBeenCalledWith('tok', 'another passphrase');
  });
});
