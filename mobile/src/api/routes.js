// Every path the app calls, in one place (plan section 24, plus the mobile contracts A to E in section 35.5 and
// 33.1). Routes marked "planned" do not exist on the server yet; calls to them return 404 until their phase lands,
// and screens show the "later server update" state.
const enc = encodeURIComponent;

export const routes = {
  // Sessions (existing, plus contracts A and B)
  token: '/v1.0/token',
  tokenRefresh: '/v1.0/token/refresh',
  tokenRefreshToken: '/v1.0/token/refresh-token',
  // Public account flows (planned, Phase 14)
  signup: '/v1.0/public/signup',
  passwordForgot: '/v1.0/public/password/forgot',
  // Server (existing; client config is contract D)
  health: '/v1.0/api/health',
  serverInfo: '/v1.0/api/server-info',
  clientConfig: '/v1.0/api/client-config',
  // Profile and usage (existing)
  me: '/v1.0/api/me',
  mePassword: '/v1.0/api/me/password',
  usageMe: '/v1.0/api/usage/me',
  // Account deletion and export (contract E, Phase 14)
  accountDelete: '/v1.0/api/account/delete',
  accountExport: '/v1.0/api/account/export',
  // Devices (contract C)
  devices: '/v1.0/api/devices',
  device: (id) => '/v1.0/api/devices/' + enc(id),
  deviceTest: (id) => '/v1.0/api/devices/' + enc(id) + '/test',
  // Topics and refinement (planned, Phase 4)
  topics: '/v1.0/api/topics',
  topic: (id) => '/v1.0/api/topics/' + enc(id),
  topicRun: (id) => '/v1.0/api/topics/' + enc(id) + '/run',
  topicPause: (id) => '/v1.0/api/topics/' + enc(id) + '/pause',
  topicResume: (id) => '/v1.0/api/topics/' + enc(id) + '/resume',
  refinementSessions: '/v1.0/api/refinement-sessions',
  refinementSession: (id) => '/v1.0/api/refinement-sessions/' + enc(id),
  // Findings, predictions, suggestions (planned, Phase 6)
  findings: '/v1.0/api/findings',
  finding: (id) => '/v1.0/api/findings/' + enc(id),
  predictions: '/v1.0/api/predictions',
  prediction: (id) => '/v1.0/api/predictions/' + enc(id),
  predictionResolve: (id) => '/v1.0/api/predictions/' + enc(id) + '/resolve',
  suggestions: '/v1.0/api/suggestions',
  suggestionStatus: (id) => '/v1.0/api/suggestions/' + enc(id) + '/status',
  // Briefings and editions (planned, Phase 7; progress is Phase 16)
  briefings: '/v1.0/api/briefings',
  briefing: (id) => '/v1.0/api/briefings/' + enc(id),
  briefingMarkdown: (id) => '/v1.0/api/briefings/' + enc(id) + '/markdown',
  briefingProgress: (id) => '/v1.0/api/briefings/' + enc(id) + '/progress',
  editions: '/v1.0/api/editions',
  edition: (id) => '/v1.0/api/editions/' + enc(id),
  editionCompose: (id) => '/v1.0/api/editions/' + enc(id) + '/compose',
  // Media (planned, Phase 8)
  media: (id) => '/v1.0/api/media/' + enc(id),
  // Feedback and learning (planned, Phase 10)
  feedback: '/v1.0/api/feedback',
  feedbackItem: (id) => '/v1.0/api/feedback/' + enc(id),
  learnedPreferences: '/v1.0/api/learned-preferences',
  analytics: (name) => '/v1.0/api/analytics/' + enc(name),
};
