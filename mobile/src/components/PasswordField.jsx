import React, { useState } from 'react';
import { View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { Field, IconButton, T } from './ui';

/**
 * A password input with a show and hide button, for choosing a new password (sign-up, reset). `autoComplete` and
 * `textContentType` default to a new password, so the system offers to make a strong one and save it. The hint and
 * error sit below the input row, so the button stays beside the input at any text size.
 */
export function PasswordField({ hint, error, autoComplete = 'new-password', textContentType = 'newPassword', ...props }) {
  const { t } = useTranslation();
  const p = useTheme();
  const [reveal, setReveal] = useState(false);
  return (
    <View style={{ gap: spacing.xs }}>
      <View>
        <Field {...props} accessibilityHint={error || hint} secureTextEntry={!reveal} autoComplete={autoComplete} textContentType={textContentType} autoCapitalize="none" autoCorrect={false} />
        <IconButton
          icon={reveal ? 'eye-off-outline' : 'eye-outline'}
          accessibilityLabel={reveal ? t('mobile.signIn.hidePassword') : t('mobile.signIn.showPassword')}
          onPress={() => setReveal(!reveal)}
          style={{ position: 'absolute', end: 0, bottom: 0 }}
        />
      </View>
      {error ? (
        <T variant="small" style={{ color: p.danger }} accessibilityRole="alert">
          {error}
        </T>
      ) : hint ? (
        <T variant="small">{hint}</T>
      ) : null}
    </View>
  );
}
