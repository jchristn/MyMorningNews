import React, { useState } from 'react';
import { RefreshControl, View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { SplitView } from '@/components/SplitView';
import { EmptyState, ResourceView } from '@/components/states';
import { Card, ListRow, Screen } from '@/components/ui';
import { formatDate } from '@/lib/format';
import { useIsWide } from '@/state/useLayout';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { itemDate, itemDetail, itemTitle } from './items';

/**
 * A list backed by one API route, with loading, error, "later server update", and empty states. On phones selecting
 * an item pushes `detailHref(id)`; on tablets the detail (`renderDetail(id, { onChanged })`) shows beside the list.
 *
 * - `header` renders above the list (filters, a "New" button).
 * - `renderRow(item, { selected, onPress })` replaces the default row (title, detail, date).
 * - `filter(items)` narrows what the server returned.
 */
export function ResourceListScreen({ load, deps = [], feature, empty, detailHref, renderDetail, placeholder, testID, header, renderRow, filter }) {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const wide = useIsWide();
  const [selected, setSelected] = useState(null);
  const resource = useResource(load, deps);
  const all = listItems(resource.data);
  const items = filter ? filter(all) : all;
  const open = (id) => (wide && renderDetail ? setSelected(id) : detailHref ? router.push(detailHref(id)) : undefined);

  const list = (
    <Screen testID={testID} refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      {header}
      <ResourceView resource={resource} feature={feature} isEmpty={() => items.length === 0} empty={<EmptyState icon={empty.icon} title={empty.title} body={empty.body} actionLabel={empty.actionLabel} onAction={empty.onAction} />}>
        {renderRow ? (
          <View style={{ gap: spacing.sm }}>{items.map((item) => <React.Fragment key={item.id}>{renderRow(item, { selected: wide && selected === item.id, onPress: () => open(item.id) })}</React.Fragment>)}</View>
        ) : (
          <Card>
            {items.map((item) => {
              const date = itemDate(item);
              const detail = [itemDetail(item), date ? formatDate(date, i18n.language) : ''].filter(Boolean).join(' · ');
              return (
                <ListRow
                  key={item.id}
                  title={itemTitle(item)}
                  detail={detail}
                  selected={wide && selected === item.id}
                  accessibilityHint={t('mobile.common.openHint')}
                  onPress={detailHref || renderDetail ? () => open(item.id) : undefined}
                />
              );
            })}
          </Card>
        )}
      </ResourceView>
    </Screen>
  );
  if (!renderDetail) return list;
  return <SplitView list={list} detail={selected ? renderDetail(selected, { onChanged: () => void resource.poll() }) : <EmptyState icon="reader-outline" title={placeholder || t('mobile.common.selectItem')} />} />;
}
