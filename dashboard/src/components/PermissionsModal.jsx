import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import ErrorState from './ErrorState.jsx';
import LoadingState from './LoadingState.jsx';
import Modal from './Modal.jsx';

// Effective permissions for a user or credential as a resource by operation matrix.
export default function PermissionsModal({ tenantId, principal, kind, onClose }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const id = principal ? principal.id : null;

  useEffect(() => {
    if (!id) return undefined;
    let cancelled = false;
    const call = kind === 'credential' ? apiClient.getCredentialPermissions(tenantId, id) : apiClient.getUserPermissions(tenantId, id);
    call.then((d) => !cancelled && setData(d)).catch((e) => !cancelled && setError(e));
    return () => {
      cancelled = true;
    };
  }, [apiClient, tenantId, id, kind]);

  const types = data ? Array.from(new Set(data.checks.map((c) => c.resourceType))) : [];
  const ops = data ? Array.from(new Set(data.checks.map((c) => c.operation))) : [];
  const allowed = (rt, op) => data.checks.some((c) => c.resourceType === rt && c.operation === op && c.permitted);

  return (
    <Modal open={Boolean(principal)} title={t('users.permissions.title')} subtitle={principal ? principal.email || principal.name : null} onClose={onClose} size="xlarge">
      <ErrorState error={error} />
      {!data && !error ? <LoadingState /> : null}
      {data ? (
        <>
          {data.bypass ? <div className="notice notice-info">{t('users.permissions.bypass', { reason: data.bypassReason || '' })}</div> : null}
          <div className="table-scroll">
            <table className="matrix">
              <thead>
                <tr>
                  <th scope="col">{t('users.permissions.resource')}</th>
                  {ops.map((op) => (
                    <th scope="col" key={op}>
                      {t('rbac.operations.' + op, { defaultValue: op })}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {types.map((rt) => (
                  <tr key={rt}>
                    <th scope="row">{t('rbac.resources.' + rt, { defaultValue: rt })}</th>
                    {ops.map((op) => (
                      <td key={op} className={allowed(rt, op) ? 'cell-allowed' : 'cell-denied'}>
                        {allowed(rt, op) ? t('users.permissions.allowed') : t('users.permissions.denied')}
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="muted small">{t('users.permissions.grants', { count: data.grants.length })}</p>
        </>
      ) : null}
    </Modal>
  );
}

PermissionsModal.propTypes = {
  tenantId: PropTypes.string.isRequired,
  principal: PropTypes.object,
  kind: PropTypes.oneOf(['user', 'credential']).isRequired,
  onClose: PropTypes.func.isRequired
};
