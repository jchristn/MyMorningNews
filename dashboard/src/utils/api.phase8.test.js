import { describe, expect, it, vi } from 'vitest';
import { ApiClient, ApiError } from './api.js';

function response(status, body, headers = {}) {
  const text = body === undefined ? '' : typeof body === 'string' ? body : JSON.stringify(body);
  return {
    status,
    ok: status >= 200 && status < 300,
    text: async () => text,
    blob: async () => new Blob([text], { type: headers['Content-Type'] || 'application/octet-stream' }),
    headers: { get: (name) => headers[Object.keys(headers).find((k) => k.toLowerCase() === name.toLowerCase())] || null }
  };
}

function client(body = {}, status = 200, headers = {}) {
  const fetch = vi.fn(async () => response(status, body, headers));
  return { api: new ApiClient('http://127.0.0.1:12100', 'tok', { fetch }), fetch };
}

function call(fetch, index = 0) {
  const [url, init] = fetch.mock.calls[index];
  return { url: url.replace('http://127.0.0.1:12100', ''), method: init.method, body: init.body ? JSON.parse(init.body) : undefined, headers: init.headers };
}

describe('media client methods', () => {
  it('lists with every filter, reads, and deletes episodes', async () => {
    const { api, fetch } = client();
    await api.listMedia({ briefingId: 'brf_1', editionId: 'edn_1', status: 'Ready', kind: 'PodcastEpisode', pageSize: 5, sortBy: 'createdUtc', sortDirection: 'Descending' });
    await api.getMedia('med 1');
    await api.deleteMedia('med_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/media?briefingId=brf_1&editionId=edn_1&status=Ready&kind=PodcastEpisode&pageSize=5&sortBy=createdUtc&sortDirection=Descending', method: 'GET' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/media/med%201', method: 'GET' });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/media/med_1', method: 'DELETE' });
  });

  it('makes audio for a briefing with or without a voice', async () => {
    const { api, fetch } = client({ id: 'med_1', status: 'Queued' }, 202);
    const queued = await api.generateBriefingAudio('brf_1', 'voi_1');
    await api.generateBriefingAudio('brf_1');
    expect(queued).toEqual({ id: 'med_1', status: 'Queued' });
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/briefings/brf_1/audio', method: 'POST', body: { voiceProfileId: 'voi_1' } });
    expect(call(fetch, 1).body).toEqual({});
  });

  it('raises a 409 when audio is already being made', async () => {
    const { api } = client({ error: 'Conflict', message: 'Audio for this briefing is already being made.' }, 409);
    const err = await api.generateBriefingAudio('brf_1').catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect(err.status).toBe(409);
    expect(err.message).toBe('Audio for this briefing is already being made.');
  });
});

describe('voice client methods', () => {
  it('lists, reads, creates, updates, and deletes voices', async () => {
    const { api, fetch } = client();
    await api.listVoices({ pageSize: 100, sortBy: 'name', sortDirection: 'Ascending' });
    await api.getVoice('voi_1');
    await api.createVoice({ name: 'Kokoro', voice: 'af_heart', speed: 1.1 });
    await api.updateVoice('voi_1', { isDefault: true });
    await api.deleteVoice('voi_1');
    expect(call(fetch, 0).url).toBe('/v1.0/api/voices?pageSize=100&sortBy=name&sortDirection=Ascending');
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/voices/voi_1', method: 'GET' });
    expect(call(fetch, 2)).toMatchObject({ url: '/v1.0/api/voices', method: 'POST', body: { name: 'Kokoro', voice: 'af_heart', speed: 1.1 } });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/voices/voi_1', method: 'PUT', body: { isDefault: true } });
    expect(call(fetch, 4)).toMatchObject({ url: '/v1.0/api/voices/voi_1', method: 'DELETE' });
  });

  it('tests a voice and resolves to the audio bytes with a JSON body and bearer auth', async () => {
    const { api, fetch } = client('RIFF....WAVE', 200, { 'Content-Type': 'audio/wav' });
    const result = await api.testVoice('voi_1', 'Hello there');
    expect(call(fetch)).toMatchObject({ url: '/v1.0/api/voices/voi_1/test', method: 'POST', body: { text: 'Hello there' } });
    expect(call(fetch).headers.Authorization).toBe('Bearer tok');
    expect(call(fetch).headers['Content-Type']).toBe('application/json');
    expect(result.contentType).toBe('audio/wav');
    expect(await result.blob.text()).toBe('RIFF....WAVE');
  });

  it('raises TtsError from a 502 voice test', async () => {
    const { api } = client({ error: 'TtsError', message: 'Speech endpoint returned 500.' }, 502);
    const err = await api.testVoice('voi_1', '').catch((e) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect(err.status).toBe(502);
    expect(err.code).toBe('TtsError');
  });
});

describe('feed token client methods', () => {
  it('lists, creates (with and without an edition), and revokes feed tokens', async () => {
    const { api, fetch } = client([]);
    await api.listFeedTokens();
    await api.createFeedToken({ name: 'Overcast', editionId: 'edn_1' });
    await api.createFeedToken({ name: 'All' });
    await api.revokeFeedToken('fdt_1');
    expect(call(fetch, 0)).toMatchObject({ url: '/v1.0/api/feed-tokens', method: 'GET' });
    expect(call(fetch, 1)).toMatchObject({ url: '/v1.0/api/feed-tokens', method: 'POST', body: { name: 'Overcast', editionId: 'edn_1' } });
    expect(call(fetch, 2).body).toEqual({ name: 'All', editionId: null });
    expect(call(fetch, 3)).toMatchObject({ url: '/v1.0/api/feed-tokens/fdt_1', method: 'DELETE' });
  });
});
