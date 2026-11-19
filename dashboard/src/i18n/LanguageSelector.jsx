import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { SUPPORTED_LOCALES } from './localeRegistry.js';
import { useLocale } from './index.js';

export default function LanguageSelector({ compact = false }) {
  const { t } = useTranslation();
  const { locale, setLocale } = useLocale();
  return (
    <label className={compact ? 'language-select compact' : 'language-select'}>
      <span className={compact ? 'sr-only' : 'field-label'}>{t('common.language')}</span>
      <select aria-label={t('common.language')} value={locale} onChange={(e) => setLocale(e.target.value)}>
        {SUPPORTED_LOCALES.map((l) => (
          <option key={l.code} value={l.code} lang={l.pseudo ? undefined : l.code}>
            {l.nativeName}
          </option>
        ))}
      </select>
    </label>
  );
}

LanguageSelector.propTypes = {
  compact: PropTypes.bool
};
