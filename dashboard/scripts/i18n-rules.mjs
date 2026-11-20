// Message rules shared by verify-i18n.mjs: how a translated ICU message must relate to its English source.
import IntlMessageFormat from 'intl-messageformat';

function parse(message) {
  return new IntlMessageFormat(message, 'en', undefined, { ignoreTag: true }).getAst();
}

// Names and tokens that stay in English in every locale when the English message contains them.
export const PROTECTED_TERMS = ['My Morning News', 'Claude', 'Anthropic', 'MCP', 'SearXNG', 'Ollama', 'Kokoro', 'Mailgun', 'OpenAI', 'Gemini', 'Postman'];

// URLs, route paths, and HTTP methods that precede a route path.
// Trailing sentence punctuation is not part of the token.
const PROTECTED_PATTERNS = [/https?:\/\/[^\s)]*[^\s).,;:]/g, /(?:GET|POST|PUT|PATCH|DELETE)\s+\/v1\.0\/[^\s,)]*[^\s.,;:)]/g, /\/v1\.0\/[^\s,)]*[^\s,;:)]/g];

const EM_DASH = String.fromCharCode(0x2014);

// ICU element types in the intl-messageformat AST.
const TYPE_NAMES = { 1: 'argument', 2: 'number', 3: 'date', 4: 'time', 5: 'select', 6: 'plural', 8: 'tag' };

/** Arguments of a parsed message as name to type, including those nested in plural and select branches. */
export function argumentTypes(ast, out = new Map()) {
  for (const el of ast) {
    if (TYPE_NAMES[el.type] && el.type !== 8) {
      const type = el.type === 6 && el.pluralType === 'ordinal' ? 'selectordinal' : TYPE_NAMES[el.type];
      const prior = out.get(el.value);
      if (!prior || prior === 'argument') out.set(el.value, type);
    }
    if (el.options) for (const option of Object.values(el.options)) argumentTypes(option.value, out);
    if (el.children) argumentTypes(el.children, out);
  }
  return out;
}

function pluralElements(ast, out = []) {
  for (const el of ast) {
    if (el.type === 6) out.push(el);
    if (el.options) for (const option of Object.values(el.options)) pluralElements(option.value, out);
  }
  return out;
}

function selectOptionSets(ast, out = new Map()) {
  for (const el of ast) {
    if (el.type === 5) out.set(el.value, Object.keys(el.options).sort().join('|'));
    if (el.options) for (const option of Object.values(el.options)) selectOptionSets(option.value, out);
  }
  return out;
}

const categoryCache = new Map();

/** Plural categories that whole numbers from 0 to 1000 reach in a locale (the ones a translation must spell out). */
export function requiredCategories(locale, ordinal) {
  const cacheKey = locale + (ordinal ? ':ordinal' : '');
  if (!categoryCache.has(cacheKey)) {
    const rules = new Intl.PluralRules(locale, { type: ordinal ? 'ordinal' : 'cardinal' });
    const set = new Set();
    for (let n = 0; n <= 1000; n++) set.add(rules.select(n));
    categoryCache.set(cacheKey, set);
  }
  return categoryCache.get(cacheKey);
}

function protectedTokens(message) {
  const tokens = [];
  for (const term of PROTECTED_TERMS) if (new RegExp('(^|[^A-Za-z])' + term + '($|[^A-Za-z])').test(message)) tokens.push(term);
  for (const pattern of PROTECTED_PATTERNS) for (const m of message.matchAll(pattern)) tokens.push(m[0]);
  return tokens;
}

/**
 * Problems with a translated message. `problems` fail verification; `warns` are advisory.
 * @param {string} locale Target locale code (a pseudo-locale formats as English).
 * @param {string} source English message.
 * @param {string} message Translated message.
 */
export function messageProblems(locale, source, message) {
  const problems = [];
  const warns = [];
  if (typeof message !== 'string') return { problems: ['is not a string'], warns };
  if (message.includes(EM_DASH)) problems.push('contains an em-dash');
  if (!message.trim() && source.trim()) problems.push('is empty');
  const intl = locale.startsWith('qps') ? 'en' : locale;
  let ast;
  let sourceAst;
  try {
    ast = parse(message);
    sourceAst = parse(source);
  } catch (err) {
    problems.push(`is not valid ICU (${err.message})`);
    return { problems, warns };
  }
  const want = argumentTypes(sourceAst);
  const got = argumentTypes(ast);
  for (const [name, type] of want) {
    if (!got.has(name)) problems.push(`is missing placeholder {${name}}`);
    else if (got.get(name) !== type && !(type === 'argument' || got.get(name) === 'argument')) problems.push(`changes {${name}} from ${type} to ${got.get(name)}`);
  }
  for (const name of got.keys()) if (!want.has(name)) problems.push(`has unknown placeholder {${name}}`);
  const wantSelects = selectOptionSets(sourceAst);
  for (const [name, options] of selectOptionSets(ast)) if (wantSelects.has(name) && wantSelects.get(name) !== options) problems.push(`changes the options of select {${name}}`);
  if (!locale.startsWith('qps') && locale !== 'en') {
    for (const el of pluralElements(ast)) {
      const keys = Object.keys(el.options);
      if (!keys.includes('other')) problems.push(`plural {${el.value}} has no "other" branch`);
      for (const category of requiredCategories(intl, el.pluralType === 'ordinal')) {
        if (!keys.includes(category)) problems.push(`plural {${el.value}} lacks the "${category}" category for ${locale}`);
      }
    }
  }
  // Pseudo-locales accent every literal by design, product names included.
  const tokens = locale.startsWith('qps') ? [] : protectedTokens(source);
  for (const token of tokens) if (!message.includes(token)) problems.push(`drops "${token}", which stays untranslated`);
  try {
    const values = {};
    for (const [name, type] of want) values[name] = type === 'plural' || type === 'number' || type === 'selectordinal' ? 2 : type === 'date' || type === 'time' ? new Date(0) : 'x';
    new IntlMessageFormat(message, intl).format(values);
  } catch (err) {
    problems.push(`does not format (${err.message})`);
  }
  return { problems, warns };
}
