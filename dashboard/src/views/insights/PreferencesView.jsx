import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import PageHeader from '../../components/PageHeader.jsx';
import { useTopics } from './insightsParts.jsx';
import PreferencesTable, { LearnNowButton } from './PreferencesTable.jsx';

// Learned preferences: proposals to accept or reject, active ones to disable, your own, and the feedback behind
// each. ?id= opens one (email links use it). Learn now runs a learning pass for one topic.
export default function PreferencesView() {
  const { t } = useTranslation();
  const topics = useTopics();
  const [params, setParams] = useSearchParams();
  const [learnTopicId, setLearnTopicId] = useState('');
  const [refresh, setRefresh] = useState(0);

  return (
    <div className="page">
      <PageHeader
        title={t('nav.items.preferences')}
        summary={t('routes.preferences.summary')}
        actions={
          <div className="row-actions">
            <label className="inline-field">
              <span>{t('insights.preferences.learnFor')}</span>
              <select value={learnTopicId} onChange={(e) => setLearnTopicId(e.target.value)}>
                <option value="">{t('insights.preferences.chooseTopic')}</option>
                {topics.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
              </select>
            </label>
            <LearnNowButton topicId={learnTopicId || null} onLearned={() => setRefresh((n) => n + 1)} />
          </div>
        }
      />
      <section className="panel">
        <PreferencesTable key={refresh} topics={topics} initialPreferenceId={params.get('id')} onPreferenceClosed={() => params.get('id') && setParams({}, { replace: true })} />
      </section>
    </div>
  );
}
