// Every path the app calls, in one place (plan section 24, plus the mobile contracts A to E in section 35.5 and
// 33.1). A server without a route answers 404, and screens show the "later server update" state. Topics through feedback, learning, and analytics (Phases 4 to 10)
// and plans with promotional codes (section 33.2) are live.
const enc = encodeURIComponent;

export const routes = {
  // Sessions (existing, plus contracts A and B)
  token: '/v1.0/token',
  tokenRefresh: '/v1.0/token/refresh',
  tokenRefreshToken: '/v1.0/token/refresh-token',
  // Public account flows (plan section 33.1): sign-up, email verification, and password reset
  signup: '/v1.0/public/signup',
  verifyEmail: '/v1.0/public/verify-email',
  verifyEmailResend: '/v1.0/public/verify-email/resend',
  passwordForgot: '/v1.0/public/password/forgot',
  passwordReset: '/v1.0/public/password/reset',
  // Server (existing; client config is contract D)
  health: '/v1.0/api/health',
  serverInfo: '/v1.0/api/server-info',
  clientConfig: '/v1.0/api/client-config',
  // Setup status: whether the tenant's model key and search provider are verified (any signed-in user)
  setup: '/v1.0/api/setup',
  // Profile and usage (existing)
  me: '/v1.0/api/me',
  mePassword: '/v1.0/api/me/password',
  usageMe: '/v1.0/api/usage/me',
  // Account deletion and export (contract E, Phase 14)
  accountDelete: '/v1.0/api/account/delete',
  accountExport: '/v1.0/api/account/export',
  accountUsage: '/v1.0/api/account/usage',
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
  // Keep-or-delete reviews for recurring topics that found nothing new for a week
  topicReviews: '/v1.0/api/topic-reviews',
  topicReview: (id) => '/v1.0/api/topic-reviews/' + enc(id),
  topicReviewKeep: (id) => '/v1.0/api/topic-reviews/' + enc(id) + '/keep',
  topicReviewDelete: (id) => '/v1.0/api/topic-reviews/' + enc(id) + '/delete',
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
  runCosts: (id) => '/v1.0/api/runs/' + enc(id) + '/costs',
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
  briefingSupport: (id) => '/v1.0/api/briefings/' + enc(id) + '/support',
  briefingRegenerate: (id) => '/v1.0/api/briefings/' + enc(id) + '/regenerate',
  briefingProgress: (id) => '/v1.0/api/briefings/' + enc(id) + '/progress',
  // Public share links for a briefing (plan section 36)
  briefingShares: (id) => '/v1.0/api/briefings/' + enc(id) + '/shares',
  briefingShare: (id, shareId) => '/v1.0/api/briefings/' + enc(id) + '/shares/' + enc(shareId),
  editions: '/v1.0/api/editions',
  edition: (id) => '/v1.0/api/editions/' + enc(id),
  editionTopics: (id) => '/v1.0/api/editions/' + enc(id) + '/topics',
  editionCompose: (id) => '/v1.0/api/editions/' + enc(id) + '/compose',
  editionPreview: (id) => '/v1.0/api/editions/' + enc(id) + '/preview',
  // Audio and podcast feeds (Phase 8): episodes, their files (bearer auth and HTTP Range), and private feed tokens
  mediaList: '/v1.0/api/media',
  media: (id) => '/v1.0/api/media/' + enc(id),
  briefingAudio: (id) => '/v1.0/api/briefings/' + enc(id) + '/audio',
  artifactContent: (id) => '/v1.0/api/artifacts/' + enc(id) + '/content',
  feedTokens: '/v1.0/api/feed-tokens',
  feedToken: (id) => '/v1.0/api/feed-tokens/' + enc(id),
  // Email preferences (Phase 9)
  emailPreferences: '/v1.0/api/email-preferences',
  // Notifications: the in-app list and the kind by channel preference matrix
  notifications: '/v1.0/api/notifications',
  notificationsUnreadCount: '/v1.0/api/notifications/unread-count',
  notificationsReadAll: '/v1.0/api/notifications/read-all',
  notification: (id) => '/v1.0/api/notifications/' + enc(id),
  notificationRead: (id) => '/v1.0/api/notifications/' + enc(id) + '/read',
  notificationPreferences: '/v1.0/api/notifications/preferences',
  // Feedback, learned preferences, and analytics (Phases 9 and 10)
  feedback: '/v1.0/api/feedback',
  feedbackItem: (id) => '/v1.0/api/feedback/' + enc(id),
  learnedPreferences: '/v1.0/api/learned-preferences',
  learnedPreference: (id) => '/v1.0/api/learned-preferences/' + enc(id),
  learnedPreferenceAction: (id, action) => '/v1.0/api/learned-preferences/' + enc(id) + '/' + action,
  analytics: (name) => '/v1.0/api/analytics/' + enc(name),
  // Plans and promotional codes (plan section 33.2)
  accountPlan: '/v1.0/api/account/plan',
  accountBilling: '/v1.0/api/account/billing',
  accountPurchase: '/v1.0/api/account/billing/purchases',
  accountProviders: '/v1.0/api/account/providers',
  accountProvider: (id) => '/v1.0/api/account/providers/' + encodeURIComponent(id),
  accountProviderTest: (id) => '/v1.0/api/account/providers/' + encodeURIComponent(id) + '/test',
  accountFunding: '/v1.0/api/account/funding',
  promoRedeem: '/v1.0/api/promo-codes/redeem',
};
