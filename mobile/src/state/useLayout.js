import { useEffect, useState } from 'react';
import { AccessibilityInfo, useWindowDimensions } from 'react-native';
import { WIDE_BREAKPOINT } from '@/theme/palette';

/** Tablets and wide windows (768 points and up, either orientation) get split views and the persistent mini player. */
export function isWideWidth(width) {
  return width >= WIDE_BREAKPOINT;
}

export function useIsWide() {
  const { width } = useWindowDimensions();
  return isWideWidth(width);
}

/** True when the user asked the system to reduce motion; animations are skipped. */
export function useReducedMotion() {
  const [reduced, setReduced] = useState(false);
  useEffect(() => {
    let alive = true;
    AccessibilityInfo.isReduceMotionEnabled()
      .then((value) => {
        if (alive) setReduced(Boolean(value));
      })
      .catch(() => undefined);
    const sub = AccessibilityInfo.addEventListener('reduceMotionChanged', (value) => setReduced(Boolean(value)));
    return () => {
      alive = false;
      sub.remove();
    };
  }, []);
  return reduced;
}
