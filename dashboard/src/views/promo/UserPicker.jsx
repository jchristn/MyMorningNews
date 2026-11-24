import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useDebounce from '../../hooks/useDebounce.js';
import FormField from '../../components/FormField.jsx';

// Pick a tenant and then one of its users through the existing admin APIs (listTenants, listUsers). value is
// { tenantId, userId, email }; onChange receives the same shape.
export default function UserPicker({ value, onChange, idPrefix = 'pick' }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [tenants, setTenants] = useState([]);
  const [users, setUsers] = useState([]);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(false);
  const debounced = useDebounce(search, 300);
  const tenantId = value.tenantId;

  useEffect(() => {
    apiClient
      .listTenants({ pageSize: 1000, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => setTenants((r && r.objects) || []))
      .catch(() => setTenants([]));
  }, [apiClient]);

  useEffect(() => {
    if (!tenantId) return undefined;
    let live = true;
    setLoading(true);
    apiClient
      .listUsers(tenantId, { pageSize: 50, search: debounced || undefined, sortBy: 'email', sortDirection: 'Ascending' })
      .then((r) => live && setUsers((r && r.objects) || []))
      .catch(() => live && setUsers([]))
      .finally(() => live && setLoading(false));
    return () => {
      live = false;
    };
  }, [apiClient, tenantId, debounced]);

  return (
    <div className="form-grid">
      <FormField id={idPrefix + '-tenant'} label={t('admin.tenant')}>
        <select id={idPrefix + '-tenant'} value={tenantId} onChange={(e) => onChange({ tenantId: e.target.value, userId: '', email: '' })}>
          {tenants.length === 0 ? <option value={tenantId}>{tenantId}</option> : null}
          {tenants.map((x) => (
            <option key={x.id} value={x.id}>
              {x.name}
            </option>
          ))}
        </select>
      </FormField>
      <FormField id={idPrefix + '-search'} label={t('promo.picker.search')}>
        <input id={idPrefix + '-search'} type="search" value={search} onChange={(e) => setSearch(e.target.value)} placeholder={t('promo.picker.searchPlaceholder')} />
      </FormField>
      <FormField id={idPrefix + '-user'} label={t('promo.picker.user')} help={loading ? t('common.loading') : users.length === 0 ? t('promo.picker.noUsers') : undefined} wide>
        <select
          id={idPrefix + '-user'}
          value={value.userId}
          onChange={(e) => {
            const u = users.find((x) => x.id === e.target.value);
            onChange({ tenantId, userId: e.target.value, email: u ? u.email : '' });
          }}
        >
          <option value="">{t('promo.picker.choose')}</option>
          {users.map((u) => (
            <option key={u.id} value={u.id}>
              {u.email}
            </option>
          ))}
        </select>
      </FormField>
    </div>
  );
}

UserPicker.propTypes = {
  value: PropTypes.shape({ tenantId: PropTypes.string.isRequired, userId: PropTypes.string.isRequired, email: PropTypes.string }).isRequired,
  onChange: PropTypes.func.isRequired,
  idPrefix: PropTypes.string
};
