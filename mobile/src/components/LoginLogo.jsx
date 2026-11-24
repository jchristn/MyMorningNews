import React, { useEffect, useState } from 'react';
import { AccessibilityInfo, Animated, Image, StyleSheet } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '@/theme/ThemeContext';

/** Mounted once per signed-in session: hold the logo for 1.5 seconds, then fade it away. */
export function LoginLogo() {
  const p = useTheme();
  const { t } = useTranslation();
  const [opacity] = useState(() => new Animated.Value(1));
  const [done, setDone] = useState(false);
  useEffect(() => {
    let active = true;
    let animation;
    AccessibilityInfo.isReduceMotionEnabled().catch(() => false).then((reduceMotion) => {
      if (!active) return;
      animation = Animated.sequence([
        Animated.delay(1500),
        Animated.timing(opacity, { toValue: 0, duration: reduceMotion ? 0 : 600, useNativeDriver: true }),
      ]);
      animation.start(({ finished }) => { if (active && finished) setDone(true); });
    });
    return () => { active = false; animation?.stop(); };
  }, [opacity]);
  if (done) return null;
  return <Animated.View testID="login-logo" accessibilityViewIsModal style={[StyleSheet.absoluteFillObject, { backgroundColor: p.bg, alignItems: 'center', justifyContent: 'center', opacity, zIndex: 100 }]}>
    <Image source={require('../../assets/images/logo.png')} style={{ width: 260, height: 87 }} resizeMode="contain" accessibilityRole="image" accessibilityLabel={t('mobile.app.name')} />
  </Animated.View>;
}
