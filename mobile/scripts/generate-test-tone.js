#!/usr/bin/env node
/**
 * Generate the bundled test tone used by the developer player screen (dev builds only) to verify playback,
 * background audio, lock-screen controls, skipping, scrubbing, chapters, speed, and the sleep timer before briefings
 * with audio exist.
 *
 * Usage (from mobile/):
 *   npm run assets:tone
 *
 * Output: mobile/assets/audio/test-tone.m4a (AAC, mono, about 700 KB), four minutes long, in four one-minute chapters
 * at different pitches (A3, C4, E4, A4). Each chapter has a short high beep every ten seconds and a double beep at
 * its start, so seeking and skipping are easy to hear. Four minutes is longer than Android's three-minute cut-off for
 * background audio without a foreground service, so a full play-through proves the media service works.
 *
 * Needs ffmpeg on PATH for the AAC encode (the PCM is synthesized here). The chapter list the app uses is in
 * src/player/testTone.js and must match CHAPTERS below.
 */
const childProcess = require('child_process');
const fs = require('fs');
const path = require('path');

const SAMPLE_RATE = 22050;
const CHAPTER_SECONDS = 60;
const CHAPTERS = [
  { title: 'Chapter 1: A3', frequency: 220 },
  { title: 'Chapter 2: C4', frequency: 261.63 },
  { title: 'Chapter 3: E4', frequency: 329.63 },
  { title: 'Chapter 4: A4', frequency: 440 },
];
const BEEP_FREQUENCY = 1760;
const BEEP_SECONDS = 0.12;

function synthesize() {
  const total = CHAPTERS.length * CHAPTER_SECONDS * SAMPLE_RATE;
  const pcm = Buffer.alloc(total * 2);
  for (let i = 0; i < total; i += 1) {
    const t = i / SAMPLE_RATE;
    const chapter = Math.min(CHAPTERS.length - 1, Math.floor(t / CHAPTER_SECONDS));
    const inChapter = t - chapter * CHAPTER_SECONDS;
    const inTen = inChapter % 10;
    const beep = inTen < BEEP_SECONDS || (inChapter < 1 && inTen > 0.25 && inTen < 0.25 + BEEP_SECONDS);
    const base = 0.25 * Math.sin(2 * Math.PI * CHAPTERS[chapter].frequency * t);
    const tick = beep ? 0.3 * Math.sin(2 * Math.PI * BEEP_FREQUENCY * t) : 0;
    const value = Math.max(-1, Math.min(1, base + tick));
    pcm.writeInt16LE(Math.round(value * 32767), i * 2);
  }
  return pcm;
}

function main() {
  const outDir = path.resolve(__dirname, '..', 'assets', 'audio');
  fs.mkdirSync(outDir, { recursive: true });
  const out = path.join(outDir, 'test-tone.m4a');
  const pcm = synthesize();
  const args = ['-y', '-loglevel', 'error', '-f', 's16le', '-ar', String(SAMPLE_RATE), '-ac', '1', '-i', 'pipe:0', '-c:a', 'aac', '-b:a', '24k', '-metadata', 'title=My Morning News test tone', out];
  const result = childProcess.spawnSync('ffmpeg', args, { input: pcm, stdio: ['pipe', 'inherit', 'inherit'] });
  if (result.error || result.status !== 0) {
    process.stderr.write('ffmpeg failed. Install ffmpeg and try again.\n');
    process.exit(1);
  }
  process.stdout.write('wrote ' + path.relative(process.cwd(), out) + ' (' + fs.statSync(out).size + ' bytes)\n');
}

module.exports = { CHAPTERS, CHAPTER_SECONDS };

if (require.main === module) main();
