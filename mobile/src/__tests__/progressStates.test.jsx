import { act, fireEvent, screen, waitFor, within } from '@testing-library/react-native';
import React, { useEffect, useState } from 'react';
import { StyleSheet } from 'react-native';
import { AUDIO_LONG_WAIT_MS, AUDIO_POLL_MS, BriefingAudio, newestEpisode } from '@/components/player/BriefingAudio';
import { Banner, BusyLine, Card } from '@/components/ui';
import { BriefingDetail } from '@/screens/today/BriefingDetail';
import { ComposeStatus } from '@/screens/today/TodayScreens';
import { useResource } from '@/state/useResource';
import { page, renderSignedIn } from './renderApp';

const LONG = 'This usually takes under a minute. You can leave this screen; the briefing appears in Briefings when it is ready. '.repeat(4);
const BRIEFING = { id: 'brf_1', kind: 'Edition', title: 'Morning News', status: 'Ready', markdown: '# Morning News\n\nHello.', createdUtc: '2026-11-19T22:41:36Z' };
const POLL = 30;

const ago = (ms) => new Date(Date.now() - ms).toISOString();
const episode = (status, extra) => ({ id: 'med_1', briefingId: 'brf_1', title: 'Morning News', status, createdUtc: ago(1000), ...extra });
const pause = (ms) => act(() => new Promise((resolve) => setTimeout(resolve, ms)));

/**
 * Fails unless `node` can wrap inside `container`: on the way up, nothing has a fixed width, and inside every row the
 * element on the path gives way (flex or flexShrink). Text placed straight into a row keeps its one-line width in
 * React Native and runs out of its card, which is what this guards.
 */
function expectWrapsInside(node, container) {
  let child = node;
  let rows = 0;
  for (let parent = node.parent; child !== container; child = parent, parent = parent.parent) {
    expect(parent).toBeTruthy();
    const style = StyleSheet.flatten(parent.props.style) || {};
    const own = StyleSheet.flatten(child.props.style) || {};
    expect(typeof own.width === 'number').toBe(false);
    if (style.flexDirection === 'row' && style.flexWrap !== 'wrap') {
      rows += 1;
      expect([own.flex >= 1 || own.flexShrink >= 1, own.minWidth === undefined || own.minWidth === 0]).toEqual([true, true]);
    }
  }
  return rows;
}

describe('progress text stays inside its card', () => {
  it('wraps the composing paragraph inside the Today compose card', async () => {
    await renderSignedIn(
      <Card testID="card">
        <ComposeStatus compose={{ status: 'composing', briefing: null, error: null }} />
      </Card>,
    );
    const card = screen.getByTestId('card');
    expect(within(card).getByText('Composing your briefing')).toBeTruthy();
    const body = within(card).getByText(/This usually takes under a minute/);
    // The banner row (icon and content) and the spinner row both hold the paragraph.
    expect(expectWrapsInside(body, card)).toBe(2);
    expect(within(screen.getByTestId('compose-progress')).getByText(/This usually takes under a minute/)).toBeTruthy();
  });

  it('wraps any long text beside a spinner, and a long banner body or title', async () => {
    await renderSignedIn(
      <Card testID="card">
        <BusyLine>{LONG}</BusyLine>
        <Banner kind="danger" title={'Title ' + LONG}>
          {'Body ' + LONG}
        </Banner>
      </Card>,
    );
    const card = screen.getByTestId('card');
    expect(expectWrapsInside(screen.getByText(LONG), card)).toBe(1);
    expect(expectWrapsInside(screen.getByText('Title ' + LONG), card)).toBe(1);
    expect(expectWrapsInside(screen.getByText('Body ' + LONG), card)).toBe(1);
  });

  it('wraps the still-composing and making-audio lines in the briefing reader', async () => {
    await renderSignedIn(<BriefingDetail id="brf_1" />, {
      'GET /v1.0/api/briefings/brf_1': () => ({ body: { briefing: { ...BRIEFING, status: 'Composing' }, audio: episode('Scripting'), sections: [], items: [], files: [] } }),
      'GET /v1.0/api/media': () => page([episode('Scripting')]),
      'GET /v1.0/api/feedback': () => page([]),
    });
    await waitFor(() => expect(screen.getByTestId('briefing-pending')).toBeTruthy());
    const pending = screen.getByTestId('briefing-pending');
    // Every line of app text (the icon glyph is a fixed-size Text of its own).
    const textsIn = (node) => within(node).getAllByText(/./).filter((text) => text.props.maxFontSizeMultiplier);
    expect(textsIn(pending).map((line) => expectWrapsInside(line, pending))).toEqual([1, 2]);
    const audio = screen.getByTestId('audio-pending');
    expect(textsIn(audio).length).toBeGreaterThanOrEqual(4);
    for (const text of textsIn(audio)) expectWrapsInside(text, audio);
    expect(expectWrapsInside(within(audio).getByText('Writing the script.'), audio)).toBe(2);
  });
});

function Harness({ initial, onDetail, ...props }) {
  const [detail, setDetail] = useState(initial);
  onDetail(detail);
  return <BriefingAudio detail={detail} onChanged={setDetail} pollMs={POLL} {...props} />;
}

/** Render the audio block for a ready briefing against a scripted server whose newest episode is `server.now`. */
async function renderAudio({ audio = null, handlers = {}, ...props } = {}) {
  const script = { now: audio, makes: 0 };
  const seen = { detail: null };
  const utils = await renderSignedIn(<Harness initial={{ briefing: BRIEFING, audio, editionName: 'Morning News' }} onDetail={(d) => (seen.detail = d)} {...props} />, {
    'GET /v1.0/api/media': () => page(script.now ? [script.now] : []),
    'GET /v1.0/api/media/med_1': () => ({ body: { media: { ...script.now, artifactId: 'art_mp3', durationSeconds: 300 }, chapters: [], audioPath: '/v1.0/api/artifacts/art_mp3/content' } }),
    'POST /v1.0/api/briefings/brf_1/audio': () => {
      script.makes += 1;
      script.now = episode('Queued', { createdUtc: ago(0) });
      return { status: 202, body: script.now };
    },
    ...handlers,
  });
  return { ...utils, script, seen };
}

describe('making audio', () => {
  it('reads the status on an interval and shows each step until the episode is ready', async () => {
    const { server, script, seen } = await renderAudio();
    await fireEvent.press(screen.getByTestId('audio-make'));
    await waitFor(() => expect(screen.getByText('Waiting to start.')).toBeTruthy());
    expect(screen.getByText('Making the audio')).toBeTruthy();
    expect(screen.getByTestId('audio-elapsed').props.children).toMatch(/^Step 1 of 3 · 0:0\d so far$/);
    // Making audio is the only thing that waits: there is no Make button to press twice, and nothing else is disabled.
    expect(screen.queryByTestId('audio-make')).toBeNull();

    script.now = { ...script.now, status: 'Scripting' };
    await waitFor(() => expect(screen.getByText('Writing the script.')).toBeTruthy());
    expect(screen.getByTestId('audio-elapsed').props.children).toMatch(/^Step 2 of 3 · /);
    script.now = { ...script.now, status: 'Synthesizing' };
    await waitFor(() => expect(screen.getByText('Recording the voices.')).toBeTruthy());
    expect(screen.getByTestId('audio-elapsed').props.children).toMatch(/^Step 3 of 3 · /);

    script.now = { ...script.now, status: 'Ready', artifactId: 'art_mp3' };
    await waitFor(() => expect(screen.getByTestId('audio-listen')).toBeTruthy());
    expect(screen.queryByTestId('audio-pending')).toBeNull();
    expect(seen.detail.audio.status).toBe('Ready');
    expect(seen.detail.briefing.mediaAssetId).toBe('med_1');

    const lists = server.calls.filter((c) => c.key === 'GET /v1.0/api/media');
    expect(lists.length).toBeGreaterThanOrEqual(3);
    expect(lists[0].url).toMatch(/briefingId=brf_1/);
    // It polls the episode, never the whole briefing, and stops when the episode is done.
    expect(server.count('GET /v1.0/api/briefings/brf_1')).toBe(0);
    const done = server.count('GET /v1.0/api/media');
    await pause(POLL * 4);
    expect(server.count('GET /v1.0/api/media')).toBe(done);
  });

  it('counts the elapsed time up every second', async () => {
    await renderAudio({ audio: episode('Synthesizing', { createdUtc: ago(58000) }) });
    expect(screen.getByTestId('audio-elapsed').props.children).toMatch(/0:5[89] so far$/);
    await waitFor(() => expect(screen.getByTestId('audio-elapsed').props.children).toMatch(/1:0\d so far$/), { timeout: 5000 });
  });

  it('shows the server error for a failed episode and makes it again with Try again', async () => {
    const { script } = await renderAudio();
    await fireEvent.press(screen.getByTestId('audio-make'));
    await waitFor(() => expect(screen.getByTestId('audio-pending')).toBeTruthy());
    script.now = { ...script.now, status: 'Failed', errorMessage: 'The speech server did not answer.' };
    await waitFor(() => expect(screen.getByTestId('audio-failed')).toBeTruthy());
    expect(screen.getByText('The audio could not be made')).toBeTruthy();
    expect(screen.getByText('The speech server did not answer.')).toBeTruthy();
    expect(screen.queryByTestId('audio-pending')).toBeNull();

    await fireEvent.press(screen.getByText('Try again'));
    await waitFor(() => expect(screen.getByText('Waiting to start.')).toBeTruthy());
    expect(script.makes).toBe(2);
    expect(screen.queryByTestId('audio-failed')).toBeNull();
    script.now = { ...script.now, status: 'Ready', artifactId: 'art_mp3' };
    await waitFor(() => expect(screen.getByTestId('audio-listen')).toBeTruthy());
  });

  it('resumes an episode that was already being made when the screen opens', async () => {
    const { server, script } = await renderAudio({ audio: episode('Scripting', { createdUtc: ago(125000) }) });
    // No Make press: the briefing's newest episode is in progress, so it shows the step and the real elapsed time.
    expect(screen.getByText('Writing the script.')).toBeTruthy();
    expect(screen.getByTestId('audio-elapsed').props.children).toMatch(/^Step 2 of 3 · 2:0\d so far$/);
    await waitFor(() => expect(server.count('GET /v1.0/api/media')).toBeGreaterThanOrEqual(1));
    script.now = { ...script.now, status: 'Synthesizing' };
    await waitFor(() => expect(screen.getByText('Recording the voices.')).toBeTruthy());
    script.now = { ...script.now, status: 'Ready', artifactId: 'art_mp3' };
    await waitFor(() => expect(screen.getByTestId('audio-listen')).toBeTruthy());
    expect(script.makes).toBe(0);
  });

  it('picks up an episode another device started (409 AudioInProgress)', async () => {
    const { script } = await renderAudio({
      handlers: {
        'POST /v1.0/api/briefings/brf_1/audio': () => {
          script.now = episode('Synthesizing');
          return { status: 409, body: { error: 'AudioInProgress', message: 'Audio for this briefing is already being made.', requestId: 'rqi_9' } };
        },
      },
    });
    await fireEvent.press(screen.getByTestId('audio-make'));
    await waitFor(() => expect(screen.getByText('Recording the voices.')).toBeTruthy());
    expect(screen.getByText(/already being made/)).toBeTruthy();
  });

  it('keeps polling through a failed status read', async () => {
    let fail = true;
    const { script } = await renderAudio({
      audio: episode('Scripting'),
      handlers: {
        'GET /v1.0/api/media': () => {
          if (fail) throw new Error('Network request failed');
          return page([script.now]);
        },
      },
    });
    await waitFor(() => expect(screen.getByTestId('audio-trouble')).toBeTruthy());
    expect(screen.getByText('Writing the script.')).toBeTruthy();
    script.now = { ...script.now, status: 'Ready', artifactId: 'art_mp3' };
    fail = false;
    await waitFor(() => expect(screen.getByTestId('audio-listen')).toBeTruthy());
    expect(screen.queryByTestId('audio-trouble')).toBeNull();
  });

  it('never has two status reads in flight when the server is slower than the interval', async () => {
    let release;
    const { server, script } = await renderAudio({
      audio: episode('Synthesizing'),
      handlers: { 'GET /v1.0/api/media': () => new Promise((resolve) => (release = () => resolve(page([script.now])))) },
    });
    await waitFor(() => expect(server.count('GET /v1.0/api/media')).toBe(1));
    await pause(POLL * 5);
    expect(server.count('GET /v1.0/api/media')).toBe(1);
    script.now = { ...script.now, status: 'Ready', artifactId: 'art_mp3' };
    await act(async () => release());
    await waitFor(() => expect(screen.getByTestId('audio-listen')).toBeTruthy());
  });

  it('says when it is taking longer than usual, and lets the user keep waiting or stop checking', async () => {
    const { server, script } = await renderAudio({ audio: episode('Synthesizing', { createdUtc: ago(AUDIO_LONG_WAIT_MS + 60000) }) });
    expect(screen.getByTestId('audio-long-wait')).toBeTruthy();
    expect(screen.getByText('This is taking longer than usual')).toBeTruthy();
    expect(screen.getByTestId('audio-elapsed').props.children).toMatch(/6:0\d so far$/);

    await fireEvent.press(screen.getByTestId('audio-stop-waiting'));
    expect(screen.getByTestId('audio-stopped')).toBeTruthy();
    expect(screen.queryByTestId('audio-pending')).toBeNull();
    await pause(POLL * 2);
    const stoppedAt = server.count('GET /v1.0/api/media');
    await pause(POLL * 4);
    expect(server.count('GET /v1.0/api/media')).toBe(stoppedAt);

    await fireEvent.press(screen.getByTestId('audio-check-again'));
    await waitFor(() => expect(screen.getByTestId('audio-pending')).toBeTruthy());
    await waitFor(() => expect(server.count('GET /v1.0/api/media')).toBeGreaterThan(stoppedAt));
    await fireEvent.press(screen.getByTestId('audio-keep-waiting'));
    expect(screen.queryByTestId('audio-long-wait')).toBeNull();
    expect(screen.getByText('Recording the voices.')).toBeTruthy();

    script.now = { ...script.now, status: 'Ready', artifactId: 'art_mp3' };
    await waitFor(() => expect(screen.getByTestId('audio-listen')).toBeTruthy());
  });

  it('has no long-wait note before the threshold, and sane defaults', async () => {
    await renderAudio({ audio: episode('Queued') });
    expect(screen.queryByTestId('audio-long-wait')).toBeNull();
    expect(screen.getByText(/You can keep reading or leave this screen/)).toBeTruthy();
    expect(AUDIO_POLL_MS).toBe(3000);
    expect(AUDIO_LONG_WAIT_MS).toBe(300000);
    expect(newestEpisode([{ id: 'a', createdUtc: '2026-11-19T10:00:00Z' }, { id: 'b', createdUtc: '2026-11-19T11:00:00Z' }, null]).id).toBe('b');
    expect(newestEpisode([])).toBeNull();
  });
});

describe('useResource polling', () => {
  it('skips a poll while a load is in flight, so a slow server still gets its answer shown', async () => {
    const releases = [];
    const loader = jest.fn(() => new Promise((resolve) => releases.push(resolve)));
    const box = { current: null };
    const keep = (resource) => {
      box.current = resource;
    };
    function Probe({ onResource }) {
      const resource = useResource(loader, []);
      useEffect(() => {
        onResource(resource);
      });
      return null;
    }
    await renderSignedIn(<Probe onResource={keep} />);
    expect(loader).toHaveBeenCalledTimes(1);
    await act(async () => releases[0]('first'));
    expect(box.current.data).toBe('first');
    await act(async () => {
      void box.current.poll();
      void box.current.poll();
      void box.current.poll();
    });
    expect(loader).toHaveBeenCalledTimes(2);
    await act(async () => releases[1]('second'));
    expect(box.current.data).toBe('second');
  });
});
