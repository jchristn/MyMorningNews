import { useEffect } from 'react';
import PropTypes from 'prop-types';
import { Moon, Sun } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useTheme } from '../../context/ThemeContext.jsx';
import LanguageSelector from '../../i18n/LanguageSelector.jsx';

// The signed-out layout shared by sign-in, sign-up, email verification, and password reset: the brand panel on one
// side and a form panel with the language and theme controls on the other.
export default function AuthShell({ title, children }) {
  const { t } = useTranslation();
  const { theme, toggleTheme } = useTheme();

  useEffect(() => {
    document.title = title + ' | ' + t('app.name');
  }, [title, t]);

  return (
    <div className="login-page">
      <section className="login-brand" aria-hidden="false">
        <img src="/assets/logo.png" alt={t('app.name')} className="login-logo" />
        <p className="login-tagline">{t('login.tagline')}</p>
      </section>
      <section className="login-panel">
        <div className="login-controls">
          <LanguageSelector compact />
          <button type="button" className="icon-button" onClick={toggleTheme} aria-label={theme === 'dark' ? t('theme.light') : t('theme.dark')} title={theme === 'dark' ? t('theme.light') : t('theme.dark')}>
            {theme === 'dark' ? <Sun size={16} aria-hidden="true" /> : <Moon size={16} aria-hidden="true" />}
          </button>
        </div>
        <div className="login-form">
          {children}
          <p className="login-copyright muted small">{t('login.copyright')}</p>
        </div>
      </section>
    </div>
  );
}

AuthShell.propTypes = { title: PropTypes.string.isRequired, children: PropTypes.node };
