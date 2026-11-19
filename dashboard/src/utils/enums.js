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
