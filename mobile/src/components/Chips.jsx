import React from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTheme } from '@/theme/ThemeContext';
import { MIN_TOUCH, font, radius, spacing } from '@/theme/palette';
import { MAX_FONT_SCALE } from './ui';

/**
 * A row of pill choices: filters, tabs, weights, days. Single choice (`value` is one value) or several
 * (`multiple`, `value` is an array). Each chip is a 44 point target and announces whether it is selected.
 * `scroll` lays the chips out in one horizontal line (tabs and filters); otherwise they wrap.
 */
export function ChipGroup({ options, value, onChange, multiple, scroll, accessibilityLabel, disabled, testID }) {
  const p = useTheme();
  const isOn = (v) => (multiple ? Array.isArray(value) && value.includes(v) : value === v);
  const press = (v) => {
    if (disabled) return;
    if (!multiple) onChange(v);
    else onChange(isOn(v) ? value.filter((x) => x !== v) : [...(Array.isArray(value) ? value : []), v]);
  };
  const chips = options.map((option) => {
    const on = isOn(option.value);
    return (
      <Pressable
        key={String(option.value)}
        testID={option.testID}
        onPress={() => press(option.value)}
        accessibilityRole={multiple ? 'checkbox' : 'radio'}
        accessibilityLabel={option.accessibilityLabel || option.label}
        accessibilityState={multiple ? { checked: on, disabled: Boolean(disabled) } : { selected: on, checked: on, disabled: Boolean(disabled) }}
        style={({ pressed }) => [
          {
            minHeight: MIN_TOUCH,
            paddingHorizontal: spacing.md,
            borderRadius: radius.pill,
            borderWidth: 1,
            borderColor: on ? p.primary : p.border,
            backgroundColor: on ? p.primarySoft : p.surface,
            flexDirection: 'row',
            alignItems: 'center',
            gap: spacing.xs,
            opacity: disabled ? 0.55 : pressed ? 0.7 : 1,
          },
        ]}
      >
        {option.icon ? <Ionicons name={option.icon} size={16} color={on ? p.primary : p.muted} /> : null}
        <Text maxFontSizeMultiplier={MAX_FONT_SCALE} style={{ color: on ? p.primary : p.text, fontSize: font.small, fontWeight: on ? '700' : '500' }}>
          {option.label}
        </Text>
      </Pressable>
    );
  });
  const role = multiple ? undefined : 'radiogroup';
  if (scroll) {
    return (
      <ScrollView testID={testID} horizontal showsHorizontalScrollIndicator={false} accessibilityRole={role} accessibilityLabel={accessibilityLabel} contentContainerStyle={{ gap: spacing.sm, paddingVertical: 2 }}>
        {chips}
      </ScrollView>
    );
  }
  return (
    <View testID={testID} accessibilityRole={role} accessibilityLabel={accessibilityLabel} style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }}>
      {chips}
    </View>
  );
}

/** Buttons side by side that wrap on narrow screens and at large text sizes. */
export function ButtonRow({ children, style }) {
  return <View style={[{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm }, style]}>{children}</View>;
}
