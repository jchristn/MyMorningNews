import PropTypes from 'prop-types';
import { CircleAlert, CircleCheck } from 'lucide-react';
import { useTranslation } from 'react-i18next';

// The marker beside a step: its number while to do, a check when done, an alert when it needs attention.
export function StepMarker({ number, state }) {
  if (state === 'done') return <CircleCheck size={22} className="setup-marker done" aria-hidden="true" />;
  if (state === 'attention') return <CircleAlert size={22} className="setup-marker attention" aria-hidden="true" />;
  return (
    <span className="setup-marker todo" aria-hidden="true">
      {number}
    </span>
  );
}

StepMarker.propTypes = { number: PropTypes.number.isRequired, state: PropTypes.string.isRequired };

// One part of the Setup page: a panel with its marker, title, state in words, and content. While setup is unfinished
// the parts are numbered steps; once everything works the page is a status page and the step numbers are left out.
export default function SetupStep({ number, id, title, state, intro = null, numbered = true, children }) {
  const { t } = useTranslation();
  return (
    <section className={'panel setup-step setup-step-' + state} aria-labelledby={id + '-title'} id={id}>
      <header className="setup-step-head">
        <StepMarker number={number} state={state} />
        <div className="setup-step-title">
          {numbered ? <span className="muted small">{t('setup.progress.step', { number })}</span> : null}
          <h2 id={id + '-title'}>{title}</h2>
        </div>
        <span className={'setup-step-state ' + state}>{t('setup.progress.' + state)}</span>
      </header>
      {intro ? <p className="muted setup-step-intro">{intro}</p> : null}
      <div className="setup-step-body">{children}</div>
    </section>
  );
}

SetupStep.propTypes = {
  number: PropTypes.number.isRequired,
  id: PropTypes.string.isRequired,
  title: PropTypes.string.isRequired,
  state: PropTypes.oneOf(['done', 'attention', 'todo']).isRequired,
  intro: PropTypes.string,
  numbered: PropTypes.bool,
  children: PropTypes.node
};
