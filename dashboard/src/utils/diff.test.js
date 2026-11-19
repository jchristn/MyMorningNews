import { describe, expect, it } from 'vitest';
import { diffLines, diffStats, splitLines, withContext } from './diff.js';
import { declaredVariables, frontMatter, sectionsPresent } from './promptTemplate.js';

describe('splitLines', () => {
  it('normalizes line endings and ignores one trailing newline', () => {
    expect(splitLines('a\r\nb\rc\n')).toEqual(['a', 'b', 'c']);
    expect(splitLines('')).toEqual([]);
    expect(splitLines(null)).toEqual([]);
    expect(splitLines('a\n\n')).toEqual(['a', '']);
  });
});

describe('diffLines', () => {
  it('marks identical text as unchanged with line numbers on both sides', () => {
    const lines = diffLines('a\nb', 'a\nb\n');
    expect(lines).toEqual([
      { type: 'same', text: 'a', oldLine: 1, newLine: 1 },
      { type: 'same', text: 'b', oldLine: 2, newLine: 2 }
    ]);
    expect(diffStats(lines)).toEqual({ added: 0, removed: 0 });
  });

  it('finds a changed line in the middle', () => {
    const lines = diffLines('one\ntwo\nthree', 'one\n2\nthree');
    expect(lines.map((l) => l.type + ':' + l.text)).toEqual(['same:one', 'remove:two', 'add:2', 'same:three']);
    expect(lines[1]).toMatchObject({ oldLine: 2, newLine: null });
    expect(lines[2]).toMatchObject({ oldLine: null, newLine: 2 });
    expect(lines[3]).toMatchObject({ oldLine: 3, newLine: 3 });
  });

  it('handles insertions, deletions, and empty sides', () => {
    expect(diffStats(diffLines('', 'a\nb'))).toEqual({ added: 2, removed: 0 });
    expect(diffStats(diffLines('a\nb', ''))).toEqual({ added: 0, removed: 2 });
    const lines = diffLines('a\nc', 'a\nb\nc\nd');
    expect(lines.map((l) => l.type + ':' + l.text)).toEqual(['same:a', 'add:b', 'same:c', 'add:d']);
    expect(lines[2]).toMatchObject({ oldLine: 2, newLine: 3 });
  });

  it('keeps the longest common subsequence when lines move', () => {
    const lines = diffLines('a\nb\nc\nd', 'b\nc\nd\na');
    expect(diffStats(lines)).toEqual({ added: 1, removed: 1 });
    expect(lines.filter((l) => l.type === 'same').map((l) => l.text)).toEqual(['b', 'c', 'd']);
  });

  it('reconstructs both texts', () => {
    const a = 'x\ny\nz\nw\nv';
    const b = 'x\nq\nz\nv\nu';
    const lines = diffLines(a, b);
    expect(lines.filter((l) => l.type !== 'add').map((l) => l.text)).toEqual(a.split('\n'));
    expect(lines.filter((l) => l.type !== 'remove').map((l) => l.text)).toEqual(b.split('\n'));
  });
});

describe('withContext', () => {
  it('collapses long unchanged runs around changes', () => {
    const a = Array.from({ length: 20 }, (_, i) => 'line ' + i).join('\n');
    const b = a.replace('line 10', 'changed');
    const shown = withContext(diffLines(a, b), 2);
    expect(shown[0]).toEqual({ type: 'skip', count: 8 });
    expect(shown.filter((l) => l.type !== 'skip').map((l) => l.text)).toEqual(['line 8', 'line 9', 'line 10', 'changed', 'line 11', 'line 12']);
    expect(shown[shown.length - 1]).toEqual({ type: 'skip', count: 7 });
  });
});

describe('prompt template helpers', () => {
  const content = '---\nkey: research.score\nrole: Scoring\nvariables: [topic.brief, candidate.title, "quoted"]\n---\n## System\nRank.\n\n## User\n{{topic.brief}}\n';

  it('reads the front matter and declared variables', () => {
    expect(frontMatter(content)).toContain('key: research.score');
    expect(declaredVariables(content)).toEqual(['topic.brief', 'candidate.title', 'quoted']);
    expect(declaredVariables('---\nvariables:\n  - a\n  - b\nkey: x\n---\nbody')).toEqual(['a', 'b']);
    expect(declaredVariables('no front matter')).toEqual([]);
  });

  it('detects the System and User sections', () => {
    expect(sectionsPresent(content)).toEqual({ system: true, user: true });
    expect(sectionsPresent('## System\nonly')).toEqual({ system: true, user: false });
  });
});
