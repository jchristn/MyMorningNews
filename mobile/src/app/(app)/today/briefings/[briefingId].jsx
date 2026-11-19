import React from 'react';
import { useLocalSearchParams } from 'expo-router';
import { BriefingDetail } from '@/screens/today/BriefingDetail';

export default function BriefingRoute() {
  const { briefingId } = useLocalSearchParams();
  return <BriefingDetail id={String(briefingId)} />;
}
