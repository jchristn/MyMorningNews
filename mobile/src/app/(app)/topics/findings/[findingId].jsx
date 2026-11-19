import React from 'react';
import { useLocalSearchParams } from 'expo-router';
import { FindingDetail } from '@/screens/research/FindingScreens';

export default function TopicFindingRoute() {
  const { findingId } = useLocalSearchParams();
  return <FindingDetail id={String(findingId)} />;
}
