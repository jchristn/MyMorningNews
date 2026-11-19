import React from 'react';
import { useLocalSearchParams } from 'expo-router';
import { RequestDetail } from '@/screens/requests/RequestScreens';

export default function RequestRoute() {
  const { requestId } = useLocalSearchParams();
  return <RequestDetail id={String(requestId)} />;
}
