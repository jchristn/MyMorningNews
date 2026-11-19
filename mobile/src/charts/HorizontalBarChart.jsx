import React, { useState } from 'react';
import { I18nManager, View } from 'react-native';
import Svg, { Rect } from 'react-native-svg';
import { T } from '@/components/ui';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { barLayout } from './barLayout';

/**
 * Hand-rolled horizontal bar chart (react-native-svg; no chart library). Labels and values are native Text so they
 * scale with Dynamic Type and read correctly in RTL; the bars are SVG. The whole chart has one accessibility label
 * that lists every bar, since screen readers cannot read SVG shapes.
 *
 * @param {{ rows: { label: string, value: number }[], formatValue: (n: number) => string, accessibilityLabel: string }} props
 */
export function HorizontalBarChart({ rows, formatValue, accessibilityLabel, testID }) {
  const p = useTheme();
  const [width, setWidth] = useState(0);
  const layout = barLayout(rows, { width, rowHeight: 10, gap: 0 });
  const summary = layout.bars.map((b) => b.label + ': ' + formatValue(b.value)).join(', ');
  return (
    <View testID={testID} accessible accessibilityRole="image" accessibilityLabel={accessibilityLabel + '. ' + summary} style={{ gap: spacing.md }} onLayout={(e) => setWidth(e.nativeEvent.layout.width)}>
      {layout.bars.map((bar, i) => (
        <View key={bar.label + i} style={{ gap: spacing.xs }}>
          <View style={{ flexDirection: 'row', justifyContent: 'space-between', gap: spacing.sm }}>
            <T variant="small" style={{ flex: 1, color: p.text }} numberOfLines={2}>
              {bar.label}
            </T>
            <T variant="small">{formatValue(bar.value)}</T>
          </View>
          {width > 0 ? (
            <Svg width={width} height={10}>
              <Rect x={0} y={0} width={width} height={10} rx={5} fill={p.surfaceAlt} />
              <Rect x={I18nManager.isRTL ? width - bar.width : 0} y={0} width={bar.width} height={10} rx={5} fill={p.chart[i % p.chart.length]} />
            </Svg>
          ) : null}
        </View>
      ))}
    </View>
  );
}
