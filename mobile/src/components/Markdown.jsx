import React, { useMemo } from 'react';
import { Text, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { isSafeUrl, parseBlocks } from '@/lib/markdown';
import { useTheme } from '@/theme/ThemeContext';
import { font, radius, spacing } from '@/theme/palette';
import { openLink } from './LegalFooter';
import { MAX_FONT_SCALE, T } from './ui';

function Inline({ nodes, onCite, linkColor, codeBg, t }) {
  return (nodes || []).map((node, i) => {
    switch (node.type) {
      case 'text':
        return node.value;
      case 'strong':
        return (
          <Text key={i} style={{ fontWeight: '700' }}>
            <Inline nodes={node.children} onCite={onCite} linkColor={linkColor} codeBg={codeBg} t={t} />
          </Text>
        );
      case 'em':
        return (
          <Text key={i} style={{ fontStyle: 'italic' }}>
            <Inline nodes={node.children} onCite={onCite} linkColor={linkColor} codeBg={codeBg} t={t} />
          </Text>
        );
      case 'code':
        return (
          <Text key={i} style={{ fontFamily: 'Menlo', fontSize: font.mono, backgroundColor: codeBg }}>
            {node.value}
          </Text>
        );
      case 'link':
        return (
          <Text
            key={i}
            accessibilityRole="link"
            onPress={isSafeUrl(node.href) ? () => void openLink(node.href) : undefined}
            style={{ color: linkColor, textDecorationLine: 'underline' }}
          >
            <Inline nodes={node.children} onCite={onCite} linkColor={linkColor} codeBg={codeBg} t={t} />
          </Text>
        );
      case 'cite':
        return (
          <Text
            key={i}
            testID={'cite-' + node.n}
            accessibilityRole="link"
            accessibilityLabel={t('mobile.reader.citation', { number: node.n })}
            accessibilityHint={t('mobile.reader.citationHint')}
            onPress={onCite ? () => onCite(node.n) : undefined}
            style={{ color: linkColor, fontWeight: '700', fontSize: font.small }}
          >
            {'[' + node.n + ']'}
          </Text>
        );
      default:
        return null;
    }
  });
}

/**
 * Native Markdown for briefings and topic briefs (src/lib/markdown.js): no HTML and no WebView, so untrusted content
 * cannot run anything. Links open in the in-app browser (http, https, and mailto only). Text is selectable and scales
 * with Dynamic Type.
 *
 * - `blocks` (already parsed, for example by parseBriefing) or `text`.
 * - `onCite(n)` runs when a citation number is tapped.
 * - `onAnchor(key, y)` receives each block's y offset (`blk:<index>`), each list item's offset within its list
 *   (`item:<block>:<item>`), and each heading's (`<heading id>`), so the reader can scroll to sections and sources.
 * - `highlight` is `{ block, item }` to mark one list item (the source a citation jumped to).
 * - `activeHeading` is the heading id of the section being read aloud: its heading is tinted and the blocks under it
 *   carry a bar at the start edge, announced as "Now playing".
 */
export function Markdown({ text, blocks, onCite, onAnchor, highlight, activeHeading, testID }) {
  const p = useTheme();
  const { t } = useTranslation();
  const parsed = useMemo(() => blocks || parseBlocks(text), [blocks, text]);
  const record = (key, y) => {
    if (onAnchor) onAnchor(key, y);
  };
  const inline = (nodes) => <Inline nodes={nodes} onCite={onCite} linkColor={p.primary} codeBg={p.surfaceAlt} t={t} />;
  const body = { color: p.text, fontSize: font.body, lineHeight: 24 };
  // The heading each block sits under (a heading owns itself), so the playing section can be marked as a whole.
  const owners = useMemo(() => {
    const list = [];
    const stack = [];
    for (const block of parsed) {
      if (block.type === 'heading') {
        while (stack.length && stack[stack.length - 1].level >= block.level) stack.pop();
        stack.push(block);
      }
      list.push(stack.map((h) => h.id));
    }
    return list;
  }, [parsed]);
  const playing = (i) => Boolean(activeHeading && owners[i] && owners[i].includes(activeHeading));
  const bar = { borderStartWidth: 3, borderColor: p.primary, paddingStart: spacing.sm };

  const rendered = parsed.map((block, i) => {
    const onLayout = (e) => {
      record('blk:' + i, e.nativeEvent.layout.y);
      if (block.type === 'heading') record(block.id, e.nativeEvent.layout.y);
    };
    if (block.type === 'heading') {
      const size = block.level === 1 ? font.heading : block.level === 2 ? font.title + 2 : font.title;
      const active = block.id === activeHeading;
      return (
        <View
          key={i}
          onLayout={onLayout}
          testID={active ? 'playing-heading' : undefined}
          style={[{ marginTop: i === 0 ? 0 : spacing.md }, playing(i) ? bar : null, active ? { backgroundColor: p.primarySoft, borderRadius: radius.sm, paddingVertical: spacing.xs } : null]}
        >
          <Text
            testID={'heading-' + block.id}
            accessibilityRole="header"
            accessibilityLabel={active ? t('mobile.reader.nowReading', { heading: block.text }) : undefined}
            selectable
            maxFontSizeMultiplier={MAX_FONT_SCALE}
            style={{ color: p.text, fontSize: size, fontWeight: '700' }}
          >
            {inline(block.children)}
          </Text>
        </View>
      );
    }
    if (block.type === 'list') {
      return (
        <View key={i} onLayout={onLayout} style={{ gap: spacing.xs }}>
          {block.items.map((item, k) => {
            const marked = highlight && highlight.block === i && highlight.item === k;
            return (
              <View
                key={k}
                testID={marked ? 'highlighted-item' : undefined}
                onLayout={(e) => record('item:' + i + ':' + k, e.nativeEvent.layout.y)}
                style={[{ flexDirection: 'row', gap: spacing.sm, paddingStart: item.indent * spacing.lg, borderRadius: radius.sm }, marked ? { backgroundColor: p.primarySoft } : null]}
              >
                <Text maxFontSizeMultiplier={MAX_FONT_SCALE} style={[body, { color: p.muted, minWidth: block.ordered ? 24 : 12 }]}>
                  {block.ordered ? (item.number || k + 1) + '.' : '•'}
                </Text>
                <Text selectable maxFontSizeMultiplier={MAX_FONT_SCALE} style={[body, { flex: 1 }]}>
                  {inline(item.children)}
                </Text>
              </View>
            );
          })}
        </View>
      );
    }
    if (block.type === 'quote') {
      return (
        <View key={i} onLayout={onLayout} style={{ borderStartWidth: 3, borderColor: p.borderStrong, paddingStart: spacing.md }}>
          <Text selectable maxFontSizeMultiplier={MAX_FONT_SCALE} style={[body, { color: p.textSecondary, fontStyle: 'italic' }]}>
            {inline(block.children)}
          </Text>
        </View>
      );
    }
    if (block.type === 'code') {
      return (
        <View key={i} onLayout={onLayout} style={{ backgroundColor: p.surfaceAlt, borderRadius: radius.md, padding: spacing.md }}>
          <T variant="mono" selectable>
            {block.value}
          </T>
        </View>
      );
    }
    if (block.type === 'rule') return <View key={i} onLayout={onLayout} style={{ height: 1, backgroundColor: p.border, marginVertical: spacing.sm }} />;
    return (
      <View key={i} onLayout={onLayout}>
        <Text selectable maxFontSizeMultiplier={MAX_FONT_SCALE} style={body}>
          {inline(block.children)}
        </Text>
      </View>
    );
  });

  return (
    <View testID={testID} style={{ gap: spacing.sm }}>
      {rendered.map((node, i) => (playing(i) && parsed[i].type !== 'heading' ? <View key={'p' + i} style={bar}>{node}</View> : node))}
    </View>
  );
}
