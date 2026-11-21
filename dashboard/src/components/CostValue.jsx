import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import useFormat from '../hooks/useFormat.js';
import { costAmount, costState } from '../utils/cost.js';
import UnpricedNote from './UnpricedNote.jsx';

// The smallest amount shown as a number; anything above zero and below it reads "under $0.0001".
const SMALLEST = 0.0001;

// A cost amount as text: currency with four decimals under a dollar and two above, "under $0.0001" for fractions of
// that. Exported for chart labels and tooltips.
export function costText(amount, f, t) {
  if (amount === null || amount === undefined) return '';
  const n = Number(amount);
  if (n > 0 && n < SMALLEST) return t('costs.under', { amount: f.usd(SMALLEST) });
  return f.usd(n);
}

// The cost of a run, a stage, or a breakdown row (anything with estimatedCostUsd, llmCallCount, and
// unpricedCallCount): the amount, "Not priced" when calls happened but none had a price, or "No model calls". A
// marker explains unpriced calls. compact shows a dash instead of "No model calls" (stage rows), and pending shows a
// dash for a run that has not made calls yet. onOpen makes the amount a button (a stage's calls).
export default function CostValue({ totals, compact = false, pending = false, onOpen, openLabel, strong = false }) {
  const { t } = useTranslation();
  const f = useFormat();
  const state = costState(totals);
  const calls = totals ? Number(totals.llmCallCount) || 0 : 0;
  const unpriced = totals ? Number(totals.unpricedCallCount) || 0 : 0;

  if (state === 'none') {
    if (compact || pending) {
      return (
        <span className="muted cost-value" title={t('costs.noCalls')}>
          <span aria-hidden="true">-</span>
          <span className="sr-only">{t('costs.noCalls')}</span>
        </span>
      );
    }
    return <span className="muted cost-value small">{t('costs.noCalls')}</span>;
  }

  if (state === 'unpriced') {
    return (
      <span className="cost-value">
        <span className="muted">{t('costs.notPriced')}</span>
        <UnpricedNote unpriced={unpriced || calls} total={calls} all />
      </span>
    );
  }

  const text = costText(costAmount(totals), f, t);
  const amount = strong ? <strong>{text}</strong> : text;
  return (
    <span className="cost-value">
      {onOpen ? (
        <button type="button" className="link-button cost-open" onClick={onOpen} title={openLabel || undefined} data-ignore-row-click="true">
          {amount}
          {openLabel ? <span className="sr-only">{' ' + openLabel}</span> : null}
        </button>
      ) : (
        <span className="cost-amount">{amount}</span>
      )}
      {state === 'partial' ? <UnpricedNote unpriced={unpriced} total={calls} /> : null}
    </span>
  );
}

CostValue.propTypes = {
  totals: PropTypes.object,
  compact: PropTypes.bool,
  pending: PropTypes.bool,
  onOpen: PropTypes.func,
  openLabel: PropTypes.string,
  strong: PropTypes.bool
};
