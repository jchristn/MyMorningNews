import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { PROVIDER_GROUPS, providerName, providerTagline } from '../../components/modelSignIn/signInCatalog.js';

// Why a provider cannot be chosen: the operator turned agent CLIs off, the CLI is not in the server image, or the
// server's own reason.
function unavailableText(option, t) {
  if (option.turnedOff) return t('setup.model.turnedOff');
  if (option.installed === false) return t('setup.model.notInstalled');
  return option.unavailableReason || t('setup.model.unavailable');
}

// The provider choice as large radio cards in three labelled groups: your AI subscription (agent CLIs), an API key,
// or a model you run. One radio group across the groups, so arrow keys move through every available provider.
export default function ProviderPicker({ options, value, onChange }) {
  const { t } = useTranslation();
  return (
    <div className="provider-groups">
      {PROVIDER_GROUPS.map((group) => {
        const items = options.filter((o) => o.group === group);
        if (items.length === 0) return null;
        return (
          <fieldset key={group} className="provider-group">
            <legend>{t('setup.model.groups.' + group + '.title')}</legend>
            <p className="muted small provider-group-note">{t('setup.model.groups.' + group + '.note')}</p>
            <div className="provider-cards">
              {items.map((option) => {
                const disabled = option.available === false;
                const selected = value === option.provider;
                return (
                  <label
                    key={option.provider}
                    className={'provider-card' + (selected ? ' selected' : '') + (disabled ? ' disabled' : '')}
                    data-testid={'provider-' + option.provider}
                  >
                    <input
                      type="radio"
                      name="setup-provider"
                      value={option.provider}
                      checked={selected}
                      disabled={disabled}
                      onChange={() => onChange(option.provider)}
                    />
                    <span className="provider-card-head">
                      <span className="provider-card-name">{providerName(t, option.provider)}</span>
                      {option.recommended && !disabled ? <span className="badge badge-success">{t('setup.model.recommended')}</span> : null}
                    </span>
                    <span className="provider-card-tagline muted small">{providerTagline(t, option.provider)}</span>
                    {disabled ? <span className="provider-card-unavailable small">{unavailableText(option, t)}</span> : null}
                  </label>
                );
              })}
            </div>
          </fieldset>
        );
      })}
    </div>
  );
}

ProviderPicker.propTypes = {
  options: PropTypes.arrayOf(PropTypes.object).isRequired,
  value: PropTypes.string,
  onChange: PropTypes.func.isRequired
};
