import React, { useState } from 'react';
import { I18nManager, View } from 'react-native';
import Svg, { Circle, G, Line, Path, Rect } from 'react-native-svg';
import { T } from '@/components/ui';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { linePath, seriesLayout } from './seriesLayout';

export const TREND_HEIGHT = 140;

/**
 * Hand-rolled trend chart (react-native-svg; no chart library): a line with points (rates over time) or columns
 * (counts over time). The first and last labels and the top of the scale are native Text so they scale with Dynamic
 * Type; in RTL the time axis runs right to left. The chart has one accessibility label that lists every value.
 *
 * @param {{ rows: { label: string, value: number | null }[], kind?: 'line' | 'columns', max?: number, formatValue: (n: number) => string, accessibilityLabel: string, testID?: string }} props
 */
export function TrendChart({ rows, kind = 'line', max, formatValue, accessibilityLabel, testID, colorIndex = 0 }) {
  const p = useTheme();
  const [width, setWidth] = useState(0);
  const list = Array.isArray(rows) ? rows : [];
  const layout = seriesLayout(list, { width, height: TREND_HEIGHT, max });
  const color = p.chart[colorIndex % p.chart.length];
  const summary = list
    .filter((r) => r.value !== null && r.value !== undefined)
    .map((r) => r.label + ': ' + formatValue(Number(r.value)))
    .join(', ');
  const flip = I18nManager.isRTL ? { transform: [{ scaleX: -1 }] } : null;
  return (
    <View testID={testID} accessible accessibilityRole="image" accessibilityLabel={accessibilityLabel + '. ' + summary} style={{ gap: spacing.xs }}>
      <T variant="small">{formatValue(layout.max)}</T>
      <View onLayout={(e) => setWidth(e.nativeEvent.layout.width)} style={[{ height: TREND_HEIGHT }, flip]}>
        {width > 0 ? (
          <Svg width={width} height={TREND_HEIGHT}>
            {layout.gridY.map((g) => (
              <Line key={g.value} x1={0} x2={width} y1={g.y} y2={g.y} stroke={p.border} strokeWidth={1} strokeDasharray={g.value === 0 ? undefined : '3 4'} />
            ))}
            {kind === 'columns' ? (
              <G>
                {layout.columns.map((c, i) => (
                  <Rect key={i} x={c.x} y={c.y} width={c.width} height={c.height} rx={2} fill={color} />
                ))}
              </G>
            ) : (
              <G>
                <Path d={linePath(layout.segments)} stroke={color} strokeWidth={2.5} fill="none" strokeLinejoin="round" strokeLinecap="round" />
                {layout.points.map((pt) => (
                  <Circle key={pt.index} cx={pt.x} cy={pt.y} r={3} fill={p.surface} stroke={color} strokeWidth={2} />
                ))}
              </G>
            )}
          </Svg>
        ) : null}
      </View>
      {list.length ? (
        <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
          <T variant="small">{list[0].label}</T>
          {list.length > 1 ? <T variant="small">{list[list.length - 1].label}</T> : null}
        </View>
      ) : null}
    </View>
  );
}
