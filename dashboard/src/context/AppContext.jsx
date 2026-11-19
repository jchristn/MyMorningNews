import { createContext, useCallback, useContext, useMemo, useState } from 'react';
import PropTypes from 'prop-types';

const SIDEBAR_KEY = 'mymorningnews.sidebarCollapsed';
const AppContext = createContext(null);

let nextId = 1;

export function AppProvider({ children }) {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(() => {
    try {
      return window.localStorage.getItem(SIDEBAR_KEY) === 'true';
    } catch {
      return false;
    }
  });
  const [mobileNavOpen, setMobileNavOpen] = useState(false);
  const [notifications, setNotifications] = useState([]);

  const toggleSidebar = useCallback(() => {
    setSidebarCollapsed((c) => {
      try {
        window.localStorage.setItem(SIDEBAR_KEY, String(!c));
      } catch {
        // Ignore storage failures.
      }
      return !c;
    });
  }, []);

  const removeNotification = useCallback((id) => {
    setNotifications((list) => list.filter((n) => n.id !== id));
  }, []);

  // type: success | error | warning | info. message is already translated by the caller.
  const addNotification = useCallback(
    (message, type = 'info', timeoutMs = 5000) => {
      const id = nextId++;
      setNotifications((list) => [...list, { id, message, type }]);
      if (timeoutMs > 0) window.setTimeout(() => removeNotification(id), timeoutMs);
      return id;
    },
    [removeNotification]
  );

  const value = useMemo(
    () => ({ sidebarCollapsed, toggleSidebar, mobileNavOpen, setMobileNavOpen, notifications, addNotification, removeNotification }),
    [sidebarCollapsed, toggleSidebar, mobileNavOpen, notifications, addNotification, removeNotification]
  );
  return <AppContext.Provider value={value}>{children}</AppContext.Provider>;
}

AppProvider.propTypes = { children: PropTypes.node };

export function useApp() {
  const ctx = useContext(AppContext);
  if (!ctx) throw new Error('useApp must be used inside AppProvider');
  return ctx;
}
