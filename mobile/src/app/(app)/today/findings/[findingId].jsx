import React from 'react';
import { useLocalSearchParams } from 'expo-router';
import { FindingDetail } from '@/screens/today/TodayScreens';

export default function FindingRoute() {
  const { findingId } = useLocalSearchParams();
  return <FindingDetail id={String(findingId)} />;
}
