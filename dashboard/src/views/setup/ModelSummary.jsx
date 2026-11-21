import PropTypes from 'prop-types';
import { CircleCheck, LoaderCircle, RefreshCw, Wrench } from 'lucide-react';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import { isAgentCli, isCliDefaultModel } from '../../utils/modelAuth.js';
import CommandChip from '../../components/modelSignIn/CommandChip.jsx';
import { failureHint, methodLabel, providerName } from '../../components/modelSignIn/signInCatalog.js';
import SetupStateBadge from '../../components/SetupStateBadge.jsx';
import { isBrokenCheck } from './setupState.js';

// The model a check reports, in words: the CLI's default model reads as such instead of "default".
export function modelText(check, t) {
  if (isAgentCli(check.provider) && isCliDefaultModel(check.model)) return t('setup.field.cliDefault');
  return check.model || '';
}

// One configured model endpoint: provider, sign-in method, model, and when it last passed, with what went wrong and
// what to try when it did not.
function CheckDetails({ check, container }) {
  const { t } = useTranslation();
  const f = useFormat();
  const broken = check.state !== 'Ready';
  const hint = check.state === 'Failed' && check.authMethod ? failureHint(check.provider, check.authMethod, container) : null;
  const rows = [];
  if (check.provider) rows.push({ key: 'provider', label: t('setup.connected.provider'), value: providerName(t, check.provider) });
  if (check.provider)
    rows.push({
      key: 'method',
      label: t('setup.connected.method'),
      value: check.usesPlatformKey ? t('setup.connected.platformKey') : methodLabel(t, check.provider, check.authMethod || 'ApiKey')
    });
  if (check.provider)
    rows.push({
      key: 'model',
      label: t('setup.connected.model'),
      value: modelText(check, t),
      mono: !(isAgentCli(check.provider) && isCliDefaultModel(check.model))
    });
  return (
    <div className="setup-check-details" data-testid={'setup-check-' + check.key}>
      {rows.length > 0 ? (
        <dl className="setup-facts">
          {rows.map((row) => (
            <div key={row.key}>
              <dt>{row.label}</dt>
              <dd className={row.mono ? 'mono' : undefined}>{row.value}</dd>
            </div>
          ))}
        </dl>
      ) : null}
      {check.verifiedUtc && !broken ? (
        <p className="muted small">{t('setup.connected.verified', { time: f.relative(check.verifiedUtc) })}</p>
      ) : null}
      {broken && check.message ? (
        <p className={'setup-check-message ' + (check.state === 'Unverified' ? 'muted' : 'notice notice-danger')} dir="auto">
          {check.message}
        </p>
      ) : null}
      {hint ? (
        <p className="setup-result-hint small">
          <span>{t(hint.key, hint.values)}</span> {hint.command ? <CommandChip command={hint.command} /> : null}
        </p>
      ) : null}
    </div>
  );
}

CheckDetails.propTypes = { check: PropTypes.object.isRequired, container: PropTypes.string };

// Step 1 when a model is set up: a summary with Change, or the problem with Fix and Test again. justConnected shows
// the success message after Connect and test, and takes focus (resultRef) so screen readers hear it.
export default function ModelSummary({
  checks,
  stepState,
  canConfigure,
  container,
  justConnected,
  resultRef,
  verifying,
  onChange,
  onVerify
}) {
  const { t } = useTranslation();
  const ready = stepState === 'ready';
  // Fix when a configured endpoint fails; Change while it works or is still being checked.
  const fixing = stepState === 'broken';
  const shown = ready ? checks : checks.filter((c) => c.state !== 'Ready');
  const first = shown[0] || checks[0] || null;
  const canTest = canConfigure && shown.some((c) => c.state === 'Failed' || c.state === 'Unverified');
  const noEndpoint = checks.some((c) => c.key === 'model:none');
  return (
    <div className="setup-summary stack-tight">
      {justConnected ? (
        <p className="setup-result setup-result-ok" ref={resultRef} tabIndex={-1} role="status" data-testid="setup-result">
          <CircleCheck size={16} aria-hidden="true" />{' '}
          <span>{t('setup.result.success', { provider: providerName(t, justConnected) })}</span>
        </p>
      ) : null}
      {stepState === 'checking' ? (
        <p className="muted small setup-testing">
          <LoaderCircle size={14} className="spin" aria-hidden="true" /> {t('setup.verifying')}
        </p>
      ) : null}
      {shown.map((check) => (
        <div key={check.key} className="setup-summary-item">
          <div className="setup-summary-state">
            <SetupStateBadge state={check.state} />
          </div>
          <CheckDetails check={check} container={container} />
        </div>
      ))}
      {canConfigure ? (
        <div className="setup-check-actions">
          <button type="button" className={'button ' + (fixing ? 'primary' : 'secondary')} onClick={() => onChange(first)}>
            {fixing ? <Wrench size={14} aria-hidden="true" /> : null} {fixing ? t('setup.connected.fix') : t('setup.connected.change')}
          </button>
          {canTest ? (
            <button type="button" className="button secondary" onClick={onVerify} disabled={verifying}>
              <RefreshCw size={14} className={verifying ? 'spin' : undefined} aria-hidden="true" /> {t('setup.connected.testAgain')}
            </button>
          ) : null}
          {noEndpoint || (first && isBrokenCheck(first) && first.resourceId) ? (
            <Link
              to={first && first.resourceId ? '/dashboard/models?endpoint=' + encodeURIComponent(first.resourceId) : '/dashboard/models'}
              className="link-button small"
            >
              {t('setup.connected.openModels')}
            </Link>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}

ModelSummary.propTypes = {
  checks: PropTypes.arrayOf(PropTypes.object).isRequired,
  stepState: PropTypes.string.isRequired,
  canConfigure: PropTypes.bool,
  container: PropTypes.string,
  justConnected: PropTypes.string,
  resultRef: PropTypes.object,
  verifying: PropTypes.bool,
  onChange: PropTypes.func.isRequired,
  onVerify: PropTypes.func
};
