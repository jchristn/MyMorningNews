import PropTypes from 'prop-types';
import { ExternalLink, Info, ShieldAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import CommandChip from './CommandChip.jsx';
import { signInNotes, signInSteps } from './signInCatalog.js';

function hostOf(url) {
  try {
    const parsed = new URL(url);
    return parsed.host + (parsed.pathname && parsed.pathname !== '/' ? parsed.pathname : '');
  } catch {
    return url;
  }
}

// A link that opens in a new tab and says so to screen readers.
export function NewTabLink({ href, children }) {
  const { t } = useTranslation();
  return (
    <a href={href} target="_blank" rel="noopener noreferrer" className="external-link">
      {children}
      <ExternalLink size={12} aria-hidden="true" />
      <span className="sr-only">{t('setup.newTab')}</span>
    </a>
  );
}

NewTabLink.propTypes = { href: PropTypes.string.isRequired, children: PropTypes.node };

// Numbered instructions for one provider and sign-in method, with commands to copy, the file to choose, and where to
// create a key, followed by notes (what it costs, what a sign-in file can do, how to install the CLI).
export default function SignInSteps({ provider, method, container, headingId }) {
  const { t } = useTranslation();
  const steps = signInSteps(provider, method, container);
  const notes = signInNotes(provider, method);
  if (steps.length === 0) return null;
  return (
    <div className="signin-steps">
      <ol aria-labelledby={headingId}>
        {steps.map((step) => (
          <li key={step.key}>
            <span>{t(step.key, step.values)}</span>
            {step.commands ? (
              <span className="signin-step-extra">
                {step.commands.map((command) => (
                  <CommandChip key={command} command={command} />
                ))}
              </span>
            ) : null}
            {step.paths ? (
              <span className="signin-step-extra signin-paths">
                <CommandChip command={step.paths.unix} />
                <span className="muted small">{t('setup.steps.windows')}</span>
                <CommandChip command={step.paths.windows} />
              </span>
            ) : null}
            {step.link ? (
              <span className="signin-step-extra">
                <NewTabLink href={step.link}>{hostOf(step.link)}</NewTabLink>
              </span>
            ) : null}
          </li>
        ))}
      </ol>
      {notes.info ? (
        <p className="signin-note muted small">
          <Info size={14} aria-hidden="true" /> <span>{t(notes.info)}</span>
        </p>
      ) : null}
      {notes.warning ? (
        <p className="notice notice-warning signin-note small">
          <ShieldAlert size={14} aria-hidden="true" /> <span>{t(notes.warning)}</span>
        </p>
      ) : null}
      {notes.tip ? <p className="muted small">{t(notes.tip)}</p> : null}
      {notes.install ? (
        <p className="signin-install muted small">
          <span>{t('setup.steps.install')}</span> <CommandChip command={notes.install} />
        </p>
      ) : null}
    </div>
  );
}

SignInSteps.propTypes = {
  provider: PropTypes.string,
  method: PropTypes.string,
  container: PropTypes.string,
  headingId: PropTypes.string
};
