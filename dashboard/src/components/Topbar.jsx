import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useNavigate } from 'react-router-dom';
import { LogOut, Menu, Moon, PanelLeftClose, PanelLeftOpen, RefreshCw, Sun } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { useTheme } from '../context/ThemeContext.jsx';
import useInterval from '../hooks/useInterval.js';
import LanguageSelector from '../i18n/LanguageSelector.jsx';
import CopyButton from './CopyButton.jsx';
import GitHubIcon from './GitHubIcon.jsx';

export const REPOSITORY_URL = 'https://github.com/jchristn/MyMorningNews';

function roleKey(identity) {
  if (!identity) return 'roles.none';
  if (identity.isAdmin) return 'roles.globalAdmin';
  if (identity.isTenantAdmin) return 'roles.tenantAdmin';
  if (identity.principalType === 'Credential') return 'roles.credential';
  return 'roles.member';
}

export default function Topbar({ title, summary, onRefresh }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { apiClient, serverUrl, identity, logout } = useAuth();
  const { toggleSidebar, sidebarCollapsed, setMobileNavOpen } = useApp();
  const { theme, toggleTheme } = useTheme();
  const [health, setHealth] = useState('unknown');

  const check = useCallback(() => {
    apiClient
      .healthCheck()
      .then((h) => setHealth(h && h.status === 'Healthy' ? 'healthy' : 'degraded'))
      .catch(() => setHealth('unreachable'));
  }, [apiClient]);

  useEffect(() => {
    check();
  }, [check]);
  useInterval(check, 15000);

  async function onLogout() {
    await logout();
    navigate('/', { replace: true });
  }

  const healthLabel = t('health.states.' + health);
  const role = t(roleKey(identity));

  return (
    <header className="topbar">
      <div className="topbar-left">
        <button type="button" className="icon-button mobile-only" onClick={() => setMobileNavOpen(true)} aria-label={t('nav.open')} title={t('nav.open')}>
          <Menu size={18} aria-hidden="true" />
        </button>
        <button type="button" className="icon-button desktop-only" onClick={toggleSidebar} aria-label={sidebarCollapsed ? t('nav.expand') : t('nav.collapse')} title={sidebarCollapsed ? t('nav.expand') : t('nav.collapse')}>
          {sidebarCollapsed ? <PanelLeftOpen size={18} aria-hidden="true" /> : <PanelLeftClose size={18} aria-hidden="true" />}
        </button>
        <div className="topbar-title">
          <strong>{title}</strong>
          {summary ? <span className="muted truncate">{summary}</span> : null}
        </div>
      </div>
      <div className="topbar-context">
        <span className="context-chip chip-server mono" title={serverUrl}>
          <span className="truncate">{serverUrl}</span>
          <CopyButton value={serverUrl} labelKey="topbar.copyServerUrl" size={12} />
        </span>
        <span className="context-chip chip-tenant" title={t('topbar.tenantTitle', { tenant: identity ? identity.tenantId : '' })}>
          <span className="chip-label">{t('topbar.tenant')}</span>
          <span className="truncate">{identity ? identity.tenantName || identity.tenantId : ''}</span>
        </span>
        <span className="context-chip chip-user" title={identity ? identity.email || identity.displayName : ''}>
          <span className="truncate">{identity ? identity.email || identity.displayName : ''}</span>
        </span>
        <span className={'badge ' + (identity && identity.isAdmin ? 'badge-danger' : identity && identity.isTenantAdmin ? 'badge-warning' : 'badge-neutral')}>{role}</span>
        <span className={'health-dot health-' + health} role="status" title={t('topbar.health', { state: healthLabel })}>
          <i aria-hidden="true" />
          <span className="sr-only">{t('topbar.health', { state: healthLabel })}</span>
        </span>
      </div>
      <div className="topbar-actions">
        {onRefresh ? (
          <button type="button" className="icon-button" onClick={onRefresh} aria-label={t('common.refresh')} title={t('common.refresh')}>
            <RefreshCw size={16} aria-hidden="true" />
          </button>
        ) : null}
        <LanguageSelector compact />
        <button type="button" className="icon-button" onClick={toggleTheme} aria-label={theme === 'dark' ? t('theme.light') : t('theme.dark')} title={theme === 'dark' ? t('theme.light') : t('theme.dark')}>
          {theme === 'dark' ? <Sun size={16} aria-hidden="true" /> : <Moon size={16} aria-hidden="true" />}
        </button>
        <a className="icon-button" href={REPOSITORY_URL} target="_blank" rel="noreferrer" aria-label={t('topbar.repository')} title={t('topbar.repository')}>
          <GitHubIcon size={16} />
        </a>
        <button type="button" className="icon-button" onClick={onLogout} aria-label={t('topbar.logout')} title={t('topbar.logout')}>
          <LogOut size={16} aria-hidden="true" />
        </button>
      </div>
    </header>
  );
}

Topbar.propTypes = {
  title: PropTypes.string.isRequired,
  summary: PropTypes.string,
  onRefresh: PropTypes.func
};
