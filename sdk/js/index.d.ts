// Type declarations for the My Morning News JavaScript SDK.
// (c) 2026 Lattice AI LLC. Released under the MIT license.

/** SDK version. */
export declare const SDK_VERSION: string;
/** Default server base URL (`http://127.0.0.1:12100`). */
export declare const DEFAULT_BASE_URL: string;
/** Default per-request timeout in milliseconds (30000). */
export declare const DEFAULT_TIMEOUT_MS: number;
/** Run statuses after which a run no longer changes. */
export declare const TERMINAL_RUN_STATUSES: readonly RunStatus[];
/** Signed request algorithm name (`MMN1-HMAC-SHA256`). */
export declare const SIGNATURE_ALGORITHM: string;

export interface SignatureInput {
    method: string;
    /** Path and query exactly as sent. */
    pathAndQuery: string;
    tenantId: string;
    /** `x-date` value (ISO 8601 UTC). */
    date: string;
    /** `x-nonce` value (8 to 128 characters). */
    nonce: string;
    /** Body exactly as sent; empty when omitted. */
    body?: string | Uint8Array | null;
}

/** The string a signed request signs. */
export declare function stringToSign(input: SignatureInput): string;
/** Lowercase hex HMAC-SHA256 of the string to sign, keyed with the UTF-8 secret. */
export declare function signRequest(input: SignatureInput & { secretKey: string }): string;

// ---------------------------------------------------------------------------------------------------------------------
// Options and shared shapes
// ---------------------------------------------------------------------------------------------------------------------

export interface ClientOptions {
    /** Server base URL. Default `http://127.0.0.1:12100`. */
    baseUrl?: string;
    /** Session token sent as `Authorization: Bearer`. */
    token?: string;
    /** Access key sent as `x-access-key` (needs `secretKey`). */
    accessKey?: string;
    /** Secret key sent as `x-secret-key`. */
    secretKey?: string;
    /** Administration API key sent as `x-api-key`. */
    apiKey?: string;
    /** Tenant sent as `x-tenant-guid` with access keys or the API key, and at login. */
    tenantId?: string;
    /** Sign each request (HMAC-SHA256) instead of sending x-secret-key. Needs accessKey, secretKey, and tenantId. Default false. */
    signedRequests?: boolean;
    /** Per-request timeout. Default 30000. Minimum 1. */
    timeoutMs?: number;
    /** Fetch implementation. Default `globalThis.fetch`. */
    fetch?: typeof fetch;
    /** Extra headers sent on every request. */
    headers?: Record<string, string>;
}

export interface ByteRange {
    /** First byte. */
    start?: number;
    /** Last byte, inclusive. */
    end?: number;
    /** Last n bytes (used when start is not given). */
    suffix?: number;
}

export interface RequestOptions {
    query?: Record<string, unknown>;
    body?: unknown;
    headers?: Record<string, string>;
    /** Send credentials. Default true. */
    auth?: boolean;
    /** Return a DownloadResult instead of parsed JSON. */
    raw?: boolean;
    range?: ByteRange;
    signal?: AbortSignal;
    timeoutMs?: number;
    /** Extra statuses treated as success. */
    okStatuses?: number[];
}

export type SortDirection = 'Ascending' | 'Descending';

export interface ListOptions {
    pageNumber?: number;
    pageSize?: number;
    sortBy?: string;
    sortDirection?: SortDirection;
    search?: string;
    fromUtc?: string | Date;
    toUtc?: string | Date;
    [filter: string]: unknown;
}

export interface EnumerationResult<T> {
    items: T[];
    pageNumber: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    durationMs: number;
}

export interface DownloadResult {
    data: ArrayBuffer;
    contentType: string | null;
    fileName: string | null;
    /** 200, or 206 for a range. */
    status: number;
    contentLength: number | null;
    contentRange: string | null;
    totalBytes: number | null;
    etag: string | null;
}

export interface DownloadOptions {
    range?: ByteRange;
    signal?: AbortSignal;
}

export interface ErrorBody {
    error: string;
    message: string;
    requestId: string | null;
    details: Record<string, string> | null;
}

// ---------------------------------------------------------------------------------------------------------------------
// Errors
// ---------------------------------------------------------------------------------------------------------------------

export interface ErrorInfo {
    status?: number;
    code?: string | null;
    requestId?: string | null;
    details?: Record<string, string> | null;
    body?: unknown;
    method?: string;
    path?: string;
    cause?: unknown;
}

/** Raised for non-2xx responses, timeouts (`code` `Timeout`), aborts (`Aborted`), and network failures (`NetworkError`). */
export declare class MyMorningNewsError extends Error {
    constructor(message: string, info?: ErrorInfo);
    /** HTTP status, or 0 when no response was received. */
    status: number;
    /** Server error code, for example `NotFound` or `ValidationFailed`. */
    code: string | null;
    requestId: string | null;
    details: Record<string, string> | null;
    /** Parsed response body (object or text). */
    body: any;
    method: string | null;
    path: string | null;
}

/** Raised by `login` when the email exists in several tenants (HTTP 409). */
export declare class TenantSelectionRequiredError extends MyMorningNewsError {
    constructor(message: string, info: ErrorInfo, tenants: TenantChoice[]);
    tenants: TenantChoice[];
}

export interface TenantChoice {
    id: string;
    name: string;
}

/** HTTP 402 `CapReached`: a plan limit was reached. */
export declare class CapReachedError extends MyMorningNewsError {
    constructor(message: string, info: ErrorInfo);
    /** Limit key, for example `topics.max`, `runs.daily.max`, `usage.monthlyCostUsd.max`, `audio.enabled`, `endpoints.custom`. */
    key: string | null;
    /** The plan's value for the limit, for example `1` or `false`. */
    limit: string | null;
    /** When the limit resets, or null when it does not reset on a schedule. */
    resetUtc: Date | null;
}

/** Raised by `login` for HTTP 403 `EmailNotVerified`: confirm the email address first. */
export declare class EmailNotVerifiedError extends MyMorningNewsError {
    constructor(message: string, info: ErrorInfo);
}

/** HTTP 409 `SetupRequired`: the workspace is missing something it needs, such as a working model key. */
export declare class SetupRequiredError extends MyMorningNewsError {
    constructor(message: string, info: ErrorInfo);
    /** Setup check key from `details.check`, for example `model:mdl_abc`; matches a `SetupCheck.key`. */
    check: string | null;
}

// ---------------------------------------------------------------------------------------------------------------------
// Models
// ---------------------------------------------------------------------------------------------------------------------

export type TopicStatus = 'Draft' | 'Active' | 'Paused' | 'Archived';
export type RunStatus = 'Queued' | 'Running' | 'Completed' | 'PartiallySucceeded' | 'Failed' | 'Cancelled' | 'Skipped';
export type BriefingStatus = 'Queued' | 'Composing' | 'Ready' | 'Failed';
export type PredictionStatus = 'Open' | 'ProposedResolved' | 'Accurate' | 'PartiallyAccurate' | 'Inaccurate' | 'Unresolvable' | 'Withdrawn';
export type SuggestionStatus = 'New' | 'Accepted' | 'Snoozed' | 'Dismissed' | 'Done';
export type FeedbackTargetType = 'Finding' | 'Prediction' | 'PredictionReview' | 'Suggestion' | 'SourceItem' | 'Domain' | 'Briefing' | 'BriefingSection' | 'LearnedPreference';
export type ConstraintKind = 'IncludeKeyword' | 'ExcludeKeyword' | 'IncludeEntity' | 'ExcludeEntity' | 'PreferredDomain' | 'BlockedDomain' | 'TrackedQuestion' | 'FeedUrl' | 'SeedUrl';
export type DimensionWeight = 'Exclude' | 'Low' | 'Normal' | 'High';
export type IntervalType = 'EveryNMinutes' | 'EveryNHours' | 'EveryNDays' | 'EveryNWeeks' | 'DaysOfWeek' | 'DaysOfMonth' | 'Once' | 'Cron' | 'Manual';
/** Hosted APIs, self-hosted servers, and the agent CLIs in the server image (`ClaudeCode`, `Codex`, `GeminiCli`, `Mux`). */
export type ModelProvider = 'Anthropic' | 'OpenAi' | 'OpenAiCompatible' | 'Ollama' | 'Gemini' | 'XAi' | 'OllamaCloud' | 'ClaudeCode' | 'Codex' | 'GeminiCli' | 'Mux';
/** How a model endpoint signs in: an API key, an access token (`claude setup-token`), a sign-in file, the server's own sign-in, or nothing. */
export type ModelAuthMethod = 'ApiKey' | 'AccessToken' | 'AuthFile' | 'ServerSignIn' | 'None';
/** How a provider is reached, for grouping choices on the Setup page. */
export type SetupProviderGroup = 'Api' | 'AgentCli' | 'SelfHosted';
export type SearchProviderKind = 'Searxng' | 'Tavily' | 'YouCom' | 'Rss' | 'HackerNews' | 'Arxiv' | 'HostedModelWebSearch';
export type PreferenceKind = 'Prefer' | 'Avoid' | 'PreferSource' | 'AvoidSource' | 'StyleNote';
export type AnalyticsReport = 'overview' | 'relevance' | 'accuracy' | 'feedback' | 'reasons' | 'suggestions' | 'quality' | 'email';
export type FeedbackChannel = 'Dashboard' | 'Email' | 'Mobile' | 'Api' | 'Mcp';

/** Fields every stored record carries. */
export interface Record_ {
    id: string;
    tenantId: string;
    active: boolean;
    createdUtc: string;
    lastUpdateUtc: string;
}

export interface UserProfile extends Record_ {
    firstName: string | null;
    lastName: string | null;
    email: string;
    isAdmin: boolean;
    isTenantAdmin: boolean;
    isProtected: boolean;
    timeZone: string | null;
    locale: string | null;
    lastLoginUtc: string | null;
}

export interface TokenResponse {
    token: string;
    expiresUtc: string;
    tenantId: string;
    sessionId: string;
    userId: string | null;
    credentialId: string | null;
    principalType: string;
    user: UserProfile | null;
    refreshToken: string | null;
    refreshExpiresUtc: string | null;
    /** True when this sign-in cancelled a pending account deletion. */
    deletionCancelled?: boolean;
}

export interface TokenDetails {
    valid: boolean;
    sessionId: string | null;
    principalType: string | null;
    scheme: string | null;
    tenantId: string | null;
    tenantName: string | null;
    userId: string | null;
    credentialId: string | null;
    email: string | null;
    displayName: string | null;
    isAdmin: boolean;
    isTenantAdmin: boolean;
    issuedUtc: string | null;
    expiresUtc: string | null;
}

export interface HealthResponse {
    status: 'Healthy' | 'Unhealthy';
    version: string;
    startedUtc: string;
    uptimeSeconds: number;
    components: Record<string, string>;
}

export interface Topic extends Record_ {
    /** Freeze earlier issues as comparison context. Off creates a standalone briefing. */
    historyMode?: 'Off' | 'PreviousIssue' | 'RecentIssues' | 'SinceDate' | 'SpecificIssue';
    historyCount?: number;
    historySinceUtc?: string | null;
    historyBriefingId?: string | null;
    historyTokenBudget?: number;

    userId: string;
    name: string;
    slug: string;
    description: string | null;
    brief: string | null;
    audienceLevel: 'Beginner' | 'Practitioner' | 'Expert';
    horizon: 'Days' | 'Weeks' | 'Months' | 'Years';
    language: string;
    status: TopicStatus;
    isOneTime: boolean;
    noveltyPreference: number;
    emailOnRun: boolean;
    emailWhenEmpty: boolean;
    maxEmailFindings: number;
    displayThreshold: number | null;
    weightLlm: number | null;
    weightFeedback: number | null;
    weightNovelty: number | null;
    weightSource: number | null;
    weightRecency: number | null;
    seedQueries: string[];
    briefingTemplateId: string | null;
    /** Agentic search: the research model searches and reads pages itself. Default false. */
    agenticSearch: boolean;
    /** MCP tool IDs (`mct_...`) allowed in agentic search. Empty allows every enabled tool. */
    mcpToolIds: string[];
    /** Voice profile that reads the topic's run briefings aloud, or null for the workspace default voice. */
    voiceProfileId: string | null;
    /** Automatic audio: every briefing of the topic's research runs is also made into audio. Default false. */
    audioEnabled: boolean;
    lastRunUtc: string | null;
    lastRefinedUtc: string | null;
}

export type McpTransport = 'StreamableHttp' | 'Sse';

export interface McpServer extends Record_ {
    name: string;
    url: string;
    transport: McpTransport;
    vaultCredentialId?: string | null;
    headerName: string;
    headerPrefix: string;
    enabled: boolean;
    toolCount: number;
    lastDiscoveredUtc?: string | null;
    lastError?: string | null;
}

export interface McpServerTool extends Record_ {
    mcpServerId: string;
    name: string;
    description?: string | null;
    /** JSON schema of the arguments, as text. */
    inputSchema?: string | null;
    /** New tools start disabled. */
    enabled: boolean;
}

export interface McpServerDetail {
    server: McpServer;
    tools: McpServerTool[];
}

export interface McpServerUpsertRequest {
    name?: string;
    url?: string;
    transport?: McpTransport;
    /** Empty string removes the credential. */
    vaultCredentialId?: string;
    headerName?: string;
    headerPrefix?: string;
    enabled?: boolean;
}

export interface McpServerTestResult {
    success: boolean;
    toolCount?: number;
    tools?: string[];
    error?: string;
}

export interface RunToolCall extends Record_ {
    runId: string;
    topicId: string;
    tool: string;
    argumentsSummary?: string | null;
    succeeded: boolean;
    errorMessage?: string | null;
    resultCharacters: number;
    durationMs: number;
    sortOrder: number;
}

/** A null or missing limit means unlimited. */
export interface UsageMeter {
    audioGeneratedSecondsToday: number;
    audioReservedSecondsToday: number;
    audioDailySecondsMax: number | null;
    audioResetUtc: string;
    systemSubscriptions: number;
    systemSubscriptionsMax: number | null;
    planKey: string;
    planName: string;
    monthToDateCostUsd: number;
    monthlyCostCapUsd?: number | null;
    costUsedFraction?: number | null;
    resetUtc: string;
    topics: number;
    topicsMax?: number | null;
    runsToday: number;
    runsDailyMax?: number | null;
    scheduleMinIntervalMinutes?: number | null;
    audioMinutes: number;
    audioMinutesMax?: number | null;
    audioEnabled: boolean;
    customEndpoints: boolean;
}

export type SetupCheckKind = 'Model' | 'Search' | 'Embeddings' | 'Email' | 'Speech';
export type SetupCheckState = 'Ready' | 'Missing' | 'Unverified' | 'Failed';

/** One thing the workspace needs before people can use it, and whether it is ready. */
export interface SetupCheck {
    /** Stable key: `model:{endpointId}` (or `model:none`), `search`, `embeddings`, `email`, or `speech`. */
    key: string;
    kind: SetupCheckKind;
    /** Whether the workspace is blocked until this check is ready. */
    required: boolean;
    state: SetupCheckState;
    /** Plain-language detail, such as the provider's error text. */
    message?: string | null;
    /** Model endpoint or search provider ID, when the check is about one. */
    resourceId?: string | null;
    resourceName?: string | null;
    provider?: string | null;
    model?: string | null;
    /** Default voice, for the speech check. Null otherwise. */
    voice?: string | null;
    /** Server address, for Ollama and OpenAI-compatible model checks and the speech check, shown to administrators. Null otherwise. */
    baseUrl?: string | null;
    /** Tasks the endpoint serves, for model checks. */
    tasks: string[];
    /** How the endpoint signs in, for model checks. */
    authMethod?: ModelAuthMethod | null;
    /** Whether the sign-in method needs a stored secret (an API key, a token, or a sign-in file). */
    needsApiKey: boolean;
    hasApiKey: boolean;
    /** Whether the endpoint uses the hosted service's key, which users never enter. */
    usesPlatformKey: boolean;
    verifiedUtc?: string | null;
}

/** Whether the workspace has everything it needs, checked with live calls. */
export interface SetupStatusResponse {
    /** True when every required check is ready. */
    ready: boolean;
    /** Whether the caller can change the configuration (a global or tenant administrator). */
    canConfigure: boolean;
    /** Checks, required ones first. */
    checks: SetupCheck[];
    checkedUtc: string;
    /** Result of the connection attempt, on responses to `setup.connect`. Null otherwise. */
    connection?: SetupConnectionResult | null;
}

/** Outcome of a Setup page connection attempt. */
export interface SetupConnectionResult {
    /** Endpoint that was connected or tried. */
    modelEndpointId?: string | null;
    /** Whether the live test prompt succeeded. */
    succeeded: boolean;
    /** Whether a working model was kept because the new one failed its test. The workspace keeps using it. */
    keptPrevious: boolean;
    /** The test's result in plain language (the provider's error text on failure). */
    message?: string | null;
}

/** One way a provider can sign in, as offered on the Setup page. */
export interface SetupAuthOption {
    method: ModelAuthMethod;
    /** What the user supplies: a key or token, a sign-in file's contents, or nothing. */
    input: 'Text' | 'File' | 'None';
    /** File name the CLI keeps the sign-in in, for File inputs (`auth.json`, `oauth_creds.json`, `endpoints.json`). */
    fileName?: string | null;
    /** Whether this is the suggested method for the provider. */
    recommended: boolean;
}

/** A model provider the Setup page can connect, with the ways it can sign in. */
export interface SetupProviderOption {
    provider: ModelProvider;
    group: SetupProviderGroup;
    /** Whether the provider can be used on this server now (for agent CLIs: allowed and installed). */
    available: boolean;
    /** For agent CLIs: whether the executable is installed in the server. Always true for other providers. */
    installed: boolean;
    /** For agent CLIs: whether the server's operator turned agent CLIs off (`Models.AllowAgentClis`). */
    turnedOff: boolean;
    /** Why the provider is unavailable, when it is. */
    unavailableReason?: string | null;
    /** Whether this is the suggested provider. */
    recommended: boolean;
    /** Model filled in by default; `default` lets an agent CLI choose; null means the user must name one. */
    defaultModel?: string | null;
    /** Whether a base URL is asked for (Ollama and OpenAI-compatible servers). */
    needsBaseUrl: boolean;
    defaultBaseUrl?: string | null;
    /** Sign-in methods, recommended first. */
    methods: SetupAuthOption[];
}

/** Model providers the Setup page can connect on this server. */
export interface SetupOptionsResponse {
    /** Providers, recommended first within each group. */
    providers: SetupProviderOption[];
    /** Name of the server container, for sign-in commands such as `docker exec -it {container} claude`. */
    serverContainer: string;
}

/** Connect the workspace's chat model: choose a provider and how it signs in, then verify it with a live call. */
export interface SetupConnectRequest {
    provider: ModelProvider;
    /** Omitted uses the provider's recommended method. */
    authMethod?: ModelAuthMethod;
    /** The API key, the access token (`claude setup-token`), or the sign-in file's contents. Required unless the method is `ServerSignIn` or `None`, or the endpoint already has one stored for the same method. Never returned. */
    secret?: string;
    /** Omitted uses the provider's default (an agent CLI's own default when it has one). */
    model?: string;
    /** Base URL for Ollama and OpenAI-compatible servers. */
    baseUrl?: string;
    /** Endpoint to change. Omitted reuses the workspace's endpoint for the same provider, or creates one. */
    modelEndpointId?: string;
}

export interface SignupRequest {
    email: string;
    password: string;
    acceptTerms: boolean;
    confirmAge: boolean;
    displayName?: string;
    timeZone?: string;
    locale?: string;
    promoCode?: string;
    turnstileToken?: string;
}

export interface TopicDimension extends Record_ {
    topicId: string;
    name: string;
    description: string | null;
    rationale: string | null;
    weight: DimensionWeight;
    includeTerms: string[];
    excludeTerms: string[];
    isLocked: boolean;
    origin: string;
    sortOrder: number;
}

export interface TopicConstraint extends Record_ {
    topicId: string;
    dimensionId: string | null;
    kind: ConstraintKind;
    value: string;
    note: string | null;
    isLocked: boolean;
    origin: string;
}

export interface TopicRecipient extends Record_ {
    topicId: string;
    email: string;
    userId: string | null;
}

export interface TopicDetail {
    topic: Topic;
    dimensions: TopicDimension[];
    constraints: TopicConstraint[];
    recipients: TopicRecipient[];
}

export interface TopicUpsertRequest {
    /** Freeze earlier issues as comparison context. Off creates a standalone briefing. */
    historyMode?: 'Off' | 'PreviousIssue' | 'RecentIssues' | 'SinceDate' | 'SpecificIssue';
    historyCount?: number;
    historySinceUtc?: string | null;
    historyBriefingId?: string | null;
    historyTokenBudget?: number;

    name?: string;
    description?: string;
    brief?: string;
    audienceLevel?: 'Beginner' | 'Practitioner' | 'Expert';
    horizon?: 'Days' | 'Weeks' | 'Months' | 'Years';
    language?: string;
    status?: TopicStatus;
    noveltyPreference?: number;
    emailOnRun?: boolean;
    emailWhenEmpty?: boolean;
    maxEmailFindings?: number;
    displayThreshold?: number;
    weightLlm?: number;
    weightFeedback?: number;
    weightNovelty?: number;
    weightSource?: number;
    weightRecency?: number;
    agenticSearch?: boolean;
    /** Turn every briefing of the topic's research runs into audio automatically. Uses the plan's audio minutes. */
    audioEnabled?: boolean;
    /** IDs that do not start with `mct_` are dropped. */
    mcpToolIds?: string[];
    seedQueries?: string[];
    /** Voice profile that reads the topic's run briefings aloud. An empty string goes back to the workspace default. */
    voiceProfileId?: string;
    /** A speech server voice from `voices.available()` instead of `voiceProfileId`; the server reuses or creates its voice profile. */
    voice?: string;
}

export interface RefinementSession extends Record_ {
    topicId: string;
    userId: string;
    status: string;
    step: string;
    round: number;
    seedText: string;
    goal: string | null;
    depth: string | null;
    horizonText: string | null;
    known: string | null;
    sufficientContext: boolean;
    outOfScopeReason: string | null;
    modelEndpointId: string | null;
    lastError: string | null;
    completedUtc: string | null;
}

export interface RefinementState {
    session: RefinementSession;
    topic: Topic;
    dimensions: TopicDimension[];
    constraints: TopicConstraint[];
    questions: any[];
    messages: any[];
}

export interface ResearchRequest extends Record_ {
    userId: string;
    topicId: string | null;
    requestText: string;
    title: string | null;
    deliverables: string[];
    length: 'Short' | 'Standard' | 'Long';
    status: string;
    runAtUtc: string | null;
    emailRecipients: string[];
    publishToFeed: boolean;
    outOfScopeReason: string | null;
    estimatedCostUsd: number | null;
    runId: string | null;
    briefingId: string | null;
    convertedTopicId: string | null;
    errorMessage: string | null;
}

export interface ResearchRequestResponse {
    request: ResearchRequest;
    topic: Topic | null;
    dimensions: TopicDimension[];
    questions: any[];
    refinementSessionId: string | null;
}

export interface Schedule extends Record_ {
    ownerType: 'Topic' | 'Edition' | 'Request';
    ownerId: string;
    userId: string;
    name: string | null;
    intervalType: IntervalType;
    intervalValue: number;
    minuteOffset: number;
    daysOfWeekMask: number;
    daysOfMonth: number[];
    timesOfDay: string[];
    cronExpression: string | null;
    timeZone: string;
    startUtc: string | null;
    endUtc: string | null;
    runOnceUtc: string | null;
    enabled: boolean;
    missedRunPolicy: 'Skip' | 'RunOnceOnRecovery';
    nextRunUtc: string | null;
    lastRunUtc: string | null;
}

export interface ScheduleUpsertRequest {
    ownerType?: 'Topic' | 'Edition' | 'Request';
    ownerId?: string;
    name?: string;
    intervalType?: IntervalType;
    intervalValue?: number;
    minuteOffset?: number;
    daysOfWeekMask?: number;
    daysOfMonth?: number[];
    timesOfDay?: string[];
    cronExpression?: string;
    timeZone?: string;
    startUtc?: string;
    endUtc?: string;
    runOnceUtc?: string;
    enabled?: boolean;
    missedRunPolicy?: 'Skip' | 'RunOnceOnRecovery';
    /** Preview only: 1 to 20, default 5. */
    previewCount?: number;
}

export interface SchedulePreview {
    valid: boolean;
    errors: string[];
    nextRunsUtc: string[];
    nextRunsLocal: string[];
}

export interface Run extends Record_ {
    topicId: string;
    userId: string;
    scheduleId: string | null;
    researchRequestId: string | null;
    trigger: string;
    status: RunStatus;
    isDryRun: boolean;
    cancelRequested: boolean;
    queuedUtc: string;
    startedUtc: string | null;
    completedUtc: string | null;
    skippedReason: string | null;
    errorMessage: string | null;
    summary: string | null;
    durationMs: number;
    newFindingCount: number;
    duplicateCount: number;
    sourceCount: number;
    inputTokens: number;
    outputTokens: number;
    /** Input tokens read from the provider's prompt cache. */
    cachedInputTokens: number;
    reasoningTokens: number;
    llmCallCount: number;
    /** Calls with no price (endpoint without prices, or a subscription CLI), left out of the cost. */
    unpricedCallCount: number;
    /** US dollar estimate, null when no call was priced. Updated after every stage. */
    estimatedCostUsd: number | null;
    /** Stage a retried run resumes from, or null for a full run. */
    resumeFromStage: string | null;
}

export type RunEventType = 'run' | 'stage' | 'ping' | 'done' | 'timeout' | 'message';

/** A server-sent event. `data` is parsed JSON (Run for run and done, RunStage for stage), or the raw text. */
export interface RunEvent<T = any> {
    event: RunEventType | string;
    id: string | null;
    data: T;
    raw: string;
}

/** Stages a retry can resume from; any other stage, or none, starts a new run. */
export type ResumableStage = 'Synthesize' | 'ReviewPredictions' | 'Predict' | 'Suggest' | 'ComposeBriefing' | 'Notify';

/** Parse a text/event-stream body (split chunks, CRLF/LF/CR, comments, multi-line data). */
export declare function parseServerSentEvents(chunks: AsyncIterable<Uint8Array | string> | Iterable<Uint8Array | string>): AsyncGenerator<RunEvent, void, unknown>;

export interface RunStage extends Record_ {
    runId: string;
    stage: string;
    status: string;
    startedUtc: string | null;
    completedUtc: string | null;
    durationMs: number;
    itemsIn: number;
    itemsOut: number;
    errorMessage: string | null;
    sortOrder: number;
    llmCallCount: number;
    unpricedCallCount: number;
    inputTokens: number;
    outputTokens: number;
    cachedInputTokens: number;
    reasoningTokens: number;
    /** US dollar estimate, null when no call was priced. */
    estimatedCostUsd: number | null;
}

/** Cost and token totals. Amounts are US dollar estimates from the endpoints' prices, null when no call was priced. */
export interface CostTotals {
    estimatedCostUsd: number | null;
    inputCostUsd: number | null;
    outputCostUsd: number | null;
    llmCallCount: number;
    /** Calls with no price, left out of the amounts. */
    unpricedCallCount: number;
    failedCallCount: number;
    inputTokens: number;
    outputTokens: number;
    cachedInputTokens: number;
    cacheWriteTokens: number;
    reasoningTokens: number;
    modelDurationMs: number;
}

export interface StageCost extends CostTotals {
    runStageId: string;
    stage: string;
    status: string;
    sortOrder: number;
    durationMs: number;
    itemsIn: number;
    itemsOut: number;
    /** Share of the run's cost, 0 to 1, or null when the run has no priced cost. */
    shareOfCost: number | null;
}

export interface ModelCost extends CostTotals {
    modelEndpointId: string | null;
    endpointName: string | null;
    provider: string | null;
    model: string | null;
}

export interface TaskCost extends CostTotals {
    task: string;
}

export interface RunCostReport {
    runId: string;
    topicId: string;
    status: RunStatus;
    currency: 'USD' | string;
    totals: CostTotals;
    costPerNewFindingUsd: number | null;
    /** One row per stage, in run order. */
    stages: StageCost[];
    /** Calls with no stage, or null when every call has one. */
    unattributed: CostTotals | null;
    byModel: ModelCost[];
    byTask: TaskCost[];
}

export interface Finding extends Record_ {
    runId: string;
    topicId: string;
    userId: string;
    dimensionId: string | null;
    sourceItemId: string | null;
    title: string;
    summary: string | null;
    whyItMatters: string | null;
    category: string;
    relevanceScore: number;
    llmScore: number;
    feedbackScore: number;
    noveltyScore: number;
    sourceScore: number;
    recencyScore: number;
    tier: string;
    relevanceRationale: string | null;
    confidence: number;
    isDuplicate: boolean;
    duplicateOfFindingId: string | null;
    isUpdateOfFindingId: string | null;
    rank: number;
    userRating: string | null;
    publishedUtc: string | null;
    isDryRun: boolean;
    pinned: boolean;
    hidden: boolean;
}

export interface Evidence extends Record_ {
    sourceItemId: string;
    runId: string | null;
    quote: string;
    startOffset: number;
    endOffset: number;
    contextText: string | null;
    quoteHash: string;
    verified: boolean;
}

export interface EvidenceView {
    evidence: Evidence;
    sourceUrl: string | null;
    sourceTitle: string | null;
    sourceDomain: string | null;
    sourcePublishedUtc: string | null;
    role: string;
}

export interface BreakdownRow {
    key: string;
    label: string | null;
    count: number;
    positive: number;
    rate: number | null;
    value: number | null;
}

export interface OverviewReport {
    fromUtc: string;
    toUtc: string;
    activeTopics: number;
    runs: number;
    runsCompleted: number;
    runsPartiallySucceeded: number;
    runsFailed: number;
    findingsShown: number;
    findingsRated: number;
    relevancePrecision: number | null;
    briefingsReady: number;
    emailsSent: number;
    emailsFailed: number;
    predictionsOpen: number;
    predictionsResolved: number;
    predictionAccuracy: number | null;
    meanBrier: number | null;
    feedbackTotal: number;
    feedbackWithReason: number;
    suggestionsNew: number;
    suggestionsAccepted: number;
    quoteFailureRate: number | null;
    modelCalls: number;
    modelCostUsd: number;
}

export interface EmailReport {
    total: number;
    sent: number;
    failed: number;
    pending: number;
    skipped: number;
    deliveryRate: number | null;
    meanSecondsToSend: number | null;
    byTemplate: BreakdownRow[];
    byDay: BreakdownRow[];
    byStatus: BreakdownRow[];
    feedbackLinkOpens: number;
    feedbackLinkSubmits: number;
    unsubscribes: number;
}

export interface FindingDetail {
    finding: Finding;
    evidence: any[];
    dimension: TopicDimension | null;
    topicName: string | null;
}

export interface Prediction extends Record_ {
    runId: string;
    topicId: string;
    userId: string;
    dimensionId: string | null;
    statement: string;
    rationale: string | null;
    confidence: number;
    horizonUtc: string;
    status: PredictionStatus;
    resolvedUtc: string | null;
    resolvedBy: string | null;
    resolutionNote: string | null;
    brierScore: number | null;
    isDryRun: boolean;
}

export interface Suggestion extends Record_ {
    runId: string;
    topicId: string;
    userId: string;
    title: string;
    description: string | null;
    rationale: string | null;
    priority: string;
    effort: string;
    status: SuggestionStatus;
    snoozedUntilUtc: string | null;
    statusChangedUtc: string | null;
    findingIds: string[];
    isDryRun: boolean;
}

export interface SourceItem extends Record_ {
    canonicalUrl: string;
    domain: string | null;
    title: string | null;
    publishedUtc: string | null;
    fetchedUtc: string;
    httpStatus: number | null;
    contentType: string | null;
    contentText: string | null;
    contentBytes: number;
    timesSeen: number;
}

export interface Feedback extends Record_ {
    userId: string;
    targetType: FeedbackTargetType;
    targetId: string;
    topicId: string | null;
    relevance: 'Relevant' | 'SomewhatRelevant' | 'NotRelevant' | null;
    accuracy: 'Accurate' | 'PartiallyAccurate' | 'Inaccurate' | 'TooEarly' | null;
    usefulness: 'Useful' | 'NotUseful' | null;
    reason: string | null;
    channel: FeedbackChannel;
    emailMessageId: string | null;
    supersedesFeedbackId: string | null;
    isCurrent: boolean;
}

export interface FeedbackCreateRequest {
    targetType: FeedbackTargetType;
    targetId: string;
    action?: string;
    relevance?: 'Relevant' | 'SomewhatRelevant' | 'NotRelevant';
    accuracy?: 'Accurate' | 'PartiallyAccurate' | 'Inaccurate' | 'TooEarly';
    usefulness?: 'Useful' | 'NotUseful';
    reason?: string;
    channel?: FeedbackChannel;
}

export interface LearnedPreference extends Record_ {
    userId: string;
    topicId: string | null;
    kind: PreferenceKind;
    statement: string;
    status: string;
    origin: string;
    supportCount: number;
    confidence: number;
    lastEvidenceUtc: string | null;
    decidedUtc: string | null;
}

export interface Edition extends Record_ {
    userId: string;
    name: string;
    description: string | null;
    briefingTemplateId: string | null;
    voiceProfileId: string | null;
    audioEnabled: boolean;
    emailEnabled: boolean;
    pushEnabled: boolean;
    enabled: boolean;
    freshResearchBeforeCompose: boolean;
    freshResearchLeadMinutes: number;
    lookbackMode: 'SinceLastEdition' | 'FixedHours';
    lookbackHours: number;
    lastComposedUtc: string | null;
}

export interface EditionResponse {
    edition: Edition;
    topics: any[];
    schedules: Schedule[];
    coveredTopicIds: string[];
}

export interface EditionUpsertRequest {
    name?: string;
    description?: string;
    briefingTemplateId?: string;
    audioEnabled?: boolean;
    emailEnabled?: boolean;
    pushEnabled?: boolean;
    enabled?: boolean;
    freshResearchBeforeCompose?: boolean;
    freshResearchLeadMinutes?: number;
    lookbackMode?: 'SinceLastEdition' | 'FixedHours';
    lookbackHours?: number;
}

export interface Briefing extends Record_ {
    userId: string;
    kind: string;
    editionId: string | null;
    topicId: string | null;
    runId: string | null;
    researchRequestId: string | null;
    scheduleId: string | null;
    periodStartUtc: string | null;
    periodEndUtc: string | null;
    title: string;
    markdown: string | null;
    status: BriefingStatus;
    briefingTemplateVersionId: string | null;
    mediaAssetId: string | null;
    markdownArtifactId: string | null;
    htmlArtifactId: string | null;
    itemCount: number;
    wordCount: number;
    readMinutes: number;
    modelFallbacks: number;
    errorMessage: string | null;
    pinned: boolean;
    isDryRun: boolean;
    composedUtc: string | null;
}

export interface Artifact extends Record_ {
    userId: string;
    kind: string;
    topicId: string | null;
    briefingId: string | null;
    researchRequestId: string | null;
    fileName: string;
    contentType: string;
    bytes: number;
    sha256: string;
    expiresUtc: string | null;
    pinned: boolean;
    deletedUtc: string | null;
}

export interface MediaAsset extends Record_ {
    userId: string;
    briefingId: string;
    editionId: string | null;
    kind: string;
    status: string;
    format: string;
    voiceProfileId: string | null;
    title: string;
    scriptMarkdown: string | null;
    artifactId: string | null;
    chaptersArtifactId: string | null;
    contentType: string | null;
    bytes: number;
    durationSeconds: number;
    errorMessage: string | null;
    completedUtc: string | null;
}

export interface MediaDetail {
    media: MediaAsset;
    chapters: any[];
    audioPath: string | null;
    chaptersPath: string | null;
}

export interface BriefingDetail {
    briefing: Briefing;
    sections: any[];
    items: any[];
    files: Artifact[];
    filesExpired: boolean;
    editionName: string | null;
    topicName: string | null;
    itemTitles: Record<string, string>;
    audio: MediaAsset | null;
}

export interface BriefingTemplate extends Record_ {
    userId: string | null;
    name: string;
    description: string | null;
    defaultKey: string | null;
    isDefault: boolean;
    currentVersion: number;
    customized: boolean;
}

export interface TemplateDetail {
    template: BriefingTemplate;
    content: string;
    problems: any[];
    sections: any[];
    shared: boolean;
    title: string;
    audio: any;
}

export interface TemplateValidation {
    valid: boolean;
    name: string;
    title: string;
    problems: any[];
    sections: any[];
    audio: any;
}

export interface PromptTemplate extends Record_ {
    key: string;
    topicId: string | null;
    name: string;
    description: string | null;
    role: string;
    outputType: string | null;
    currentVersion: number;
    isCustomized: boolean;
}

export interface PromptTemplateResponse {
    template: PromptTemplate;
    content: string;
    variables: string[];
    defaultContent: string;
}

export interface ModelEndpoint extends Record_ {
    name: string;
    provider: ModelProvider;
    endpoint: string | null;
    credentialId: string | null;
    model: string | null;
    embeddingModel: string | null;
    embeddingDimensions: number | null;
    supportsChat: boolean;
    supportsTools: boolean | null;
    supportsEmbeddings: boolean;
    temperature: number | null;
    maxTokens: number;
    reasoningEffort: string | null;
    contextLength: number | null;
    timeoutMs: number;
    inputCostPerMTok: number | null;
    outputCostPerMTok: number | null;
    cachedInputCostPerMTok: number | null;
    cacheWriteCostPerMTok: number | null;
    lastValidatedUtc: string | null;
    lastValidationResult: string | null;
    /** True after a successful validation or model call, false after a failed validation, null when never checked or after its connection settings changed. */
    lastValidationSucceeded: boolean | null;
    /** How the endpoint signs in. Null means the provider's usual method: an API key for hosted APIs, none for Ollama and OpenAI-compatible servers without a key, and the server's own sign-in for agent CLIs. */
    authMethod?: ModelAuthMethod | null;
}

export interface ModelValidation {
    reachable: boolean;
    /** Whether every check that applies to the endpoint passed (chat when it has a chat model, embeddings when it has an embedding model). */
    succeeded: boolean;
    modelFound: boolean;
    supportsTools: boolean | null;
    embeddingDimensions: number | null;
    messages: string[];
    validatedUtc: string;
}

export interface ModelTestResult {
    success: boolean;
    text: string | null;
    model: string | null;
    inputTokens: number;
    outputTokens: number;
    durationMs: number;
    error: string | null;
    supportsTools: boolean | null;
}

export interface ModelAssignment extends Record_ {
    task: string;
    topicId: string | null;
    modelEndpointId: string;
    modelOverride: string | null;
    reasoningEffort: string | null;
}

export interface SearchProvider extends Record_ {
    name: string;
    kind: SearchProviderKind;
    endpoint: string | null;
    credentialId: string | null;
    isDefault: boolean;
    maxResults: number;
    timeoutMs: number;
    lastTestedUtc: string | null;
    lastTestOk: boolean | null;
}

export interface VoiceProfile extends Record_ {
    name: string;
    provider: 'OpenAiCompatibleSpeech' | 'Disabled';
    endpoint: string | null;
    model: string;
    voice: string;
    secondaryVoice: string | null;
    speed: number;
    credentialId: string | null;
    isDefault: boolean;
}

/** A voice someone can choose for a topic: a workspace voice profile, or a voice the speech server offers. */
export interface AvailableVoice {
    /** The speech server's voice ID, such as `af_heart`. */
    voice: string;
    /** A readable label, such as "Heart (American English, female)"; the voice ID when it follows no known naming. */
    label: string;
    /** The workspace voice profile that uses this voice, or null for a speech server voice that has no profile yet. */
    voiceProfileId: string | null;
    /** The voice profile's name, or null. */
    name: string | null;
    /** True for the workspace's default voice profile. */
    isDefault: boolean;
}

/** The voices someone can choose for a topic. */
export interface AvailableVoicesResponse {
    /** Whether speech is turned on for this server. */
    enabled: boolean;
    /** True when the speech server listed its voices; false when the list is only the configured default voices. */
    listed: boolean;
    defaultVoiceProfileId: string | null;
    /** Voice profiles first, then the speech server's other voices. */
    voices: AvailableVoice[];
}

export interface FeedToken extends Record_ {
    userId: string;
    editionId: string | null;
    name: string;
    /** Always `redacted` in responses. */
    tokenHash: string;
    tokenPrefix: string;
    lastUsedUtc: string | null;
    revokedUtc: string | null;
}

/** A public share link for a briefing. It no longer works once `expiresUtc` has passed or `revokedUtc` is set. */
export interface BriefingShare extends Record_ {
    userId: string;
    briefingId: string;
    /** Always `redacted` in responses. */
    tokenHash: string;
    tokenPrefix: string;
    showOwnerName: boolean;
    /** Always set: 1 to 14 days after creation. */
    expiresUtc: string;
    revokedUtc: string | null;
    viewCount: number;
    lastViewedUtc: string | null;
}

export interface BriefingShareCreated {
    share: BriefingShare;
    /** Shown once. */
    secret: string;
    /** The public link: one page to listen to and read the briefing; no sign-in needed. */
    url: string;
    /** Same value as url; kept for older code. */
    readUrl: string;
    /** Same value as url; kept for older code. */
    listenUrl: string;
    audioReady: boolean;
}

export interface FeedTokenCreated {
    token: FeedToken;
    /** Shown once. */
    secret: string;
    feedUrl: string;
}

export interface EmailMessage extends Record_ {
    userId: string | null;
    template: string;
    runId: string | null;
    briefingId: string | null;
    topicId: string | null;
    recipient: string;
    subject: string;
    status: string;
    provider: string;
    providerMessageId: string | null;
    attempts: number;
    nextAttemptUtc: string | null;
    lastError: string | null;
    renderedHtml: string | null;
    renderedText: string | null;
    sentUtc: string | null;
    failedUtc: string | null;
}

export interface EmailPreferences {
    address: string;
    runEmails: boolean;
    briefingEmails: boolean;
    failureEmails: boolean;
    unsubscribedTopicIds: string[];
}

export interface EmailPreferencesRequest {
    runEmails?: boolean;
    briefingEmails?: boolean;
    failureEmails?: boolean;
    resubscribeTopicIds?: string[];
    unsubscribeTopicIds?: string[];
}

export type NotificationKind = 'RunResults' | 'BriefingReady' | 'AudioReady' | 'RunFailed' | 'CapReached' | 'PredictionDue';
export type NotificationChannel = 'Email' | 'Push' | 'InApp';
export type NotificationUnavailableReason = 'NoEmailAddress' | 'EmailNotVerified' | 'EmailNotConfigured' | 'EmailUnsubscribed' | 'PushNotConfigured' | 'NoDevice';

export interface Notification extends Record_ {
    userId: string;
    kind: NotificationKind;
    title: string;
    body?: string | null;
    topicId?: string | null;
    runId?: string | null;
    briefingId?: string | null;
    catalogPublicationId?: string | null;
    mediaAssetId?: string | null;
    predictionId?: string | null;
    readUtc?: string | null;
}

export interface NotificationPreferenceCell {
    kind: NotificationKind;
    channel: NotificationChannel;
    enabled: boolean;
}

export interface NotificationChannelStatus {
    channel: NotificationChannel;
    available: boolean;
    reason?: NotificationUnavailableReason | null;
}

export interface NotificationPreferences {
    preferences: NotificationPreferenceCell[];
    channels: NotificationChannelStatus[];
    address: string;
    unsubscribedTopicIds: string[];
}

export interface NotificationPreferencesRequest {
    preferences?: NotificationPreferenceCell[];
    unsubscribeTopicIds?: string[];
    resubscribeTopicIds?: string[];
}

export interface NotificationCount {
    count: number;
}

export interface NotificationListOptions extends ListOptions {
    unread?: boolean;
    kind?: NotificationKind;
}

export interface VaultCredential extends Record_ {
    name: string;
    kind: 'ApiKey' | 'BearerToken' | 'BasicAuth' | 'CustomHeader' | 'ConnectionString' | 'SignInFile';
    provider: string;
    headerName: string | null;
    username: string | null;
    secretPreview: string;
    description: string | null;
    createdByUserId: string | null;
    lastUsedUtc: string | null;
    lastRevealedUtc: string | null;
    expiresUtc: string | null;
}

export interface VaultCredentialUpsertRequest {
    name?: string;
    kind?: 'ApiKey' | 'BearerToken' | 'BasicAuth' | 'CustomHeader' | 'ConnectionString' | 'SignInFile';
    provider?: string;
    headerName?: string;
    username?: string;
    secret?: string;
    description?: string;
    expiresUtc?: string;
    active?: boolean;
    /** Optional values to clear to null on update: `headerName`, `username`, `description`, `expiresUtc`. An omitted property is left unchanged, so this is the only way to remove one. A name that also has a value in the same request, or any other name, is refused with 400. */
    clearFields?: Array<'headerName' | 'username' | 'description' | 'expiresUtc'>;
}

export interface Device extends Record_ {
    userId: string;
    expoPushToken: string;
    platform: 'ios' | 'android';
    name: string | null;
    locale: string | null;
    lastSeenUtc: string | null;
    lastError: string | null;
}

export interface LlmCall extends Record_ {
    providerConnectionId?: string | null;
    task: string;
    runId: string | null;
    /** Run stage the call was made in. */
    runStageId: string | null;
    refinementSessionId: string | null;
    briefingId: string | null;
    modelEndpointId: string | null;
    provider: string | null;
    model: string | null;
    promptKey: string | null;
    userId: string | null;
    topicId: string | null;
    streamed: boolean;
    attempts: number;
    inputTokens: number;
    cachedInputTokens: number;
    cacheWriteTokens: number;
    outputTokens: number;
    reasoningTokens: number;
    totalTokens: number;
    durationMs: number;
    timeToFirstTokenMs: number | null;
    toolCallCount: number;
    toolNames: string[];
    success: boolean;
    statusCode: number | null;
    finishReason: string | null;
    errorMessage: string | null;
    estimatedCostUsd: number | null;
    isDryRun: boolean;
    promptText: string | null;
    responseText: string | null;
}

// TYPES BEGIN PLATFORM
/** Public catalog projection. Contains no canonical workspace, prompts, credentials, or research trace. */
export interface CatalogEntryResponse {
    /** Publication identifier. */
    id?: string;
    /** Public title. */
    title?: string;
    /** Public description. */
    description?: string | null;
    /** Category. */
    category?: string;
    /** Language. */
    language?: string;
    /** Publication state. */
    status?: string;
    /** Available to free accounts. */
    freeEligible?: boolean;
    /** Caller may subscribe under their effective plan. */
    eligible?: boolean;
    /** Caller has an active subscription. */
    subscribed?: boolean;
    /** Subscription retained but suspended under the current allowance. */
    suspended?: boolean;
    /** Subscriber email preference; null when not subscribed. */
    emailEnabled?: boolean | null;
    /** Subscriber push preference; null when not subscribed. */
    pushEnabled?: boolean | null;
    /** Display order. */
    sortOrder?: number;
    /** First publication time. */
    publishedUtc?: string | null;
}

/** Shared issue content projection. Always authorized through the caller's subscription. */
export interface CatalogIssueResponse {
    /** Canonical issue identifier. */
    id?: string;
    /** Publication identifier. */
    publicationId?: string;
    /** Issue title. */
    title?: string;
    /** Composed content on detail reads. */
    markdown?: string | null;
    /** Issue timestamp. */
    createdUtc?: string;
    /** Authenticated shared audio path when ready. */
    audioPath?: string | null;
    /** Shared episode duration when known. */
    audioSeconds?: number | null;
}

/** System-administrator-only canonical research settings. */
export interface CatalogResearchResponse {
    /** Canonical topic. Never included in member catalog projections. */
    topic?: Topic;
    /** Primary research schedule, or null before configuration. */
    schedule?: Schedule | null;
}

/** A user subscribes to a canonical publication; delivery and interaction state stay private. */
export interface CatalogSubscription {
    /** Identifier (prefix csu_). */
    id?: string;
    /** Tenant identifier. */
    tenantId?: string;
    /** Owning user identifier. */
    userId?: string;
    /** Catalog publication. */
    publicationId?: string;
    /** Deliver canonical issues by email. */
    emailEnabled?: boolean;
    /** Send publication notifications. */
    pushEnabled?: boolean;
    /** Subscription suspended by eligibility or plan changes. */
    suspended?: boolean;
    /** Whether the record is active. */
    active?: boolean;
    /** UTC creation time. */
    createdUtc?: string;
    /** UTC time of the last update. */
    lastUpdateUtc?: string;
}

/** Explicit administrator publication of a canonical system topic. Reads use an allowlisted projection. */
export interface CatalogPublication {
    /** Identifier (prefix cat_). */
    id?: string;
    /** Canonical topic; publishing does not transfer subscriber ownership. */
    topicId?: string;
    /** Protected workspace of the canonical topic. */
    ownerTenantId?: string;
    /** Public catalog title. */
    title?: string;
    /** Public scope description. May be null. */
    description?: string | null;
    /** Catalog category. */
    category?: string;
    /** Output language. */
    language?: string;
    /** Draft, Published, Retired, or Retracted. */
    status?: string;
    /** Whether free catalog members may subscribe. */
    freeEligible?: boolean;
    /** Administrator display ordering. */
    sortOrder?: number;
    /** Publication revision for optimistic concurrency. */
    revision?: number;
    /** First publication timestamp. May be null. */
    publishedUtc?: string | null;
    /** Whether the record is active. */
    active?: boolean;
    /** UTC creation time. */
    createdUtc?: string;
    /** UTC time of the last update. */
    lastUpdateUtc?: string;
}

/** Account-bound purchase offers, captured signup benefit and verified access. */
export interface BillingStatusResponse {
    /** Whether new purchases are enabled. */
    enabled?: boolean;
    /** Account binding UUID for native purchases. */
    purchaseAccountToken?: string;
    /** Whether Apple verification is configured. */
    appleAvailable?: boolean;
    /** Whether Google verification is configured. */
    googleAvailable?: boolean;
    /** Available package offers. */
    offers?: BillingOfferResponse[];
    /** Captured signup offer, if any. Payment verification is still required. */
    signupBenefit?: SignupBenefit | null;
    /** Verified subscriptions owned by this account. */
    subscriptions?: BillingSubscriptionResponse[];
    /** Effective package including administrator overrides. */
    plan?: EffectivePlan;
}

/** Public purchase offer and package allowances. Provider secrets are never included. */
export interface BillingOfferResponse {
    /** Stable server offer key. */
    key?: string;
    /** Base package key. */
    planKey?: string;
    /** Month or Year. */
    interval?: string;
    /** Web price in USD cents. Native clients use localized store prices. */
    usdCents?: number;
    /** Apple product identifier, or null. */
    appleProductId?: string | null;
    /** Google subscription identifier, or null. */
    googleProductId?: string | null;
    /** Google base-plan identifier, or null. */
    googleBasePlanId?: string | null;
    /** Display name. */
    planName?: string;
    /** Custom topic cap, unlimited, or null. */
    customTopics?: string | null;
    /** Separate system subscription cap, unlimited, or null. */
    systemSubscriptions?: string | null;
    /** Generated audio seconds per UTC day, unlimited, or null. */
    dailyAudioSeconds?: string | null;
    /** Whether checkout is currently configured and enabled. */
    webAvailable?: boolean;
}

/** Verified subscription status without payment identifiers or verification credentials. */
export interface BillingSubscriptionResponse {
    /** Internal subscription identifier. */
    id?: string;
    /** Stripe, Apple, or Google. */
    provider?: string;
    /** Verified provider environment. */
    environment?: string;
    /** Base plan identifier. */
    planId?: string;
    /** Active, Grace, Suspended, Canceled, Expired, or Revoked. */
    status?: string;
    /** Verified paid or grace period end. */
    endsUtc?: string | null;
    /** Whether renewal has been canceled. */
    cancelAtPeriodEnd?: boolean;
    /** Last authoritative provider verification. */
    lastVerifiedUtc?: string;
}

/** A server-selected billing redirect. The URL alone never grants paid access. */
export interface BillingRedirectResponse {
    /** Checkout operation identifier, or null for portal. */
    id?: string | null;
    /** HTTPS provider destination. */
    url?: string;
    /** Checkout expiry if applicable. */
    expiresUtc?: string | null;
}

/** Private connection metadata and current funding selection. */
export interface ProviderConnectionsResponse {
    /** Account-owned connections with secrets excluded from serialization. */
    connections?: ProviderConnection[];
    /** Whether an operator-funded local speech engine is offered to BYOK users. */
    localSpeechAvailable?: boolean;
    /** Effective package and funding mode. */
    plan?: EffectivePlan;
}

/** Private per-account model or speech connection. Secrets are encrypted and excluded from all API projections. */
export interface ProviderConnection {
    /** Identifier (prefix pco_). */
    id?: string;
    /** Tenant identifier. */
    tenantId?: string;
    /** Owning user identifier. */
    userId?: string;
    /** Chat, Embedding, or Speech. */
    kind?: string;
    /** Supported API provider or Local speech. */
    provider?: string;
    /** Optional provider URL; local speech uses operator configuration. May be null. */
    endpoint?: string | null;
    /** Provider model identifier. */
    model?: string;
    /** Speech voice identifier. May be null. */
    voice?: string | null;
    /** Masked display suffix. May be null. */
    secretLast4?: string | null;
    /** Successful connection test time, cleared on change. May be null. */
    validatedUtc?: string | null;
    /** Whether the record is active. */
    active?: boolean;
    /** UTC creation time. */
    createdUtc?: string;
    /** UTC time of the last update. */
    lastUpdateUtc?: string;
}

/** Immutable signup offer captured before checkout; never grants paid access before verification. */
export interface SignupBenefit {
    /** Identifier (prefix sbe_). */
    id?: string;
    /** Tenant identifier. */
    tenantId?: string;
    /** Owning user identifier. */
    userId?: string;
    /** Source signup code. */
    promoCodeId?: string;
    /** Custom or standard package selected by the offer. */
    planId?: string;
    /** Percentage discount captured at signup, from zero through 100. */
    discountPercent?: number;
    /** Discount billing months; null means ongoing. May be null. */
    discountMonths?: number | null;
    /** When the offer was applied to a verified subscription. May be null. */
    redeemedUtc?: string | null;
    /** Explicit administrator revocation. May be null. */
    revokedUtc?: string | null;
    /** Whether the record is active. */
    active?: boolean;
    /** UTC creation time. */
    createdUtc?: string;
    /** UTC time of the last update. */
    lastUpdateUtc?: string;
    /** Required base subscription package. */
    billingPlanKey?: string;
    /** Custom tier duration from verified activation; null means subscription lifetime. */
    durationDays?: number | null;
    /** Immutable verified subscription binding; null until payment is verified. */
    redeemedSubscriptionId?: string | null;
    /** Captured native Apple offer identifier. */
    appleOfferId?: string | null;
    /** Captured native Google offer identifier. */
    googleOfferId?: string | null;
}

/** Administrator catalog metadata. A new publication creates an isolated canonical topic. */
export interface CatalogPublicationRequest {
    /** Required title, 1 to 200 characters. */
    title?: string;
    /** Public research scope, up to 4000 characters. */
    description?: string | null;
    /** Category, 1 to 100 characters. */
    category?: string;
    /** Output language, 1 to 16 characters. */
    language?: string;
    /** Allow free users to select this publication. */
    freeEligible?: boolean;
    /** Draft, Published, Retired, or Retracted. */
    status?: string;
    /** Display ordering. */
    sortOrder?: number;
    /** Required current revision when updating; conflicts return 409. */
    revision?: number | null;
}

/** Canonical research configuration. The publication determines the protected owner; caller-supplied owner IDs are ignored. */
export interface CatalogResearchRequest {
    /** Research scope, history and audio controls. Null leaves the topic unchanged. */
    topic?: TopicUpsertRequest | null;
    /** Canonical research schedule. Null leaves scheduling unchanged. */
    schedule?: ScheduleUpsertRequest | null;
}

/** Personal delivery preferences for a shared briefing subscription. */
export interface CatalogSubscriptionRequest {
    /** Email new issues. */
    emailEnabled?: boolean;
    /** Notify mobile devices about new issues. */
    pushEnabled?: boolean;
}

/** Set one user's entitlement override. Global administrator only. Delete the override to restore plan inheritance. */
export interface EntitlementOverrideRequest {
    /** Registered feature key, required. Maximum 128 characters. */
    featureKey?: string | null;
    /** Valid feature value, required. Numeric zero and unlimited have different meanings. */
    value?: string | null;
    /** Required explanation, retained in the audit trail. Maximum 1000 characters. */
    reason?: string | null;
    /** Optional UTC expiry. Null means no expiry. */
    expiresUtc?: string | null;
}

/** Start web checkout using an operator-configured public offer key. */
export interface BillingCheckoutRequest {
    /** Configured offer key; prices, customers, and redirect URLs cannot be supplied by the caller. */
    offerKey?: string;
}

/** Native purchase evidence. Server verification and immutable account binding are always required. */
export interface BillingPurchaseRequest {
    /** Apple or Google. */
    provider?: string;
    /** StoreKit signed transaction, required for Apple. Never logged. */
    signedTransaction?: string | null;
    /** Play purchase token, required for Google. Never logged. */
    purchaseToken?: string | null;
}

/** Save a private model or speech connection. Secrets are write-only; an empty key retains an unchanged provider's key. */
export interface ProviderConnectionRequest {
    /** Chat, Embedding, or Speech. Default Chat. */
    kind?: string;
    /** Anthropic, OpenAi, Gemini, XAi, OpenAiCompatible, or Local speech. */
    provider?: string;
    /** Model identifier, required except for Local speech. */
    model?: string;
    /** Optional API endpoint. Custom URLs must pass server egress restrictions. */
    endpoint?: string | null;
    /** Write-only API key, up to 8192 characters. Null retains a key for the same provider and URL. */
    apiKey?: string | null;
    /** Speech voice identifier. Required for a personal speech API. */
    voice?: string | null;
}

/** Choose Personal for BYOK or Platform to restore the account's assigned package. Existing subscriptions must be managed separately. */
export interface FundingModeRequest {
    /** Personal or Platform. Default Personal. */
    mode?: string;
}

/** Save where the caller stopped listening to a briefing. */
export interface BriefingProgressRequest {
    /** Playback position in seconds. Minimum 0, maximum 86400. */
    positionSeconds?: number;
    /** Episode length in seconds, when the player knows it. */
    durationSeconds?: number | null;
    /** Whether the caller finished listening. */
    completed?: boolean;
}

// TYPES END PLATFORM

export interface EffectivePlan {
    subscriptionRequired: boolean;
    fundingMode: "Platform" | "Personal" | "SelfHosted";
    featureSources: Record<string, string>;
    billingSubscriptionId: string | null;
    billingProvider: string | null;
    plan: { id: string; key: string; name: string; description?: string | null; [k: string]: unknown };
    features: Record<string, string>;
    assignment: any | null;
    endsUtc: string | null;
    upcoming: any[];
    planNames: Record<string, string>;
}

// ---------------------------------------------------------------------------------------------------------------------
// Pagination
// ---------------------------------------------------------------------------------------------------------------------

export interface PaginateControl {
    /** Stop after this many items. */
    maxItems?: number;
}

/** Iterate over every item of a paged list. A list method that returns an array yields it once. */
export declare function paginate<T>(
    listFn: (options: any) => Promise<EnumerationResult<T> | T[]>,
    options?: ListOptions,
    control?: PaginateControl
): AsyncGenerator<T, void, unknown>;

// ---------------------------------------------------------------------------------------------------------------------
// Resource groups
// ---------------------------------------------------------------------------------------------------------------------

export declare class ResourceGroup {
    constructor(client: MyMorningNewsClient, basePath: string);
}

export declare class CrudResource<T = any, TGet = T, TCreate = any, TUpdate = TCreate, TCreated = T, TUpdated = TCreated> extends ResourceGroup {
    list(options?: ListOptions): Promise<EnumerationResult<T>>;
    get(id: string): Promise<TGet>;
    create(body: TCreate): Promise<TCreated>;
    update(id: string, body: TUpdate): Promise<TUpdated>;
    remove(id: string): Promise<null>;
}

export declare class TopicsResource extends CrudResource<Topic, TopicDetail, TopicUpsertRequest, TopicUpsertRequest, Topic, Topic> {
    pause(id: string): Promise<Topic>;
    resume(id: string): Promise<Topic>;
    archive(id: string): Promise<Topic>;
    setStatus(id: string, status: 'Paused' | 'Active' | 'Archived'): Promise<Topic>;
    duplicate(id: string): Promise<TopicDetail>;
    refine(id: string): Promise<RefinementState>;
    run(id: string, options?: { dryRun?: boolean }): Promise<Run>;
    dryRun(id: string): Promise<Run>;
    learn(id: string): Promise<{ proposed: number; preferences: LearnedPreference[] }>;
    rankingRecommendation(id: string): Promise<any>;
    listDimensions(id: string): Promise<TopicDimension[]>;
    addDimension(id: string, body: { name: string; description?: string; rationale?: string; weight?: DimensionWeight; includeTerms?: string[]; excludeTerms?: string[]; isLocked?: boolean; sortOrder?: number }): Promise<TopicDimension>;
    updateDimension(id: string, dimensionId: string, body: Partial<{ name: string; description: string; rationale: string; weight: DimensionWeight; includeTerms: string[]; excludeTerms: string[]; isLocked: boolean; sortOrder: number }>): Promise<TopicDimension>;
    removeDimension(id: string, dimensionId: string): Promise<null>;
    listConstraints(id: string, options?: { kind?: ConstraintKind }): Promise<TopicConstraint[]>;
    addConstraint(id: string, body: { kind: ConstraintKind; value: string; note?: string; dimensionId?: string; isLocked?: boolean }): Promise<TopicConstraint>;
    updateConstraint(id: string, constraintId: string, body: Partial<{ kind: ConstraintKind; value: string; note: string; dimensionId: string; isLocked: boolean }>): Promise<TopicConstraint>;
    removeConstraint(id: string, constraintId: string): Promise<null>;
    listRecipients(id: string): Promise<TopicRecipient[]>;
    addRecipient(id: string, body: { email: string; active?: boolean }): Promise<TopicRecipient>;
    removeRecipient(id: string, recipientId: string): Promise<null>;
    getSearchProviders(id: string): Promise<any[]>;
    setSearchProviders(id: string, providers: { searchProviderId: string; enabled?: boolean; maxResults?: number }[]): Promise<any[]>;
}

export interface RefinementStartRequest {
    seedText: string;
    goal?: string;
    depth?: string;
    horizon?: string;
    known?: string;
    modelEndpointId?: string;
}

export interface QuestionAnswer {
    answerText?: string;
    selectedOptionIds?: string[];
    skipped?: boolean;
}

export declare class RefinementResource extends ResourceGroup {
    start(body: RefinementStartRequest): Promise<RefinementState>;
    create(body: RefinementStartRequest): Promise<RefinementState>;
    list(options?: ListOptions): Promise<EnumerationResult<RefinementSession>>;
    get(id: string): Promise<RefinementState>;
    generateDimensions(id: string, body?: { guidance?: string }): Promise<RefinementState>;
    generateQuestions(id: string, body?: { guidance?: string }): Promise<RefinementState>;
    answer(id: string, questionId: string, body: QuestionAnswer): Promise<RefinementState>;
    generateContext(id: string): Promise<RefinementState>;
    suggestSources(id: string): Promise<any[]>;
    acceptSources(id: string, body: { feedUrls?: string[]; seedUrls?: string[] }): Promise<RefinementState>;
    complete(id: string): Promise<RefinementState>;
    abandon(id: string): Promise<null>;
}

export declare class RequestsResource extends ResourceGroup {
    create(body: { requestText: string; deliverables?: string[]; length?: 'Short' | 'Standard' | 'Long'; runAtUtc?: string; emailRecipients?: string[]; publishToFeed?: boolean; skipQuestions?: boolean; modelEndpointId?: string }): Promise<ResearchRequestResponse>;
    list(options?: ListOptions): Promise<EnumerationResult<ResearchRequest>>;
    get(id: string): Promise<ResearchRequestResponse>;
    answer(id: string, questionId: string, body: QuestionAnswer): Promise<ResearchRequestResponse>;
    replan(id: string): Promise<ResearchRequestResponse>;
    confirm(id: string, options?: { runAtUtc?: string | Date }): Promise<ResearchRequestResponse>;
    cancel(id: string): Promise<ResearchRequestResponse>;
    convert(id: string): Promise<ResearchRequestResponse>;
    remove(id: string): Promise<null>;
}

export declare class SchedulesResource extends CrudResource<Schedule, Schedule, ScheduleUpsertRequest> {
    preview(body: ScheduleUpsertRequest): Promise<SchedulePreview>;
}

export interface RunWaitOptions {
    /** Default 600000. Minimum 1. */
    timeoutMs?: number;
    /** Default 2000. Minimum 100. */
    intervalMs?: number;
    signal?: AbortSignal;
    onPoll?: (run: Run) => void;
}

export declare class RunsResource extends ResourceGroup {
    static isTerminal(status: string): boolean;
    /** sortBy `estimatedCostUsd` (unpriced runs last) or `durationMs` ranks runs by cost or duration. */
    list(options?: RunListOptions): Promise<EnumerationResult<Run>>;
    get(id: string): Promise<Run>;
    start(topicId: string, options?: { dryRun?: boolean }): Promise<Run>;
    /** Totals, stages, models, and tasks. Current while the run is going. 404 for an unknown run. */
    costs(id: string): Promise<RunCostReport>;
    stages(id: string): Promise<RunStage[]>;
    queries(id: string): Promise<any[]>;
    results(id: string, options?: { disposition?: string }): Promise<any[]>;
    /** 202 with the queued run. 409 when the run is not finished, 400 for an unknown stage. Real research and model calls. */
    retry(id: string, options?: { fromStage?: ResumableStage | string }): Promise<Run>;
    /** Server-sent events: the current run and stages first, then changes, pings, and done (or timeout). */
    events(id: string, options?: { signal?: AbortSignal; connectTimeoutMs?: number }): AsyncGenerator<RunEvent, void, unknown>;
    /** Follow until done, reconnecting after timeout events (default 20 reconnects). */
    follow(id: string, onEvent?: (event: RunEvent) => void, options?: { signal?: AbortSignal; maxReconnects?: number }): Promise<Run>;
    /** Paged, oldest first unless sortDirection is given. */
    llmCalls(id: string, options?: ListOptions): Promise<EnumerationResult<LlmCall>>;
    /** Agentic tool calls in call order. 404 for an unknown run. */
    toolCalls(id: string): Promise<RunToolCall[]>;
    emailPreview(id: string): Promise<any>;
    cancel(id: string): Promise<Run>;
    /** Polls `GET /runs/{id}` until a terminal status. Rejects with code `Timeout`. */
    wait(id: string, options?: RunWaitOptions): Promise<Run>;
}

export declare class FindingsResource extends ResourceGroup {
    list(options?: ListOptions): Promise<EnumerationResult<Finding>>;
    get(id: string): Promise<FindingDetail>;
    update(id: string, body: { pinned?: boolean; hidden?: boolean }): Promise<Finding>;
    pin(id: string, pinned?: boolean): Promise<Finding>;
    unpin(id: string): Promise<Finding>;
    hide(id: string, hidden?: boolean): Promise<Finding>;
    unhide(id: string): Promise<Finding>;
    remove(id: string): Promise<null>;
    getEvidence(evidenceId: string): Promise<EvidenceView>;
}

export declare class EvidenceResource extends ResourceGroup {
    get(id: string): Promise<EvidenceView>;
}

export declare class SourcesResource extends ResourceGroup {
    list(options?: ListOptions): Promise<EnumerationResult<SourceItem>>;
    get(id: string): Promise<SourceItem>;
}

export declare class PredictionsResource extends ResourceGroup {
    list(options?: ListOptions): Promise<EnumerationResult<Prediction>>;
    get(id: string): Promise<{ prediction: Prediction; reviews: any[] }>;
    resolve(id: string, body: { status: 'Accurate' | 'PartiallyAccurate' | 'Inaccurate' | 'Unresolvable' | 'Withdrawn'; note?: string }): Promise<Prediction>;
    acceptReview(reviewId: string): Promise<Prediction>;
    rejectReview(reviewId: string): Promise<Prediction>;
}

export declare class SuggestionsResource extends ResourceGroup {
    list(options?: ListOptions): Promise<EnumerationResult<Suggestion>>;
    get(id: string): Promise<Suggestion>;
    setStatus(id: string, status: SuggestionStatus, options?: { snoozedUntilUtc?: string | Date }): Promise<Suggestion>;
}

export declare class FeedbackResource extends ResourceGroup {
    submit(body: FeedbackCreateRequest): Promise<Feedback>;
    create(body: FeedbackCreateRequest): Promise<Feedback>;
    list(options?: ListOptions): Promise<EnumerationResult<Feedback>>;
    update(id: string, body: { reason: string | null }): Promise<Feedback>;
    remove(id: string): Promise<null>;
}

export declare class PreferencesResource extends CrudResource<LearnedPreference, { preference: LearnedPreference; support: Feedback[] }, { statement?: string; kind?: PreferenceKind; topicId?: string }> {
    accept(id: string): Promise<LearnedPreference>;
    reject(id: string): Promise<LearnedPreference>;
    enable(id: string): Promise<LearnedPreference>;
    disable(id: string): Promise<LearnedPreference>;
}

export declare class EditionsResource extends CrudResource<Edition, EditionResponse, EditionUpsertRequest, EditionUpsertRequest, EditionResponse, EditionResponse> {
    setTopics(id: string, topicIds: string[]): Promise<EditionResponse>;
    compose(id: string): Promise<Briefing>;
    preview(id: string, body?: { templateContent?: string }): Promise<Briefing>;
}

export interface BriefingProgress {
    id: string;
    tenantId: string;
    userId: string;
    briefingId: string;
    positionSeconds: number;
    durationSeconds?: number | null;
    completed: boolean;
    createdUtc: string;
    lastUpdateUtc: string;
}

export declare class BriefingsResource extends ResourceGroup {
    list(options?: ListOptions): Promise<EnumerationResult<Briefing>>;
    get(id: string): Promise<BriefingDetail>;
    remove(id: string): Promise<null>;
    pin(id: string, pinned?: boolean): Promise<Briefing>;
    unpin(id: string): Promise<Briefing>;
    getProgress(id: string): Promise<BriefingProgress>;
    saveProgress(id: string, progress: { positionSeconds: number; durationSeconds?: number; completed?: boolean }): Promise<BriefingProgress>;
    regenerate(id: string): Promise<Briefing>;
    generateAudio(id: string, body?: { voiceProfileId?: string }): Promise<MediaAsset>;
    listShares(id: string, options?: ListOptions): Promise<EnumerationResult<BriefingShare>>;
    /** `expiresInDays` is 1 to 14 (default 7); outside the range is a 400. */
    share(id: string, body?: { expiresInDays?: number; showOwnerName?: boolean }): Promise<BriefingShareCreated>;
    revokeShare(id: string, shareId: string, options?: { purge?: boolean }): Promise<null>;
    download(id: string, options?: DownloadOptions & { format?: 'md' | 'markdown' | 'html' }): Promise<DownloadResult>;
    markdown(id: string): Promise<string>;
    html(id: string): Promise<string>;
    downloadAudio(id: string, options?: DownloadOptions): Promise<DownloadResult>;
}

export declare class TemplatesResource extends CrudResource<BriefingTemplate, TemplateDetail, { content: string; name?: string; shared?: boolean }, { content: string; changeNote?: string }, TemplateDetail, TemplateDetail> {
    versions(id: string): Promise<any[]>;
    validate(content: string | { content: string }): Promise<TemplateValidation>;
    rollback(id: string, version: number): Promise<TemplateDetail>;
    reset(id: string): Promise<TemplateDetail>;
    export(id: string): Promise<DownloadResult & { text: string }>;
}

export declare class PromptsResource extends ResourceGroup {
    list(options?: ListOptions): Promise<EnumerationResult<PromptTemplate>>;
    get(id: string): Promise<PromptTemplateResponse>;
    update(id: string, body: { content: string; changeNote?: string }): Promise<PromptTemplateResponse>;
    remove(id: string): Promise<null>;
    versions(id: string): Promise<any[]>;
    rollback(id: string, version: number): Promise<PromptTemplateResponse>;
    reset(id: string): Promise<PromptTemplateResponse>;
    createOverride(body: { key: string; topicId: string; content?: string }): Promise<PromptTemplateResponse>;
    lab(body: { key: string; content?: string; variables?: Record<string, string>; modelEndpointId?: string; model?: string; topicId?: string }): Promise<any>;
}

export declare class MediaResource extends ResourceGroup {
    list(options?: ListOptions): Promise<EnumerationResult<MediaAsset>>;
    get(id: string): Promise<MediaDetail>;
    remove(id: string): Promise<null>;
    download(id: string, options?: DownloadOptions): Promise<DownloadResult>;
}

export declare class ArtifactsResource extends ResourceGroup {
    list(options?: ListOptions): Promise<EnumerationResult<Artifact>>;
    get(id: string): Promise<Artifact>;
    content(id: string, options?: DownloadOptions & { download?: boolean }): Promise<DownloadResult>;
    download(id: string, options?: DownloadOptions): Promise<DownloadResult>;
    downloadUrl(id: string): Promise<{ url: string | null; expiresUtc: string | null; contentPath: string }>;
}

export declare class FeedTokensResource extends ResourceGroup {
    list(): Promise<FeedToken[]>;
    create(body?: { name?: string; editionId?: string }): Promise<FeedTokenCreated>;
    remove(id: string, options?: { purge?: boolean }): Promise<null>;
    revoke(id: string): Promise<null>;
    purge(id: string): Promise<null>;
}

export declare class EmailResource extends ResourceGroup {
    listMessages(options?: ListOptions): Promise<EnumerationResult<EmailMessage>>;
    list(options?: ListOptions): Promise<EnumerationResult<EmailMessage>>;
    getMessage(id: string): Promise<EmailMessage>;
    get(id: string): Promise<EmailMessage>;
    resend(id: string): Promise<EmailMessage>;
    removeMessage(id: string): Promise<null>;
    remove(id: string): Promise<null>;
    test(): Promise<EmailMessage>;
    runPreview(runId: string): Promise<any>;
    getPreferences(): Promise<EmailPreferences>;
    updatePreferences(body: EmailPreferencesRequest): Promise<EmailPreferences>;
}

export interface AnalyticsOptions {
    topicId?: string;
    from?: string | Date;
    to?: string | Date;
    userId?: string;
    all?: boolean;
}

export declare class AnalyticsResource extends ResourceGroup {
    report(report: AnalyticsReport, options?: AnalyticsOptions): Promise<any>;
    overview(options?: AnalyticsOptions): Promise<OverviewReport>;
    email(options?: AnalyticsOptions): Promise<EmailReport>;
    relevance(options?: AnalyticsOptions): Promise<any>;
    accuracy(options?: AnalyticsOptions): Promise<any>;
    feedback(options?: AnalyticsOptions): Promise<any>;
    reasons(options?: AnalyticsOptions): Promise<any>;
    suggestions(options?: AnalyticsOptions): Promise<any>;
    quality(options?: AnalyticsOptions): Promise<any>;
}

export interface UsageOptions {
    from?: string | Date;
    to?: string | Date;
    /** Report grouping (default Model; `me` defaults to Task). On the time series it is optional: when set, the series carries `groups` and each bucket carries `rows`. `Day` is refused for a series and `Tenant` needs `allTenants`. */
    groupBy?: UsageGroupBy;
    userId?: string;
    topicId?: string;
    modelEndpointId?: string;
    task?: string;
    includeDryRun?: boolean;
    /** Most rows in a report (default 50), or most groups in a grouped time series (default 10); the rest of a series is combined under the key `(other)`. */
    top?: number;
    bucketMinutes?: number;
    allTenants?: boolean;
}

export type UsageGroupBy = 'User' | 'Model' | 'Task' | 'Provider' | 'Topic' | 'PromptKey' | 'Day' | 'Tenant';

export interface UsageRow {
    /** Group key. For Model grouping it is the endpoint ID and the model name joined by `|`; `(other)` combines groups beyond `top` in a grouped series. */
    key: string;
    label: string | null;
    /** Set only for Model grouping. Null when the endpoint was deleted. */
    modelEndpointId: string | null;
    /** Set only for Model grouping. */
    model: string | null;
    calls: number;
    failures: number;
    inputTokens: number;
    cachedInputTokens: number;
    cacheWriteTokens: number;
    outputTokens: number;
    reasoningTokens: number;
    totalTokens: number;
    toolCalls: number;
    costUsd: number;
    averageDurationMs: number;
    averageTimeToFirstTokenMs: number | null;
    p95TimeToFirstTokenMs: number | null;
    costShare: number;
}

export interface UsageReport {
    fromUtc: string;
    toUtc: string;
    groupBy: UsageGroupBy;
    totals: UsageRow;
    rows: UsageRow[];
}

export interface UsageBucket {
    bucketStartUtc: string;
    bucketEndUtc: string;
    calls: number;
    failures: number;
    inputTokens: number;
    outputTokens: number;
    cachedInputTokens: number;
    cacheWriteTokens: number;
    reasoningTokens: number;
    toolCalls: number;
    costUsd: number;
    averageTimeToFirstTokenMs: number | null;
    p95TimeToFirstTokenMs: number | null;
    /** One row per group with calls in this bucket, in the order of the series `groups`. Empty when the series is not grouped. */
    rows: UsageRow[];
}

export interface UsageTimeseries {
    fromUtc: string;
    toUtc: string;
    bucketMinutes: number;
    /** Null when the series is not grouped. */
    groupBy: UsageGroupBy | null;
    /** Range totals per group, highest cost first, `(other)` last. Empty when the series is not grouped. */
    groups: UsageRow[];
    buckets: UsageBucket[];
}

export declare class UsageResource extends ResourceGroup {
    me(options?: UsageOptions): Promise<UsageReport>;
    report(options?: UsageOptions): Promise<UsageReport>;
    timeseries(options?: UsageOptions): Promise<UsageTimeseries>;
}

export interface LlmCallListOptions extends ListOptions {
    runId?: string;
    /** Only calls made in this run stage. */
    runStageId?: string;
    refinementSessionId?: string;
    briefingId?: string;
    modelEndpointId?: string;
    model?: string;
    promptKey?: string;
    userId?: string;
    topicId?: string;
    success?: boolean;
    isDryRun?: boolean;
    task?: string;
}

export interface RunListOptions extends ListOptions {
    topicId?: string;
    status?: RunStatus | string;
    trigger?: string;
    userId?: string;
    mine?: boolean;
    sortBy?: 'estimatedCostUsd' | 'durationMs' | 'queuedUtc' | 'completedUtc' | 'createdUtc' | 'lastUpdateUtc' | string;
}

export declare class LlmCallsResource extends ResourceGroup {
    list(options?: LlmCallListOptions): Promise<EnumerationResult<LlmCall>>;
    get(id: string): Promise<LlmCall>;
}

export interface ModelEndpointUpsertRequest {
    name?: string;
    provider?: ModelProvider;
    endpoint?: string;
    credentialId?: string;
    /** Stored in the vault and referenced: the API key, the access token (`claude setup-token`), or the sign-in file contents, depending on `authMethod`. */
    apiKey?: string;
    /** Omitted keeps the current method (or the provider's usual one on create). */
    authMethod?: ModelAuthMethod;
    model?: string;
    embeddingModel?: string;
    supportsChat?: boolean;
    supportsEmbeddings?: boolean;
    temperature?: number;
    maxTokens?: number;
    reasoningEffort?: 'None' | 'Minimal' | 'Low' | 'Medium' | 'High' | 'XHigh' | 'Max';
    contextLength?: number;
    timeoutMs?: number;
    inputCostPerMTok?: number;
    outputCostPerMTok?: number;
    cachedInputCostPerMTok?: number;
    cacheWriteCostPerMTok?: number;
    active?: boolean;
    /** Optional values to set back to null on update. An omitted property is left unchanged, so this is the only way to clear a number or enum. A name that also has a value in the same request, or any other name, is refused with 400. */
    clearFields?: Array<'endpoint' | 'credentialId' | 'authMethod' | 'model' | 'embeddingModel' | 'temperature' | 'reasoningEffort' | 'contextLength' | 'inputCostPerMTok' | 'outputCostPerMTok' | 'cachedInputCostPerMTok' | 'cacheWriteCostPerMTok'>;
}

export declare class ModelEndpointsResource extends CrudResource<ModelEndpoint, ModelEndpoint, ModelEndpointUpsertRequest> {
    validate(id: string): Promise<ModelValidation>;
    models(id: string): Promise<any[]>;
    test(id: string, body?: { prompt?: string; model?: string }): Promise<ModelTestResult>;
}

export declare class ModelAssignmentsResource extends ResourceGroup {
    list(options?: ListOptions): Promise<EnumerationResult<ModelAssignment>>;
    upsert(body: { task: string; modelEndpointId: string; topicId?: string; modelOverride?: string; reasoningEffort?: string }): Promise<ModelAssignment>;
    remove(id: string): Promise<null>;
}

export interface SearchProviderUpsertRequest {
    name?: string;
    kind?: SearchProviderKind;
    endpoint?: string;
    credentialId?: string;
    apiKey?: string;
    isDefault?: boolean;
    maxResults?: number;
    timeoutMs?: number;
    active?: boolean;
}

export declare class SearchProvidersResource extends CrudResource<SearchProvider, SearchProvider, SearchProviderUpsertRequest> {
    test(id: string, body?: { query?: string; feedUrls?: string[]; maxResults?: number }): Promise<any>;
    fetchTest(url: string): Promise<any>;
}

export interface VoiceProfileUpsertRequest {
    name?: string;
    provider?: 'OpenAiCompatibleSpeech' | 'Disabled';
    endpoint?: string;
    model?: string;
    voice?: string;
    secondaryVoice?: string;
    speed?: number;
    credentialId?: string;
    isDefault?: boolean;
}

export declare class VoicesResource extends CrudResource<VoiceProfile, VoiceProfile, VoiceProfileUpsertRequest> {
    test(id: string, body?: { text?: string }): Promise<DownloadResult>;
    /** The workspace's voice profiles, then the speech server's other voices, each with a readable label. */
    available(): Promise<AvailableVoicesResponse>;
    /** A short sample (up to 300 characters). 409 when speech is off, 429 over 20 a minute, 502 `TtsError` when the speech endpoint fails. */
    preview(body: { voice?: string; voiceProfileId?: string; text?: string }): Promise<DownloadResult>;
}

export declare class VaultResource extends CrudResource<VaultCredential, VaultCredential, VaultCredentialUpsertRequest> {
    usage(id: string): Promise<any[]>;
}

export declare class NotificationsResource extends ResourceGroup {
    list(options?: NotificationListOptions): Promise<EnumerationResult<Notification>>;
    unreadCount(): Promise<NotificationCount>;
    markRead(id: string): Promise<Notification>;
    markAllRead(): Promise<NotificationCount>;
    remove(id: string): Promise<null>;
    getPreferences(): Promise<NotificationPreferences>;
    updatePreferences(body: NotificationPreferencesRequest): Promise<NotificationPreferences>;
}

export declare class DevicesResource extends ResourceGroup {
    list(): Promise<Device[]>;
    register(body: { expoPushToken: string; platform: 'ios' | 'android'; name?: string; locale?: string }): Promise<Device>;
    create(body: { expoPushToken: string; platform: 'ios' | 'android'; name?: string; locale?: string }): Promise<Device>;
    remove(id: string): Promise<null>;
    test(id: string): Promise<any>;
}

// BEGIN PLATFORM
export declare class CatalogResource extends ResourceGroup {
    /** Browse published system briefings and account eligibility. */
    list(options?: ListOptions & { category?: string; language?: string }): Promise<EnumerationResult<CatalogEntryResponse>>;
    /** List private subscriptions, including suspended subscriptions. */
    subscriptions(options?: ListOptions): Promise<EnumerationResult<CatalogSubscription>>;
    /** Subscribe or update delivery preferences. Separate system subscription limits apply. */
    subscribe(id: string, body: CatalogSubscriptionRequest): Promise<CatalogSubscription>;
    /** Unsubscribe without deleting canonical content. */
    unsubscribe(id: string): Promise<null>;
    /** List issues readable through this subscription. */
    issues(id: string, options?: ListOptions): Promise<EnumerationResult<CatalogIssueResponse>>;
    /** List shared issues across all current subscriptions. */
    allIssues(options?: ListOptions & { audioOnly?: boolean }): Promise<EnumerationResult<CatalogIssueResponse>>;
    /** Read shared content without exposing canonical workspace internals. */
    issue(id: string, issueId: string): Promise<CatalogIssueResponse>;
    /** Download authenticated shared audio. Playback does not consume generation allowance. */
    audio(id: string, issueId: string, options?: DownloadOptions): Promise<DownloadResult>;
    /** Read private listening progress. */
    progress(id: string, issueId: string): Promise<BriefingProgress>;
    /** Save private listening progress for a shared issue. */
    saveProgress(id: string, issueId: string, body: BriefingProgressRequest): Promise<BriefingProgress>;
}

export declare class BillingResource extends ResourceGroup {
    /** Read offers, native account binding, verified subscriptions and effective access. */
    status(): Promise<BillingStatusResponse>;
    /** Create or resume one account-bound Stripe checkout. A redirect never grants access. */
    checkout(body: BillingCheckoutRequest): Promise<BillingRedirectResponse>;
    /** Open management for the verified Stripe customer. */
    portal(): Promise<BillingRedirectResponse>;
    /** Verify or restore an account-bound Apple or Google purchase. */
    verifyPurchase(body: BillingPurchaseRequest): Promise<EffectivePlan>;
}

export declare class ProvidersResource extends ResourceGroup {
    /** List only this account's masked provider connections. */
    list(): Promise<ProviderConnectionsResponse>;
    /** Connect or rotate a write-only model or speech API key. Clears validation. */
    save(body: ProviderConnectionRequest): Promise<ProviderConnection>;
    /** Run a fixed small probe with this account's credential. Six probes per minute. */
    test(id: string): Promise<Record<string, unknown>>;
    /** Remove a private key. BYOK calls fail closed, without platform fallback. */
    remove(id: string): Promise<Record<string, unknown>>;
    /** Choose Personal or Platform funding. A verified chat connection is required for BYOK. */
    setFunding(body: FundingModeRequest): Promise<EffectivePlan>;
}

export declare class AdminCatalogResource extends ResourceGroup {
    /** System administrator: list every publication lifecycle state. */
    list(options?: ListOptions): Promise<EnumerationResult<CatalogPublication>>;
    /** System administrator: read a canonical publication. */
    get(id: string): Promise<CatalogPublication>;
    /** System administrator: create a canonical system topic and publication. */
    create(body: CatalogPublicationRequest): Promise<CatalogPublication>;
    /** System administrator: update publication with current revision for conflict protection. */
    update(id: string, body: CatalogPublicationRequest): Promise<CatalogPublication>;
    /** System administrator: inspect canonical issue projections. */
    issues(id: string, options?: ListOptions): Promise<EnumerationResult<CatalogIssueResponse>>;
    /** System administrator: read canonical research and schedule configuration. */
    research(id: string): Promise<CatalogResearchResponse>;
    /** System administrator: configure canonical research and schedule. */
    saveResearch(id: string, body: CatalogResearchRequest): Promise<CatalogResearchResponse>;
    /** System administrator: start a canonical research run. */
    run(id: string): Promise<Run>;
}

export declare class AdminEntitlementsResource extends ResourceGroup {
    /** System administrator: inspect effective user allowances and provenance. */
    get(tenantId: string, userId: string): Promise<EffectivePlan>;
    /** System administrator: override one limit with an audit reason and optional expiry. */
    set(tenantId: string, userId: string, body: EntitlementOverrideRequest): Promise<EffectivePlan>;
    /** System administrator: remove an override and restore plan inheritance. */
    clear(tenantId: string, userId: string, featureKey: string): Promise<null>;
}

// END PLATFORM

export declare class AccountResource extends ResourceGroup {
    plan(): Promise<EffectivePlan>;
    redeem(code: string): Promise<EffectivePlan>;
    /** Needs a signed-in user, not an access key. */
    usage(): Promise<UsageMeter>;
    /** ZIP of JSON and Markdown. */
    export(options?: { signal?: AbortSignal }): Promise<DownloadResult>;
    /** 202; signing in again before `purgeAfterUtc` cancels it. 409 for administrators and protected accounts. */
    delete(): Promise<{ purgeAfterUtc: string; message: string }>;
    /** Public. Always 202 with the same body; 404 `SignupClosed` when sign-up is off. */
    signup(body: SignupRequest): Promise<{ message: string; verificationRequired: boolean }>;
    /** Public. 400 when the link is not valid, used, or expired. */
    verifyEmail(token: string): Promise<{ verified: boolean; email: string }>;
    /** Public. Always 202. */
    resendVerification(email: string): Promise<{ message: string }>;
    /** Public. Always 202. */
    forgotPassword(email: string): Promise<{ message: string }>;
    /** Public. Signs out every session. */
    resetPassword(token: string, password: string): Promise<{ reset: boolean; email: string }>;
}

export declare class SetupResource extends ResourceGroup {
    status(): Promise<SetupStatusResponse>;
    /** Administrators only (403 otherwise). Live probes of each chat endpoint, the default search providers, embeddings, and speech; `check` limits the probes to one kind. */
    verify(check?: 'Model' | 'Search' | 'Embeddings' | 'Speech'): Promise<SetupStatusResponse>;
    /** Administrators only (403 otherwise). Stores the key in the vault, attaches it to the endpoint, and checks it live; 404 for an unknown endpoint. */
    setModelKey(modelEndpointId: string, apiKey: string): Promise<SetupStatusResponse>;
    /** Administrators only (403 otherwise). Providers and sign-in methods the server offers, with whether each agent CLI is installed and allowed. */
    options(): Promise<SetupOptionsResponse>;
    /** Administrators only (403 otherwise). Stores the secret for the chosen method, tests it live, and points every chat task at the endpoint; a working model is kept when the new one fails (`connection.keptPrevious`). 400 `ValidationFailed` with `details.field` for a bad request or an unavailable CLI, 404 for an unknown endpoint. */
    connect(request: SetupConnectRequest): Promise<SetupStatusResponse>;
}

export declare class McpServersResource extends CrudResource<McpServer, McpServerDetail, McpServerUpsertRequest> {
    list(options?: ListOptions & { enabled?: boolean }): Promise<EnumerationResult<McpServer>>;
    /** HTTP 200 even when the connection fails (`success: false`). */
    test(id: string): Promise<McpServerTestResult>;
    /** New tools start disabled. 502 `McpServerUnavailable` when unreachable. */
    discover(id: string): Promise<McpServerDetail>;
    setToolEnabled(id: string, toolId: string, enabled: boolean): Promise<McpServerTool>;
}

export declare class SettingsResource extends ResourceGroup {
    get(): Promise<{ settings: any; fields: any[]; pendingRestart: string[] }>;
    update(settings: Record<string, unknown>): Promise<{ success: boolean; errors: Record<string, string>; changed: string[]; pendingRestart: string[] }>;
}

export declare class PublicResource extends ResourceGroup {
    checkPromoCode(code: string): Promise<any>;
    podcastFeed(feedSecret: string): Promise<string>;
    episode(feedSecret: string, file: string, options?: DownloadOptions): Promise<DownloadResult>;
    chapters(feedSecret: string, file: string): Promise<any>;
    asset(name: string): Promise<DownloadResult>;
}

// ---------------------------------------------------------------------------------------------------------------------
// Client
// ---------------------------------------------------------------------------------------------------------------------

export declare class MyMorningNewsClient {
    constructor(options?: ClientOptions);

    baseUrl: string;
    token: string | null;
    accessKey: string | null;
    tenantId: string | null;
    signedRequests: boolean;
    /** Minimum 1. */
    timeoutMs: number;
    readonly isAuthenticated: boolean;

    readonly topics: TopicsResource;
    readonly refinement: RefinementResource;
    readonly requests: RequestsResource;
    readonly schedules: SchedulesResource;
    readonly runs: RunsResource;
    readonly findings: FindingsResource;
    readonly evidence: EvidenceResource;
    readonly sources: SourcesResource;
    readonly predictions: PredictionsResource;
    readonly suggestions: SuggestionsResource;
    readonly feedback: FeedbackResource;
    readonly preferences: PreferencesResource;
    readonly editions: EditionsResource;
    readonly briefings: BriefingsResource;
    readonly templates: TemplatesResource;
    readonly prompts: PromptsResource;
    readonly media: MediaResource;
    readonly artifacts: ArtifactsResource;
    readonly feedTokens: FeedTokensResource;
    readonly email: EmailResource;
    readonly analytics: AnalyticsResource;
    readonly usage: UsageResource;
    readonly llmCalls: LlmCallsResource;
    readonly modelEndpoints: ModelEndpointsResource;
    readonly modelAssignments: ModelAssignmentsResource;
    readonly searchProviders: SearchProvidersResource;
    readonly voices: VoicesResource;
    readonly vault: VaultResource;
    readonly devices: DevicesResource;
    readonly notifications: NotificationsResource;
    readonly catalog: CatalogResource;
    readonly billing: BillingResource;
    readonly providers: ProvidersResource;
    readonly adminCatalog: AdminCatalogResource;
    readonly adminEntitlements: AdminEntitlementsResource;
    readonly account: AccountResource;
    readonly mcpServers: McpServersResource;
    readonly setup: SetupResource;
    readonly settings: SettingsResource;
    readonly publicApi: PublicResource;

    login(email: string, password: string, options?: { tenantId?: string }): Promise<TokenResponse>;
    loginWithAccessKey(accessKey: string, secretKey: string, options?: { tenantId?: string }): Promise<TokenResponse>;
    logout(): Promise<null>;
    refreshToken(): Promise<TokenResponse>;
    validateToken(): Promise<TokenDetails>;
    tokenDetails(): Promise<TokenDetails>;
    health(): Promise<HealthResponse>;
    clientConfig(): Promise<any>;
    serverInfo(): Promise<any>;
    me(): Promise<UserProfile>;
    updateMe(body: { firstName?: string; lastName?: string; timeZone?: string; locale?: string }): Promise<UserProfile>;
    changePassword(currentPassword: string, newPassword: string): Promise<any>;
    paginate<T>(listFn: (options: any) => Promise<EnumerationResult<T> | T[]>, options?: ListOptions, control?: PaginateControl): AsyncGenerator<T, void, unknown>;
    request<T = any>(method: string, path: string, options?: RequestOptions): Promise<T>;
}

export default MyMorningNewsClient;
