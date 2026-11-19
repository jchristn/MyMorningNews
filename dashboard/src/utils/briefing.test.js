import { describe, expect, it } from 'vitest';
import { briefingFilename, citationAnchor, itemHref, linkCitations, matchSections, parseSourceList, splitBriefingMarkdown } from './briefing.js';
import { frontMatterName, lineRange, starterTemplate, templateCapabilities, templateFilename } from './briefingTemplate.js';
import { MARKDOWN, SECTIONS } from '../test/briefingFixtures.js';

describe('briefing Markdown helpers', () => {
  it('splits the title, opening, and level-two sections, ignoring headings in code', () => {
    const parsed = splitBriefingMarkdown(MARKDOWN + '\n## Notes\n\n```\n## not a heading\n```\n');
    expect(parsed.title).toBe('Morning News for November 19, 2026');
    expect(parsed.preamble).toContain('The most important development');
    expect(parsed.chunks.map((c) => c.heading)).toEqual(['Top of the news', 'Sources', 'Notes']);
    expect(parsed.chunks[2].body).toContain('## not a heading');
  });

  it('links bare citations and leaves links, definitions, images, and code alone', () => {
    const out = linkCitations('A [1]: and [2][3]. See [4](https://x.test), ![5](img.png), `[6]`, and \\[7].\n[8]: https://y.test');
    expect(out).toContain('[\\[1\\]](#' + citationAnchor(1) + ')');
    expect(out).toContain('[\\[2\\]](#' + citationAnchor(2) + ')[\\[3\\]](#' + citationAnchor(3) + ')');
    expect(out).toContain('[4](https://x.test)');
    expect(out).toContain('![5](img.png)');
    expect(out).toContain('`[6]`');
    expect(out).toContain('\\[7]');
    expect(out).toContain('[8]: https://y.test');
    expect(linkCitations(out)).toBe(out);
  });

  it('parses the Sources list into numbered entries', () => {
    const sources = parseSourceList('1. [A](https://a.test), a.test\n2. [B](https://b.test)\n   continued\n\nnot a list');
    expect(sources).toEqual([
      { number: 1, text: '[A](https://a.test), a.test' },
      { number: 2, text: '[B](https://b.test) continued not a list' }
    ]);
  });

  it('matches chunks to stored sections by heading', () => {
    const chunks = matchSections(splitBriefingMarkdown(MARKDOWN).chunks, SECTIONS.slice().reverse());
    expect(chunks.map((c) => c.key)).toEqual(['top-of-the-news', 'sources']);
    expect(chunks[1].section.source).toBe('citations');
    expect(matchSections([{ heading: 'Extra', body: '' }], [])[0].key).toBe('extra');
  });

  it('builds item links and download names', () => {
    expect(itemHref({ itemType: 'Finding', itemId: 'fnd 1' })).toBe('/dashboard/findings/fnd%201');
    expect(itemHref({ itemType: 'Prediction', itemId: 'prd_1' })).toBe('/dashboard/predictions?prediction=prd_1');
    expect(itemHref({ itemType: 'Suggestion', itemId: 'sug_1' })).toBe('/dashboard/suggestions');
    expect(briefingFilename({ title: 'Morning News for Nov 19', composedUtc: '2026-11-19T06:30:00Z' }, 'html')).toBe('morning-news-for-nov-19-2026-11-19.html');
    expect(briefingFilename({}, 'md')).toBe('briefing.md');
  });
});

describe('briefing template helpers', () => {
  it('builds a starter with a quoted name and reads it back', () => {
    const content = starterTemplate('My "big" one');
    expect(content).toContain('name: "My \\"big\\" one"');
    expect(content).toContain('<!-- mmn:section source=citations -->');
    expect(frontMatterName('---\nname: Evening # comment\ntone: calm\n---\n')).toBe('Evening');
    expect(frontMatterName('no front matter')).toBeNull();
  });

  it('finds the character range of a line, clamped to the text', () => {
    expect(lineRange('ab\ncde\nf', 2)).toEqual({ start: 3, end: 6, line: 2 });
    expect(lineRange('ab\ncde', 9)).toEqual({ start: 3, end: 6, line: 2 });
    expect(lineRange('ab', 0)).toEqual({ start: 0, end: 2, line: 1 });
  });

  it('knows which templates can be reset and deleted', () => {
    expect(templateCapabilities({ defaultKey: 'morning-news' })).toEqual({ canReset: true, canDelete: false });
    expect(templateCapabilities({ userId: 'usr_1', defaultKey: 'morning-news' })).toEqual({ canReset: true, canDelete: true });
    expect(templateCapabilities({ userId: 'usr_1' })).toEqual({ canReset: false, canDelete: true });
    expect(templateFilename('Morning News!')).toBe('morning-news.md');
  });
});
