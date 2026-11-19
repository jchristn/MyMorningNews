import { useCallback, useState } from 'react';
import { Braces } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import { pagedQuery } from '../utils/api.js';
import DataTable from '../components/DataTable.jsx';
import DateCell from '../components/DateCell.jsx';
import JsonViewerModal from '../components/JsonViewerModal.jsx';
import PageHeader from '../components/PageHeader.jsx';
import StatusBadge from '../components/StatusBadge.jsx';

export const AUDIT_EVENT_TYPES = [
  'AuthSuccess',
  'AuthFailure',
  'SessionIssued',
  'SessionRefreshed',
  'SessionRevoked',
  'CredentialCreated',
  'CredentialUsed',
  'CredentialRevoked',
  'AuthorizationDenied',
  'AuthorizationBypass',
  'RoleChanged',
  'PermissionChanged',
  'AssignmentChanged',
  'UserChanged',
  'TenantChanged',
  'SettingsChanged',
  'RestartRequested',
  'CredentialRevealed',
  'VaultCredentialChanged'
];

const AUTHZ = ['Permitted', 'DeniedExplicit', 'DeniedImplicit', 'NotFound', 'Conflict'];

function eventTone(type) {
  if (type === 'AuthFailure' || type === 'AuthorizationDenied') return 'danger';
  if (type === 'AuthorizationBypass' || type === 'CredentialRevealed' || type === 'RestartRequested') return 'warning';
  if (type.endsWith('Changed') || type.startsWith('Credential')) return 'info';
  return 'success';
}

export default function AuditView() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [json, setJson] = useState(null);
  const fetchPage = useCallback((q) => apiClient.listAudit(pagedQuery(q)), [apiClient]);

  const columns = [
    { key: 'createdUtc', header: t('audit.columns.time'), sortKey: 'createdUtc', pinned: true, render: (r) => <DateCell value={r.createdUtc} /> },
    {
      key: 'eventType',
      header: t('audit.columns.event'),
      sortKey: 'eventType',
      filter: { type: 'select', param: 'eventType', options: AUDIT_EVENT_TYPES.map((v) => ({ value: v, label: t('audit.events.' + v) })) },
      render: (r) => <StatusBadge tone={eventTone(r.eventType)}>{t('audit.events.' + r.eventType, { defaultValue: r.eventType })}</StatusBadge>
    },
    { key: 'principalId', header: t('audit.columns.principal'), filter: { type: 'text', param: 'principalId' }, render: (r) => <span className="mono truncate">{r.principalId || ''}</span> },
    { key: 'requiredPermission', header: t('audit.columns.permission'), render: (r) => <span className="mono">{r.requiredPermission || ''}</span> },
    {
      key: 'authorizationResult',
      header: t('audit.columns.result'),
      filter: { type: 'select', param: 'authorizationResult', options: AUTHZ.map((v) => ({ value: v, label: t('audit.authz.' + v) })) },
      render: (r) => (r.authorizationResult ? t('audit.authz.' + r.authorizationResult, { defaultValue: r.authorizationResult }) : '')
    },
    { key: 'path', header: t('audit.columns.path'), render: (r) => <span className="mono truncate">{(r.method ? r.method + ' ' : '') + (r.path || '')}</span> },
    { key: 'reason', header: t('audit.columns.reason'), render: (r) => <span className="truncate" title={r.reason || ''}>{r.reason || r.description || ''}</span> },
    { key: 'sourceIp', header: t('audit.columns.sourceIp'), defaultHidden: true, render: (r) => <span className="mono">{r.sourceIp || ''}</span> },
    { key: 'tenantId', header: t('audit.columns.tenant'), defaultHidden: true, render: (r) => <span className="mono">{r.tenantId || ''}</span> }
  ];

  return (
    <div className="page">
      <PageHeader title={t('nav.items.audit')} summary={t('routes.audit.summary')} />
      <section className="panel">
        <DataTable
          tableKey="audit"
          columns={columns}
          fetchPage={fetchPage}
          defaultSort={{ key: 'createdUtc', direction: 'desc' }}
          onRowClick={setJson}
          rowActions={(r) => [{ key: 'json', label: t('common.viewJson'), icon: Braces, onClick: () => setJson(r) }]}
          emptyTitle={t('audit.empty.title')}
          emptyMessage={t('audit.empty.message')}
        />
      </section>
      <JsonViewerModal open={Boolean(json)} title={t('audit.recordTitle')} value={json} onClose={() => setJson(null)} />
    </div>
  );
}
