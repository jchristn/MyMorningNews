import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import useLocalStorage from '../../hooks/useLocalStorage.js';
import HorizontalBarChart from '../../charts/HorizontalBarChart.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import { INSIGHT_RANGES, insightsQuery } from './insightsData.js';

// Topics the caller can see, for the topic filters. [] until loaded.
export function useTopics() {
  const { apiClient } = useAuth();
  const [topics, setTopics] = useState([]);
  useEffect(() => {
    let live = true;
    apiClient
      .listTopics({ pageSize: 1000, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => live && setTopics((r && r.items) || []))
      .catch(() => live && setTopics([]));
    return () => {
      live = false;
    };
  }, [apiClient]);
  return topics;
}

// The shared Insights scope (range, topic, and the whole tenant for administrators), kept per browser.
export function useInsightsScope() {
  const [days, setDays] = useLocalStorage('mymorningnews.insights.days', 90);
  const [topicId, setTopicId] = useLocalStorage('mymorningnews.insights.topic', '');
  const [everyone, setEveryone] = useLocalStorage('mymorningnews.insights.everyone', false);
  const range = INSIGHT_RANGES.includes(Number(days)) ? Number(days) : 90;
  return { days: range, setDays, topicId: topicId || '', setTopicId, everyone: Boolean(everyone), setEveryone };
}

// One analytics report for a scope, reloaded when the scope changes.
export function useReport(report, scope, extra = null) {
  const { apiClient } = useAuth();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const requestId = useRef(0);
  const extraKey = JSON.stringify(extra || {});
  const load = useCallback(async () => {
    const id = ++requestId.current;
    setLoading(true);
    setError(null);
    try {
      const result = await apiClient.getAnalytics(report, { ...insightsQuery(scope), ...JSON.parse(extraKey) });
      if (id === requestId.current) setData(result);
    } catch (err) {
      if (id === requestId.current) setError(err);
    } finally {
      if (id === requestId.current) setLoading(false);
    }
  }, [apiClient, report, scope.days, scope.topicId, scope.everyone, extraKey]); // eslint-disable-line react-hooks/exhaustive-deps
  useEffect(() => {
    load();
  }, [load]);
  return { data, error, loading, reload: load };
}

// Range, topic, and (for administrators) whole-tenant controls, plus refresh.
export function ScopeBar({ scope, topics, onRefresh, loading = false, showTopic = true }) {
  const { t } = useTranslation();
  const { isTenantAdmin } = useAuth();
  return (
    <div className="toolbar-row insights-scope">
      <div className="segmented" role="group" aria-label={t('charts.range')}>
        {INSIGHT_RANGES.map((d) => (
          <button type="button" key={d} className={scope.days === d ? 'active' : ''} aria-pressed={scope.days === d} onClick={() => scope.setDays(d)}>
            {t('insights.scope.days', { count: d })}
          </button>
        ))}
      </div>
      {showTopic ? (
        <label className="inline-field">
          <span>{t('insights.scope.topic')}</span>
          <select value={scope.topicId} onChange={(e) => scope.setTopicId(e.target.value)}>
            <option value="">{t('insights.scope.allTopics')}</option>
            {topics.map((x) => (
              <option key={x.id} value={x.id}>
                {x.name}
              </option>
            ))}
          </select>
        </label>
      ) : null}
      {isTenantAdmin ? (
        <label className="checkbox-row">
          <input type="checkbox" checked={scope.everyone} onChange={(e) => scope.setEveryone(e.target.checked)} />
          <span>{t('insights.scope.everyone')}</span>
        </label>
      ) : null}
      {onRefresh ? (
        <button type="button" className="icon-button" onClick={onRefresh} aria-label={t('common.refresh')} title={t('common.refresh')}>
          <RefreshCw size={16} className={loading ? 'spin' : ''} aria-hidden="true" />
        </button>
      ) : null}
    </div>
  );
}

ScopeBar.propTypes = { scope: PropTypes.object.isRequired, topics: PropTypes.array.isRequired, onRefresh: PropTypes.func, loading: PropTypes.bool, showTopic: PropTypes.bool };

// Rate bars with the share as a percentage and the count in the label. Shows the empty text when there are none.
export function RateChart({ items, ariaLabel, loading = false, color }) {
  const { t } = useTranslation();
  const f = useFormat();
  const labelled = useMemo(() => items.map((i) => ({ ...i, label: t('insights.withCount', { label: i.label, count: i.count }) })), [items, t]);
  if (loading && items.length === 0) return <LoadingState />;
  if (items.length === 0) return <div className="chart-empty">{t('insights.noData')}</div>;
  return <HorizontalBarChart items={labelled} ariaLabel={ariaLabel} formatValue={(v) => f.percent(v, 0)} color={color} />;
}

RateChart.propTypes = { items: PropTypes.array.isRequired, ariaLabel: PropTypes.string.isRequired, loading: PropTypes.bool, color: PropTypes.string };

// A percentage or a dash when the value is unknown.
export function pctOrDash(f, value, digits = 0) {
  return value === null || value === undefined ? '-' : f.percent(value, digits);
}
