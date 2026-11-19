// Pseudo-locales generated from the English catalog at runtime. qps-ploc expands text by about 40% with accented
// characters; qps-rtl wraps text in right-to-left embedding marks. ICU placeholders and plural syntax are preserved.
const ACCENTS = {
  a: 'ȧ', b: 'ƀ', c: 'ç', d: 'ḓ', e: 'ḗ', f: 'ƒ', g: 'ɠ', h: 'ħ', i: 'ī', j: 'ĵ', k: 'ķ', l: 'ŀ', m: 'ḿ', n: 'ƞ', o: 'ǿ',
  p: 'ƥ', q: 'ɋ', r: 'ř', s: 'ş', t: 'ŧ', u: 'ŭ', v: 'ṽ', w: 'ẇ', x: 'ẋ', y: 'ẏ', z: 'ẑ',
  A: 'Ȧ', B: 'Ɓ', C: 'Ç', D: 'Ḓ', E: 'Ḗ', F: 'Ƒ', G: 'Ɠ', H: 'Ħ', I: 'Ī', J: 'Ĵ', K: 'Ķ', L: 'Ŀ', M: 'Ḿ', N: 'Ƞ', O: 'Ǿ',
  P: 'Ƥ', Q: 'Ɋ', R: 'Ř', S: 'Ş', T: 'Ŧ', U: 'Ŭ', V: 'Ṽ', W: 'Ẇ', X: 'Ẋ', Y: 'Ẏ', Z: 'Ẑ'
};

// Transform only literal text, leaving {placeholders} and ICU blocks untouched.
export function transformLiterals(value, fn) {
  let out = '';
  let depth = 0;
  let literal = '';
  for (const ch of value) {
    if (ch === '{') {
      if (depth === 0 && literal) {
        out += fn(literal);
        literal = '';
      }
      depth += 1;
      out += ch;
    } else if (ch === '}') {
      depth = Math.max(0, depth - 1);
      out += ch;
    } else if (depth === 0) {
      literal += ch;
    } else {
      out += ch;
    }
  }
  if (literal) out += fn(literal);
  return out;
}

function expand(text) {
  const accented = Array.from(text)
    .map((c) => ACCENTS[c] || c)
    .join('');
  return accented + '~'.repeat(Math.max(1, Math.round(text.length * 0.4)));
}

function mapCatalog(catalog, fn) {
  const result = {};
  for (const [key, value] of Object.entries(catalog)) {
    result[key] = typeof value === 'string' ? fn(value) : mapCatalog(value, fn);
  }
  return result;
}

export function buildExpansionPseudo(catalog) {
  return mapCatalog(catalog, (value) => '[' + transformLiterals(value, expand) + ']');
}

export function buildRtlPseudo(catalog) {
  return mapCatalog(catalog, (value) => '‫' + value + '‬');
}
