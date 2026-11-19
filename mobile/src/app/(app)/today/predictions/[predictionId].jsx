import React from 'react';
import { useLocalSearchParams } from 'expo-router';
import { PredictionDetail } from '@/screens/today/TodayScreens';

export default function PredictionRoute() {
  const { predictionId } = useLocalSearchParams();
  return <PredictionDetail id={String(predictionId)} />;
}
