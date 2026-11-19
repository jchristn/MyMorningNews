import React, { createContext, useContext, useEffect, useMemo, useState, useSyncExternalStore } from 'react';
import { createAudioPlayer, setAudioModeAsync } from 'expo-audio';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { PlayerService } from './PlayerService';
import { createProgressStore } from './progress';
import { createDownloads } from './downloads';

const PlayerContext = createContext(null);

/** One PlayerService for the whole app; the Listen tab, the mini player, and the lock screen share it. */
export function PlayerProvider({ children, service: injected }) {
  const { client, state: authState } = useAuth();
  const { prefs, update } = usePrefs();
  const service = useMemo(() => injected || new PlayerService({ audio: { createAudioPlayer, setAudioModeAsync }, progress: createProgressStore() }), [injected]);
  // Progress sync uses the active server's client.
  useEffect(() => {
    service.setClientProvider(() => client);
  }, [service, client]);
  const downloads = useMemo(() => createDownloads(), []);
  const subscribe = useMemo(() => (listener) => service.subscribe(listener), [service]);
  const state = useSyncExternalStore(subscribe, () => service.getState());

  // The saved speed applies to every track: restore it once per player, then persist the player's speed when it
  // changes. Comparing against the service's live state (not the rendered one) keeps the two from ping-ponging.
  const [savedRate] = useState(prefs.playbackRate);
  useEffect(() => {
    service.setRate(savedRate);
  }, [service, savedRate]);
  useEffect(() => {
    const rate = service.getState().rate;
    if (rate !== prefs.playbackRate) update({ playbackRate: rate });
  }, [state.rate, prefs.playbackRate, service, update]);

  // Signing out stops playback (briefing audio belongs to the account).
  useEffect(() => {
    if (authState.status === 'signedOut' && service.getState().track) void service.stop();
  }, [authState.status, service]);

  const value = useMemo(() => ({ service, state, downloads }), [service, state, downloads]);
  return <PlayerContext.Provider value={value}>{children}</PlayerContext.Provider>;
}

/** `{ service, state, downloads }`. */
export function usePlayer() {
  const value = useContext(PlayerContext);
  if (!value) throw new Error('usePlayer must be used inside PlayerProvider');
  return value;
}
