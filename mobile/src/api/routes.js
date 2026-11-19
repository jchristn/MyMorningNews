// Every path the app calls, in one place (plan section 24, plus the mobile contracts A to E in section 35.5 and
// 33.1). Routes marked "planned" do not exist on the server yet; calls to them return 404 until their phase lands,
// and screens show the "later server update" state. Topics through editions (Phases 4 to 7) are live.
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
  // Topics, dimensions, constraints, and the refinement wizard (Phase 4)
  topics: '/v1.0/api/topics',
  topic: (id) => '/v1.0/api/topics/' + enc(id),
  topicRun: (id) => '/v1.0/api/topics/' + enc(id) + '/run',
  topicPause: (id) => '/v1.0/api/topics/' + enc(id) + '/pause',
  topicResume: (id) => '/v1.0/api/topics/' + enc(id) + '/resume',
  topicRefine: (id) => '/v1.0/api/topics/' + enc(id) + '/refine',
  topicDimensions: (id) => '/v1.0/api/topics/' + enc(id) + '/dimensions',
  topicDimension: (id, dimensionId) => '/v1.0/api/topics/' + enc(id) + '/dimensions/' + enc(dimensionId),
  topicConstraints: (id) => '/v1.0/api/topics/' + enc(id) + '/constraints',
  topicConstraint: (id, constraintId) => '/v1.0/api/topics/' + enc(id) + '/constraints/' + enc(constraintId),
  refinementSessions: '/v1.0/api/refinement-sessions',
  refinementSession: (id) => '/v1.0/api/refinement-sessions/' + enc(id),
  refinementStep: (id, step) => '/v1.0/api/refinement-sessions/' + enc(id) + '/' + step,
  refinementQuestion: (id, questionId) => '/v1.0/api/refinement-sessions/' + enc(id) + '/questions/' + enc(questionId),
  // One-time requests (plan section 13.1, Phase 4)
  requests: '/v1.0/api/requests',
  request: (id) => '/v1.0/api/requests/' + enc(id),
  requestAction: (id, action) => '/v1.0/api/requests/' + enc(id) + '/' + action,
  requestQuestion: (id, questionId) => '/v1.0/api/requests/' + enc(id) + '/questions/' + enc(questionId),
  // Schedules and runs (Phase 5), with the run drill-down (Phase 6)
  schedules: '/v1.0/api/schedules',
  schedulePreview: '/v1.0/api/schedules/preview',
  schedule: (id) => '/v1.0/api/schedules/' + enc(id),
  runs: '/v1.0/api/runs',
  run: (id) => '/v1.0/api/runs/' + enc(id),
  runCancel: (id) => '/v1.0/api/runs/' + enc(id) + '/cancel',
  runStages: (id) => '/v1.0/api/runs/' + enc(id) + '/stages',
  runQueries: (id) => '/v1.0/api/runs/' + enc(id) + '/queries',
  runResults: (id) => '/v1.0/api/runs/' + enc(id) + '/results',
  // Findings, evidence sources, predictions, suggestions (Phase 6)
  findings: '/v1.0/api/findings',
  finding: (id) => '/v1.0/api/findings/' + enc(id),
  sources: '/v1.0/api/sources',
  source: (id) => '/v1.0/api/sources/' + enc(id),
  predictions: '/v1.0/api/predictions',
  prediction: (id) => '/v1.0/api/predictions/' + enc(id),
  predictionResolve: (id) => '/v1.0/api/predictions/' + enc(id) + '/resolve',
  predictionReviewAccept: (id) => '/v1.0/api/prediction-reviews/' + enc(id) + '/accept',
  predictionReviewReject: (id) => '/v1.0/api/prediction-reviews/' + enc(id) + '/reject',
  suggestions: '/v1.0/api/suggestions',
  suggestionStatus: (id) => '/v1.0/api/suggestions/' + enc(id) + '/status',
  // Briefings and editions (Phase 7; listening progress is Phase 16)
  briefings: '/v1.0/api/briefings',
  briefing: (id) => '/v1.0/api/briefings/' + enc(id),
  briefingDownload: (id) => '/v1.0/api/briefings/' + enc(id) + '/download',
  briefingPin: (id) => '/v1.0/api/briefings/' + enc(id) + '/pin',
  briefingRegenerate: (id) => '/v1.0/api/briefings/' + enc(id) + '/regenerate',
  briefingProgress: (id) => '/v1.0/api/briefings/' + enc(id) + '/progress',
  editions: '/v1.0/api/editions',
  edition: (id) => '/v1.0/api/editions/' + enc(id),
  editionTopics: (id) => '/v1.0/api/editions/' + enc(id) + '/topics',
  editionCompose: (id) => '/v1.0/api/editions/' + enc(id) + '/compose',
  editionPreview: (id) => '/v1.0/api/editions/' + enc(id) + '/preview',
  // Media (planned, Phase 8)
  media: (id) => '/v1.0/api/media/' + enc(id),
  // Feedback and learning (planned, Phase 10)
  feedback: '/v1.0/api/feedback',
  feedbackItem: (id) => '/v1.0/api/feedback/' + enc(id),
  learnedPreferences: '/v1.0/api/learned-preferences',
  analytics: (name) => '/v1.0/api/analytics/' + enc(name),
};
