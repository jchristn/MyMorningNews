import React, { useRef } from 'react';
import { ActivityIndicator, I18nManager, Pressable, ScrollView, StyleSheet, Switch, Text, TextInput, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTheme } from '@/theme/ThemeContext';
import { CONTENT_MAX_WIDTH, MIN_TOUCH, font, radius, spacing } from '@/theme/palette';

/** Text grows with Dynamic Type and the Android font scale, up to this multiple, so layouts stay usable. */
export const MAX_FONT_SCALE = 2;

/** Themed text. Variants: body, muted, small, title, heading, label, mono. */
export function T({ children, variant = 'body', style, numberOfLines, selectable, accessibilityRole, accessibilityLabel, testID }) {
  const p = useTheme();
  const variants = {
    body: {},
    muted: { color: p.muted },
    small: { color: p.muted, fontSize: font.small },
    title: { fontSize: font.title, fontWeight: '600' },
    heading: { fontSize: font.heading, fontWeight: '700' },
    label: { color: p.muted, fontSize: font.small, fontWeight: '600', letterSpacing: 0.4 },
    mono: { fontFamily: 'Menlo', fontSize: font.mono },
  };
  return (
    <Text
      testID={testID}
      style={[{ color: p.text, fontSize: font.body, textAlign: 'auto' }, variants[variant], style]}
      numberOfLines={numberOfLines}
      selectable={selectable}
      maxFontSizeMultiplier={MAX_FONT_SCALE}
      accessibilityLabel={accessibilityLabel}
      accessibilityRole={accessibilityRole || (variant === 'heading' || variant === 'title' ? 'header' : 'text')}
    >
      {children}
    </Text>
  );
}

/**
 * Scrollable screen body with standard padding, centered in a readable column on wide screens. `scrollRef` receives
 * the ScrollView (the briefing reader scrolls to sections and sources with it).
 */
export function Screen({ children, scroll = true, refreshControl, contentStyle, testID, scrollRef, onScroll }) {
  const p = useTheme();
  if (!scroll) return <View testID={testID} style={[{ flex: 1, backgroundColor: p.bg }, contentStyle]}>{children}</View>;
  return (
    <ScrollView
      ref={scrollRef}
      testID={testID}
      onScroll={onScroll}
      scrollEventThrottle={onScroll ? 64 : undefined}
      style={{ flex: 1, backgroundColor: p.bg }}
      contentContainerStyle={[{ padding: spacing.lg, gap: spacing.md, paddingBottom: spacing.xxl, width: '100%', maxWidth: CONTENT_MAX_WIDTH, alignSelf: 'center' }, contentStyle]}
      keyboardShouldPersistTaps="handled"
      automaticallyAdjustKeyboardInsets
      refreshControl={refreshControl}
    >
      {children}
    </ScrollView>
  );
}

/** A grouped surface; pressable when `onPress` is given. */
export function Card({ children, style, onPress, accessibilityLabel, accessibilityHint, testID }) {
  const p = useTheme();
  const cardStyle = [{ backgroundColor: p.surface, borderColor: p.border, borderWidth: StyleSheet.hairlineWidth, borderRadius: radius.lg, padding: spacing.lg, gap: spacing.sm }, style];
  if (!onPress) return <View testID={testID} style={cardStyle}>{children}</View>;
  return (
    <Pressable
      testID={testID}
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      accessibilityHint={accessibilityHint}
      style={({ pressed }) => [cardStyle, { minHeight: MIN_TOUCH }, pressed ? { opacity: 0.7 } : null]}
    >
      {children}
    </Pressable>
  );
}

/** Themed button with a busy state. Kinds: primary, secondary, danger, ghost. Minimum 44 point target. */
export function Button({ title, onPress, kind = 'primary', busy, disabled, style, accessibilityLabel, accessibilityHint, icon, testID }) {
  const p = useTheme();
  const colors = {
    primary: { bg: p.primary, fg: p.onPrimary, border: p.primary },
    secondary: { bg: p.surfaceAlt, fg: p.text, border: p.border },
    danger: { bg: p.danger, fg: '#ffffff', border: p.danger },
    ghost: { bg: 'transparent', fg: p.primary, border: 'transparent' },
  };
  const c = colors[kind] || colors.primary;
  const off = Boolean(disabled || busy);
  return (
    <Pressable
      testID={testID}
      onPress={off ? undefined : onPress}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel || title}
      accessibilityHint={accessibilityHint}
      accessibilityState={{ disabled: off, busy: Boolean(busy) }}
      style={({ pressed }) => [
        {
          minHeight: MIN_TOUCH,
          paddingHorizontal: spacing.lg,
          borderRadius: radius.md,
          backgroundColor: c.bg,
          borderColor: c.border,
          borderWidth: 1,
          alignItems: 'center',
          justifyContent: 'center',
          flexDirection: 'row',
          gap: spacing.sm,
          opacity: off ? 0.55 : pressed ? 0.8 : 1,
        },
        style,
      ]}
    >
      {busy ? <ActivityIndicator color={c.fg} size="small" /> : icon ? <Ionicons name={icon} size={18} color={c.fg} /> : null}
      <Text maxFontSizeMultiplier={MAX_FONT_SCALE} style={{ color: c.fg, fontSize: font.body, fontWeight: '600' }}>
        {title}
      </Text>
    </Pressable>
  );
}

/** Round icon-only button with a required accessibility label. 44 point target. */
export function IconButton({ icon, onPress, accessibilityLabel, size = 24, color, disabled, style, testID }) {
  const p = useTheme();
  return (
    <Pressable
      testID={testID}
      onPress={disabled ? undefined : onPress}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      accessibilityState={{ disabled: Boolean(disabled) }}
      hitSlop={8}
      style={({ pressed }) => [{ minWidth: MIN_TOUCH, minHeight: MIN_TOUCH, alignItems: 'center', justifyContent: 'center', borderRadius: radius.pill, opacity: disabled ? 0.4 : pressed ? 0.6 : 1 }, style]}
    >
      <Ionicons name={icon} size={size} color={color || p.text} />
    </Pressable>
  );
}

/** Labeled text input with an optional hint and error. */
export function Field({ label, hint, error, style, testID, ...input }) {
  const p = useTheme();
  const ref = useRef(null);
  return (
    <View style={[{ gap: spacing.xs }, style]}>
      <Pressable onPress={() => ref.current && ref.current.focus()} accessible={false}>
        <T variant="small" style={{ fontWeight: '600', color: p.text }}>
          {label}
        </T>
      </Pressable>
      <TextInput
        ref={ref}
        testID={testID}
        accessibilityLabel={label}
        accessibilityHint={hint}
        placeholderTextColor={p.muted}
        maxFontSizeMultiplier={MAX_FONT_SCALE}
        style={{
          minHeight: MIN_TOUCH,
          borderWidth: 1,
          borderColor: error ? p.danger : p.border,
          borderRadius: radius.md,
          paddingHorizontal: spacing.md,
          paddingVertical: spacing.sm,
          color: p.text,
          backgroundColor: p.surface,
          fontSize: font.body,
          textAlign: 'auto',
        }}
        {...input}
      />
      {error ? (
        <T variant="small" style={{ color: p.danger }} accessibilityRole="alert">
          {error}
        </T>
      ) : hint ? (
        <T variant="small">{hint}</T>
      ) : null}
    </View>
  );
}

/** A tappable list row with an optional icon, detail text, and chevron. */
export function ListRow({ title, detail, icon, onPress, accessibilityLabel, accessibilityHint, destructive, right, testID, selected }) {
  const p = useTheme();
  const content = (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md, minHeight: MIN_TOUCH, paddingVertical: spacing.sm }}>
      {icon ? <Ionicons name={icon} size={22} color={destructive ? p.danger : p.primary} /> : null}
      <View style={{ flex: 1, gap: 2 }}>
        <T style={destructive ? { color: p.danger } : null}>{title}</T>
        {detail ? <T variant="small">{detail}</T> : null}
      </View>
      {right}
      {onPress && !right ? <Ionicons name={I18nManager.isRTL ? 'chevron-back' : 'chevron-forward'} size={18} color={p.muted} /> : null}
    </View>
  );
  if (!onPress) return <View testID={testID}>{content}</View>;
  return (
    <Pressable
      testID={testID}
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel || (detail ? title + ', ' + detail : title)}
      accessibilityHint={accessibilityHint}
      accessibilityState={selected ? { selected: true } : undefined}
      style={({ pressed }) => [{ paddingHorizontal: spacing.xs, borderRadius: radius.md }, selected ? { backgroundColor: p.primarySoft } : null, pressed ? { opacity: 0.6 } : null]}
    >
      {content}
    </Pressable>
  );
}

/** A row with a switch; the whole row toggles it. */
export function SwitchRow({ title, detail, value, onValueChange, disabled, testID }) {
  const p = useTheme();
  return (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.md, minHeight: MIN_TOUCH }}>
      <View style={{ flex: 1, gap: 2 }}>
        <T>{title}</T>
        {detail ? <T variant="small">{detail}</T> : null}
      </View>
      <Switch
        testID={testID}
        value={value}
        onValueChange={onValueChange}
        disabled={disabled}
        accessibilityLabel={title}
        accessibilityHint={detail}
        trackColor={{ true: p.primary, false: p.borderStrong }}
      />
    </View>
  );
}

/** A checkbox row (consent, age); the whole row toggles it and is announced as a checkbox. */
export function CheckRow({ title, detail, value, onValueChange, disabled, testID }) {
  const p = useTheme();
  return (
    <Pressable
      testID={testID}
      onPress={disabled ? undefined : () => onValueChange(!value)}
      accessibilityRole="checkbox"
      accessibilityLabel={title}
      accessibilityHint={detail}
      accessibilityState={{ checked: Boolean(value), disabled: Boolean(disabled) }}
      style={({ pressed }) => [{ flexDirection: 'row', alignItems: 'center', gap: spacing.md, minHeight: MIN_TOUCH, opacity: disabled ? 0.55 : pressed ? 0.7 : 1 }]}
    >
      <Ionicons name={value ? 'checkbox' : 'square-outline'} size={24} color={value ? p.primary : p.muted} />
      <View style={{ flex: 1, gap: 2 }}>
        <T>{title}</T>
        {detail ? <T variant="small">{detail}</T> : null}
      </View>
    </Pressable>
  );
}

/** A labeled set of choices (theme, language, speed). The selected one is announced as selected. */
export function Choice({ options, value, onChange, accessibilityLabel }) {
  const p = useTheme();
  return (
    <View accessibilityRole="radiogroup" accessibilityLabel={accessibilityLabel} style={{ gap: spacing.xs }}>
      {options.map((option) => {
        const selected = option.value === value;
        return (
          <Pressable
            key={String(option.value)}
            onPress={() => onChange(option.value)}
            accessibilityRole="radio"
            accessibilityLabel={option.label}
            accessibilityState={{ checked: selected }}
            style={({ pressed }) => [
              { flexDirection: 'row', alignItems: 'center', gap: spacing.md, minHeight: MIN_TOUCH, paddingHorizontal: spacing.sm, borderRadius: radius.md },
              selected ? { backgroundColor: p.primarySoft } : null,
              pressed ? { opacity: 0.6 } : null,
            ]}
          >
            <Ionicons name={selected ? 'radio-button-on' : 'radio-button-off'} size={20} color={selected ? p.primary : p.muted} />
            <View style={{ flex: 1 }}>
              <T>{option.label}</T>
              {option.detail ? <T variant="small">{option.detail}</T> : null}
            </View>
          </Pressable>
        );
      })}
    </View>
  );
}

/** Inline notice: info, warning, danger, or success. */
export function Banner({ kind = 'info', title, children, testID }) {
  const p = useTheme();
  const tones = {
    info: { bg: p.infoSoft, fg: p.text, icon: 'information-circle-outline', iconColor: p.info },
    warning: { bg: p.warningSoft, fg: p.text, icon: 'warning-outline', iconColor: p.warning },
    danger: { bg: p.dangerSoft, fg: p.text, icon: 'alert-circle-outline', iconColor: p.danger },
    success: { bg: p.successSoft, fg: p.text, icon: 'checkmark-circle-outline', iconColor: p.success },
  };
  const tone = tones[kind] || tones.info;
  return (
    <View testID={testID} accessibilityRole={kind === 'danger' ? 'alert' : undefined} style={{ flexDirection: 'row', gap: spacing.sm, padding: spacing.md, borderRadius: radius.md, backgroundColor: tone.bg }}>
      <Ionicons name={tone.icon} size={20} color={tone.iconColor} />
      <View style={{ flex: 1, gap: 2 }}>
        {title ? <T style={{ fontWeight: '600', color: tone.fg }}>{title}</T> : null}
        {typeof children === 'string' ? <T variant="small" style={{ color: tone.fg }}>{children}</T> : children}
      </View>
    </View>
  );
}

/** Small pill (status, tier). */
export function Badge({ label, tone = 'neutral' }) {
  const p = useTheme();
  const tones = { neutral: [p.surfaceAlt, p.textSecondary], primary: [p.primarySoft, p.primary], success: [p.successSoft, p.success], warning: [p.warningSoft, p.warning], danger: [p.dangerSoft, p.danger] };
  const [bg, fg] = tones[tone] || tones.neutral;
  return (
    <View style={{ alignSelf: 'flex-start', paddingHorizontal: spacing.sm, paddingVertical: 2, borderRadius: radius.pill, backgroundColor: bg }}>
      <Text maxFontSizeMultiplier={MAX_FONT_SCALE} style={{ color: fg, fontSize: font.small, fontWeight: '600' }}>
        {label}
      </Text>
    </View>
  );
}

/** Section title above a group of cards. */
export function SectionHeader({ title }) {
  return (
    <T variant="label" accessibilityRole="header" style={{ marginTop: spacing.sm }}>
      {title}
    </T>
  );
}

export function Divider() {
  const p = useTheme();
  return <View style={{ height: StyleSheet.hairlineWidth, backgroundColor: p.border }} />;
}

/** One label and value pair. */
export function KeyValue({ label, value, selectable }) {
  return (
    <View style={{ flexDirection: 'row', gap: spacing.md, minHeight: 28, alignItems: 'center' }}>
      <T variant="muted" style={{ flex: 1 }}>
        {label}
      </T>
      <T selectable={selectable} style={{ flex: 1, textAlign: 'right' }}>
        {value}
      </T>
    </View>
  );
}
