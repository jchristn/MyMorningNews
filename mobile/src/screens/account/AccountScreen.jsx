import React, { useState } from 'react';
import { View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { SplitView } from '@/components/SplitView';
import { Button, Card, ListRow, Screen, SectionHeader, T } from '@/components/ui';
import { displayName } from '@/lib/format';
import { UsageScreen } from '@/screens/insights/UsageScreen';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { useIsWide } from '@/state/useLayout';
import { spacing } from '@/theme/palette';
import { useServer } from '@/state/ServerContext';
import { DeleteSection, ExportSection } from './DataSections';
import { AboutSection, ServerSection } from './InfoSections';
import { NotificationsSection } from './NotificationsSection';
import { PasswordSection } from './PasswordSection';
import { PlanSection, RedeemSection } from './PlanSections';
import { BillingSection } from './BillingSection';
import { ProvidersSection } from './ProvidersSection';
import { PodcastSection } from './PodcastSection';
import { AppearanceSection, LanguageSection, SecuritySection } from './PreferenceSections';
import { ProfileSection } from './ProfileSection';
import { TopicSlotsSection } from './TopicSlotsSection';
import { useSignOut } from './useSignOut';

/**
 * Account sections, in menu order. Each has a route under /account and renders beside the menu on tablets. `hidden`
 * sections have a route but no menu row (Redeem opens from Plan and from links; Topics that keep running opens from Plan and from the Topics banner; Email is the old address of the email
 * switches, which now live in Notifications); `requires: 'audio'` hides a row when the server has audio turned off.
 */
export const ACCOUNT_SECTIONS = [
  { key: 'profile', icon: 'person-outline', group: 'account', Component: ProfileSection },
  { key: 'password', icon: 'key-outline', group: 'account', Component: PasswordSection },
  { key: 'plan', icon: 'ribbon-outline', group: 'account', Component: PlanSection },
  { key: 'billing', icon: 'card-outline', group: 'account', Component: BillingSection },
  { key: 'providers', icon: 'key-outline', group: 'account', Component: ProvidersSection },
  { key: 'redeem', icon: 'gift-outline', group: 'account', Component: RedeemSection, hidden: true },
  { key: 'slots', icon: 'reorder-four-outline', group: 'account', Component: TopicSlotsSection, hidden: true },
  { key: 'usage', icon: 'stats-chart-outline', group: 'account', Component: UsageScreen },
  { key: 'notifications', icon: 'notifications-outline', group: 'delivery', Component: NotificationsSection },
  { key: 'email', icon: 'mail-outline', group: 'delivery', Component: NotificationsSection, hidden: true },
  { key: 'podcast', icon: 'radio-outline', group: 'delivery', Component: PodcastSection, requires: 'audio' },
  { key: 'language', icon: 'language-outline', group: 'app', Component: LanguageSection },
  { key: 'appearance', icon: 'contrast-outline', group: 'app', Component: AppearanceSection },
  { key: 'security', icon: 'finger-print-outline', group: 'app', Component: SecuritySection },
  { key: 'server', icon: 'server-outline', group: 'app', Component: ServerSection },
  { key: 'about', icon: 'information-circle-outline', group: 'app', Component: AboutSection },
  { key: 'export', icon: 'archive-outline', group: 'data', Component: ExportSection },
  { key: 'delete', icon: 'trash-outline', group: 'data', Component: DeleteSection, destructive: true },
];

const GROUPS = ['account', 'delivery', 'app', 'data'];

/** Menu rows for the server's switches (client config). */
export function visibleSections(config) {
  const audio = !config || config.audioEnabled !== false;
  return ACCOUNT_SECTIONS.filter((s) => !s.hidden && (s.requires !== 'audio' || audio));
}

/**
 * Account: profile, password, plan and promotional codes, usage, notifications (email, phone, and in-app), podcast feeds,
 * language, theme, lock, server, about, export, delete, and sign out.
 */
export function AccountScreen() {
  const { t } = useTranslation();
  const { state } = useAuth();
  const { prefs } = usePrefs();
  const { config } = useServer();
  const sections = visibleSections(config);
  const wide = useIsWide();
  const signOut = useSignOut();
  const [selected, setSelected] = useState('profile');
  const [signingOut, setSigningOut] = useState(false);

  const open = (key) => (wide ? setSelected(key) : router.push('/account/' + key));
  const menu = (
    <Screen testID="account-screen">
      <View style={{ gap: spacing.xs }}>
        <T variant="title">{displayName(state.user)}</T>
        {state.user && state.user.email ? <T variant="small">{state.user.email}</T> : null}
        <T variant="small" numberOfLines={1}>
          {prefs.serverUrl}
        </T>
      </View>
      {GROUPS.map((group) => (
        <View key={group} style={{ gap: spacing.sm }}>
          <SectionHeader title={t('mobile.account.groups.' + group)} />
          <Card>
            {sections.filter((s) => s.group === group).map((s) => (
              <ListRow key={s.key} testID={'account-' + s.key} icon={s.icon} title={t('mobile.account.sections.' + s.key)} destructive={s.destructive} selected={wide && selected === s.key} onPress={() => open(s.key)} />
            ))}
          </Card>
        </View>
      ))}
      <Button
        title={t('mobile.account.signOut')}
        kind="secondary"
        icon="log-out-outline"
        busy={signingOut}
        testID="sign-out"
        onPress={() => {
          setSigningOut(true);
          void signOut().finally(() => setSigningOut(false));
        }}
      />
    </Screen>
  );
  const Section = (ACCOUNT_SECTIONS.find((s) => s.key === selected) || ACCOUNT_SECTIONS[0]).Component;
  return <SplitView list={menu} detail={<Section key={selected} />} />;
}
