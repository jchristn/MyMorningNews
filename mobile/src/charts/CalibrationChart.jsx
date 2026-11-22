import React, { useState } from 'react';
import { I18nManager, View } from 'react-native';
import Svg, { Circle, Line, Rect } from 'react-native-svg';
import { T } from '@/components/ui';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { calibrationLayout } from './seriesLayout';

/**
 * Hand-rolled reliability diagram (plan section 25.6, chart 15): stated confidence against how often predictions came
 * true. Columns span each confidence bucket up to its observed share, points sit at the bucket's mean confidence
 * (larger when it holds more predictions), and the dashed diagonal is perfect calibration: points below it mean
 * overconfidence. Axis captions are native Text; one accessibility label lists every bucket.
 *
 * @param {{ buckets: object[], formatPercent: (n: number) => string, labels: { x: string, y: string, diagonal: string, bucket: (b: object) => string }, accessibilityLabel: string, testID?: string }} props
 */
export function CalibrationChart({ buckets, formatPercent, labels, accessibilityLabel, testID }) {
  const p = useTheme();
  const [width, setWidth] = useState(0);
  const size = Math.min(width, 360);
  const layout = calibrationLayout(buckets, { size });
  const summary = layout.points.map((pt) => labels.bucket(pt.bucket)).join(', ');
  return (
    <View testID={testID} accessible accessibilityRole="image" accessibilityLabel={accessibilityLabel + '. ' + (summary || labels.empty)} style={{ gap: spacing.xs }} onLayout={(e) => setWidth(e.nativeEvent.layout.width)}>
      <T variant="small">{labels.y}</T>
      {size > 0 ? (
        <View style={[{ alignSelf: 'center' }, I18nManager.isRTL ? { transform: [{ scaleX: -1 }] } : null]}>
          <Svg width={size} height={size}>
            <Rect x={0} y={0} width={size} height={size} fill={p.surfaceAlt} rx={6} />
            {layout.grid.map((g) => (
              <Line key={'v' + g.value} x1={g.x} x2={g.x} y1={layout.diagonal.y2} y2={layout.diagonal.y1} stroke={p.border} strokeWidth={1} />
            ))}
            {layout.grid.map((g) => (
              <Line key={'h' + g.value} x1={layout.diagonal.x1} x2={layout.diagonal.x2} y1={g.y} y2={g.y} stroke={p.border} strokeWidth={1} />
            ))}
            {layout.columns.map((c, i) => (
              <Rect key={'c' + i} x={c.x} y={c.y} width={c.width} height={c.height} fill={p.primarySoft} />
            ))}
            <Line x1={layout.diagonal.x1} y1={layout.diagonal.y1} x2={layout.diagonal.x2} y2={layout.diagonal.y2} stroke={p.muted} strokeWidth={1.5} strokeDasharray="5 5" />
            {layout.points.map((pt, i) => (
              <Circle key={'p' + i} cx={pt.x} cy={pt.y} r={pt.r} fill={p.chart[0]} stroke={p.surface} strokeWidth={1.5} />
            ))}
          </Svg>
        </View>
      ) : null}
      <View style={{ flexDirection: 'row', justifyContent: 'space-between' }}>
        <T variant="small">{formatPercent(0)}</T>
        <T variant="small" style={{ flexShrink: 1, textAlign: 'center' }}>{labels.x}</T>
        <T variant="small">{formatPercent(1)}</T>
      </View>
      <T variant="small">{labels.diagonal}</T>
    </View>
  );
}
