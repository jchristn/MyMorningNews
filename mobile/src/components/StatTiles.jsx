import React from 'react';
import { StyleSheet, View } from 'react-native';
import { useTheme } from '@/theme/ThemeContext';
import { radius, spacing } from '@/theme/palette';
import { T } from './ui';

/**
 * Headline numbers as tiles that wrap two or more per row (plan section 25.6 "stat tiles"). Each tile is one
 * accessibility element that reads its label, value, and detail.
 *
 * @param {{ tiles: { key: string, label: string, value: string, detail?: string }[], testID?: string }} props
 */
export function StatTiles({ tiles, testID }) {
  const p = useTheme();
  return (
    <View testID={testID} style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
      {(tiles || []).map((tile) => (
        <View
          key={tile.key}
          testID={'tile-' + tile.key}
          accessible
          accessibilityLabel={[tile.label, tile.value, tile.detail].filter(Boolean).join(', ')}
          style={{ flexGrow: 1, flexBasis: 140, backgroundColor: p.surface, borderColor: p.border, borderWidth: StyleSheet.hairlineWidth, borderRadius: radius.lg, padding: spacing.md, gap: 2 }}
        >
          <T variant="label">{tile.label}</T>
          <T variant="heading">{tile.value}</T>
          {tile.detail ? <T variant="small">{tile.detail}</T> : null}
        </View>
      ))}
    </View>
  );
}
