// Stable server enum codes. Display labels come from the i18n catalog under enums.<name>.<code>.
export const MODEL_PROVIDERS = ['Anthropic', 'OpenAi', 'OpenAiCompatible', 'Ollama', 'Gemini', 'XAi', 'OllamaCloud'];

export const MODEL_TASKS = ['Refinement', 'QueryPlanning', 'Extraction', 'Scoring', 'Synthesis', 'Prediction', 'Suggestion', 'Composition', 'PodcastScript', 'FeedbackAnalysis', 'Embedding'];

// Roles whose results are parsed as structured output; a model without tool support falls back to JSON prompting.
export const STRUCTURED_TASKS = ['Refinement', 'QueryPlanning', 'Extraction', 'Scoring', 'Synthesis', 'Prediction', 'Suggestion', 'FeedbackAnalysis'];

export const REASONING_EFFORTS = ['None', 'Minimal', 'Low', 'Medium', 'High', 'XHigh', 'Max'];

export const VAULT_KINDS = ['ApiKey', 'BearerToken', 'BasicAuth', 'CustomHeader', 'ConnectionString'];

export const VAULT_PROVIDERS = ['Anthropic', 'OpenAi', 'Gemini', 'XAi', 'OllamaCloud', 'OpenAiCompatible', 'Tavily', 'YouCom', 'Mailgun', 'ElevenLabs', 'Mcp', 'Custom'];

export const USAGE_GROUPS = ['User', 'Model', 'Task', 'Provider', 'Topic', 'PromptKey', 'Day'];

// Providers that need (or commonly use) an explicit base URL, with an example for the placeholder.
export const PROVIDER_URL_EXAMPLES = {
  Anthropic: 'https://api.anthropic.com',
  OpenAi: 'https://api.openai.com/v1',
  OpenAiCompatible: 'http://host.docker.internal:8000/v1',
  Ollama: 'http://host.docker.internal:11434',
  Gemini: 'https://generativelanguage.googleapis.com',
  XAi: 'https://api.x.ai/v1',
  OllamaCloud: 'https://ollama.com'
};

// Providers that take an API key. Local Ollama does not.
export const PROVIDERS_WITH_KEYS = ['Anthropic', 'OpenAi', 'OpenAiCompatible', 'Gemini', 'XAi', 'OllamaCloud'];

// The vault provider a model provider's key is filed under.
export const VAULT_PROVIDER_FOR = {
  Anthropic: 'Anthropic',
  OpenAi: 'OpenAi',
  OpenAiCompatible: 'OpenAiCompatible',
  Gemini: 'Gemini',
  XAi: 'XAi',
  OllamaCloud: 'OllamaCloud',
  Ollama: 'Custom'
};

// Topics, refinement, requests, schedules, and runs (Phases 4 and 5).
export const TOPIC_STATUSES = ['Draft', 'Active', 'Paused', 'Archived'];
export const TOPIC_STATUS_TONES = { Draft: 'info', Active: 'success', Paused: 'warning', Archived: 'neutral' };

export const RUN_STATUSES = ['Queued', 'Running', 'Completed', 'Failed', 'Cancelled', 'Skipped'];
export const RUN_STATUS_TONES = { Queued: 'info', Running: 'accent', Completed: 'success', Failed: 'danger', Cancelled: 'neutral', Skipped: 'warning' };
export const ACTIVE_RUN_STATUSES = ['Queued', 'Running'];
export const RUN_TRIGGERS = ['Schedule', 'Manual', 'Request', 'Edition', 'Preview'];

export const REQUEST_STATUSES = ['Clarifying', 'Scheduled', 'Running', 'Completed', 'Failed', 'Cancelled', 'Declined'];
export const REQUEST_STATUS_TONES = { Clarifying: 'info', Scheduled: 'accent', Running: 'accent', Completed: 'success', Failed: 'danger', Cancelled: 'neutral', Declined: 'warning' };
export const DELIVERABLES = ['Report', 'Podcast', 'Email'];
export const DELIVERABLE_LENGTHS = ['Short', 'Standard', 'Long'];

export const DIMENSION_WEIGHTS = ['High', 'Normal', 'Low', 'Exclude'];
export const DIMENSION_WEIGHT_TONES = { High: 'accent', Normal: 'info', Low: 'neutral', Exclude: 'danger' };
export const CONSTRAINT_KINDS = ['IncludeKeyword', 'ExcludeKeyword', 'IncludeEntity', 'ExcludeEntity', 'PreferredDomain', 'BlockedDomain', 'TrackedQuestion', 'FeedUrl', 'SeedUrl'];
export const SOURCE_CONSTRAINT_KINDS = ['FeedUrl', 'SeedUrl'];
export const CONTEXT_CONSTRAINT_KINDS = CONSTRAINT_KINDS.filter((k) => !SOURCE_CONSTRAINT_KINDS.includes(k));
export const QUESTION_KINDS = ['SingleChoice', 'MultiChoice', 'Scale', 'YesNo', 'FreeText'];
export const AUDIENCE_LEVELS = ['Beginner', 'Practitioner', 'Expert'];
export const HORIZONS = ['Days', 'Weeks', 'Months', 'Years'];
export const REFINEMENT_GOALS = ['work', 'investing', 'curiosity', 'hiring', 'building'];

export const INTERVAL_TYPES = ['DaysOfWeek', 'EveryNDays', 'EveryNWeeks', 'DaysOfMonth', 'EveryNHours', 'EveryNMinutes', 'Once', 'Cron', 'Manual'];
export const MISSED_RUN_POLICIES = ['Skip', 'RunOnceOnRecovery'];
