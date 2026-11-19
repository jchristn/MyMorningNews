// Refinement wizard state: a reducer over the server's RefinementStateResponse plus pure step logic.
export const WIZARD_STEPS = ['Describe', 'Dimensions', 'Questions', 'Context', 'Sources', 'Delivery', 'Review'];

// Question rounds the dashboard offers (the plan's MaxRounds default). The server does not cap rounds itself.
export const MAX_ROUNDS = 3;

const dims = (data) => (data && data.dimensions) || [];
const constraints = (data) => (data && data.constraints) || [];
const brief = (data) => Boolean(data && data.topic && data.topic.brief && data.topic.brief.trim());

export function isOutOfScope(data) {
  return Boolean(data && data.session && data.session.outOfScopeReason && dims(data).length === 0);
}

export function isCompleted(data) {
  return Boolean(data && data.session && data.session.status === 'Completed');
}

// Where to land when a session is opened or resumed.
export function initialStep(data) {
  if (!data || !data.session) return 'Describe';
  if (isCompleted(data)) return 'Review';
  const saved = data.session.step;
  if (!saved || saved === 'Describe') return 'Dimensions';
  if ((saved === 'Questions' || saved === 'Context') && dims(data).length === 0) return 'Dimensions';
  if ((saved === 'Sources' || saved === 'Delivery' || saved === 'Review') && !brief(data)) return dims(data).length > 0 ? 'Context' : 'Dimensions';
  return WIZARD_STEPS.includes(saved) ? saved : 'Dimensions';
}

export function stepAvailable(step, data) {
  if (step === 'Describe') return true;
  if (!data || !data.session) return false;
  if (isCompleted(data)) return step === 'Review';
  switch (step) {
    case 'Dimensions':
      return true;
    case 'Questions':
    case 'Context':
      return dims(data).length > 0;
    case 'Sources':
    case 'Delivery':
      return brief(data);
    case 'Review':
      return brief(data) && dims(data).length > 0;
    default:
      return false;
  }
}

export function stepComplete(step, data) {
  if (!data || !data.session) return false;
  switch (step) {
    case 'Describe':
      return true;
    case 'Dimensions':
      return dims(data).length > 0;
    case 'Questions':
      return (data.session.round || 0) > 0;
    case 'Context':
      return brief(data);
    case 'Sources':
      return constraints(data).some((c) => c.kind === 'FeedUrl' || c.kind === 'SeedUrl');
    case 'Review':
      return isCompleted(data);
    default:
      return false;
  }
}

export function nextStep(step) {
  const i = WIZARD_STEPS.indexOf(step);
  return i >= 0 && i < WIZARD_STEPS.length - 1 ? WIZARD_STEPS[i + 1] : step;
}

export function previousStep(step) {
  const i = WIZARD_STEPS.indexOf(step);
  return i > 0 ? WIZARD_STEPS[i - 1] : step;
}

// Questions grouped by round, oldest first, each in display order.
export function questionRounds(questions) {
  const byRound = new Map();
  for (const item of questions || []) {
    const round = item.question.round || 1;
    if (!byRound.has(round)) byRound.set(round, []);
    byRound.get(round).push(item);
  }
  return Array.from(byRound.entries())
    .sort((a, b) => a[0] - b[0])
    .map(([round, items]) => ({ round, items: items.sort((a, b) => (a.question.sortOrder || 0) - (b.question.sortOrder || 0)) }));
}

export function answeredCount(questions) {
  return (questions || []).filter((q) => q.question.answered || q.question.skipped).length;
}

export function canAskMore(data) {
  return Boolean(data && data.session && (data.session.round || 0) < MAX_ROUNDS);
}

// What Next does from a step: run a generation first, or just move. Returns { generate, to }.
export function advancePlan(step, data, sources) {
  switch (step) {
    case 'Dimensions':
      return { generate: data && data.session && (data.session.round || 0) === 0 ? 'questions' : null, to: 'Questions' };
    case 'Questions':
      return { generate: brief(data) ? null : 'context', to: 'Context' };
    case 'Context':
      return { generate: sources === null && !stepComplete('Sources', data) ? 'sources' : null, to: 'Sources' };
    default:
      return { generate: null, to: nextStep(step) };
  }
}

function upsert(list, item, key = (x) => x.id) {
  const found = list.some((x) => key(x) === key(item));
  return found ? list.map((x) => (key(x) === key(item) ? item : x)) : [...list, item];
}

export const initialWizardState = { data: null, step: 'Describe', sources: null, generating: null, failure: null };

export function wizardReducer(state, action) {
  switch (action.type) {
    case 'loaded':
      return { ...state, data: action.data, step: action.step || initialStep(action.data), generating: null };
    case 'generate':
      return { ...state, generating: action.kind, failure: null };
    case 'generated':
      return { ...state, data: action.data, generating: null, failure: null, step: action.step || state.step };
    case 'sources':
      return { ...state, sources: action.checks, generating: null, failure: null, step: 'Sources' };
    case 'failed':
      return { ...state, generating: null, failure: { kind: action.kind, error: action.error } };
    case 'stopped':
      return { ...state, generating: null };
    case 'dismissFailure':
      return { ...state, failure: null };
    case 'goto':
      return { ...state, step: action.step };
    case 'question':
      return {
        ...state,
        data: { ...state.data, questions: state.data.questions.map((q) => (q.question.id === action.item.question.id ? action.item : q)) }
      };
    case 'dimension':
      return { ...state, data: { ...state.data, dimensions: upsert(state.data.dimensions, action.dimension).sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)) } };
    case 'dimensionRemoved':
      return { ...state, data: { ...state.data, dimensions: state.data.dimensions.filter((d) => d.id !== action.id) } };
    case 'constraint':
      return { ...state, data: { ...state.data, constraints: upsert(state.data.constraints, action.constraint) } };
    case 'constraintRemoved':
      return { ...state, data: { ...state.data, constraints: state.data.constraints.filter((c) => c.id !== action.id) } };
    case 'topic':
      return { ...state, data: { ...state.data, topic: action.topic } };
    default:
      return state;
  }
}
