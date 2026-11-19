import React from 'react';
import { Redirect } from 'expo-router';
import { LoadingState } from '@/components/states';
import { useAuth } from '@/state/AuthContext';

/** Launch: wait for the saved session, then open Today or sign-in. */
export default function Index() {
  const { state } = useAuth();
  if (state.status === 'loading') return <LoadingState />;
  return <Redirect href={state.status === 'signedIn' ? '/today' : '/sign-in'} />;
}
