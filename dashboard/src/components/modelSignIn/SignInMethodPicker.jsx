import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { methodLabel } from './signInCatalog.js';

// "How should it sign in?": a radio group of the provider's sign-in methods, recommended first. Native radios, so
// arrow keys move the choice and screen readers announce the group.
export default function SignInMethodPicker({ name, provider, methods, value, onChange, legend, disabled = false }) {
  const { t } = useTranslation();
  return (
    <fieldset className="signin-methods" disabled={disabled}>
      <legend className="field-label">{legend || t('setup.model.methodLegend')}</legend>
      <div className="choice-segmented">
        {methods.map((method) => (
          <label key={method} className={'choice-segment' + (value === method ? ' selected' : '')}>
            <input type="radio" name={name} value={method} checked={value === method} onChange={() => onChange(method)} />
            <span>{methodLabel(t, provider, method)}</span>
          </label>
        ))}
      </div>
    </fieldset>
  );
}

SignInMethodPicker.propTypes = {
  name: PropTypes.string.isRequired,
  provider: PropTypes.string.isRequired,
  methods: PropTypes.arrayOf(PropTypes.string).isRequired,
  value: PropTypes.string,
  onChange: PropTypes.func.isRequired,
  legend: PropTypes.string,
  disabled: PropTypes.bool
};
