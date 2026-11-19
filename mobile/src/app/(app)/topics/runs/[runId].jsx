import React from 'react';
import { useLocalSearchParams } from 'expo-router';
import { RunDetail } from '@/screens/topics/RunDetail';

export default function RunRoute() {
  const { runId } = useLocalSearchParams();
  return <RunDetail id={String(runId)} />;
}
