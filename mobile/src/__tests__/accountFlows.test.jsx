import React from 'react';
import { Text } from 'react-native';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react-native';
import { I18nextProvider } from 'react-i18next';
import * as WebBrowser from 'expo-web-browser';
import { router, useLocalSearchParams } from 'expo-router';
import { ApiError, NetworkError, isEmailNotVerified, isSignupClosed } from '@/api/errors';
import { AuthController } from '@/auth/AuthController';
import { memoryTokenStorage } from '@/auth/tokens';
import { initI18n } from '@/i18n/runtime';
import { ForgotPasswordScreen } from '@/screens/auth/ForgotPasswordScreen';
import { ResetPasswordScreen, resetProblem } from '@/screens/auth/ResetPasswordScreen';
import { SignInScreen, signInErrorKey } from '@/screens/auth/SignInScreen';
import { SignUpScreen, signUpBody, signUpReady, webSignUpUrl } from '@/screens/auth/SignUpScreen';
import { VerifyEmailScreen } from '@/screens/auth/VerifyEmailScreen';
import { AuthProvider } from '@/state/AuthContext';
import { PrefsProvider, usePrefs } from '@/state/PrefsContext';
import { ServerProvider } from '@/state/ServerContext';
import { DEFAULT_PREFS } from '@/storage/prefs';
import { ThemeProvider } from '@/theme/ThemeContext';
import { fakeServer, tokenResponse } from './fakeServer';

const SERVER = 'http://127.0.0.1:12100';
const OPEN_CONFIG = { productName: 'My Morning News', serverVersion: '0.1.0-alpha', signupEnabled: true, requireEmailVerification: true, minimumAge: 21, termsVersion: '2026-11', turnstileSiteKey: null, emailEnabled: true, links: {}, copyright: '(c) 2026 Lattice AI LLC' };

/** The saved email, so tests can see what sign-in will be prefilled with. */
function PrefsProbe() {
  const { prefs } = usePrefs();
  return <Text testID="probe-email">{prefs.lastEmail}</Text>;
}

/** Render a screen signed out (or signed in with `signedIn`) against a scripted server, in a locale and theme. */
async function renderFlow(ui, handlers = {}, { config = OPEN_CONFIG, locale = 'en', theme = 'light', signedIn = false } = {}) {
  const server = fakeServer({
    'GET /v1.0/api/client-config': () => ({ body: config }),
    ...handlers,
  });
  const controller = new AuthController({ serverUrl: SERVER, storage: memoryTokenStorage(), fetchImpl: server.fetchImpl });
  if (signedIn) await controller.signIn({ email: 'admin@mymorningnews', password: 'password' });
  const i18n = initI18n(locale);
  const utils = await render(
    <I18nextProvider i18n={i18n}>
      <PrefsProvider initial={{ ...DEFAULT_PREFS, serverUrl: SERVER }}>
        <ThemeProvider preference={theme}>
          <AuthProvider controller={controller}>
            <ServerProvider>
              {ui}
              <PrefsProbe />
            </ServerProvider>
          </AuthProvider>
        </ThemeProvider>
      </PrefsProvider>
    </I18nextProvider>,
  );
  return { ...utils, server, controller };
}

beforeEach(() => {
  jest.clearAllMocks();
  useLocalSearchParams.mockReturnValue({});
});

describe('sign-up', () => {
  it('sends every field, checks both boxes first, and ends on "check your email" with a resend', async () => {
    const { server } = await renderFlow(<SignUpScreen />, {
      'POST /v1.0/public/signup': () => ({ status: 202, body: { message: 'Check your email for a link to confirm your address.', verificationRequired: true } }),
      'POST /v1.0/public/verify-email/resend': () => ({ status: 202, body: { message: 'If that address has an unconfirmed account, a new link is on its way.' } }),
    });
    await screen.findByTestId('sign-up-screen');
    expect(screen.getByLabelText('I am at least 21 years old.')).toBeTruthy();
    await fireEvent.changeText(screen.getByTestId('sign-up-email'), '  new.reader@example.com ');
    await fireEvent.changeText(screen.getByTestId('sign-up-password'), 'a long passphrase');
    await fireEvent.changeText(screen.getByTestId('sign-up-name'), 'New Reader');
    await fireEvent.changeText(screen.getByTestId('sign-up-code'), 'LAUNCH-2026');
    expect(screen.getByTestId('sign-up-submit').props.accessibilityState.disabled).toBe(true);
    await fireEvent.press(screen.getByTestId('sign-up-terms'));
    expect(screen.getByTestId('sign-up-terms').props.accessibilityState.checked).toBe(true);
    expect(screen.getByTestId('sign-up-submit').props.accessibilityState.disabled).toBe(true);
    await fireEvent.press(screen.getByTestId('sign-up-age'));
    expect(screen.getByTestId('sign-up-submit').props.accessibilityState.disabled).toBe(false);
    await fireEvent.press(screen.getByTestId('sign-up-submit'));
    await screen.findByTestId('sign-up-done');
    expect(screen.getByText('Check your email')).toBeTruthy();
    const call = server.calls.find((c) => c.key === 'POST /v1.0/public/signup');
    expect(call.headers.Authorization).toBeUndefined();
    expect(call.body).toMatchObject({ email: 'new.reader@example.com', password: 'a long passphrase', displayName: 'New Reader', locale: 'en', acceptTerms: true, confirmAge: true, promoCode: 'LAUNCH-2026' });
    expect(typeof call.body.timeZone).toBe('string');
    expect(screen.getByTestId('probe-email').props.children).toBe('new.reader@example.com');

    await fireEvent.press(screen.getByTestId('resend-link'));
    await screen.findByTestId('resend-sent');
    expect(server.calls.find((c) => c.key === 'POST /v1.0/public/verify-email/resend').body).toEqual({ email: 'new.reader@example.com' });
    await fireEvent.press(screen.getByTestId('sign-up-sign-in'));
    expect(router.replace).toHaveBeenCalledWith('/sign-in');
  });

  it('opens the terms and privacy pages and fills the code from a link', async () => {
    useLocalSearchParams.mockReturnValue({ code: 'SPRING-2027' });
    await renderFlow(<SignUpScreen />);
    await screen.findByTestId('sign-up-screen');
    expect(screen.getByTestId('sign-up-code').props.value).toBe('SPRING-2027');
    await fireEvent.press(screen.getByTestId('sign-up-terms-link'));
    await fireEvent.press(screen.getByTestId('sign-up-privacy-link'));
    expect(WebBrowser.openBrowserAsync.mock.calls.map((c) => c[0])).toEqual(['https://mymorningnews.ai/terms.html', 'https://mymorningnews.ai/privacy.html']);
  });

  it('says the account is ready when the server does not ask for verification', async () => {
    await renderFlow(<SignUpScreen />, { 'POST /v1.0/public/signup': () => ({ status: 202, body: { message: 'Your account is ready. Sign in.', verificationRequired: false } }) });
    await screen.findByTestId('sign-up-screen');
    await fireEvent.changeText(screen.getByTestId('sign-up-email'), 'a@b.co');
    await fireEvent.changeText(screen.getByTestId('sign-up-password'), '12345678');
    await fireEvent.press(screen.getByTestId('sign-up-terms'));
    await fireEvent.press(screen.getByTestId('sign-up-age'));
    await fireEvent.press(screen.getByTestId('sign-up-submit'));
    await screen.findByText('Your account is ready');
    expect(screen.queryByTestId('resend-link')).toBeNull();
  });

  it('is not offered when client config has sign-up closed', async () => {
    await renderFlow(<SignUpScreen />, {}, { config: { ...OPEN_CONFIG, signupEnabled: false } });
    await screen.findByTestId('sign-up-closed');
    expect(screen.queryByTestId('sign-up-submit')).toBeNull();
  });

  it('is not offered when the client config route is missing', async () => {
    await renderFlow(<SignUpScreen />, { 'GET /v1.0/api/client-config': () => ({ status: 404, body: { error: 'NotFound' } }) });
    await screen.findByTestId('sign-up-closed');
  });

  it('closes when the server answers SignupClosed', async () => {
    await renderFlow(<SignUpScreen />, { 'POST /v1.0/public/signup': () => ({ status: 404, body: { error: 'SignupClosed', message: 'Sign-up is not open on this server.', requestId: 'rq_1' } }) });
    await screen.findByTestId('sign-up-screen');
    await fireEvent.changeText(screen.getByTestId('sign-up-email'), 'a@b.co');
    await fireEvent.changeText(screen.getByTestId('sign-up-password'), '12345678');
    await fireEvent.press(screen.getByTestId('sign-up-terms'));
    await fireEvent.press(screen.getByTestId('sign-up-age'));
    await fireEvent.press(screen.getByTestId('sign-up-submit'));
    await screen.findByTestId('sign-up-closed');
  });

  it('shows validation errors beside their fields', async () => {
    await renderFlow(<SignUpScreen />, {
      'POST /v1.0/public/signup': () => ({ status: 400, body: { error: 'BadRequest', message: 'The request is not valid.', requestId: 'rq_2', details: { email: 'Enter a valid email address.' } } }),
    });
    await screen.findByTestId('sign-up-screen');
    await fireEvent.changeText(screen.getByTestId('sign-up-email'), 'a@b');
    await fireEvent.changeText(screen.getByTestId('sign-up-password'), '12345678');
    await fireEvent.press(screen.getByTestId('sign-up-terms'));
    await fireEvent.press(screen.getByTestId('sign-up-age'));
    await fireEvent.press(screen.getByTestId('sign-up-submit'));
    await screen.findByText('Enter a valid email address.');
    expect(screen.getByTestId('sign-up-error')).toBeTruthy();
  });

  it('sends people to the web sign-up page when the server needs Turnstile', async () => {
    await renderFlow(<SignUpScreen />, {}, { config: { ...OPEN_CONFIG, turnstileSiteKey: '0x4AAAAAAA' } });
    await screen.findByTestId('sign-up-web');
    expect(screen.queryByTestId('sign-up-submit')).toBeNull();
    await fireEvent.press(screen.getByTestId('sign-up-open-web'));
    expect(WebBrowser.openBrowserAsync).toHaveBeenCalledWith(SERVER + '/signup');
  });

  it('renders in Arabic with the dark theme', async () => {
    await renderFlow(<SignUpScreen />, {}, { locale: 'ar', theme: 'dark' });
    await screen.findByTestId('sign-up-screen');
    expect(screen.getByText('أوافق على شروط الخدمة وسياسة الخصوصية.')).toBeTruthy();
    expect(screen.getByLabelText('عمري 21 سنة على الأقل.')).toBeTruthy();
  });

  it('checks the form and builds the request body', () => {
    const form = { email: ' a@b.co ', password: '12345678', displayName: ' ', promoCode: '', acceptTerms: true, confirmAge: true };
    expect(signUpReady(form)).toBe(true);
    expect(signUpReady({ ...form, password: '1234567' })).toBe(false);
    expect(signUpReady({ ...form, email: 'ab.co' })).toBe(false);
    expect(signUpReady({ ...form, confirmAge: false })).toBe(false);
    expect(signUpBody(form, { locale: 'fr', zone: 'Europe/Paris' })).toEqual({ email: 'a@b.co', password: '12345678', displayName: undefined, timeZone: 'Europe/Paris', locale: 'fr', acceptTerms: true, confirmAge: true, promoCode: undefined });
    expect(webSignUpUrl('https://app.mymorningnews.ai/')).toBe('https://app.mymorningnews.ai/signup');
  });
});

describe('sign-in account links', () => {
  it('offers a new verification link when the email is not confirmed (403 EmailNotVerified)', async () => {
    const { server } = await renderFlow(<SignInScreen />, {
      'POST /v1.0/token': () => ({ status: 403, body: { error: 'EmailNotVerified', message: 'Confirm your email address first.', requestId: 'rq_3' } }),
      'POST /v1.0/public/verify-email/resend': () => ({ status: 202, body: {} }),
    });
    await fireEvent.changeText(screen.getByTestId('sign-in-email'), 'new.reader@example.com');
    await fireEvent.changeText(screen.getByTestId('sign-in-password'), 'a long passphrase');
    await fireEvent.press(screen.getByTestId('sign-in-submit'));
    await screen.findByTestId('sign-in-unverified');
    expect(screen.getByText('Confirm your email first')).toBeTruthy();
    expect(screen.queryByTestId('sign-in-error')).toBeNull();
    await fireEvent.press(screen.getByTestId('resend-link'));
    await screen.findByTestId('resend-sent');
    expect(server.calls.find((c) => c.key === 'POST /v1.0/public/verify-email/resend').body).toEqual({ email: 'new.reader@example.com' });
  });

  it('shows an error raised after choosing a tenant instead of hiding it behind the picker', async () => {
    await renderFlow(<SignInScreen />, {
      'POST /v1.0/token': (call) =>
        call.headers['x-tenant-guid']
          ? { status: 403, body: { error: 'EmailNotVerified', message: 'Confirm your email address first.', requestId: 'rq_4' } }
          : { status: 409, body: { error: 'TenantSelectionRequired', tenants: [{ id: 'ten_a', name: 'Acme' }, { id: 'ten_b', name: 'Beta' }] } },
    });
    await fireEvent.changeText(screen.getByTestId('sign-in-email'), 'a@b.co');
    await fireEvent.changeText(screen.getByTestId('sign-in-password'), 'password');
    await fireEvent.press(screen.getByTestId('sign-in-submit'));
    await screen.findByTestId('tenant-picker');
    await fireEvent.press(screen.getByTestId('tenant-continue'));
    await screen.findByTestId('sign-in-unverified');
    expect(screen.queryByTestId('tenant-picker')).toBeNull();
  });

  it('links to sign-up only when it is open, and always to forgot password', async () => {
    await renderFlow(<SignInScreen />);
    await screen.findByTestId('sign-in-create');
    await fireEvent.press(screen.getByTestId('sign-in-create'));
    expect(router.push).toHaveBeenCalledWith('/sign-up');
    await fireEvent.press(screen.getByTestId('sign-in-forgot'));
    expect(router.push).toHaveBeenCalledWith('/forgot-password');
  });

  it('hides the sign-up link when sign-up is closed', async () => {
    const { server } = await renderFlow(<SignInScreen />, {}, { config: { ...OPEN_CONFIG, signupEnabled: false } });
    await waitFor(() => expect(server.count('GET /v1.0/api/client-config')).toBe(1));
    await act(async () => undefined);
    expect(screen.getByTestId('sign-in-forgot')).toBeTruthy();
    expect(screen.queryByTestId('sign-in-create')).toBeNull();
  });

  it('maps the unverified error and the sign-up switches', () => {
    const unverified = new ApiError(403, { error: 'EmailNotVerified', message: 'Confirm your email address first.' });
    expect(isEmailNotVerified(unverified)).toBe(true);
    expect(isEmailNotVerified(new ApiError(403, { error: 'Forbidden' }))).toBe(false);
    expect(signInErrorKey(unverified)).toBe('mobile.signIn.unverifiedBody');
    expect(signInErrorKey(new ApiError(403, { error: 'Unauthorized' }))).toBe('mobile.signIn.errorCredentials');
    expect(isSignupClosed(new ApiError(404, { error: 'SignupClosed', requestId: 'rq' }))).toBe(true);
    expect(isSignupClosed(new ApiError(404, { error: 'NotFound' }))).toBe(true);
    expect(isSignupClosed(new ApiError(404, { error: 'NotFound', requestId: 'rq' }))).toBe(false);
    expect(isSignupClosed(new NetworkError())).toBe(false);
  });
});

describe('verify email link', () => {
  it('confirms the address once, remembers the email, and offers sign-in', async () => {
    useLocalSearchParams.mockReturnValue({ token: 'tok_verify-1' });
    const { server } = await renderFlow(<VerifyEmailScreen />, { 'POST /v1.0/public/verify-email': () => ({ body: { verified: true, email: 'new.reader@example.com' } }) });
    await screen.findByTestId('verify-done');
    expect(screen.getByText('Email confirmed')).toBeTruthy();
    expect(server.count('POST /v1.0/public/verify-email')).toBe(1);
    expect(server.calls.find((c) => c.key === 'POST /v1.0/public/verify-email').body).toEqual({ token: 'tok_verify-1' });
    expect(screen.getByTestId('probe-email').props.children).toBe('new.reader@example.com');
    await fireEvent.press(screen.getByTestId('verify-sign-in'));
    expect(router.replace).toHaveBeenCalledWith('/sign-in');
  });

  it('offers a new link when the link does not work', async () => {
    useLocalSearchParams.mockReturnValue({ token: 'used' });
    const { server } = await renderFlow(<VerifyEmailScreen />, {
      'POST /v1.0/public/verify-email': () => ({ status: 400, body: { error: 'BadRequest', message: 'This link has expired or was already used.', requestId: 'rq_5' } }),
      'POST /v1.0/public/verify-email/resend': () => ({ status: 202, body: {} }),
    });
    await screen.findByTestId('verify-failed');
    await fireEvent.changeText(screen.getByTestId('verify-email'), 'new.reader@example.com');
    await fireEvent.press(screen.getByTestId('resend-link'));
    await screen.findByTestId('resend-sent');
    expect(server.calls.find((c) => c.key === 'POST /v1.0/public/verify-email/resend').body).toEqual({ email: 'new.reader@example.com' });
  });

  it('asks for a new link without calling the server when the link has no token', async () => {
    const { server } = await renderFlow(<VerifyEmailScreen />);
    await screen.findByTestId('verify-failed');
    expect(server.count('POST /v1.0/public/verify-email')).toBe(0);
    expect(screen.getByTestId('resend-link').props.accessibilityState.disabled).toBe(true);
  });

  it('retries after a network failure, since the link may still be good', async () => {
    useLocalSearchParams.mockReturnValue({ token: 'tok_2' });
    let attempt = 0;
    const { server } = await renderFlow(<VerifyEmailScreen />, {
      'POST /v1.0/public/verify-email': () => {
        attempt += 1;
        if (attempt === 1) throw new Error('offline');
        return { body: { verified: true, email: 'a@b.co' } };
      },
    });
    await screen.findByTestId('verify-offline');
    await fireEvent.press(screen.getByTestId('verify-retry'));
    await screen.findByTestId('verify-done');
    expect(server.count('POST /v1.0/public/verify-email')).toBe(2);
  });
});

describe('reset password link', () => {
  it('sets the new password and ends on sign-in', async () => {
    useLocalSearchParams.mockReturnValue({ token: 'tok_reset' });
    const { server } = await renderFlow(<ResetPasswordScreen />, { 'POST /v1.0/public/password/reset': () => ({ body: { reset: true, email: 'reader@example.com' } }) });
    await fireEvent.changeText(screen.getByTestId('reset-password'), 'a new passphrase');
    await fireEvent.changeText(screen.getByTestId('reset-confirm'), 'a new passphras');
    expect(screen.getByText('The new passwords do not match.')).toBeTruthy();
    expect(screen.getByTestId('reset-submit').props.accessibilityState.disabled).toBe(true);
    await fireEvent.changeText(screen.getByTestId('reset-confirm'), 'a new passphrase');
    await fireEvent.press(screen.getByTestId('reset-submit'));
    await screen.findByTestId('reset-done');
    expect(server.calls.find((c) => c.key === 'POST /v1.0/public/password/reset').body).toEqual({ token: 'tok_reset', password: 'a new passphrase' });
    expect(screen.getByTestId('probe-email').props.children).toBe('reader@example.com');
    await fireEvent.press(screen.getByTestId('reset-sign-in'));
    expect(router.replace).toHaveBeenCalledWith('/sign-in');
  });

  it('signs this device out when the reset account is the one signed in', async () => {
    useLocalSearchParams.mockReturnValue({ token: 'tok_reset' });
    const { controller } = await renderFlow(
      <ResetPasswordScreen />,
      {
        'POST /v1.0/token': () => ({ body: tokenResponse() }),
        'POST /v1.0/public/password/reset': () => ({ body: { reset: true, email: 'Admin@MyMorningNews' } }),
        'DELETE /v1.0/token': () => ({ status: 401, body: { error: 'Unauthorized' } }),
      },
      { signedIn: true },
    );
    expect(controller.getState().status).toBe('signedIn');
    await fireEvent.changeText(screen.getByTestId('reset-password'), 'a new passphrase');
    await fireEvent.changeText(screen.getByTestId('reset-confirm'), 'a new passphrase');
    await fireEvent.press(screen.getByTestId('reset-submit'));
    await screen.findByTestId('reset-sign-in');
    expect(controller.getState().status).toBe('signedOut');
  });

  it('offers a new link when the server rejects this one', async () => {
    useLocalSearchParams.mockReturnValue({ token: 'old' });
    await renderFlow(<ResetPasswordScreen />, { 'POST /v1.0/public/password/reset': () => ({ status: 400, body: { error: 'BadRequest', message: 'This link has expired or was already used.', requestId: 'rq_6' } }) });
    await fireEvent.changeText(screen.getByTestId('reset-password'), 'a new passphrase');
    await fireEvent.changeText(screen.getByTestId('reset-confirm'), 'a new passphrase');
    await fireEvent.press(screen.getByTestId('reset-submit'));
    await screen.findByText('This link has expired or was already used.');
    await fireEvent.press(screen.getByTestId('reset-request-new'));
    expect(router.replace).toHaveBeenCalledWith('/forgot-password');
  });

  it('explains a link without a token', async () => {
    await renderFlow(<ResetPasswordScreen />);
    await screen.findByTestId('reset-no-token');
    expect(screen.getByText('This page needs the link from your reset email.')).toBeTruthy();
  });

  it('checks the new password before sending it', () => {
    expect(resetProblem('short', 'short')).toBe('mobile.password.errorShort');
    expect(resetProblem('12345678', '1234567x')).toBe('mobile.password.errorMismatch');
    expect(resetProblem('12345678', '12345678')).toBeNull();
  });
});

describe('forgot password', () => {
  it('sends the reset email request', async () => {
    const { server } = await renderFlow(<ForgotPasswordScreen />, { 'POST /v1.0/public/password/forgot': () => ({ status: 202, body: { message: 'If that address has an account, a reset link is on its way.' } }) });
    await fireEvent.changeText(screen.getByTestId('forgot-email'), ' reader@example.com ');
    await fireEvent.press(screen.getByTestId('forgot-submit'));
    await screen.findByTestId('forgot-back');
    expect(server.calls.find((c) => c.key === 'POST /v1.0/public/password/forgot').body).toEqual({ email: 'reader@example.com' });
  });

  it('warns when the server sends no email, and explains a rate limit', async () => {
    await renderFlow(<ForgotPasswordScreen />, { 'POST /v1.0/public/password/forgot': () => ({ status: 429, body: { error: 'QuotaExceeded', message: 'Slow down.', requestId: 'rq_7' } }) }, { config: { ...OPEN_CONFIG, emailEnabled: false } });
    await screen.findByTestId('forgot-no-email');
    await fireEvent.changeText(screen.getByTestId('forgot-email'), 'reader@example.com');
    await fireEvent.press(screen.getByTestId('forgot-submit'));
    await screen.findByText('Too many attempts. Wait a minute and try again.');
  });
});
