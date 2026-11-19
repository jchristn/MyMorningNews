import { describe, expect, it } from 'vitest';
import { chapterAt, chapterIndexAt, chapterTicks, clampTime, formatClock, normalizeChapters, progressFraction, timeAtOffset } from './audio.js';

const RAW = [
  { id: 'chp_2', title: 'By topic', startSeconds: 60, endSeconds: 150, briefingSectionId: 'bsc_2', sortOrder: 1 },
  { id: 'chp_1', title: 'Top of the news', startSeconds: 0, endSeconds: 60, briefingSectionId: 'bsc_1', sortOrder: 0 },
  { id: 'chp_3', title: 'Where this might go', startSeconds: 160, briefingSectionId: 'bsc_3', sortOrder: 2 }
];

describe('normalizeChapters', () => {
  it('sorts by start and fills a missing end from the next start or the duration', () => {
    const list = normalizeChapters(RAW, 240);
    expect(list.map((c) => c.id)).toEqual(['chp_1', 'chp_2', 'chp_3']);
    expect(list[2].endSeconds).toBe(240);
    expect(list[1].endSeconds).toBe(150);
  });

  it('repairs an inverted end and drops chapters without a start', () => {
    const list = normalizeChapters([{ id: 'a', startSeconds: 10, endSeconds: 5 }, { id: 'b', startSeconds: 30 }, { id: 'c' }, null], 0);
    expect(list.map((c) => c.id)).toEqual(['a', 'b']);
    expect(list[0].endSeconds).toBe(30);
    expect(list[1].endSeconds).toBe(30);
  });

  it('returns an empty list for no chapters', () => {
    expect(normalizeChapters(undefined, 100)).toEqual([]);
  });
});

describe('chapterIndexAt', () => {
  const list = normalizeChapters(RAW, 240);

  it('finds the chapter playing at a time, with boundaries belonging to the later chapter', () => {
    expect(chapterIndexAt(list, 0)).toBe(0);
    expect(chapterIndexAt(list, 59.9)).toBe(0);
    expect(chapterIndexAt(list, 60)).toBe(1);
    expect(chapterIndexAt(list, 200)).toBe(2);
  });

  it('keeps a gap between chapters in the earlier chapter and the time past the end in the last', () => {
    expect(chapterIndexAt(list, 155)).toBe(1);
    expect(chapterIndexAt(list, 999)).toBe(2);
  });

  it('returns -1 before the first chapter or with none', () => {
    expect(chapterIndexAt(normalizeChapters([{ id: 'x', startSeconds: 5 }], 10), 2)).toBe(-1);
    expect(chapterIndexAt([], 10)).toBe(-1);
    expect(chapterAt(list, 61).briefingSectionId).toBe('bsc_2');
    expect(chapterAt([], 1)).toBeNull();
  });
});

describe('chapterTicks', () => {
  it('places a tick at each chapter start as a share of the duration, skipping zero and the end', () => {
    const ticks = chapterTicks(normalizeChapters([...RAW, { id: 'late', startSeconds: 240 }], 240), 240);
    expect(ticks.map((tk) => tk.key)).toEqual(['chp_2', 'chp_3']);
    expect(ticks[0].fraction).toBeCloseTo(0.25);
    expect(ticks[1].fraction).toBeCloseTo(160 / 240);
    expect(ticks[0].title).toBe('By topic');
  });

  it('has no ticks without a duration', () => {
    expect(chapterTicks(normalizeChapters(RAW, 0), 0)).toEqual([]);
  });
});

describe('seeking helpers', () => {
  it('computes progress, clamps times, and maps a click offset to a time', () => {
    expect(progressFraction(60, 240)).toBe(0.25);
    expect(progressFraction(500, 240)).toBe(1);
    expect(progressFraction(10, 0)).toBe(0);
    expect(clampTime(-5, 100)).toBe(0);
    expect(clampTime(130, 100)).toBe(100);
    expect(clampTime(130, 0)).toBe(130);
    expect(timeAtOffset(50, 200, 240)).toBe(60);
    expect(timeAtOffset(500, 200, 240)).toBe(240);
    expect(timeAtOffset(10, 0, 240)).toBe(0);
  });

  it('formats clock labels', () => {
    expect(formatClock(0)).toBe('0:00');
    expect(formatClock(65.9)).toBe('1:05');
    expect(formatClock(3725)).toBe('1:02:05');
    expect(formatClock(-3)).toBe('0:00');
    expect(formatClock(undefined)).toBe('0:00');
  });
});
