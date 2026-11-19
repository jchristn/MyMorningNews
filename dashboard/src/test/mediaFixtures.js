import { vi } from 'vitest';

// Fixtures and jsdom shims for the audio player, briefing audio, Listen, Voices, and podcast feed tests.
export const EPISODE = {
  id: 'med_1',
  tenantId: 'default',
  userId: 'usr_admin',
  briefingId: 'brf_1',
  editionId: 'edn_1',
  kind: 'BriefingAudio',
  status: 'Ready',
  voiceProfileId: 'voi_1',
  title: 'Morning News for November 19, 2026',
  scriptFromModel: true,
  artifactId: 'art_mp3',
  chaptersArtifactId: 'art_chapters',
  contentType: 'audio/mpeg',
  bytes: 2400000,
  durationSeconds: 240,
  segmentCount: 6,
  attempts: 1,
  completedUtc: '2026-11-19T06:35:00Z',
  createdUtc: '2026-11-19T06:31:00Z',
  lastUpdateUtc: '2026-11-19T06:35:00Z'
};

export const CHAPTERS = [
  { id: 'chp_1', mediaAssetId: 'med_1', briefingSectionId: 'bsc_1', title: 'Top of the news', startSeconds: 0, endSeconds: 120, sortOrder: 0 },
  { id: 'chp_2', mediaAssetId: 'med_1', briefingSectionId: 'bsc_2', title: 'Sources', startSeconds: 120, endSeconds: 240, sortOrder: 1 }
];

export function mediaDetail(overrides = {}) {
  return { media: EPISODE, chapters: CHAPTERS, audioPath: '/v1.0/api/artifacts/art_mp3/content', chaptersPath: '/v1.0/api/artifacts/art_chapters/content', ...overrides };
}

export const VOICE = {
  id: 'voi_1',
  tenantId: 'default',
  name: 'Kokoro narrator',
  provider: 'OpenAiCompatibleSpeech',
  model: 'kokoro',
  voice: 'af_heart',
  secondaryVoice: 'am_adam',
  speed: 1,
  isDefault: true,
  active: true,
  createdUtc: '2026-11-18T06:30:00Z',
  lastUpdateUtc: '2026-11-18T06:30:00Z'
};

export const FEED_TOKEN = {
  id: 'fdt_1',
  tenantId: 'default',
  userId: 'usr_admin',
  name: 'Overcast',
  tokenHash: 'redacted',
  tokenPrefix: 'mmnf_ab12',
  editionId: 'edn_1',
  lastUsedUtc: '2026-11-19T07:00:00Z',
  active: true,
  createdUtc: '2026-11-18T06:30:00Z',
  lastUpdateUtc: '2026-11-19T07:00:00Z'
};

export function audioResult() {
  return { blob: new Blob(['ID3'], { type: 'audio/mpeg' }), filename: 'episode.mp3', contentType: 'audio/mpeg', status: 200 };
}

// jsdom has no media playback or object URLs. play() and pause() dispatch their events so the player state follows.
export function installMediaShims() {
  URL.createObjectURL = vi.fn(() => 'blob:audio-1');
  URL.revokeObjectURL = vi.fn();
  vi.spyOn(HTMLMediaElement.prototype, 'play').mockImplementation(function play() {
    this.dispatchEvent(new Event('play'));
    return Promise.resolve();
  });
  vi.spyOn(HTMLMediaElement.prototype, 'pause').mockImplementation(function pause() {
    this.dispatchEvent(new Event('pause'));
  });
}

// Report a playback position or duration from the audio element the way a browser would.
export function setMediaProperty(audio, name, value) {
  Object.defineProperty(audio, name, { value, writable: true, configurable: true });
}
