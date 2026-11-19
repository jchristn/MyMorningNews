import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useLocalStorage from '../hooks/useLocalStorage.js';

// The tenant an administration page works in. Global administrators may pick any tenant; tenant administrators are
// fixed to their own.
export function useAdminTenant() {
  const { identity, isAdmin } = useAuth();
  const own = identity ? identity.tenantId : 'default';
  const [picked, setPicked] = useLocalStorage('mymorningnews.admin.tenant', own);
  return [isAdmin ? picked || own : own, setPicked];
}

export default function TenantPicker({ value, onChange }) {
  const { t } = useTranslation();
  const { apiClient, isAdmin } = useAuth();
  const [tenants, setTenants] = useState([]);

  useEffect(() => {
    if (!isAdmin) return;
    apiClient
      .listTenants({ pageSize: 1000, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => setTenants(r.items || []))
      .catch(() => setTenants([]));
  }, [apiClient, isAdmin]);

  if (!isAdmin) return null;
  return (
    <label className="inline-field">
      <span>{t('admin.tenant')}</span>
      <select value={value} onChange={(e) => onChange(e.target.value)}>
        {tenants.length === 0 ? <option value={value}>{value}</option> : null}
        {tenants.map((tenant) => (
          <option key={tenant.id} value={tenant.id}>
            {tenant.name}
          </option>
        ))}
      </select>
    </label>
  );
}

TenantPicker.propTypes = {
  value: PropTypes.string.isRequired,
  onChange: PropTypes.func.isRequired
};
