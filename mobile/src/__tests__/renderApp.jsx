import React from 'react';
import { render } from '@testing-library/react-native';
import { I18nextProvider } from 'react-i18next';
import { AuthController } from '@/auth/AuthController';
import { memoryTokenStorage } from '@/auth/tokens';
import { initI18n } from '@/i18n/runtime';
import { PlayerProvider } from '@/player/PlayerContext';
import { PlayerService } from '@/player/PlayerService';
import { AuthProvider } from '@/state/AuthContext';
import { PrefsProvider } from '@/state/PrefsContext';
import { ServerProvider } from '@/state/ServerContext';
import { DEFAULT_PREFS } from '@/storage/prefs';
import { ThemeProvider } from '@/theme/ThemeContext';
import { fakeServer, tokenResponse } from './fakeServer';

export const SERVER = 'http://127.0.0.1:12100';

/**
 * Render a screen signed in against a scripted fake server (`handlers` keyed by "METHOD /path"), inside every
 * provider the app wraps screens in. Resolves the render result plus `server` (its recorded calls).
 */
export async function renderSignedIn(ui, handlers = {}) {
  const server = fakeServer({
    'POST /v1.0/token': () => ({ body: tokenResponse() }),
    'GET /v1.0/api/client-config': () => ({ body: { productName: 'My Morning News', serverVersion: '0.1.0-alpha', links: {}, copyright: '(c) 2026 Lattice AI LLC' } }),
    ...handlers,
  });
  const controller = new AuthController({ serverUrl: SERVER, storage: memoryTokenStorage(), fetchImpl: server.fetchImpl });
  await controller.signIn({ email: 'admin@mymorningnews', password: 'password' });
  const service = new PlayerService({ audio: { createAudioPlayer: jest.fn(), setAudioModeAsync: jest.fn(async () => undefined) } });
  const i18n = initI18n('en');
  const utils = await render(
    <I18nextProvider i18n={i18n}>
      <PrefsProvider initial={{ ...DEFAULT_PREFS, serverUrl: SERVER }}>
        <ThemeProvider preference="light">
          <AuthProvider controller={controller}>
            <ServerProvider>
              <PlayerProvider service={service}>{ui}</PlayerProvider>
            </ServerProvider>
          </AuthProvider>
        </ThemeProvider>
      </PrefsProvider>
    </I18nextProvider>,
  );
  return { ...utils, server, controller };
}

/** A paged list response like the server's. */
export function page(items) {
  return { body: { items, pageNumber: 1, pageSize: items.length || 10, totalCount: items.length, totalPages: 1 } };
}

/** The server's 502 when no model key is configured. */
export const MODEL_ERROR = { status: 502, body: { error: 'ModelError', message: 'Endpoint Anthropic (Anthropic) needs an API key.', requestId: 'rqi_model' } };
