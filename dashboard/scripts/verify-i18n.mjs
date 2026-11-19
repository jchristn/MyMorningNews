#!/usr/bin/env node
// Verifies the dashboard translation catalogs and source:
//   1. every key the source uses exists in the English catalog (missing keys)
//   2. every English key is used by the source (orphaned keys), allowing keys under dynamic prefixes
//   3. every message in every catalog parses as ICU MessageFormat and keeps the English placeholders
//   4. non-English catalogs contain no keys that English lacks
//   5. no user-visible text is hard-coded in JSX (text children and user-facing attributes)
//   6. no catalog value contains an em-dash
// With --pseudo it also builds the qps-ploc and qps-rtl pseudo-locales and formats every message in them.
// Exits non-zero on any failure.
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import * as espree from 'espree';
import IntlMessageFormat from 'intl-messageformat';

const here = dirname(fileURLToPath(import.meta.url));
const root = join(here, '..');
const srcDir = join(root, 'src');
const localesDir = join(srcDir, 'i18n', 'locales');
const pseudo = process.argv.includes('--pseudo');

const problems = [];
const fail = (msg) => problems.push(msg);

// JSX attributes whose values a user sees or hears.
const USER_FACING_ATTRIBUTES = new Set(['title', 'alt', 'placeholder', 'aria-label', 'aria-description', 'aria-valuetext', 'label', 'summary']);

// Literal JSX text that is not language: JSON tokens, HTTP methods, units, and symbols.
const ALLOWED_LITERALS = new Set(['null', 'true', 'false', 'GET', 'POST', 'PUT', 'PATCH', 'DELETE', 'HEAD', 'OPTIONS', 'ms', 'JSON', 'URL', 'ID', 'C#']);

function flatten(obj, prefix = '', out = {}) {
  for (const [k, v] of Object.entries(obj)) {
    const key = prefix ? prefix + '.' + k : k;
    if (v && typeof v === 'object') flatten(v, key, out);
    else out[key] = v;
  }
  return out;
}

function walkFiles(dir, out = []) {
  for (const name of readdirSync(dir)) {
    const p = join(dir, name);
    if (statSync(p).isDirectory()) {
      if (name === 'test' || name === 'locales') continue;
      walkFiles(p, out);
    } else if (/\.(js|jsx)$/.test(name) && !/\.test\.(js|jsx)$/.test(name)) {
      out.push(p);
    }
  }
  return out;
}

function walkAst(node, visit, parent = null) {
  if (!node || typeof node.type !== 'string') return;
  visit(node, parent);
  for (const [key, value] of Object.entries(node)) {
    if (key === 'parent' || key === 'loc' || key === 'range') continue;
    if (Array.isArray(value)) value.forEach((child) => walkAst(child, visit, node));
    else if (value && typeof value.type === 'string') walkAst(value, visit, node);
  }
}

function placeholders(message) {
  const names = new Set();
  for (const m of message.matchAll(/\{\s*([a-zA-Z_][a-zA-Z0-9_]*)\s*[,}]/g)) names.add(m[1]);
  return names;
}

function sampleValues(message) {
  const values = {};
  for (const name of placeholders(message)) values[name] = name === 'count' || name === 'phase' || name === 'total' ? 2 : 'x';
  return values;
}

function checkMessage(locale, key, message, sourceMessage) {
  if (typeof message !== 'string') {
    fail(`${locale}: ${key} is not a string`);
    return;
  }
  if (message.includes(String.fromCharCode(0x2014))) fail(`${locale}: ${key} contains an em-dash`);
  try {
    new IntlMessageFormat(message, locale.startsWith('qps') ? 'en' : locale).format(sampleValues(sourceMessage || message));
  } catch (err) {
    fail(`${locale}: ${key} is not valid ICU (${err.message})`);
  }
  if (sourceMessage !== undefined) {
    const want = placeholders(sourceMessage);
    const got = placeholders(message);
    for (const name of want) if (!got.has(name)) fail(`${locale}: ${key} is missing placeholder {${name}}`);
    for (const name of got) if (!want.has(name)) fail(`${locale}: ${key} has unknown placeholder {${name}}`);
  }
}

// Catalogs
const en = JSON.parse(readFileSync(join(localesDir, 'en.json'), 'utf8'));
const enFlat = flatten(en);
for (const [key, value] of Object.entries(enFlat)) checkMessage('en', key, value);

for (const file of readdirSync(localesDir)) {
  if (!file.endsWith('.json') || file === 'en.json') continue;
  const locale = file.replace(/\.json$/, '');
  const flat = flatten(JSON.parse(readFileSync(join(localesDir, file), 'utf8')));
  for (const [key, value] of Object.entries(flat)) {
    if (!(key in enFlat)) fail(`${locale}: ${key} does not exist in en.json`);
    else checkMessage(locale, key, value, enFlat[key]);
  }
}

// Source
const usedKeys = new Map();
const dynamicPrefixes = new Set();
const stringLiterals = new Set();
const keyProps = /(?:Key|key)$/;

for (const file of walkFiles(srcDir)) {
  const rel = relative(root, file);
  const code = readFileSync(file, 'utf8');
  let ast;
  try {
    ast = espree.parse(code, { ecmaVersion: 'latest', sourceType: 'module', ecmaFeatures: { jsx: true }, loc: true });
  } catch (err) {
    fail(`${rel}: could not parse (${err.message})`);
    continue;
  }
  const where = (node) => `${rel}:${node.loc.start.line}`;
  const use = (key, node) => {
    if (!usedKeys.has(key)) usedKeys.set(key, where(node));
  };

  walkAst(ast, (node, parent) => {
    if (node.type === 'Literal' && typeof node.value === 'string') stringLiterals.add(node.value);

    // t('key'), t('prefix.' + x), i18n.t('key')
    if (node.type === 'CallExpression') {
      const callee = node.callee;
      const isT = (callee.type === 'Identifier' && callee.name === 't') || (callee.type === 'MemberExpression' && callee.property.name === 't');
      const arg = node.arguments[0];
      if (isT && arg) {
        if (arg.type === 'Literal' && typeof arg.value === 'string') use(arg.value, arg);
        else if (arg.type === 'BinaryExpression' && arg.left.type === 'Literal') dynamicPrefixes.add(arg.left.value);
        else if (arg.type === 'BinaryExpression' && arg.left.type === 'BinaryExpression' && arg.left.left.type === 'Literal') dynamicPrefixes.add(arg.left.left.value);
        else if (arg.type === 'TemplateLiteral') dynamicPrefixes.add(arg.quasis[0].value.cooked);
      }
    }

    // labelKey="common.copy" and similar props or object keys that carry a translation key.
    if (node.type === 'JSXAttribute' && keyProps.test(node.name.name || '') && node.value && node.value.type === 'Literal' && /^[a-z][a-zA-Z]*\.[a-zA-Z0-9_.]+$/.test(node.value.value)) {
      use(node.value.value, node);
    }

    // Hard-coded JSX text
    if (node.type === 'JSXText') {
      const text = node.value.replace(/\s+/g, ' ').trim();
      if (text && /\p{L}/u.test(text) && !ALLOWED_LITERALS.has(text)) fail(`${where(node)}: hard-coded JSX text "${text}"`);
    }
    if (node.type === 'JSXExpressionContainer' && parent && parent.type === 'JSXElement' && node.expression.type === 'Literal' && typeof node.expression.value === 'string') {
      const text = node.expression.value.trim();
      if (/\p{L}/u.test(text) && !ALLOWED_LITERALS.has(text)) fail(`${where(node)}: hard-coded JSX text "${text}"`);
    }
    if (node.type === 'JSXAttribute' && USER_FACING_ATTRIBUTES.has(node.name.name) && node.value) {
      const value = node.value.type === 'Literal' ? node.value.value : node.value.type === 'JSXExpressionContainer' && node.value.expression.type === 'Literal' ? node.value.expression.value : null;
      if (typeof value === 'string' && /\p{L}/u.test(value) && !ALLOWED_LITERALS.has(value)) fail(`${where(node)}: hard-coded ${node.name.name}="${value}"`);
    }
  });
}

for (const [key, location] of usedKeys) {
  if (!(key in enFlat)) fail(`missing key ${key} (used at ${location})`);
}

for (const key of Object.keys(enFlat)) {
  if (usedKeys.has(key) || stringLiterals.has(key)) continue;
  if ([...dynamicPrefixes].some((prefix) => prefix && key.startsWith(prefix))) continue;
  fail(`orphaned key ${key} (not referenced by the source)`);
}

// Pseudo-locales
if (pseudo) {
  const { buildExpansionPseudo, buildRtlPseudo } = await import('../src/i18n/pseudo.js');
  for (const [locale, catalog] of [
    ['qps-ploc', buildExpansionPseudo(en)],
    ['qps-rtl', buildRtlPseudo(en)]
  ]) {
    const flat = flatten(catalog);
    for (const key of Object.keys(enFlat)) {
      if (!(key in flat)) fail(`${locale}: ${key} is missing from the generated catalog`);
      else {
        checkMessage(locale, key, flat[key], enFlat[key]);
        if (flat[key] === enFlat[key] && /\p{L}/u.test(enFlat[key].replace(/\{[^}]*\}/g, ''))) fail(`${locale}: ${key} was not transformed`);
      }
    }
  }
}

const count = Object.keys(enFlat).length;
if (problems.length > 0) {
  for (const p of problems) console.error('  ' + p);
  console.error(`i18n verify failed: ${problems.length} problem(s) across ${count} keys.`);
  process.exit(1);
}
console.log(`i18n verify passed: ${count} keys, ${usedKeys.size} referenced directly, ${dynamicPrefixes.size} dynamic prefixes${pseudo ? ', pseudo-locales formatted' : ''}.`);
