import React from 'react';
import { StyleSheet, View } from 'react-native';
import { useIsWide } from '@/state/useLayout';
import { useTheme } from '@/theme/ThemeContext';

/**
 * List beside detail on tablets and wide windows (768 points and up); on phones only the list renders and selecting
 * an item pushes the detail route instead. The list column is a fixed share so the reader stays readable.
 */
export function SplitView({ list, detail }) {
  const p = useTheme();
  const wide = useIsWide();
  if (!wide) return list;
  return (
    <View style={{ flex: 1, flexDirection: 'row', backgroundColor: p.bg }}>
      <View style={{ width: '38%', maxWidth: 420, minWidth: 300, borderEndWidth: StyleSheet.hairlineWidth, borderColor: p.border }}>{list}</View>
      <View style={{ flex: 1 }}>{detail}</View>
    </View>
  );
}
