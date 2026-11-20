import DateCell from '../../components/DateCell.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

// DataTable columns for model calls, shared by the model call log and a run's Model calls tab. t and f are the
// translation and format functions. endpointLabel maps an endpoint ID to a name (omit it to hide endpoint names),
// userLabel maps a user ID to an email (omit it to leave out the user column), and promptKeyHidden hides the prompt
// key column by default.
export function llmCallColumns({ t, f, endpointLabel = null, userLabel = null, promptKeyHidden = true }) {
  return [
    { key: 'createdUtc', header: t('models.calls.columns.time'), sortKey: 'createdUtc', pinned: true, render: (r) => <DateCell value={r.createdUtc} /> },
    {
      key: 'task',
      header: t('models.calls.columns.task'),
      render: (r) => (
        <span className="date-cell">
          <span>{t('enums.task.' + r.task, { defaultValue: r.task })}</span>
          {r.isDryRun ? <span className="muted small">{t('models.calls.dryRun')}</span> : null}
        </span>
      )
    },
    {
      key: 'model',
      header: t('models.calls.columns.model'),
      render: (r) => (
        <span className="date-cell">
          <span className="mono">{r.model || ''}</span>
          {endpointLabel && r.modelEndpointId ? <span className="muted small">{endpointLabel(r.modelEndpointId)}</span> : null}
        </span>
      )
    },
    { key: 'promptKey', header: t('models.calls.columns.promptKey'), defaultHidden: promptKeyHidden, render: (r) => <span className="mono">{r.promptKey || ''}</span> },
    ...(userLabel
      ? [
          {
            key: 'user',
            header: t('models.calls.columns.user'),
            className: 'hide-narrow',
            render: (r) =>
              r.userId ? (
                <span className="truncate cell-user" title={userLabel(r.userId)}>
                  {userLabel(r.userId)}
                </span>
              ) : (
                <span className="muted">{t('models.calls.system')}</span>
              )
          }
        ]
      : []),
    { key: 'tokens', header: t('models.calls.columns.tokens'), align: 'right', render: (r) => <span title={t('models.calls.tokensTitle', { input: f.number(r.inputTokens || 0), output: f.number(r.outputTokens || 0) })}>{f.compact(r.totalTokens || 0)}</span> },
    { key: 'ttft', header: t('models.calls.columns.ttft'), sortKey: 'timeToFirstTokenMs', align: 'right', defaultHidden: true, render: (r) => (r.timeToFirstTokenMs === null || r.timeToFirstTokenMs === undefined ? '' : f.duration(r.timeToFirstTokenMs)) },
    { key: 'durationMs', header: t('models.calls.columns.duration'), sortKey: 'durationMs', align: 'right', render: (r) => f.duration(r.durationMs || 0) },
    { key: 'cost', header: t('models.calls.columns.cost'), align: 'right', render: (r) => f.usd(r.estimatedCostUsd || 0) },
    {
      key: 'success',
      header: t('models.calls.columns.outcome'),
      render: (r) =>
        r.success ? (
          <StatusBadge tone="success">{t('models.calls.succeeded')}</StatusBadge>
        ) : (
          <StatusBadge tone="danger" title={r.errorMessage || undefined}>
            {t('models.calls.failed')}
          </StatusBadge>
        )
    }
  ];
}
