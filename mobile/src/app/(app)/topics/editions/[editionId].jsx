import React from 'react';
import { useLocalSearchParams } from 'expo-router';
import { EditionDetail } from '@/screens/editions/EditionScreens';

export default function EditionRoute() {
  const { editionId } = useLocalSearchParams();
  return <EditionDetail id={String(editionId)} />;
}
