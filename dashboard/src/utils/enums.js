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

export const RUN_STATUSES = ['Queued', 'Running', 'Completed', 'PartiallySucceeded', 'Failed', 'Cancelled', 'Skipped'];
export const RUN_STATUS_TONES = { Queued: 'info', Running: 'accent', Completed: 'success', PartiallySucceeded: 'warning', Failed: 'danger', Cancelled: 'neutral', Skipped: 'warning' };
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

// Research results (Phase 6).
export const FINDING_TIERS = ['MustRead', 'WorthALook', 'Fyi', 'BelowThreshold'];
export const FINDING_TIER_TONES = { MustRead: 'accent', WorthALook: 'success', Fyi: 'info', BelowThreshold: 'neutral' };
export const FINDING_CATEGORIES = ['News', 'Release', 'Research', 'Opinion', 'Tutorial', 'Data', 'Other'];
export const EVIDENCE_ROLE_TONES = { Supports: 'success', Contradicts: 'danger', Context: 'neutral' };

// CredentialAuthModeEnum: how an access key authenticates. DirectHeader (the default) and Hybrid accept everything.
export const CREDENTIAL_AUTH_MODES = ['DirectHeader', 'SignedRequest', 'SessionExchange', 'Hybrid'];
export const CREDENTIAL_AUTH_MODE_TONES = { DirectHeader: 'neutral', SignedRequest: 'accent', SessionExchange: 'info', Hybrid: 'neutral' };
// Modes that accept x-access-key and x-secret-key on every request, which a Streamable HTTP MCP client sends.
export const PER_REQUEST_AUTH_MODES = ['DirectHeader', 'Hybrid'];

// FeedbackChannelEnum: where a rating came from.
export const FEEDBACK_CHANNELS = ['Dashboard', 'Email', 'Mobile', 'Api', 'Mcp'];

// Composite relevance components in display order, with the chart color for each.
export const SCORE_COMPONENTS = [
  { key: 'llm', field: 'llmScore', color: 'var(--chart-series-1)' },
  { key: 'feedback', field: 'feedbackScore', color: 'var(--chart-series-2)' },
  { key: 'novelty', field: 'noveltyScore', color: 'var(--chart-series-5)' },
  { key: 'source', field: 'sourceScore', color: 'var(--chart-series-6)' },
  { key: 'recency', field: 'recencyScore', color: 'var(--chart-series-3)' }
];

export const PREDICTION_STATUSES = ['Open', 'ProposedResolved', 'Accurate', 'PartiallyAccurate', 'Inaccurate', 'Unresolvable', 'Withdrawn'];
export const PREDICTION_STATUS_TONES = { Open: 'info', ProposedResolved: 'accent', Accurate: 'success', PartiallyAccurate: 'warning', Inaccurate: 'danger', Unresolvable: 'neutral', Withdrawn: 'neutral' };
// Statuses a person can resolve a prediction to. Open and ProposedResolved are set by the pipeline.
export const PREDICTION_RESOLUTIONS = ['Accurate', 'PartiallyAccurate', 'Inaccurate', 'Unresolvable', 'Withdrawn'];
// The observed outcome a graded status counts as, for Brier scores and calibration. Others are not graded.
export const PREDICTION_OUTCOMES = { Accurate: 1, PartiallyAccurate: 0.5, Inaccurate: 0 };

export const SUGGESTION_STATUSES = ['New', 'Accepted', 'Snoozed', 'Dismissed', 'Done'];
export const SUGGESTION_STATUS_TONES = { New: 'info', Accepted: 'accent', Snoozed: 'warning', Dismissed: 'neutral', Done: 'success' };
export const SUGGESTION_PRIORITIES = ['High', 'Medium', 'Low'];
export const SUGGESTION_PRIORITY_TONES = { High: 'danger', Medium: 'warning', Low: 'neutral' };
export const SUGGESTION_EFFORTS = ['Minutes', 'Hours', 'Days'];

export const STAGE_STATUSES = ['Pending', 'Running', 'Succeeded', 'Failed', 'Skipped'];
export const STAGE_STATUS_TONES = { Pending: 'neutral', Running: 'accent', Succeeded: 'success', Failed: 'danger', Skipped: 'warning' };
export const STAGE_STATUS_COLORS = { Pending: 'var(--color-border-strong)', Running: 'var(--color-accent)', Succeeded: 'var(--color-success)', Failed: 'var(--color-danger)', Skipped: 'var(--color-warning)' };
export const RUN_STAGES = ['AssembleContext', 'PlanQueries', 'Search', 'Filter', 'Fetch', 'Extract', 'Embed', 'Dedupe', 'Score', 'Synthesize', 'ReviewPredictions', 'Predict', 'Suggest', 'ComposeBriefing', 'Notify'];
// Stages a finished run can be re-queued from with its saved findings (ResearchPipeline.FirstResumableStage onward).
export const RESUMABLE_STAGES = RUN_STAGES.slice(RUN_STAGES.indexOf('Synthesize'));
export const FINISHED_RUN_STATUSES = ['Completed', 'PartiallySucceeded', 'Failed', 'Cancelled', 'Skipped'];
// Stage statuses that count as finished for a retry.
export const FINISHED_STAGE_STATUSES = ['Succeeded', 'Failed', 'Skipped'];

// Briefings, editions, and templates (Phase 7).
export const BRIEFING_KINDS = ['Edition', 'Run', 'OneTime'];
export const BRIEFING_KIND_TONES = { Edition: 'accent', Run: 'info', OneTime: 'neutral' };
export const BRIEFING_STATUSES = ['Queued', 'Composing', 'Ready', 'Failed'];
export const BRIEFING_STATUS_TONES = { Queued: 'info', Composing: 'accent', Ready: 'success', Failed: 'danger' };
export const ACTIVE_BRIEFING_STATUSES = ['Queued', 'Composing'];
export const BRIEFING_ITEM_TYPES = ['Finding', 'Prediction', 'Suggestion'];
export const LOOKBACK_MODES = ['SinceLastEdition', 'FixedHours'];
// Bounds the server clamps edition values to.
export const EDITION_BOUNDS = { leadMinutes: { min: 5, max: 240 }, lookbackHours: { min: 1, max: 2160 } };

export const SEARCH_DISPOSITIONS =['Pending', 'Fetched', 'SkippedSeen', 'SkippedBlocked', 'SkippedRobots', 'SkippedLimit', 'SkippedDuplicate', 'FetchFailed'];
export const SEARCH_DISPOSITION_TONES = {
  Pending: 'neutral',
  Fetched: 'success',
  SkippedSeen: 'info',
  SkippedBlocked: 'warning',
  SkippedRobots: 'warning',
  SkippedLimit: 'neutral',
  SkippedDuplicate: 'info',
  FetchFailed: 'danger'
};

// Audio and podcasts (Phase 8).
export const MEDIA_STATUSES = ['Queued', 'Scripting', 'Synthesizing', 'Ready', 'Failed'];
export const MEDIA_STATUS_TONES = { Queued: 'info', Scripting: 'accent', Synthesizing: 'accent', Ready: 'success', Failed: 'danger' };
export const ACTIVE_MEDIA_STATUSES = ['Queued', 'Scripting', 'Synthesizing'];
export const MEDIA_KINDS = ['BriefingAudio', 'PodcastEpisode'];
export const TTS_PROVIDERS = ['OpenAiCompatibleSpeech', 'Disabled'];
// Bounds the server clamps a voice speed to.
export const VOICE_SPEED_BOUNDS = { min: 0.5, max: 2, step: 0.05 };
