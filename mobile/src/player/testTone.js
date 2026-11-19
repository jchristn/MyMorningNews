// The bundled test tone for the developer player screen (dev builds only). Chapters match
// scripts/generate-test-tone.js: four one-minute chapters at different pitches, a beep every ten seconds.
export const TEST_TONE_ID = 'dev.test-tone';

export const TEST_TONE_CHAPTERS = Object.freeze([
  { title: 'Chapter 1: A3', startSeconds: 0 },
  { title: 'Chapter 2: C4', startSeconds: 60 },
  { title: 'Chapter 3: E4', startSeconds: 120 },
  { title: 'Chapter 4: A4', startSeconds: 180 },
]);

/**
 * The test tone track. Only dev builds call this: the `require` sits behind `__DEV__`, which Metro folds to false in
 * release bundles, so the audio file never ships in a store build.
 */
export function testToneTrack(artworkUrl) {
  if (!__DEV__) return null;
  return {
    id: TEST_TONE_ID,
    title: 'Test tone',
    artist: 'My Morning News developer tools',
    album: 'Player spike',
    asset: require('../../assets/audio/test-tone.m4a'),
    artworkUrl,
    chapters: TEST_TONE_CHAPTERS,
    durationSeconds: 240,
  };
}
