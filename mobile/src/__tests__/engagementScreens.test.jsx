import { act, fireEvent, screen, waitFor } from '@testing-library/react-native';
import { Linking, Share } from 'react-native';
import * as Clipboard from 'expo-clipboard';
import { router, useLocalSearchParams } from 'expo-router';
import React from 'react';
import { PlanSection, RedeemSection } from '@/screens/account/PlanSections';
import { PodcastSection } from '@/screens/account/PodcastSection';
import { AccountScreen } from '@/screens/account/AccountScreen';
import { AccuracyScreen, FeedbackInsightsScreen, InsightsScreen, PreferencesScreen, RelevanceScreen } from '@/screens/insights/InsightsScreens';
import { DownloadsScreen, ListenScreen } from '@/screens/listen/ListenScreens';
import { FindingDetail, FindingsScreen } from '@/screens/research/FindingScreens';
import { PredictionsScreen } from '@/screens/research/PredictionScreens';
import { SuggestionsScreen } from '@/screens/research/SuggestionsScreen';
import { BriefingDetail } from '@/screens/today/BriefingDetail';
import { TodayScreen } from '@/screens/today/TodayScreens';
import { fakeAudioPlayer, page, renderSignedIn } from './renderApp';

const NOW = '2026-11-19T22:41:36Z';
const CONFIG = { productName: 'My Morning News', serverVersion: '0.1.0-alpha', audioEnabled: true, emailEnabled: true, links: {}, copyright: '(c) 2026 Lattice AI LLC' };
const config = (extra) => () => ({ body: { ...CONFIG, ...extra } });

const PLAN = {
  plan: { id: 'pln_1', key: 'free', name: 'Free', description: 'Free with caps.' },
  features: { 'usage.monthlyCostUsd.max': '5.00', 'topics.max': '5', 'audio.enabled': 'true', 'runs.daily.max': 'unlimited' },
  upcoming: [{ id: 'pla_2', source: 'Promo', startsUtc: '2027-01-01T00:00:00Z', endsUtc: '2027-02-01T00:00:00Z' }],
};
const COMPLIMENTARY = { plan: { id: 'pln_2', key: 'complimentary', name: 'Complimentary' }, features: { 'topics.max': 'unlimited' }, assignment: { source: 'Promo' }, endsUtc: '2026-12-19T00:00:00Z', upcoming: [] };

describe('Account: plan and promotional codes', () => {
  it('shows the plan, its limits, and what starts later, and redeems a code', async () => {
    const { server } = await renderSignedIn(<PlanSection />, {
      'GET /v1.0/api/account/plan': () => ({ body: PLAN }),
      'POST /v1.0/api/promo-codes/redeem': () => ({ status: 201, body: COMPLIMENTARY }),
    });
    await waitFor(() => expect(screen.getByTestId('plan-current')).toBeTruthy());
    expect(screen.getByText('Free')).toBeTruthy();
    expect(screen.getByText('No end date')).toBeTruthy();
    expect(screen.getByText('Model spending per month')).toBeTruthy();
    expect(screen.getByText('$5.00')).toBeTruthy();
    expect(screen.getByText('Unlimited')).toBeTruthy();
    expect(screen.getByTestId('plan-upcoming')).toBeTruthy();
    expect(screen.getByTestId('redeem-submit').props.accessibilityState.disabled).toBe(true);
    await fireEvent.changeText(screen.getByTestId('redeem-code'), 'spring-2027');
    await fireEvent.press(screen.getByTestId('redeem-submit'));
    await waitFor(() => expect(screen.getByTestId('redeem-done')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'POST /v1.0/api/promo-codes/redeem').body).toEqual({ code: 'SPRING-2027' });
    expect(screen.getByText('Complimentary')).toBeTruthy();
    expect(screen.getByText('From a code')).toBeTruthy();
  });

  it.each([
    ['NotFound', 'That code was not found. Check the spelling and try again.'],
    ['Disabled', 'That code has been turned off.'],
    ['NotStarted', 'That code is not active yet. Try again later.'],
    ['Expired', 'That code has expired.'],
    ['Exhausted', 'That code has been used as many times as it allows.'],
    ['AlreadyRedeemed', 'You have already redeemed this code.'],
    ['NewAccountsOnly', 'That code is for new accounts only.'],
  ])('explains %s', async (reason, text) => {
    useLocalSearchParams.mockReturnValueOnce({ code: 'welcome-1' }).mockReturnValue({ code: 'welcome-1' });
    await renderSignedIn(<RedeemSection />, {
      'POST /v1.0/api/promo-codes/redeem': () => ({ status: 409, body: { error: 'PromoCodeInvalid', message: 'This code is not valid.', requestId: 'rqi_1', details: { reason } } }),
    });
    expect(screen.getByTestId('redeem-code').props.value).toBe('WELCOME-1');
    await fireEvent.press(screen.getByTestId('redeem-submit'));
    await waitFor(() => expect(screen.getByText(text)).toBeTruthy());
    useLocalSearchParams.mockReturnValue({});
  });

  it('lists plan and podcast feeds in the Account menu, hides feeds when audio is off, and keeps email under Notifications', async () => {
    await renderSignedIn(<AccountScreen />, { 'GET /v1.0/api/client-config': config({ audioEnabled: false }) });
    await waitFor(() => expect(screen.queryByTestId('account-podcast')).toBeNull());
    expect(screen.getByTestId('account-plan')).toBeTruthy();
    expect(screen.getByTestId('account-notifications')).toBeTruthy();
    expect(screen.queryByTestId('account-email')).toBeNull();
    expect(screen.queryByTestId('account-redeem')).toBeNull();
    await fireEvent.press(screen.getByTestId('account-plan'));
    expect(router.push).toHaveBeenCalledWith('/account/plan');
  });
});

describe('Account: podcast feeds', () => {
  const token = { id: 'fdt_1', name: 'iPhone', tokenPrefix: 'mmnf_abc', createdUtc: NOW };
  const created = { token: { id: 'fdt_2', name: 'Car', tokenPrefix: 'mmnf_xyz', editionId: 'edn_1', createdUtc: NOW }, secret: 'mmnf_xyz_secret', feedUrl: 'https://app.mymorningnews.ai/v1.0/public/feeds/mmnf_xyz_secret/podcast.xml' };

  it('creates a feed for an edition and offers copy, share, and open in Podcasts once', async () => {
    const open = jest.spyOn(Linking, 'openURL').mockResolvedValue(true);
    const share = jest.spyOn(Share, 'share').mockResolvedValue({ action: 'sharedAction' });
    const { server } = await renderSignedIn(<PodcastSection />, {
      'GET /v1.0/api/feed-tokens': () => ({ body: page([token]).body }),
      'GET /v1.0/api/editions': () => page([{ id: 'edn_1', name: 'Morning News' }]),
      'POST /v1.0/api/feed-tokens': () => ({ status: 201, body: created }),
    });
    await waitFor(() => expect(screen.getByTestId('feed-fdt_1')).toBeTruthy());
    await fireEvent.changeText(screen.getByTestId('feed-name'), 'Car');
    await fireEvent.press(screen.getByTestId('feed-edition-edn_1'));
    await fireEvent.press(screen.getByTestId('feed-create'));
    await waitFor(() => expect(screen.getByTestId('feed-created')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'POST /v1.0/api/feed-tokens').body).toEqual({ name: 'Car', editionId: 'edn_1' });
    expect(screen.getByTestId('feed-url').props.children).toBe(created.feedUrl);
    await fireEvent.press(screen.getByTestId('feed-copy'));
    expect(Clipboard.setStringAsync).toHaveBeenCalledWith(created.feedUrl);
    await waitFor(() => expect(screen.getByText('Copied')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('feed-share'));
    expect(share).toHaveBeenCalledWith(expect.objectContaining({ url: created.feedUrl }));
    await fireEvent.press(screen.getByTestId('feed-open'));
    expect(open).toHaveBeenCalledWith('podcast://app.mymorningnews.ai/v1.0/public/feeds/mmnf_xyz_secret/podcast.xml');
    expect(screen.getByTestId('feed-fdt_2')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('feed-dismiss'));
    expect(screen.queryByTestId('feed-created')).toBeNull();
    open.mockRestore();
    share.mockRestore();
  });

  it('revokes after a confirmation, and deletes a revoked feed', async () => {
    let tokens = [token, { id: 'fdt_9', name: 'Old', revokedUtc: NOW, createdUtc: NOW }];
    const { server } = await renderSignedIn(<PodcastSection />, {
      'GET /v1.0/api/feed-tokens': () => page(tokens),
      'GET /v1.0/api/editions': () => page([]),
      'DELETE /v1.0/api/feed-tokens/fdt_1': () => {
        tokens = tokens.map((x) => (x.id === 'fdt_1' ? { ...x, revokedUtc: NOW } : x));
        return { status: 204 };
      },
      'DELETE /v1.0/api/feed-tokens/fdt_9': () => ({ status: 204 }),
    });
    await waitFor(() => expect(screen.getByTestId('feed-revoke-fdt_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('feed-revoke-fdt_1'));
    expect(server.count('DELETE /v1.0/api/feed-tokens/fdt_1')).toBe(0);
    expect(screen.getByText('Revoke now')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('feed-revoke-fdt_1'));
    await waitFor(() => expect(server.count('DELETE /v1.0/api/feed-tokens/fdt_1')).toBe(1));
    await fireEvent.press(screen.getByTestId('feed-revoke-fdt_9'));
    await fireEvent.press(screen.getByTestId('feed-revoke-fdt_9'));
    await waitFor(() => expect(server.calls.find((c) => c.key === 'DELETE /v1.0/api/feed-tokens/fdt_9').url).toMatch(/purge=true/));
  });

  it('has no feeds when the server has audio off', async () => {
    await renderSignedIn(<PodcastSection />, { 'GET /v1.0/api/client-config': config({ audioEnabled: false }), 'GET /v1.0/api/feed-tokens': () => ({ body: page([]).body }) });
    await waitFor(() => expect(screen.getByText('Audio is turned off on this server, so there are no podcast feeds.')).toBeTruthy());
    expect(screen.queryByTestId('feed-create')).toBeNull();
  });
});

const MARKDOWN = ['# Morning News', '', 'Hello.', '', '## Top of the news', '', '- **Model release** [1]: shipped.', '', '## Sources', '', '1. [Notes](https://example.com/notes)'].join('\n');
const BRIEFING = { id: 'brf_1', kind: 'Edition', title: 'Morning News', status: 'Ready', markdown: MARKDOWN, composedUtc: NOW, createdUtc: NOW };
const SECTIONS = [
  { id: 'bsc_1', heading: 'Top of the news', source: 'findings', sortOrder: 1 },
  { id: 'bsc_2', heading: 'Sources', source: 'citations', sortOrder: 2 },
];
const EPISODE = { id: 'med_1', briefingId: 'brf_1', title: 'Morning News', status: 'Ready', artifactId: 'art_mp3', durationSeconds: 300, completedUtc: NOW, createdUtc: NOW };
const MEDIA = {
  media: EPISODE,
  chapters: [
    { id: 'chp_1', title: 'Introduction', startSeconds: 0, endSeconds: 60, sortOrder: 0 },
    { id: 'chp_2', title: 'Top of the news', startSeconds: 60, endSeconds: 300, sortOrder: 1, briefingSectionId: 'bsc_1' },
  ],
  audioPath: '/v1.0/api/artifacts/art_mp3/content',
};
const detail = ({ briefing, ...extra } = {}) => ({ briefing: { ...BRIEFING, ...briefing }, sections: SECTIONS, items: [{ id: 'bit_1', sectionId: 'bsc_1', itemType: 'Finding', itemId: 'fnd_1', citationNumber: 1 }], files: [], filesExpired: false, editionName: 'Morning News', itemTitles: { fnd_1: 'Model release' }, ...extra });

describe('Briefing audio', () => {
  it('plays ready audio from the artifact with the bearer token, lists chapters, and marks the section being read', async () => {
    const audio = fakeAudioPlayer();
    const { server, service } = await renderSignedIn(
      <BriefingDetail id="brf_1" />,
      {
        'GET /v1.0/api/briefings/brf_1': () => ({ body: detail({ briefing: { mediaAssetId: 'med_1' }, audio: EPISODE }) }),
        'GET /v1.0/api/media/med_1': () => ({ body: MEDIA }),
        'GET /v1.0/api/feedback': () => page([]),
      },
      { createAudioPlayer: audio.createAudioPlayer },
    );
    await waitFor(() => expect(screen.getByTestId('audio-chapters')).toBeTruthy());
    expect(screen.getByTestId('audio-make')).toBeTruthy();
    expect(server.count('GET /v1.0/api/findings/fnd_1')).toBe(0);
    await fireEvent.press(screen.getByTestId('audio-listen'));
    await waitFor(() => expect(audio.createAudioPlayer).toHaveBeenCalled());
    const source = audio.createAudioPlayer.mock.calls[0][0];
    expect(source.uri).toBe('http://127.0.0.1:12100/v1.0/api/artifacts/art_mp3/content');
    expect(source.headers.Authorization).toMatch(/^Bearer /);
    expect(audio.player.setActiveForLockScreen).toHaveBeenCalledWith(true, expect.objectContaining({ title: 'Morning News', albumTitle: 'Morning News' }), expect.objectContaining({ seekForwardIntervalSeconds: 30 }));
    expect(service.getState().track).toMatchObject({ briefingId: 'brf_1', mediaId: 'med_1' });
    await fireEvent.press(screen.getByTestId('chapter-1'));
    await waitFor(() => expect(service.getState().position).toBe(60));
    expect(audio.player.seekTo).toHaveBeenCalledWith(60);
    await waitFor(() => expect(screen.getByTestId('playing-heading')).toBeTruthy());
    expect(screen.getByLabelText('Now playing: Top of the news')).toBeTruthy();
    await act(() => service.stop());
  });

  it('makes audio, polls while it is being made, and explains 409s', async () => {
    let reads = 0;
    let make = 0;
    const { server } = await renderSignedIn(<BriefingDetail id="brf_1" />, {
      'GET /v1.0/api/briefings/brf_1': () => {
        reads += 1;
        return { body: detail() };
      },
      // Progress is read from the briefing's newest episode, not by reloading the whole briefing.
      'GET /v1.0/api/media': () => {
        reads += 1;
        return page([reads > 2 ? EPISODE : { ...EPISODE, status: 'Synthesizing' }]);
      },
      'POST /v1.0/api/briefings/brf_1/audio': () => {
        make += 1;
        if (make === 1) return { status: 409, body: { error: 'AudioDisabled', message: 'Audio is turned off on this server (tts.enabled).', requestId: 'rqi_2' } };
        return { status: 202, body: { ...EPISODE, status: 'Queued' } };
      },
      'GET /v1.0/api/media/med_1': () => ({ body: MEDIA }),
      'GET /v1.0/api/feedback': () => page([]),
    });
    await waitFor(() => expect(screen.getByTestId('audio-make')).toBeTruthy());
    expect(screen.getByText('Make audio')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('audio-make'));
    await waitFor(() => expect(screen.getByText('Audio is turned off on this server.')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('audio-make'));
    await waitFor(() => expect(screen.getByTestId('audio-pending')).toBeTruthy());
    expect(screen.getByText('Waiting to start.')).toBeTruthy();
    await waitFor(() => expect(screen.getByTestId('audio-listen')).toBeTruthy(), { timeout: 12000 });
    expect(server.count('POST /v1.0/api/briefings/brf_1/audio')).toBe(2);
    expect(server.count('GET /v1.0/api/briefings/brf_1')).toBe(1);
  }, 20000);

  it('hides Make audio when the server has audio off', async () => {
    await renderSignedIn(<BriefingDetail id="brf_1" />, {
      'GET /v1.0/api/client-config': config({ audioEnabled: false }),
      'GET /v1.0/api/briefings/brf_1': () => ({ body: detail() }),
      'GET /v1.0/api/feedback': () => page([]),
    });
    await waitFor(() => expect(screen.getByTestId('briefing-body')).toBeTruthy());
    await act(async () => undefined);
    expect(screen.queryByTestId('audio-make')).toBeNull();
    expect(screen.queryByTestId('briefing-audio')).toBeNull();
  });

  it('shows a failed episode with Try again', async () => {
    await renderSignedIn(<BriefingDetail id="brf_1" />, {
      'GET /v1.0/api/briefings/brf_1': () => ({ body: detail({ audio: { ...EPISODE, status: 'Failed', errorMessage: 'The speech server did not answer.' } }) }),
      'GET /v1.0/api/feedback': () => page([]),
    });
    await waitFor(() => expect(screen.getByTestId('audio-failed')).toBeTruthy());
    expect(screen.getByText('The speech server did not answer.')).toBeTruthy();
    expect(screen.getByText('Try again')).toBeTruthy();
  });

  it('rates the findings a briefing cites, inline', async () => {
    const { server } = await renderSignedIn(<BriefingDetail id="brf_1" />, {
      'GET /v1.0/api/briefings/brf_1': () => ({ body: detail() }),
      'GET /v1.0/api/feedback': () => page([]),
      'POST /v1.0/api/feedback': (call) => ({ status: 201, body: { id: 'fbk_1', ...call.body, relevance: 'Relevant', isCurrent: true } }),
    });
    await waitFor(() => expect(screen.getByTestId('rating-fnd_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('rating-fnd_1-relevant'));
    await waitFor(() => expect(server.count('POST /v1.0/api/feedback')).toBe(1));
    expect(server.calls.find((c) => c.key === 'POST /v1.0/api/feedback').body).toEqual({ targetType: 'Finding', targetId: 'fnd_1', action: 'relevant', channel: 'Mobile' });
    await waitFor(() => expect(screen.getByTestId('rating-fnd_1-relevant').props.accessibilityState.selected).toBe(true));
  });
});

describe('Listen', () => {
  it('lists ready episodes and plays one with its chapters', async () => {
    const audio = fakeAudioPlayer();
    const { server, service } = await renderSignedIn(
      <ListenScreen />,
      {
        'GET /v1.0/api/media': () => page([EPISODE]),
        'GET /v1.0/api/media/med_1': () => ({ body: MEDIA }),
      },
      { createAudioPlayer: audio.createAudioPlayer },
    );
    await waitFor(() => expect(screen.getByTestId('episode-med_1')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'GET /v1.0/api/media').url).toMatch(/status=Ready/);
    await fireEvent.press(screen.getByTestId('episode-med_1'));
    await waitFor(() => expect(router.push).toHaveBeenCalledWith('/listen/player'));
    expect(service.getState().track.chapters).toHaveLength(2);
    await fireEvent.press(screen.getByTestId('episode-open-med_1'));
    expect(router.push).toHaveBeenCalledWith('/today/briefings/brf_1');
    await fireEvent.press(screen.getByTestId('open-feeds'));
    expect(router.push).toHaveBeenCalledWith('/account/podcast');
    await act(() => service.stop());
  });

  it('plays a download offline from the downloads screen', async () => {
    const audio = fakeAudioPlayer();
    const { useFocusEffect } = require('expo-router');
    useFocusEffect.mockImplementation((fn) => React.useEffect(fn, [fn]));
    const { File } = require('expo-file-system');
    const original = Object.getOwnPropertyDescriptor(File.prototype, 'exists');
    Object.defineProperty(File.prototype, 'exists', { configurable: true, get: () => true });
    const AsyncStorage = require('@react-native-async-storage/async-storage');
    await AsyncStorage.setItem('mymorningnews.downloads', JSON.stringify({ brf_1: { title: 'Morning News', uri: 'file:///docs/audio/brf_1.mp3', bytes: 1000, downloadedUtc: NOW, meta: { briefingId: 'brf_1', mediaId: 'med_1', chapters: [{ title: 'Introduction', startSeconds: 0 }], durationSeconds: 300 } } }));
    const { service } = await renderSignedIn(<DownloadsScreen />, {}, { createAudioPlayer: audio.createAudioPlayer });
    await waitFor(() => expect(screen.getByTestId('download-brf_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('download-brf_1'));
    await waitFor(() => expect(audio.createAudioPlayer).toHaveBeenCalled());
    expect(audio.createAudioPlayer.mock.calls[0][0]).toEqual({ uri: 'file:///docs/audio/brf_1.mp3', headers: undefined });
    expect(service.getState().track).toMatchObject({ briefingId: 'brf_1', mediaId: 'med_1' });
    await act(() => service.stop());
    if (original) Object.defineProperty(File.prototype, 'exists', original);
    else delete File.prototype.exists;
    useFocusEffect.mockReset();
    await AsyncStorage.clear();
  });
});

const FINDING = { id: 'fnd_1', runId: 'run_1', topicId: 'top_1', title: 'Model release', summary: 'Something shipped.', tier: 'MustRead', relevanceScore: 86, confidence: 0.8, createdUtc: NOW };

describe('Feedback', () => {
  it('shows the current rating on a finding, saves a reason in the why sheet, and retracts', async () => {
    let current = { id: 'fbk_1', targetType: 'Finding', targetId: 'fnd_1', relevance: 'SomewhatRelevant', isCurrent: true, createdUtc: NOW };
    const { server } = await renderSignedIn(<FindingDetail id="fnd_1" />, {
      'GET /v1.0/api/findings/fnd_1': () => ({ body: { finding: FINDING, evidence: [] } }),
      'GET /v1.0/api/feedback': (call) => {
        expect(call.url).toMatch(/targetType=Finding&targetId=fnd_1/);
        return page(current ? [current] : []);
      },
      'POST /v1.0/api/feedback': (call) => {
        current = { id: 'fbk_2', targetType: 'Finding', targetId: 'fnd_1', relevance: call.body.action === 'not-relevant' ? 'NotRelevant' : 'Relevant', reason: call.body.reason, isCurrent: true, createdUtc: NOW };
        return { status: 201, body: current };
      },
      'DELETE /v1.0/api/feedback/fbk_2': () => ({ status: 204 }),
    });
    await waitFor(() => expect(screen.getByTestId('feedback-bar')).toBeTruthy());
    expect(screen.getByText('Was this relevant?')).toBeTruthy();
    expect(screen.getByTestId('feedback-bar-somewhat').props.accessibilityState.selected).toBe(true);
    await fireEvent.press(screen.getByTestId('feedback-bar-why'));
    await waitFor(() => expect(screen.getByTestId('why-sheet')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('why-action-not-relevant'));
    await fireEvent.changeText(screen.getByTestId('why-reason'), 'Vendor funding news');
    await fireEvent.press(screen.getByTestId('why-save'));
    await waitFor(() => expect(server.count('POST /v1.0/api/feedback')).toBe(1));
    expect(server.calls.find((c) => c.key === 'POST /v1.0/api/feedback').body).toEqual({ targetType: 'Finding', targetId: 'fnd_1', action: 'not-relevant', reason: 'Vendor funding news', channel: 'Mobile' });
    await waitFor(() => expect(screen.getByTestId('feedback-bar-reason')).toBeTruthy());
    expect(screen.getByText('Why: Vendor funding news')).toBeTruthy();
    expect(screen.getByTestId('feedback-bar-not-relevant').props.accessibilityState.selected).toBe(true);
    await fireEvent.press(screen.getByTestId('feedback-bar-why'));
    await waitFor(() => expect(screen.getByTestId('why-retract')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('why-retract'));
    await waitFor(() => expect(server.count('DELETE /v1.0/api/feedback/fbk_2')).toBe(1));
    await waitFor(() => expect(screen.getByTestId('feedback-bar-not-relevant').props.accessibilityState.selected).toBe(false));
  });

  it('rates findings, predictions, and suggestions from their lists', async () => {
    const { server } = await renderSignedIn(<FindingsScreen />, {
      'GET /v1.0/api/findings': () => page([FINDING]),
      'GET /v1.0/api/feedback': () => page([{ id: 'fbk_1', targetType: 'Finding', targetId: 'fnd_1', relevance: 'Relevant', isCurrent: true }]),
      'POST /v1.0/api/feedback': (call) => ({ status: 201, body: { id: 'fbk_2', ...call.body, relevance: 'NotRelevant', isCurrent: true } }),
    });
    await waitFor(() => expect(screen.getByTestId('rating-fnd_1-relevant').props.accessibilityState.selected).toBe(true));
    expect(server.calls.find((c) => c.key === 'GET /v1.0/api/feedback').url).toMatch(/targetType=Finding/);
    await fireEvent.press(screen.getByTestId('rating-fnd_1-not-relevant'));
    await waitFor(() => expect(screen.getByTestId('rating-fnd_1-not-relevant').props.accessibilityState.selected).toBe(true));
    screen.unmount();

    const predictions = await renderSignedIn(<PredictionsScreen />, {
      'GET /v1.0/api/predictions': () => page([{ id: 'prd_1', statement: 'It ships by June', status: 'Open', confidence: 0.7, horizonUtc: '2026-01-01T00:00:00Z', createdUtc: NOW }]),
      'GET /v1.0/api/feedback': () => page([]),
      'POST /v1.0/api/feedback': (call) => ({ status: 201, body: { id: 'fbk_3', ...call.body, accuracy: 'TooEarly', isCurrent: true } }),
    });
    await waitFor(() => expect(screen.getByTestId('rating-prd_1-too-early')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('rating-prd_1-too-early'));
    await waitFor(() => expect(predictions.server.count('POST /v1.0/api/feedback')).toBe(1));
    expect(predictions.server.calls.find((c) => c.key === 'POST /v1.0/api/feedback').body).toMatchObject({ targetType: 'Prediction', action: 'too-early', channel: 'Mobile' });
    screen.unmount();

    const suggestions = await renderSignedIn(<SuggestionsScreen />, {
      'GET /v1.0/api/suggestions': () => page([{ id: 'sug_1', title: 'Try the new SDK', status: 'New', priority: 'High', createdUtc: NOW }]),
      'GET /v1.0/api/feedback': () => page([]),
      'POST /v1.0/api/feedback': (call) => ({ status: 201, body: { id: 'fbk_4', ...call.body, usefulness: 'Useful', isCurrent: true } }),
    });
    await waitFor(() => expect(screen.getByTestId('rating-sug_1-useful')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('rating-sug_1-useful'));
    await waitFor(() => expect(suggestions.server.calls.find((c) => c.key === 'POST /v1.0/api/feedback').body).toMatchObject({ targetType: 'Suggestion', action: 'useful' }));
  });

  it('says a rating failed without losing the screen', async () => {
    await renderSignedIn(<FindingDetail id="fnd_1" />, {
      'GET /v1.0/api/findings/fnd_1': () => ({ body: { finding: FINDING, evidence: [] } }),
      'GET /v1.0/api/feedback': () => page([]),
      'POST /v1.0/api/feedback': () => ({ status: 400, body: { error: 'ValidationFailed', message: 'That rating does not apply to a Finding.', requestId: 'rqi_9' } }),
    });
    await waitFor(() => expect(screen.getByTestId('feedback-bar-relevant')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('feedback-bar-relevant'));
    await waitFor(() => expect(screen.getByText('That rating does not apply to a Finding.')).toBeTruthy());
  });
});

const RELEVANCE = {
  shown: 40,
  rated: 20,
  precision: 0.75,
  coverage: 0.5,
  byTopic: [{ key: 'top_1', label: 'AI tools', count: 20, positive: 15, rate: 0.75 }],
  byTier: [{ key: 'MustRead', count: 10, positive: 9, rate: 0.9 }],
  byDomain: [{ key: 'example.com', count: 5, positive: 4, rate: 0.8 }],
  byDimension: [],
  byDay: [
    { key: '2026-11-17', count: 5, positive: 3, rate: 0.6 },
    { key: '2026-11-18', count: 5, positive: 4, rate: 0.8 },
  ],
};

const OVERVIEW = { runs: 12, runsCompleted: 10, runsPartiallySucceeded: 1, runsFailed: 1, findingsShown: 40, findingsRated: 10, relevancePrecision: 0.75, briefingsReady: 9, predictionsOpen: 3, predictionsResolved: 4, predictionAccuracy: 0.5, meanBrier: 0.2, feedbackTotal: 14, feedbackWithReason: 5 };

describe('Insights', () => {
  it('shows the overview tiles above the Insights menu for the chosen window', async () => {
    const { server } = await renderSignedIn(<InsightsScreen />, { 'GET /v1.0/api/analytics/overview': () => ({ body: OVERVIEW }) });
    await waitFor(() => expect(screen.getByTestId('overview-tiles')).toBeTruthy());
    expect(screen.getByLabelText('Runs, 12, 10 completed, 1 failed')).toBeTruthy();
    expect(screen.getByLabelText('Findings shown, 40, 10 rated')).toBeTruthy();
    expect(screen.getByLabelText('Relevance precision, 75%, Rated findings you found relevant')).toBeTruthy();
    expect(screen.getByLabelText('Prediction accuracy, 50%, 4 resolved, 3 open')).toBeTruthy();
    expect(screen.getByLabelText('Feedback, 14, 5 with a reason')).toBeTruthy();
    expect(server.calls.find((c) => c.key === 'GET /v1.0/api/analytics/overview').url).toMatch(/from=.*&to=/);
    await fireEvent.press(screen.getByTestId('range-30'));
    await waitFor(() => expect(server.count('GET /v1.0/api/analytics/overview')).toBe(2));
  });

  it('shows the overview empty, and says when the server has no overview yet', async () => {
    await renderSignedIn(<InsightsScreen />, { 'GET /v1.0/api/analytics/overview': () => ({ body: { runs: 0, findingsShown: 0, feedbackTotal: 0, predictionsOpen: 0, predictionsResolved: 0 } }) });
    await waitFor(() => expect(screen.getByText('The overview fills in after your first research runs.')).toBeTruthy());
    screen.unmount();
    await renderSignedIn(<InsightsScreen />, {});
    await waitFor(() => expect(screen.getByText('Not on this server yet')).toBeTruthy());
  });

  it('shows the overview on Today with a link to Insights', async () => {
    const { server } = await renderSignedIn(<TodayScreen />, {
      'GET /v1.0/api/editions': () => page([]),
      'GET /v1.0/api/briefings': () => page([]),
      'GET /v1.0/api/analytics/overview': () => ({ body: OVERVIEW }),
    });
    await waitFor(() => expect(screen.getByTestId('today-overview')).toBeTruthy());
    expect(screen.getByText('At a glance')).toBeTruthy();
    expect(screen.getByLabelText('Prediction accuracy, 50%, 4 resolved, 3 open')).toBeTruthy();
    const call = server.calls.find((c) => c.key === 'GET /v1.0/api/analytics/overview');
    const from = Date.parse(decodeURIComponent(call.url.match(/from=([^&]+)/)[1]));
    const to = Date.parse(decodeURIComponent(call.url.match(/to=([^&]+)/)[1]));
    expect(Math.round((to - from) / 86400000)).toBe(30);
    await fireEvent.press(screen.getByTestId('today-insights'));
    expect(router.push).toHaveBeenCalledWith('/insights');
  });

  it('shows relevance tiles, the precision trend, breakdowns, and evidence quality', async () => {
    const { server } = await renderSignedIn(<RelevanceScreen />, {
      'GET /v1.0/api/analytics/relevance': () => ({ body: RELEVANCE }),
      'GET /v1.0/api/analytics/quality': () => ({ body: { quotes: 50, verified: 48, failureRate: 0.04, byModel: [{ key: 'claude-opus-5-5', count: 50, positive: 48, rate: 0.96 }], byDay: [] } }),
    });
    await waitFor(() => expect(screen.getByTestId('relevance-tiles')).toBeTruthy());
    expect(screen.getByLabelText('Precision, 75%, Rated findings you found relevant')).toBeTruthy();
    expect(screen.getByLabelText(/^Precision over time\. Nov 17: 60%, Nov 18: 80%$/)).toBeTruthy();
    expect(screen.getByLabelText(/^Precision by tier\. Must read \(10\): 90%$/)).toBeTruthy();
    expect(screen.getByText('Nothing in this period yet.')).toBeTruthy();
    expect(screen.getByLabelText(/^Verified share by model\. claude-opus-5-5 \(50\)?: 96%$|^Verified share by model\. claude-opus-5-5: 96%$/)).toBeTruthy();
    const first = server.calls.find((c) => c.key === 'GET /v1.0/api/analytics/relevance');
    expect(first.url).toMatch(/from=.*&to=/);
    await fireEvent.press(screen.getByTestId('range-30'));
    await waitFor(() => expect(server.count('GET /v1.0/api/analytics/relevance')).toBe(2));
  });

  it('shows an empty state before there is data', async () => {
    await renderSignedIn(<RelevanceScreen />, {
      'GET /v1.0/api/analytics/relevance': () => ({ body: { shown: 0, rated: 0, byTopic: [], byTier: [], byDomain: [], byDimension: [], byDay: [] } }),
      'GET /v1.0/api/analytics/quality': () => ({ body: { quotes: 0, verified: 0, byModel: [], byDay: [] } }),
    });
    await waitFor(() => expect(screen.getByText('Relevance fills in after you rate a few findings.')).toBeTruthy());
  });

  it('shows accuracy with a calibration chart and links to due predictions', async () => {
    await renderSignedIn(<AccuracyScreen />, {
      'GET /v1.0/api/analytics/accuracy': () => ({
        body: {
          total: 12,
          open: 4,
          dueForReview: 2,
          resolved: 8,
          unresolvable: 1,
          accuracyRate: 0.625,
          meanBrier: 0.18,
          reviewsDecided: 5,
          agentAgreement: 0.8,
          meanResolutionLatencyDays: 3.4,
          calibration: [
            { lower: 0, upper: 0.2, count: 0 },
            { lower: 0.6, upper: 0.8, count: 5, meanConfidence: 0.7, observed: 0.6 },
            { lower: 0.8, upper: 1, count: 3, meanConfidence: 0.9, observed: 0.33 },
          ],
          byTopic: [{ key: 'top_1', label: 'AI tools', count: 8, positive: 5, rate: 0.625, value: 0.18 }],
        },
      }),
    });
    await waitFor(() => expect(screen.getByTestId('calibration')).toBeTruthy());
    expect(screen.getByLabelText('Accuracy rate, 63%, Accurate, plus half of partially accurate')).toBeTruthy();
    expect(screen.getByLabelText('Brier score, 0.18, Lower is better (0 is perfect)')).toBeTruthy();
    expect(screen.getByText('60% to 80%: 5 predictions, 60% came true')).toBeTruthy();
    expect(screen.getByLabelText(/^Calibration\. 60% to 80%: 5 predictions, 60% came true, 80% to 100%: 3 predictions, 33% came true$/)).toBeTruthy();
    expect(screen.getByText('3 days')).toBeTruthy();
    await fireEvent.press(screen.getByTestId('grade-due'));
    expect(router.push).toHaveBeenCalledWith('/today/predictions');
  });

  it('shows feedback volume, channels, themes, suggestion outcomes, and the history with edit and retract', async () => {
    let items = [
      { id: 'fbk_1', targetType: 'Finding', targetId: 'fnd_1', relevance: 'Relevant', reason: 'Exactly my stack', channel: 'Mobile', isCurrent: true, createdUtc: NOW },
      { id: 'fbk_2', targetType: 'Prediction', targetId: 'prd_1', accuracy: 'Inaccurate', channel: 'Email', isCurrent: true, createdUtc: NOW },
      { id: 'fbk_3', targetType: 'Finding', targetId: 'fnd_2', relevance: 'Somewhat', channel: 'Mcp', isCurrent: true, createdUtc: NOW },
    ];
    const { server } = await renderSignedIn(<FeedbackInsightsScreen />, {
      'GET /v1.0/api/analytics/feedback': () => ({
        body: { total: 2, withReason: 1, byChannel: [{ key: 'Mobile', count: 1 }, { key: 'Email', count: 1 }, { key: 'Mcp', count: 1 }], byTargetType: [{ key: 'Finding', count: 1 }], byDay: [{ key: '2026-11-19', count: 2 }], emailLinkOpens: 3, emailLinkSubmits: 1 },
      }),
      'GET /v1.0/api/analytics/reasons': () => ({ body: { tags: [{ key: 'too basic', count: 3 }], byWeek: [] } }),
      'GET /v1.0/api/analytics/suggestions': () => ({ body: { total: 4, new: 1, accepted: 1, done: 1, dismissed: 1, snoozed: 0, meanDaysToDecision: 2.5, byPriority: [] } }),
      'GET /v1.0/api/feedback': () => ({ body: { objects: items, pageNumber: 1, pageSize: 50, totalRecords: items.length, totalPages: 1 } }),
      'PUT /v1.0/api/feedback/fbk_1': (call) => {
        items = items.map((x) => (x.id === 'fbk_1' ? { ...x, reason: call.body.reason } : x));
        return { body: items[0] };
      },
      'DELETE /v1.0/api/feedback/fbk_2': () => {
        items = items.filter((x) => x.id !== 'fbk_2');
        return { status: 204 };
      },
    });
    await waitFor(() => expect(screen.getByTestId('feedback-tiles')).toBeTruthy());
    expect(screen.getByLabelText(/^By channel\. This app: 1, Email: 1, MCP client: 1$/)).toBeTruthy();
    expect(screen.getByLabelText(/^Reason themes\. too basic: 3$/)).toBeTruthy();
    expect(screen.getByText('You decide in 2.5 days on average.')).toBeTruthy();
    await waitFor(() => expect(screen.getByTestId('history-fbk_1')).toBeTruthy());
    expect(screen.getByText('Exactly my stack')).toBeTruthy();
    expect(screen.getAllByText('MCP client').length).toBeGreaterThanOrEqual(1);
    await fireEvent.press(screen.getByTestId('history-edit-fbk_1'));
    await waitFor(() => expect(screen.getByTestId('why-reason')).toBeTruthy());
    await fireEvent.changeText(screen.getByTestId('why-reason'), 'Exactly my stack, and timely');
    await fireEvent.press(screen.getByTestId('why-save'));
    await waitFor(() => expect(screen.getByText('Exactly my stack, and timely')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'PUT /v1.0/api/feedback/fbk_1').body).toEqual({ reason: 'Exactly my stack, and timely' });
    await fireEvent.press(screen.getByTestId('history-edit-fbk_2'));
    await waitFor(() => expect(screen.getByTestId('why-retract')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('why-retract'));
    await waitFor(() => expect(screen.queryByTestId('history-fbk_2')).toBeNull());
    await fireEvent.press(screen.getByTestId('history-open-fbk_1'));
    expect(router.push).toHaveBeenCalledWith('/today/findings/fnd_1');
    await fireEvent.press(screen.getByTestId('history-filter-Prediction'));
    await waitFor(() => expect(server.calls.some((c) => c.key === 'GET /v1.0/api/feedback' && c.url.includes('targetType=Prediction'))).toBe(true));
  });

  it('accepts, rejects, disables, enables, edits, deletes, and writes preferences', async () => {
    let prefs = [
      { id: 'lpr_1', statement: 'Skip vendor funding news', kind: 'Avoid', status: 'Proposed', origin: 'Distilled', confidence: 0.7, createdUtc: NOW },
      { id: 'lpr_2', statement: 'Prefer primary sources', kind: 'PreferSource', status: 'Active', origin: 'User', topicId: 'top_1', createdUtc: NOW },
      { id: 'lpr_3', statement: 'Shorter summaries', kind: 'StyleNote', status: 'Disabled', origin: 'User', createdUtc: NOW },
      { id: 'lpr_4', statement: 'More benchmarks', kind: 'Prefer', status: 'Proposed', origin: 'Distilled', confidence: 0.4, createdUtc: NOW },
    ];
    const act = (id, status) => () => {
      prefs = prefs.map((x) => (x.id === id ? { ...x, status, lastUpdateUtc: 'x' + status } : x));
      return { body: prefs.find((x) => x.id === id) };
    };
    const { server } = await renderSignedIn(<PreferencesScreen />, {
      'GET /v1.0/api/learned-preferences': () => page(prefs),
      'GET /v1.0/api/topics': () => page([{ id: 'top_1', name: 'AI tools' }]),
      'POST /v1.0/api/learned-preferences/lpr_1/accept': act('lpr_1', 'Active'),
      'POST /v1.0/api/learned-preferences/lpr_4/reject': act('lpr_4', 'Rejected'),
      'POST /v1.0/api/learned-preferences/lpr_2/disable': act('lpr_2', 'Disabled'),
      'POST /v1.0/api/learned-preferences/lpr_3/enable': act('lpr_3', 'Active'),
      'PUT /v1.0/api/learned-preferences/lpr_3': (call) => ({ body: { ...prefs.find((x) => x.id === 'lpr_3'), statement: call.body.statement, lastUpdateUtc: 'edited' } }),
      'DELETE /v1.0/api/learned-preferences/lpr_4': () => ({ status: 204 }),
      'POST /v1.0/api/learned-preferences': (call) => ({ status: 201, body: { id: 'lpr_5', status: 'Active', origin: 'User', createdUtc: NOW, ...call.body } }),
    });
    await waitFor(() => expect(screen.getByTestId('preference-lpr_1')).toBeTruthy());
    expect(screen.getByText('2 proposals wait for your decision.')).toBeTruthy();
    expect(screen.getAllByText('AI tools').length).toBe(2);
    await fireEvent.press(screen.getByTestId('preference-accept-lpr_1'));
    await waitFor(() => expect(screen.getByTestId('preference-disable-lpr_1')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('preference-reject-lpr_4'));
    await waitFor(() => expect(screen.queryByTestId('preference-reject-lpr_4')).toBeNull());
    await fireEvent.press(screen.getByTestId('preference-disable-lpr_2'));
    await waitFor(() => expect(screen.getByTestId('preference-enable-lpr_2')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('preference-enable-lpr_3'));
    await waitFor(() => expect(screen.getByTestId('preference-disable-lpr_3')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('preference-edit-lpr_3'));
    await fireEvent.changeText(screen.getByTestId('preference-text-lpr_3'), 'Two-sentence summaries');
    await fireEvent.press(screen.getByTestId('preference-save-lpr_3'));
    await waitFor(() => expect(screen.getByText('Two-sentence summaries')).toBeTruthy());
    await fireEvent.press(screen.getByTestId('preference-delete-lpr_4'));
    expect(server.count('DELETE /v1.0/api/learned-preferences/lpr_4')).toBe(0);
    await fireEvent.press(screen.getByTestId('preference-delete-lpr_4'));
    await waitFor(() => expect(screen.queryByTestId('preference-lpr_4')).toBeNull());
    await fireEvent.changeText(screen.getByTestId('preference-statement'), 'No crypto');
    await fireEvent.press(screen.getByTestId('preference-kind-Avoid'));
    await fireEvent.press(screen.getByTestId('preference-topic-top_1'));
    await fireEvent.press(screen.getByTestId('preference-add'));
    await waitFor(() => expect(screen.getByTestId('preference-lpr_5')).toBeTruthy());
    expect(server.calls.find((c) => c.key === 'POST /v1.0/api/learned-preferences').body).toEqual({ statement: 'No crypto', kind: 'Avoid', topicId: 'top_1' });
    await fireEvent.press(screen.getByTestId('preferences-filter-Proposed'));
    await waitFor(() => expect(server.calls.some((c) => c.key === 'GET /v1.0/api/learned-preferences' && c.url.includes('status=Proposed'))).toBe(true));
  });
});
