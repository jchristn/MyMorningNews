import React from 'react';
import { View } from 'react-native';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { T } from './ui';

/** Split Markdown into blocks: headings, bullet items, quotes, and paragraphs. Inline markup is stripped. */
export function markdownBlocks(text) {
  const lines = String(text || '').replace(/\r\n/g, '\n').split('\n');
  const blocks = [];
  let paragraph = [];
  const strip = (s) =>
    s
      .replace(/!\[[^\]]*\]\([^)]*\)/g, '')
      .replace(/\[([^\]]+)\]\(([^)]+)\)/g, '$1')
      .replace(/(\*\*|__)(.+?)\1/g, '$2')
      .replace(/(\*|_)(.+?)\1/g, '$2')
      .replace(/`([^`]+)`/g, '$1')
      .trim();
  const flush = () => {
    if (paragraph.length) blocks.push({ type: 'paragraph', text: strip(paragraph.join(' ')) });
    paragraph = [];
  };
  for (const raw of lines) {
    const line = raw.trimEnd();
    const heading = line.match(/^(#{1,6})\s+(.*)$/);
    const bullet = line.match(/^\s*(?:[-*+]|\d+\.)\s+(.*)$/);
    const quote = line.match(/^>\s?(.*)$/);
    if (!line.trim()) flush();
    else if (heading) {
      flush();
      blocks.push({ type: 'heading', level: heading[1].length, text: strip(heading[2]) });
    } else if (bullet) {
      flush();
      blocks.push({ type: 'bullet', text: strip(bullet[1]) });
    } else if (quote) {
      flush();
      blocks.push({ type: 'quote', text: strip(quote[1]) });
    } else paragraph.push(line.trim());
  }
  flush();
  return blocks.filter((b) => b.text);
}

/**
 * Minimal Markdown reader for briefings: headings, paragraphs, bullets, and quotes, selectable and scaled with
 * Dynamic Type. Links render as their text. The full reader (linked findings, evidence, sources) arrives with the
 * briefing API in Phase 7.
 */
export function Markdown({ text }) {
  const p = useTheme();
  return (
    <View style={{ gap: spacing.sm }}>
      {markdownBlocks(text).map((block, i) => {
        if (block.type === 'heading') {
          return (
            <T key={i} variant={block.level <= 2 ? 'heading' : 'title'} style={{ marginTop: spacing.sm }}>
              {block.text}
            </T>
          );
        }
        if (block.type === 'bullet') {
          return (
            <View key={i} style={{ flexDirection: 'row', gap: spacing.sm }}>
              <T>{'•'}</T>
              <T selectable style={{ flex: 1 }}>
                {block.text}
              </T>
            </View>
          );
        }
        if (block.type === 'quote') {
          return (
            <View key={i} style={{ borderStartWidth: 3, borderColor: p.borderStrong, paddingStart: spacing.md }}>
              <T selectable variant="muted">
                {block.text}
              </T>
            </View>
          );
        }
        return (
          <T key={i} selectable style={{ lineHeight: 24 }}>
            {block.text}
          </T>
        );
      })}
    </View>
  );
}
