import { useCallback, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useCanReadCalls from '../hooks/useCanReadCalls.js';
import ModelKeyWarning from '../components/ModelKeyWarning.jsx';
import PageHeader from '../components/PageHeader.jsx';
import AssignmentsTab from './models/AssignmentsTab.jsx';
import CallsTab from './models/CallsTab.jsx';
import EndpointsTab from './models/EndpointsTab.jsx';
import useModelEndpoints from './models/useModelEndpoints.js';

const TABS = ['endpoints', 'assignments', 'calls'];

export default function ModelsView() {
  const { t } = useTranslation();
  const { isTenantAdmin } = useAuth();
  const [params, setParams] = useSearchParams();
  const [changed, setChanged] = useState(0);
  const { endpoints } = useModelEndpoints(changed);
  const callsAllowed = useCanReadCalls();
  const requested = params.get('tab') || (params.get('call') ? 'calls' : 'endpoints');
  const visibleTabs = TABS.filter((id) => id !== 'calls' || callsAllowed);
  const tab = visibleTabs.includes(requested) ? requested : 'endpoints';
  const onChanged = useCallback(() => setChanged((n) => n + 1), []);

  function select(id) {
    setParams(id === 'endpoints' ? {} : { tab: id }, { replace: true });
  }

  return (
    <div className="page">
      <PageHeader title={t('nav.items.models')} summary={t('routes.models.summary')} />
      <ModelKeyWarning refreshSignal={changed} />
      {!isTenantAdmin ? <div className="notice notice-info">{t('models.memberNotice')}</div> : null}
      <section className="panel">
        <div className="tabs" role="tablist" aria-label={t('nav.items.models')}>
          {visibleTabs.map((id) => (
            <button type="button" role="tab" key={id} aria-selected={tab === id} className={tab === id ? 'active' : ''} onClick={() => select(id)}>
              {t('models.tabs.' + id)}
            </button>
          ))}
        </div>
        {tab === 'endpoints' ? <EndpointsTab canWrite={isTenantAdmin} canExecute={isTenantAdmin} focusId={params.get('endpoint')} onChanged={onChanged} /> : null}
        {tab === 'assignments' ? <AssignmentsTab endpoints={endpoints} canWrite={isTenantAdmin} initialTopicId={params.get('topic')} /> : null}
        {tab === 'calls' ? <CallsTab endpoints={endpoints} initialCallId={params.get('call')} onCallClosed={() => params.get('call') && setParams({ tab: 'calls' }, { replace: true })} /> : null}
      </section>
    </div>
  );
}
