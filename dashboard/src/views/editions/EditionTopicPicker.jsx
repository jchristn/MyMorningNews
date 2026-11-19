import { useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { ArrowDown, ArrowUp, Plus, Save, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { TOPIC_STATUS_TONES } from '../../utils/enums.js';
import ErrorState from '../../components/ErrorState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

export function moveItem(list, index, delta) {
  const target = index + delta;
  if (target < 0 || target >= list.length) return list;
  const next = list.slice();
  const [item] = next.splice(index, 1);
  next.splice(target, 0, item);
  return next;
}

// The topics an edition covers, in order. "All topics" (an empty list on the server) covers every active recurring
// topic; choosing topics saves an ordered list. topics are the owner's recurring topics.
export default function EditionTopicPicker({ editionId, links, coveredTopicIds, topics, onSaved, disabled = false }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const initial = useMemo(() => (links || []).slice().sort((a, b) => a.sortOrder - b.sortOrder).map((l) => l.topicId), [links]);
  const [mode, setMode] = useState(initial.length > 0 ? 'chosen' : 'all');
  const [chosen, setChosen] = useState(initial);
  const [adding, setAdding] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    setChosen(initial);
    setMode(initial.length > 0 ? 'chosen' : 'all');
  }, [initial]);

  const byId = useMemo(() => new Map(topics.map((x) => [x.id, x])), [topics]);
  const available = topics.filter((x) => !chosen.includes(x.id));
  const target = mode === 'all' ? [] : chosen;
  const dirty = target.length !== initial.length || target.some((id, i) => id !== initial[i]);
  const covered = (coveredTopicIds || []).map((id) => byId.get(id)).filter(Boolean);

  async function save() {
    if (mode === 'chosen' && chosen.length === 0) {
      setError(new Error(t('editions.topics.chooseOne')));
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const described = await apiClient.setEditionTopics(editionId, target);
      addNotification(t('editions.topics.saved'), 'success');
      if (onSaved) onSaved(described);
    } catch (err) {
      setError(err);
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="stack edition-topics">
      <div className="segmented" role="radiogroup" aria-label={t('editions.topics.mode')}>
        {['all', 'chosen'].map((m) => (
          <button type="button" key={m} role="radio" aria-checked={mode === m} className={mode === m ? 'active' : ''} disabled={disabled} onClick={() => setMode(m)}>
            {t('editions.topics.modes.' + m)}
          </button>
        ))}
      </div>
      {mode === 'all' ? (
        <div className="stack">
          <p className="muted small">{t('editions.topics.allHelp')}</p>
          {covered.length > 0 ? (
            <ul className="chip-list" aria-label={t('editions.topics.covered')}>
              {covered.map((x) => (
                <li key={x.id}>
                  <StatusBadge tone="info">{x.name}</StatusBadge>
                </li>
              ))}
            </ul>
          ) : (
            <p className="muted small">{t('editions.topics.noneActive')}</p>
          )}
        </div>
      ) : (
        <div className="stack">
          <p className="muted small">{t('editions.topics.chosenHelp')}</p>
          {chosen.length === 0 ? <p className="muted small">{t('editions.topics.noneChosen')}</p> : null}
          <ol className="ordered-picker" aria-label={t('editions.topics.chosen')}>
            {chosen.map((id, i) => {
              const topic = byId.get(id);
              const name = topic ? topic.name : id;
              return (
                <li key={id}>
                  <span className="ordered-index muted small">{i + 1}</span>
                  <span className="truncate">{name}</span>
                  {topic && topic.status !== 'Active' ? <StatusBadge tone={TOPIC_STATUS_TONES[topic.status] || 'neutral'}>{t('enums.topicStatus.' + topic.status)}</StatusBadge> : null}
                  <span className="row-actions">
                    <button type="button" className="icon-button" disabled={disabled || i === 0} onClick={() => setChosen((l) => moveItem(l, i, -1))} aria-label={t('editions.topics.moveUp', { name })} title={t('editions.topics.moveUp', { name })}>
                      <ArrowUp size={14} aria-hidden="true" />
                    </button>
                    <button
                      type="button"
                      className="icon-button"
                      disabled={disabled || i === chosen.length - 1}
                      onClick={() => setChosen((l) => moveItem(l, i, 1))}
                      aria-label={t('editions.topics.moveDown', { name })}
                      title={t('editions.topics.moveDown', { name })}
                    >
                      <ArrowDown size={14} aria-hidden="true" />
                    </button>
                    <button type="button" className="icon-button" disabled={disabled} onClick={() => setChosen((l) => l.filter((x) => x !== id))} aria-label={t('editions.topics.remove', { name })} title={t('editions.topics.remove', { name })}>
                      <X size={14} aria-hidden="true" />
                    </button>
                  </span>
                </li>
              );
            })}
          </ol>
          {available.length > 0 ? (
            <div className="row-actions">
              <select value={adding} onChange={(e) => setAdding(e.target.value)} disabled={disabled} aria-label={t('editions.topics.addLabel')}>
                <option value="">{t('editions.topics.pick')}</option>
                {available.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.name}
                  </option>
                ))}
              </select>
              <button
                type="button"
                className="button secondary small"
                disabled={disabled || !adding}
                onClick={() => {
                  setChosen((l) => [...l, adding]);
                  setAdding('');
                }}
              >
                <Plus size={14} aria-hidden="true" /> {t('editions.topics.add')}
              </button>
            </div>
          ) : null}
        </div>
      )}
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <div className="form-actions">
        <button type="button" className="button primary" disabled={disabled || saving || !dirty} onClick={save}>
          <Save size={14} aria-hidden="true" /> {saving ? t('common.saving') : t('editions.topics.save')}
        </button>
      </div>
    </div>
  );
}

EditionTopicPicker.propTypes = {
  editionId: PropTypes.string.isRequired,
  links: PropTypes.array,
  coveredTopicIds: PropTypes.array,
  topics: PropTypes.array.isRequired,
  onSaved: PropTypes.func,
  disabled: PropTypes.bool
};
