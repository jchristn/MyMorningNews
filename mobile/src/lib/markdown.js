// Markdown for the briefing reader (plan section 21.3), parsed into a small tree that the app renders with native Text
// and View elements. Nothing is ever rendered as HTML or in a WebView, so there is nothing to inject: raw HTML, images,
// and tables stay literal text. The subset follows the server's SafeMarkdown (MyMorningNews.Core/Briefings): headings,
// paragraphs, bullet and numbered lists, quotes, fenced code, rules, and inline bold, italic, code, and links, plus
// numbered citations ("[3]") that resolve to the Sources section.
//
// Blocks: heading { level, id, text, children }, paragraph { children }, list { ordered, items: [{ number, indent,
// children }] }, quote { children }, code { value }, rule.
// Inline: text { value }, strong | em { children }, code { value }, link { href, children }, cite { n }.

const SAFE_URL = /^(https?:\/\/|mailto:)[^\s<>"']+$/i;
const HEADING = /^(#{1,6})\s+(.*?)\s*#*\s*$/;
const BULLET = /^(\s*)[-*+]\s+(.*)$/;
const ORDERED = /^(\s*)(\d{1,3})[.)]\s+(.*)$/;
const QUOTE = /^\s{0,3}>\s?(.*)$/;
const RULE = /^\s{0,3}([-*_])(\s*\1){2,}\s*$/;
const FENCE = /^\s{0,3}(```|~~~)/;
const CITE = /^\[(\d{1,3}(?:\s*,\s*\d{1,3})*)\](?!\()/;
const BARE_URL = /^https?:\/\/[^\s<>]+/i;
const WORD = /[A-Za-z0-9]/;
const ESCAPABLE = '\\`*_[]()#+-.!>~';

/** Headings that hold the numbered source list (the template's `source=citations` section). */
export const SOURCES_HEADINGS = ['sources', 'references', 'citations'];

/** Whether a link target may be opened (http, https, and mailto only). */
export function isSafeUrl(href) {
  return typeof href === 'string' && SAFE_URL.test(href.trim());
}

function pushText(out, value) {
  if (!value) return;
  const last = out[out.length - 1];
  if (last && last.type === 'text') last.value += value;
  else out.push({ type: 'text', value });
}

function closing(text, delimiter, from) {
  for (let i = from; i <= text.length - delimiter.length; i += 1) {
    if (text[i] === '\\') {
      i += 1;
      continue;
    }
    if (text.startsWith(delimiter, i)) {
      if (delimiter.length === 1 && text[i + 1] === delimiter) {
        i += 1;
        continue;
      }
      return i;
    }
  }
  return -1;
}

function trimUrl(url) {
  let result = url.replace(/[.,;:!?'"]+$/, '');
  while (result.endsWith(')') && (result.match(/\(/g) || []).length < (result.match(/\)/g) || []).length) result = result.slice(0, -1);
  return result;
}

/** Parse inline markup into nodes. */
export function parseInline(text, depth = 0) {
  const out = [];
  const s = String(text || '');
  let i = 0;
  while (i < s.length) {
    const ch = s[i];
    const prev = i > 0 ? s[i - 1] : '';
    if (ch === '\\' && i + 1 < s.length && ESCAPABLE.includes(s[i + 1])) {
      pushText(out, s[i + 1]);
      i += 2;
      continue;
    }
    if (ch === '`') {
      const end = s.indexOf('`', i + 1);
      if (end > i + 1) {
        out.push({ type: 'code', value: s.slice(i + 1, end) });
        i = end + 1;
        continue;
      }
    }
    if (depth < 5 && (s.startsWith('**', i) || s.startsWith('__', i))) {
      const delimiter = s.slice(i, i + 2);
      const end = closing(s, delimiter, i + 2);
      if (end > i + 2 && !/\s/.test(s[i + 2]) && !/\s/.test(s[end - 1])) {
        out.push({ type: 'strong', children: parseInline(s.slice(i + 2, end), depth + 1) });
        i = end + 2;
        continue;
      }
    }
    if (depth < 5 && (ch === '*' || ch === '_') && !(ch === '_' && WORD.test(prev))) {
      const end = closing(s, ch, i + 1);
      const after = end >= 0 ? s[end + 1] || '' : '';
      if (end > i + 1 && !/\s/.test(s[i + 1]) && !/\s/.test(s[end - 1]) && !(ch === '_' && WORD.test(after))) {
        out.push({ type: 'em', children: parseInline(s.slice(i + 1, end), depth + 1) });
        i = end + 1;
        continue;
      }
    }
    if (ch === '[') {
      const cite = CITE.exec(s.slice(i));
      if (cite) {
        for (const n of cite[1].split(',')) out.push({ type: 'cite', n: Number(n.trim()) });
        i += cite[0].length;
        continue;
      }
      const close = closing(s, ']', i + 1);
      if (close > i + 1 && s[close + 1] === '(') {
        const end = s.indexOf(')', close + 2);
        const href = end > 0 ? s.slice(close + 2, end).trim() : '';
        if (end > 0) {
          const children = parseInline(s.slice(i + 1, close), depth + 1);
          if (isSafeUrl(href)) out.push({ type: 'link', href, children });
          else {
            // An unsafe target (javascript:, data:, a relative path) keeps only its text.
            for (const child of children) {
              if (child.type === 'text') pushText(out, child.value);
              else out.push(child);
            }
          }
          i = end + 1;
          continue;
        }
      }
    }
    if (ch === '<') {
      const end = s.indexOf('>', i + 1);
      const href = end > 0 ? s.slice(i + 1, end) : '';
      if (end > 0 && isSafeUrl(href)) {
        out.push({ type: 'link', href, children: [{ type: 'text', value: href.replace(/^mailto:/i, '') }] });
        i = end + 1;
        continue;
      }
    }
    if ((ch === 'h' || ch === 'H') && !WORD.test(prev)) {
      const match = BARE_URL.exec(s.slice(i));
      if (match) {
        const url = trimUrl(match[0]);
        if (isSafeUrl(url) && url.length > 8) {
          out.push({ type: 'link', href: url, children: [{ type: 'text', value: url }] });
          i += url.length;
          continue;
        }
      }
    }
    pushText(out, ch);
    i += 1;
  }
  return out;
}

/** The plain text of inline nodes (headings for the outline, accessibility labels, comparisons). */
export function plainText(nodes) {
  return (nodes || [])
    .map((n) => (n.type === 'text' || n.type === 'code' ? n.value : n.type === 'cite' ? '[' + n.n + ']' : plainText(n.children)))
    .join('');
}

/** Split Markdown into blocks with inline children. */
export function parseBlocks(markdown) {
  const lines = String(markdown || '').replace(/\r\n?/g, '\n').split('\n');
  const blocks = [];
  let paragraph = [];
  let list = null;
  let quote = [];
  let headingCount = 0;

  const flushParagraph = () => {
    if (paragraph.length) blocks.push({ type: 'paragraph', children: parseInline(paragraph.join(' ')) });
    paragraph = [];
  };
  const flushList = () => {
    if (list) blocks.push({ type: 'list', ordered: list.ordered, items: list.items.map((item) => ({ number: item.number, indent: item.indent, children: parseInline(item.lines.join(' ')) })) });
    list = null;
  };
  const flushQuote = () => {
    if (quote.length) blocks.push({ type: 'quote', children: parseInline(quote.join(' ')) });
    quote = [];
  };
  const flushAll = () => {
    flushParagraph();
    flushList();
    flushQuote();
  };

  for (let i = 0; i < lines.length; i += 1) {
    const line = lines[i];
    if (FENCE.test(line)) {
      flushAll();
      const fence = FENCE.exec(line)[1];
      const code = [];
      i += 1;
      while (i < lines.length && !lines[i].trim().startsWith(fence)) {
        code.push(lines[i]);
        i += 1;
      }
      blocks.push({ type: 'code', value: code.join('\n') });
      continue;
    }
    if (!line.trim()) {
      flushParagraph();
      flushQuote();
      if (list && !(lines[i + 1] && (BULLET.test(lines[i + 1]) || ORDERED.test(lines[i + 1])))) flushList();
      continue;
    }
    const heading = HEADING.exec(line);
    if (heading) {
      flushAll();
      const children = parseInline(heading[2]);
      headingCount += 1;
      blocks.push({ type: 'heading', level: heading[1].length, id: 'h' + headingCount, text: plainText(children).trim(), children });
      continue;
    }
    if (RULE.test(line)) {
      flushAll();
      blocks.push({ type: 'rule' });
      continue;
    }
    const quoted = QUOTE.exec(line);
    if (quoted) {
      flushParagraph();
      flushList();
      quote.push(quoted[1].trim());
      continue;
    }
    const ordered = ORDERED.exec(line);
    const bullet = ordered ? null : BULLET.exec(line);
    if (ordered || bullet) {
      flushParagraph();
      flushQuote();
      const isOrdered = Boolean(ordered);
      if (list && list.ordered !== isOrdered && (ordered ? ordered[1] : bullet[1]).length === 0) flushList();
      if (!list) list = { ordered: isOrdered, items: [] };
      const indent = Math.min(3, Math.floor((ordered ? ordered[1] : bullet[1]).replace(/\t/g, '    ').length / 2));
      list.items.push({ number: ordered ? Number(ordered[2]) : null, indent, lines: [(ordered ? ordered[3] : bullet[2]).trim()] });
      continue;
    }
    if (list && /^\s+\S/.test(line)) {
      list.items[list.items.length - 1].lines.push(line.trim());
      continue;
    }
    if (quote.length) {
      quote.push(line.trim());
      continue;
    }
    flushList();
    paragraph.push(line.trim());
  }
  flushAll();
  return blocks;
}

function sourceEntry(children) {
  const link = children.find((c) => c.type === 'link');
  return { text: plainText(children).trim(), href: link ? link.href : null, title: link ? plainText(link.children).trim() : plainText(children).trim() };
}

/**
 * Everything the reader needs from a briefing's Markdown: the blocks, the outline (headings of level 1 and 2 for
 * section navigation), and the numbered sources (`{ [n]: { text, href, title, blockIndex, itemIndex } }`) from the
 * numbered list under the Sources heading. `sourcesHeadings` names that heading (default: Sources, References,
 * Citations), and `skipTitle` drops a leading level-1 heading that repeats the title the screen already shows.
 */
export function parseBriefing(markdown, { skipTitle, sourcesHeadings = SOURCES_HEADINGS } = {}) {
  let blocks = parseBlocks(markdown);
  if (skipTitle && blocks[0] && blocks[0].type === 'heading' && blocks[0].level === 1 && blocks[0].text.toLowerCase() === String(skipTitle).trim().toLowerCase()) blocks = blocks.slice(1);
  const wanted = sourcesHeadings.map((h) => String(h).trim().toLowerCase());
  const outline = blocks.filter((b) => b.type === 'heading' && b.level <= 2).map((b) => ({ id: b.id, text: b.text, level: b.level }));
  const sources = {};
  let sourcesId = null;
  for (let i = 0; i < blocks.length; i += 1) {
    const b = blocks[i];
    if (b.type !== 'heading' || !wanted.includes(b.text.toLowerCase())) continue;
    sourcesId = b.id;
    for (let j = i + 1; j < blocks.length && blocks[j].type !== 'heading'; j += 1) {
      if (blocks[j].type !== 'list') continue;
      blocks[j].items.forEach((item, k) => {
        const n = blocks[j].ordered && item.number ? item.number : k + 1;
        sources[n] = { ...sourceEntry(item.children), blockIndex: j, itemIndex: k };
      });
      break;
    }
  }
  return { blocks, outline, sources, sourcesId };
}

/** Every citation number used in the body, in order of first use. */
export function citationsUsed(blocks) {
  const seen = [];
  const walk = (nodes) => {
    for (const n of nodes || []) {
      if (n.type === 'cite' && !seen.includes(n.n)) seen.push(n.n);
      if (n.children) walk(n.children);
    }
  };
  for (const b of blocks || []) {
    if (b.children) walk(b.children);
    if (b.items) for (const item of b.items) walk(item.children);
  }
  return seen;
}
