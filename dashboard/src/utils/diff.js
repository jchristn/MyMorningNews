// Line-based diff (longest common subsequence). Small and dependency-free; prompt templates are a few hundred lines
// at most, so the quadratic table is fine. Each output line is { type: 'same' | 'add' | 'remove', text, oldLine,
// newLine } with 1-based line numbers (null on the side where the line does not exist).
export function splitLines(text) {
  if (text === null || text === undefined || text === '') return [];
  const lines = String(text).replace(/\r\n?/g, '\n').split('\n');
  // A trailing newline does not add an empty last line.
  if (lines.length > 0 && lines[lines.length - 1] === '') lines.pop();
  return lines;
}

export function diffLines(oldText, newText) {
  const a = splitLines(oldText);
  const b = splitLines(newText);

  // Trim the common prefix and suffix so the table only covers the changed middle.
  let start = 0;
  while (start < a.length && start < b.length && a[start] === b[start]) start++;
  let endA = a.length;
  let endB = b.length;
  while (endA > start && endB > start && a[endA - 1] === b[endB - 1]) {
    endA--;
    endB--;
  }

  const midA = a.slice(start, endA);
  const midB = b.slice(start, endB);
  const rows = midA.length + 1;
  const cols = midB.length + 1;
  const table = new Array(rows);
  for (let i = 0; i < rows; i++) table[i] = new Uint32Array(cols);
  for (let i = midA.length - 1; i >= 0; i--) {
    for (let j = midB.length - 1; j >= 0; j--) {
      table[i][j] = midA[i] === midB[j] ? table[i + 1][j + 1] + 1 : Math.max(table[i + 1][j], table[i][j + 1]);
    }
  }

  const out = [];
  for (let k = 0; k < start; k++) out.push({ type: 'same', text: a[k], oldLine: k + 1, newLine: k + 1 });
  let i = 0;
  let j = 0;
  while (i < midA.length || j < midB.length) {
    if (i < midA.length && j < midB.length && midA[i] === midB[j]) {
      out.push({ type: 'same', text: midA[i], oldLine: start + i + 1, newLine: start + j + 1 });
      i++;
      j++;
    } else if (i < midA.length && (j >= midB.length || table[i + 1][j] >= table[i][j + 1])) {
      // Removals come before additions at the same point, as in a unified diff.
      out.push({ type: 'remove', text: midA[i], oldLine: start + i + 1, newLine: null });
      i++;
    } else {
      out.push({ type: 'add', text: midB[j], oldLine: null, newLine: start + j + 1 });
      j++;
    }
  }
  for (let k = 0; k < a.length - endA; k++) {
    out.push({ type: 'same', text: a[endA + k], oldLine: endA + k + 1, newLine: endB + k + 1 });
  }
  return out;
}

export function diffStats(lines) {
  let added = 0;
  let removed = 0;
  for (const line of lines) {
    if (line.type === 'add') added++;
    else if (line.type === 'remove') removed++;
  }
  return { added, removed };
}

// Collapse long runs of unchanged lines to `context` lines around each change. Collapsed runs become
// { type: 'skip', count } entries.
export function withContext(lines, context = 3) {
  const keep = new Array(lines.length).fill(false);
  lines.forEach((line, index) => {
    if (line.type === 'same') return;
    for (let k = Math.max(0, index - context); k <= Math.min(lines.length - 1, index + context); k++) keep[k] = true;
  });
  const out = [];
  let skipped = 0;
  lines.forEach((line, index) => {
    if (keep[index]) {
      if (skipped > 0) out.push({ type: 'skip', count: skipped });
      skipped = 0;
      out.push(line);
    } else {
      skipped++;
    }
  });
  if (skipped > 0) out.push({ type: 'skip', count: skipped });
  return out;
}
