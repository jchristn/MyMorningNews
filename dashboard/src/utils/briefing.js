// Helpers for reading composed briefings: splitting the Markdown into its sections, linking numbered citations to
// the Sources list, and naming downloaded files. Pure functions; the reader components render the result.

// The id of the element a section or citation scrolls to.
export function sectionAnchor(key) {
  return 'briefing-section-' + String(key || '').replace(/[^a-zA-Z0-9_-]+/g, '-');
}

export function citationAnchor(number) {
  return 'briefing-cite-' + Number(number);
}

// Split briefing Markdown at its level-two headings. Returns { title, preamble, chunks: [{ heading, body }] } where
// title is the first level-one heading (removed from the preamble). Headings inside fenced code are ignored.
export function splitBriefingMarkdown(markdown) {
  const lines = String(markdown || '').replace(/\r\n/g, '\n').split('\n');
  let title = null;
  const preamble = [];
  const chunks = [];
  let current = null;
  let fenced = false;
  for (const line of lines) {
    if (/^\s*(```|~~~)/.test(line)) fenced = !fenced;
    const h2 = !fenced ? /^##\s+(.+?)\s*#*\s*$/.exec(line) : null;
    if (h2 && !line.startsWith('###')) {
      current = { heading: h2[1].trim(), lines: [] };
      chunks.push(current);
      continue;
    }
    const h1 = !fenced && !current && title === null ? /^#\s+(.+?)\s*#*\s*$/.exec(line) : null;
    if (h1) {
      title = h1[1].trim();
      continue;
    }
    if (current) current.lines.push(line);
    else preamble.push(line);
  }
  return {
    title,
    preamble: preamble.join('\n').trim(),
    chunks: chunks.map((c) => ({ heading: c.heading, body: c.lines.join('\n').trim() }))
  };
}

// Turn bare numbered citations ("[3]") into links to the Sources list. Markdown links ("[3](url)"), reference
// definition lines ("[3]: url"), images, escaped brackets, and fenced or inline code are left alone.
export function linkCitations(markdown) {
  const lines = String(markdown || '').split('\n');
  let fenced = false;
  return lines
    .map((line) => {
      if (/^\s*(```|~~~)/.test(line)) {
        fenced = !fenced;
        return line;
      }
      if (fenced || /^\s{0,3}\[\d{1,3}\]:/.test(line)) return line;
      return line
        .split(/(`[^`]*`)/)
        .map((part) => (part.startsWith('`') ? part : part.replace(/(?<![!\\])\[(\d{1,3})\](?!\()/g, (m, n) => '[\\[' + n + '\\]](#' + citationAnchor(n) + ')')))
        .join('');
    })
    .join('\n');
}

// Split a Sources section ("1. [Title](url), domain, date") into numbered entries and any text after the list.
// Indented or adjacent lines continue the previous entry; a blank line ends it, so a closing note after the list
// (a footnote) stays its own paragraph in `trailing` instead of running into the last source.
export function splitSourceList(body) {
  const entries = [];
  const trailing = [];
  let open = false;
  for (const line of String(body || '').split('\n')) {
    const m = /^\s*(\d{1,3})[.)]\s+(.*)$/.exec(line);
    if (m && trailing.length === 0) {
      entries.push({ number: Number(m[1]), text: m[2].trim() });
      open = true;
    } else if (!line.trim()) {
      open = false;
      if (trailing.length > 0) trailing.push('');
    } else if (open && entries.length > 0) {
      entries[entries.length - 1].text += ' ' + line.trim();
    } else if (entries.length > 0) {
      trailing.push(line);
    }
  }
  return { entries, trailing: trailing.join('\n').trim() };
}

// The numbered entries of a Sources section. Returns [] when the body has no numbered items.
export function parseSourceList(body) {
  return splitSourceList(body).entries;
}

// Match a split chunk to the stored section with the same heading (case-insensitive), else by position.
export function matchSections(chunks, sections) {
  const list = Array.isArray(sections) ? sections.slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)) : [];
  const used = new Set();
  return chunks.map((chunk, index) => {
    const wanted = chunk.heading.toLowerCase();
    let section = list.find((s) => !used.has(s.id) && String(s.heading || '').toLowerCase() === wanted);
    if (!section && list[index] && !used.has(list[index].id) && !list.some((s) => String(s.heading || '').toLowerCase() === wanted)) section = list[index];
    if (section) used.add(section.id);
    return { ...chunk, section: section || null, key: section ? section.sectionKey : slugify(chunk.heading) || 'section-' + index };
  });
}

export function slugify(text) {
  return String(text || '')
    .toLowerCase()
    .normalize('NFKD')
    .replace(/[̀-ͯ]/g, '')
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 80);
}

// A file name for a downloaded briefing when the server's Content-Disposition is not readable cross-origin.
export function briefingFilename(briefing, format) {
  const base = slugify(briefing && briefing.title) || 'briefing';
  const when = briefing && (briefing.composedUtc || briefing.createdUtc) ? String(briefing.composedUtc || briefing.createdUtc).slice(0, 10) : '';
  return [base, when].filter(Boolean).join('-') + (format === 'html' ? '.html' : '.md');
}

// The dashboard route for a briefing item (finding, prediction, or suggestion).
export function itemHref(item) {
  if (!item || !item.itemId) return null;
  const id = encodeURIComponent(item.itemId);
  switch (item.itemType) {
    case 'Finding':
      return '/dashboard/findings/' + id;
    case 'Prediction':
      return '/dashboard/predictions?id=' + id;
    case 'Suggestion':
      return '/dashboard/suggestions?id=' + id;
    default:
      return null;
  }
}

// Save a Blob with a file name through a temporary link.
export function saveBlob(filename, blob) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = filename;
  link.rel = 'noopener';
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  window.setTimeout(() => URL.revokeObjectURL(url), 0);
}
