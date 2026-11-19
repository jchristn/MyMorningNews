import { useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { diffLines, diffStats, withContext } from '../../utils/diff.js';

// Unified line diff with line numbers on both sides. Unchanged runs collapse unless "show all" is on.
export default function DiffView({ oldText, newText }) {
  const { t } = useTranslation();
  const [all, setAll] = useState(false);
  const lines = useMemo(() => diffLines(oldText, newText), [oldText, newText]);
  const stats = diffStats(lines);
  const shown = all ? lines : withContext(lines, 3);

  return (
    <div className="stack">
      <div className="toolbar-row">
        <span className="row-actions">
          <span className="badge badge-success">{t('prompts.diff.added', { count: stats.added })}</span>
          <span className="badge badge-danger">{t('prompts.diff.removed', { count: stats.removed })}</span>
        </span>
        <label className="checkbox-row">
          <input type="checkbox" checked={all} onChange={(e) => setAll(e.target.checked)} />
          <span>{t('prompts.diff.showAll')}</span>
        </label>
      </div>
      {stats.added === 0 && stats.removed === 0 ? (
        <p className="muted">{t('prompts.diff.identical')}</p>
      ) : (
        <div className="diff-view" role="table" aria-label={t('prompts.diff.label')}>
          {shown.map((line, index) =>
            line.type === 'skip' ? (
              <div key={'skip-' + index} className="diff-skip" role="row">
                {t('prompts.diff.skipped', { count: line.count })}
              </div>
            ) : (
              <div key={index} className={'diff-line diff-' + line.type} role="row">
                <span className="ln">{line.oldLine || ''}</span>
                <span className="ln">{line.newLine || ''}</span>
                <span className="sign" aria-label={line.type === 'add' ? t('prompts.diff.addedLine') : line.type === 'remove' ? t('prompts.diff.removedLine') : undefined}>
                  {line.type === 'add' ? '+' : line.type === 'remove' ? '-' : ' '}
                </span>
                <span>{line.text}</span>
              </div>
            )
          )}
        </div>
      )}
    </div>
  );
}

DiffView.propTypes = { oldText: PropTypes.string, newText: PropTypes.string };
