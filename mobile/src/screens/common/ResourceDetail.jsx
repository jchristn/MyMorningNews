import React from 'react';
import { RefreshControl } from 'react-native';
import { useTranslation } from 'react-i18next';
import { ResourceView } from '@/components/states';
import { Card, KeyValue, Screen, T } from '@/components/ui';
import { formatDateTime } from '@/lib/format';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { itemDate, itemDetail, itemTitle } from './items';

/**
 * Detail for one item from one API route: title, summary, date, and any extra content from `children(data)`. The
 * full detail views (evidence drawer, score breakdown, resolve and feedback actions) fill in as the server phases land.
 */
export function ResourceDetail({ load, deps, feature, children, testID }) {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const resource = useResource(load, deps);
  return (
    <Screen testID={testID} refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <ResourceView resource={resource} feature={feature}>
        {(data) => (
          <>
            <T variant="heading" selectable>
              {itemTitle(data)}
            </T>
            {itemDetail(data) ? <T variant="muted">{itemDetail(data)}</T> : null}
            {itemDate(data) ? (
              <Card>
                <KeyValue label={t('mobile.common.date')} value={formatDateTime(itemDate(data), i18n.language)} />
              </Card>
            ) : null}
            {children ? children(data) : null}
          </>
        )}
      </ResourceView>
    </Screen>
  );
}
