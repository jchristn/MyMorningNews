import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import StatusBadge from './StatusBadge.jsx';

const TONES = { Ready: 'success', Missing: 'danger', Unverified: 'warning', Failed: 'danger' };

// The check's kind and the resource it is about, for example "Model: Anthropic".
export function checkTitle(check, t) {
  const kind = t('setup.kind.' + check.kind, { defaultValue: check.kind || '' });
  return check.resourceName ? t('setup.check.title', { kind, name: check.resourceName }) : kind;
}

// A setup check's state: Ready, Missing, Unverified, or Failed.
export default function SetupStateBadge({ state }) {
  const { t } = useTranslation();
  return <StatusBadge tone={TONES[state] || 'neutral'}>{t('setup.state.' + state, { defaultValue: state || '' })}</StatusBadge>;
}

SetupStateBadge.propTypes = { state: PropTypes.string };
