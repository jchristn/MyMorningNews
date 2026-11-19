// Helpers for the briefing template editor (plan section 21.1).

// A small valid template to start from: front matter, a title with variables, one findings section, and Sources.
export function starterTemplate(name) {
  const quoted = '"' + String(name || 'My briefing').replace(/\\/g, '\\\\').replace(/"/g, '\\"') + '"';
  return [
    '---',
    'name: ' + quoted,
    'tone: direct, skeptical, no hype',
    'length: medium',
    'citations: numbered',
    '---',
    '',
    '# {{edition.name}} for {{date.long}}',
    '',
    '## Top of the news',
    '<!-- mmn:section source=findings limit=5 order=relevance -->',
    'One paragraph per item. Say why it matters to me.',
    '',
    '## Sources',
    '<!-- mmn:section source=citations -->',
    ''
  ].join('\n');
}

// The front matter name of a template, or null.
export function frontMatterName(content) {
  const m = /^---\s*\n([\s\S]*?)\n---/.exec(String(content || '').replace(/\r\n/g, '\n'));
  if (!m) return null;
  const line = m[1].split('\n').find((l) => /^name\s*:/.test(l));
  if (!line) return null;
  const value = line
    .replace(/^name\s*:\s*/, '')
    .replace(/\s+#.*$/, '')
    .trim()
    .replace(/^["']|["']$/g, '');
  return value || null;
}

// The [start, end) character range of a 1-based line, clamped to the text.
export function lineRange(text, line) {
  const lines = String(text || '').split('\n');
  const n = Math.min(Math.max(1, Number(line) || 1), Math.max(1, lines.length));
  let start = 0;
  for (let i = 0; i < n - 1; i++) start += lines[i].length + 1;
  return { start, end: start + (lines[n - 1] || '').length, line: n };
}

// A template file name: the template name as a slug.
export function templateFilename(name) {
  const slug = String(name || 'template')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
  return (slug || 'template') + '.md';
}

// Whether the server lets this template be deleted (seeded tenant defaults cannot be) and reset (only templates
// that came from a default can be).
export function templateCapabilities(template) {
  const seeded = Boolean(template && template.defaultKey);
  return { canReset: seeded, canDelete: !(template && !template.userId && seeded) };
}
