import React from 'react';
import { RefreshControl, View } from 'react-native';
import { ResourceView } from '@/components/states';
import { Badge, Card, Screen, T } from '@/components/ui';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';

/**
 * A detail screen for one API read: pull to refresh, and the loading, error, "later server update", and not found
 * states. `children(data, resource)` renders the content; `resource.setData` applies an action's result and
 * `resource.poll()` refetches quietly.
 */
export function DetailScreen({ load, deps, feature, children, testID, scrollRef }) {
  const p = useTheme();
  const resource = useResource(load, deps);
  return (
    <Screen testID={testID} scrollRef={scrollRef} refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <ResourceView resource={resource} feature={feature}>
        {(data) => children(data, resource)}
      </ResourceView>
    </Screen>
  );
}

/** A detail header: title, optional subtitle, and badges. */
export function DetailHeader({ title, subtitle, badges }) {
  return (
    <View style={{ gap: spacing.xs }}>
      <T variant="heading" selectable>
        {title}
      </T>
      {subtitle ? <T variant="muted">{subtitle}</T> : null}
      {badges && badges.filter(Boolean).length ? (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs }}>
          {badges.filter(Boolean).map((b) => (
            <Badge key={b.label} label={b.label} tone={b.tone} />
          ))}
        </View>
      ) : null}
    </View>
  );
}

/** A list item card: title, badges, and one or two lines of detail. */
export function ItemCard({ title, badges, lines, onPress, selected, accessibilityLabel, testID, children }) {
  const p = useTheme();
  return (
    <Card onPress={onPress} accessibilityLabel={accessibilityLabel || title} testID={testID} style={selected ? { borderColor: p.primary, backgroundColor: p.primarySoft } : null}>
      <T style={{ fontWeight: '600' }} numberOfLines={3}>
        {title}
      </T>
      {badges && badges.filter(Boolean).length ? (
        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs }}>
          {badges.filter(Boolean).map((b) => (
            <Badge key={b.label} label={b.label} tone={b.tone} />
          ))}
        </View>
      ) : null}
      {(lines || []).filter(Boolean).map((line, i) => (
        <T key={i} variant="small" numberOfLines={3}>
          {line}
        </T>
      ))}
      {children}
    </Card>
  );
}
