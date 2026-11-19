import React from 'react';
import { useLocalSearchParams } from 'expo-router';
import { PredictionDetail } from '@/screens/research/PredictionScreens';

export default function PredictionRoute() {
  const { predictionId } = useLocalSearchParams();
  return <PredictionDetail id={String(predictionId)} />;
}
