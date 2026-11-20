import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { AppRoutes } from '../App.jsx';
import { AppProvider } from '../context/AppContext.jsx';
import { AuthProvider } from '../context/AuthContext.jsx';
import { ThemeProvider } from '../context/ThemeContext.jsx';
import AudioPlayer from '../components/AudioPlayer.jsx';
import { MEMBER_DETAILS, fakeClient } from '../test/fakeClient.js';
import { audioResult, installMediaShims } from '../test/mediaFixtures.js';
import { page, renderRoute } from '../test/renderRoute.jsx';
import { RUN, TOPIC } from '../test/researchFixtures.js';
import { ApiClient } from '../utils/api.js';
import { emailSeries, emailsByDay } from '../utils/email.js';

const today = new Date().toISOString();
const MESSAGES = [
  { id: 'eml_1', template: 'RunDigest', recipient: 'demo@mymorningnews', subject: 'Run digest for AI tools', status: 'Sent', attempts: 1, providerMessageId: 'file:eml_1', createdUtc: today },
  { id: 'eml_2', template: 'Test', recipient: 'admin@mymorningnews', subject: 'Test email', status: 'Failed', attempts: 3, lastError: 'Mailgun 401', createdUtc: today }
];

describe('email client methods', () => {
  it('calls the outbox, test, preview, and preference routes', async () => {
    const fetch = vi.fn(async () => ({ status: 200, ok: true, text: async () => '{}', headers: { get: () => null } }));
    const api = new ApiClient('http://127.0.0.1:12100', 'tok', { fetch });
    await api.listEmailMessages({ status: 'Sent', template: 'RunDigest', recipient: 'a@b.c' });
    await api.getEmailMessage('eml_1');
    await api.resendEmailMessage('eml_1');
    await api.deleteEmailMessage('eml_1');
    await api.sendTestEmail();
    await api.getRunEmailPreview('run_1');
    await api.getEmailPreferences();
    await api.updateEmailPreferences({ runEmails: false });
    const calls = fetch.mock.calls.map(([url, init]) => init.method + ' ' + url.replace('http://127.0.0.1:12100', ''));
    expect(calls).toEqual([
      'GET /v1.0/api/email-messages?status=Sent&template=RunDigest&recipient=a%40b.c',
      'GET /v1.0/api/email-messages/eml_1',
      'POST /v1.0/api/email-messages/eml_1/resend',
      'DELETE /v1.0/api/email-messages/eml_1',
      'POST /v1.0/api/email/test',
      'GET /v1.0/api/runs/run_1/email-preview',
      'GET /v1.0/api/email-preferences',
      'PUT /v1.0/api/email-preferences'
    ]);
    expect(JSON.parse(fetch.mock.calls[7][1].body)).toEqual({ runEmails: false });
  });
});

describe('email shaping', () => {
  it('counts messages per day by status with gaps filled', () => {
    const now = new Date('2026-11-19T12:00:00Z');
    const buckets = emailsByDay(
      [
        { status: 'Sent', createdUtc: '2026-11-19T01:00:00Z' },
        { status: 'Sent', createdUtc: '2026-11-19T02:00:00Z' },
        { status: 'Failed', createdUtc: '2026-11-17T02:00:00Z' },
        { status: 'Sent', createdUtc: '2026-01-01T00:00:00Z' }
      ],
      { now, days: 7 }
    );
    expect(buckets).toHaveLength(7);
    expect(buckets[6].counts).toEqual({ Sent: 2 });
    expect(buckets[4].counts).toEqual({ Failed: 1 });
    const series = emailSeries(buckets, (s) => s);
    expect(series.map((s) => s.key)).toEqual(['Sent', 'Failed']);
    expect(series[0].values[6]).toBe(2);
  });
});

describe('EmailView', () => {
  it('lists the outbox with filters, a chart, and view, resend, and delete actions', async () => {
    const client = renderRoute('/dashboard/email', {
      listEmailMessages: vi.fn(async () => page(MESSAGES)),
      getEmailMessage: vi.fn(async () => ({ ...MESSAGES[0], renderedHtml: '<p>Hello digest</p>', renderedText: 'Hello digest' })),
      resendEmailMessage: vi.fn(async () => MESSAGES[0]),
      deleteEmailMessage: vi.fn(async () => null)
    });
    expect(await screen.findByText('Run digest for AI tools')).toBeInTheDocument();
    expect(screen.getByText('Failed', { selector: '.badge' })).toHaveAttribute('title', 'Mailgun 401');
    expect(await screen.findByRole('img', { name: 'Messages per day by status' })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText('Filter by Status'), { target: { value: 'Failed' } });
    fireEvent.change(screen.getByLabelText('Filter by Template'), { target: { value: 'Test' } });
    fireEvent.change(screen.getByLabelText('Filter by Recipient'), { target: { value: 'admin' } });
    await waitFor(() => expect(client.listEmailMessages).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'Failed', template: 'Test', recipient: 'admin' })));

    await userEvent.click(screen.getByText('Run digest for AI tools'));
    const frame = await screen.findByTitle('Rendered email');
    expect(frame).toHaveAttribute('sandbox', '');
    expect(frame.getAttribute('srcdoc')).toContain('Hello digest');
    await userEvent.click(screen.getByRole('tab', { name: 'Plain text' }));
    expect(screen.getByText('Hello digest', { selector: 'pre' })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Close' }));

    const row = screen.getByText('Run digest for AI tools').closest('tr');
    await userEvent.click(within(row).getByRole('button', { name: /actions/i }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Resend' }));
    await waitFor(() => expect(client.resendEmailMessage).toHaveBeenCalledWith('eml_1'));
    await userEvent.click(within(row).getByRole('button', { name: /actions/i }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Delete' }));
    await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Delete' }));
    await waitFor(() => expect(client.deleteEmailMessage).toHaveBeenCalledWith('eml_1'));
  });

  it('sends a test email', async () => {
    const client = renderRoute('/dashboard/email', { sendTestEmail: vi.fn(async () => ({ id: 'eml_9', recipient: 'admin@mymorningnews' })) });
    await screen.findByText('No email sent yet');
    await userEvent.click(screen.getByRole('button', { name: 'Send test email' }));
    expect(await screen.findByText('Test email queued for admin@mymorningnews')).toBeInTheDocument();
    expect(client.sendTestEmail).toHaveBeenCalled();
  });

  it('tells members they see their own email and hides the recipient filter', async () => {
    renderRoute('/dashboard/email', { getTokenDetails: async () => MEMBER_DETAILS, listEmailMessages: vi.fn(async () => page([MESSAGES[0]])) });
    expect(await screen.findByText('You see the email sent to you. Administrators see every message.')).toBeInTheDocument();
    await screen.findByText('Run digest for AI tools');
    expect(screen.queryByLabelText('Filter by Recipient')).not.toBeInTheDocument();
  });
});

describe('run email preview and deep links', () => {
  it('opens a run from ?id= and previews its digest from the drawer', async () => {
    const client = renderRoute('/dashboard/runs?id=run_1', {
      listTopics: vi.fn(async () => page([TOPIC])),
      listRuns: vi.fn(async () => page([RUN])),
      getRun: vi.fn(async () => RUN),
      getRunEmailPreview: vi.fn(async () => ({ recipient: 'demo@mymorningnews', subject: 'Digest preview', html: '<p>Preview body</p>', text: 'Preview body' }))
    });
    await screen.findByTestId('run-detail');
    expect(client.getRun).toHaveBeenCalledWith('run_1');
    await userEvent.click(screen.getByRole('button', { name: 'Email preview' }));
    expect((await screen.findByTitle('Rendered email')).getAttribute('srcdoc')).toContain('Preview body');
    expect(screen.getByText('To demo@mymorningnews')).toBeInTheDocument();
    expect(client.getRunEmailPreview).toHaveBeenCalledWith(RUN.id);
  });

  it('returns to the deep link after signing in', async () => {
    const client = fakeClient({ getRun: vi.fn(async () => RUN), listRuns: vi.fn(async () => page([RUN])), listTopics: vi.fn(async () => page([TOPIC])) });
    render(
      <ThemeProvider>
        <AuthProvider clientFactory={() => client}>
          <AppProvider>
            <MemoryRouter initialEntries={['/dashboard/runs?id=run_1']}>
              <AppRoutes />
            </MemoryRouter>
          </AppProvider>
        </AuthProvider>
      </ThemeProvider>
    );
    await userEvent.type(await screen.findByLabelText('Email'), 'admin@mymorningnews');
    await userEvent.type(screen.getByLabelText('Password'), 'password');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));
    await screen.findByTestId('run-detail');
    expect(client.getRun).toHaveBeenCalledWith('run_1');
  });
});

describe('listen deep link', () => {
  beforeEach(() => installMediaShims());
  afterEach(() => vi.restoreAllMocks());

  it('starts playing as soon as the audio loads when autoPlay is set', async () => {
    const client = fakeClient({ getArtifactContent: vi.fn(async () => audioResult()) });
    render(
      <AuthProvider clientFactory={() => client}>
        <AudioPlayer artifactId="art_mp3" durationSeconds={60} title="Morning News" autoPlay />
      </AuthProvider>
    );
    const audio = await screen.findByTestId('audio-element');
    await waitFor(() => expect(audio.getAttribute('src')).toBeTruthy());
    fireEvent.loadedMetadata(audio);
    expect(HTMLMediaElement.prototype.play).toHaveBeenCalled();
  });
});

describe('settings and profile email surfaces', () => {
  it('labels the email, tts, learning, and hosting groups and offers a test email', async () => {
    const fields = ['email.provider', 'tts.provider', 'learning.enabled', 'hosting.publicSignup'].map((path) => ({ path, group: path.split('.')[0], name: path.split('.')[1], type: 'string', restartRequired: false, secret: false, isSet: false }));
    const settings = { email: { provider: 'File' }, tts: { provider: 'Disabled' }, learning: { enabled: 'true' }, hosting: { publicSignup: 'false' } };
    renderRoute('/dashboard/settings', { getSettings: vi.fn(async () => ({ settings, fields, pendingRestart: [] })) });
    for (const label of ['Email', 'Text to speech', 'Learning', 'Hosting']) expect(await screen.findByText(label, { selector: 'legend' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Send test email' })).toBeInTheDocument();
  });

  it('shows and saves my email preferences with the topics I unsubscribed from', async () => {
    const client = renderRoute('/dashboard/profile', {
      getMe: vi.fn(async () => ({ id: 'usr_admin', email: 'admin@mymorningnews' })),
      getMyPlan: vi.fn(async () => null),
      getEmailPreferences: vi.fn(async () => ({ address: 'admin@mymorningnews', runEmails: true, briefingEmails: true, failureEmails: true, unsubscribedTopicIds: ['top_1'] })),
      updateEmailPreferences: vi.fn(async () => ({ address: 'admin@mymorningnews', runEmails: false, briefingEmails: true, failureEmails: true, unsubscribedTopicIds: ['top_1'] })),
      listTopics: vi.fn(async () => page([TOPIC]))
    });
    const box = await screen.findByRole('checkbox', { name: /Run emails/ });
    expect(box).toBeChecked();
    expect(await screen.findByRole('link', { name: TOPIC.name })).toBeInTheDocument();
    await userEvent.click(box);
    await waitFor(() => expect(client.updateEmailPreferences).toHaveBeenCalledWith({ runEmails: false }));
    await waitFor(() => expect(screen.getByRole('checkbox', { name: /Run emails/ })).not.toBeChecked());
  });
});
