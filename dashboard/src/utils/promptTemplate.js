// Client-side reading of a prompt template's YAML front matter, for live hints while editing. The server's parser
// and validator remain authoritative.
export function frontMatter(content) {
  const text = String(content || '').replace(/\r\n?/g, '\n');
  const match = /^---\n([\s\S]*?)\n---(?:\n|$)/.exec(text);
  return match ? match[1] : null;
}

export function declaredVariables(content) {
  const fm = frontMatter(content);
  if (fm === null) return [];
  const lines = fm.split('\n');
  for (let i = 0; i < lines.length; i++) {
    const m = /^variables:\s*(.*)$/.exec(lines[i]);
    if (!m) continue;
    const inline = m[1].trim();
    if (inline.startsWith('[')) {
      return inline
        .replace(/^\[|\]$/g, '')
        .split(',')
        .map((v) => v.trim().replace(/^['"]|['"]$/g, ''))
        .filter(Boolean);
    }
    const out = [];
    for (let j = i + 1; j < lines.length; j++) {
      const item = /^\s*-\s*(.+)$/.exec(lines[j]);
      if (!item) break;
      out.push(item[1].trim().replace(/^['"]|['"]$/g, ''));
    }
    return out;
  }
  return [];
}

// Whether the body has the two sections the renderer needs.
export function sectionsPresent(content) {
  const text = String(content || '');
  return { system: /^##\s+System\s*$/m.test(text), user: /^##\s+User\s*$/m.test(text) };
}
