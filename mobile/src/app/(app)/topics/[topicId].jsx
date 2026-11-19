import React from 'react';
import { useLocalSearchParams } from 'expo-router';
import { TopicWorkspace } from '@/screens/topics/TopicsScreens';

export default function TopicRoute() {
  const { topicId } = useLocalSearchParams();
  return <TopicWorkspace id={String(topicId)} />;
}
