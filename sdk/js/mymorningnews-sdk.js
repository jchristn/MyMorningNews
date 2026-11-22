/**
 * My Morning News JavaScript SDK.
 *
 * A single ES module for Node 20 and later that wraps the My Morning News REST API. It uses the global fetch only and
 * has no runtime dependencies. Every method returns the parsed JSON body (camelCase, enums as PascalCase strings,
 * dates as ISO 8601 UTC strings), or a download result for binary routes.
 *
 * (c) 2026 Lattice AI LLC. Released under the MIT license.
 *
 * @module mymorningnews-sdk
 */

import { createHash, createHmac, randomBytes } from 'node:crypto';

/** SDK version. */
export const SDK_VERSION = '0.1.0-alpha';

/** Default server base URL. */
export const DEFAULT_BASE_URL = 'http://127.0.0.1:12100';

/** Default per-request timeout in milliseconds. */
export const DEFAULT_TIMEOUT_MS = 30000;

/** Run statuses after which a run no longer changes. */
export const TERMINAL_RUN_STATUSES = Object.freeze(['Completed', 'PartiallySucceeded', 'Failed', 'Cancelled', 'Skipped']);

/** Signed request algorithm name (the first line of the string to sign). */
export const SIGNATURE_ALGORITHM = 'MMN1-HMAC-SHA256';

// ---------------------------------------------------------------------------------------------------------------------
// Type definitions for the main models. Property lists mirror the REST DTOs in src/MyMorningNews.Core.
// ---------------------------------------------------------------------------------------------------------------------

/**
 * Client constructor options.
 * @typedef {object} ClientOptions
 * @property {string} [baseUrl] Server base URL. Default `http://127.0.0.1:12100`.
 * @property {string} [token] Session token sent as `Authorization: Bearer`.
 * @property {string} [accessKey] Access key sent as `x-access-key` on every request (needs `secretKey`).
 * @property {string} [secretKey] Secret key sent as `x-secret-key`.
 * @property {string} [apiKey] Administration API key sent as `x-api-key`.
 * @property {string} [tenantId] Tenant sent as `x-tenant-guid` with access keys or the API key, and at login.
 * @property {boolean} [signedRequests] Sign each request with the access key and secret (HMAC-SHA256) instead of
 *   sending `x-secret-key`. Needs `accessKey`, `secretKey`, and `tenantId`. Default false.
 * @property {number} [timeoutMs] Per-request timeout. Default 30000. Minimum 1.
 * @property {typeof fetch} [fetch] Fetch implementation. Default `globalThis.fetch`.
 * @property {Record<string, string>} [headers] Extra headers sent on every request.
 */

/**
 * Per-request options accepted by the low-level `request` method.
 * @typedef {object} RequestOptions
 * @property {Record<string, any>} [query] Query values; null and undefined are left out, dates become ISO strings.
 * @property {any} [body] JSON body.
 * @property {Record<string, string>} [headers] Extra headers for this request.
 * @property {boolean} [auth] Send credentials. Default true.
 * @property {boolean} [raw] Return a {@link DownloadResult} instead of parsed JSON.
 * @property {ByteRange} [range] Byte range for binary routes.
 * @property {AbortSignal} [signal] Caller abort signal.
 * @property {number} [timeoutMs] Timeout for this request.
 * @property {number[]} [okStatuses] Extra statuses treated as success (the body is returned).
 */

/**
 * Common list options. Typed filters for each route are passed alongside these.
 * @typedef {object} ListOptions
 * @property {number} [pageNumber] Page number, starting at 1.
 * @property {number} [pageSize] Page size.
 * @property {string} [sortBy] Sort field.
 * @property {'Ascending'|'Descending'} [sortDirection] Sort direction.
 * @property {string} [search] Free-text search.
 * @property {string|Date} [fromUtc] Created on or after.
 * @property {string|Date} [toUtc] Created on or before.
 */

/**
 * A page of results.
 * @template T
 * @typedef {object} EnumerationResult
 * @property {T[]} items Items on this page.
 * @property {number} pageNumber Page number.
 * @property {number} pageSize Page size.
 * @property {number} totalCount Total matching items.
 * @property {number} totalPages Total pages.
 * @property {number} durationMs Server query time.
 */

/**
 * Byte range for downloads. `end` is inclusive and optional. `suffix` requests the last n bytes.
 * @typedef {object} ByteRange
 * @property {number} [start] First byte.
 * @property {number} [end] Last byte, inclusive.
 * @property {number} [suffix] Last n bytes (used when start is not given).
 */

/**
 * Result of a binary download.
 * @typedef {object} DownloadResult
 * @property {ArrayBuffer} data Bytes received.
 * @property {string|null} contentType Content-Type header.
 * @property {string|null} fileName File name from Content-Disposition.
 * @property {number} status HTTP status (200, or 206 for a range).
 * @property {number|null} contentLength Bytes in this response.
 * @property {string|null} contentRange Content-Range header.
 * @property {number|null} totalBytes Total file size from Content-Range or Content-Length.
 * @property {string|null} etag ETag header.
 */

/**
 * Error body returned by the server for non-2xx responses.
 * @typedef {object} ErrorBody
 * @property {string} error Error code.
 * @property {string} message Message.
 * @property {string|null} requestId Request identifier.
 * @property {Record<string, string>|null} details Details.
 */

/**
 * Session token issued by `POST /v1.0/token`.
 * @typedef {object} TokenResponse
 * @property {string} token Session token.
 * @property {string} expiresUtc Expiry.
 * @property {string} tenantId Tenant.
 * @property {string} sessionId Session.
 * @property {string|null} userId User, for user logins.
 * @property {string|null} credentialId Credential, for access key logins.
 * @property {string} principalType Principal type.
 * @property {object|null} user User profile, for user logins.
 * @property {string|null} refreshToken Refresh token (mobile clients only).
 * @property {string|null} refreshExpiresUtc Refresh token expiry.
 * @property {boolean} [deletionCancelled] True when this sign-in cancelled a pending account deletion.
 */

/**
 * Token and principal details.
 * @typedef {object} TokenDetails
 * @property {boolean} valid Whether the token is valid.
 * @property {string|null} sessionId Session.
 * @property {string|null} principalType Principal type.
 * @property {string|null} scheme Authentication scheme.
 * @property {string|null} tenantId Tenant.
 * @property {string|null} tenantName Tenant name.
 * @property {string|null} userId User.
 * @property {string|null} credentialId Credential.
 * @property {string|null} email Email.
 * @property {string|null} displayName Display name.
 * @property {boolean} isAdmin Global administrator.
 * @property {boolean} isTenantAdmin Tenant administrator.
 * @property {string|null} issuedUtc Issued.
 * @property {string|null} expiresUtc Expires.
 */

/**
 * Server health.
 * @typedef {object} HealthResponse
 * @property {string} status `Healthy` or `Unhealthy`.
 * @property {string} version Server version.
 * @property {string} startedUtc Start time.
 * @property {number} uptimeSeconds Uptime.
 * @property {Record<string, string>} components Component status.
 */

/**
 * A research topic.
 * @typedef {object} Topic
 * @property {string} id Identifier.
 * @property {string} tenantId Tenant.
 * @property {string} userId Owner.
 * @property {string} name Name.
 * @property {string} slug Slug.
 * @property {string|null} description Description.
 * @property {string|null} brief Research brief.
 * @property {string} audienceLevel `Beginner`, `Practitioner`, or `Expert`.
 * @property {string} horizon `Days`, `Weeks`, `Months`, or `Years`.
 * @property {string} language Language.
 * @property {string} status `Draft`, `Active`, `Paused`, or `Archived`.
 * @property {boolean} isOneTime One-time request topic.
 * @property {number} noveltyPreference Novelty preference.
 * @property {boolean} emailOnRun Email after every run.
 * @property {boolean} emailWhenEmpty Email when a run finds nothing.
 * @property {number} maxEmailFindings Findings per email.
 * @property {number|null} displayThreshold Minimum score to show.
 * @property {number|null} weightLlm Ranking weight override for the model score, or null for the tenant default.
 * @property {number|null} weightFeedback Ranking weight override for feedback, or null.
 * @property {number|null} weightNovelty Ranking weight override for novelty, or null.
 * @property {number|null} weightSource Ranking weight override for source quality, or null.
 * @property {number|null} weightRecency Ranking weight override for recency, or null.
 * @property {string[]} seedQueries Seed queries.
 * @property {string|null} briefingTemplateId Briefing template, or null for the default.
 * @property {boolean} agenticSearch Agentic search: the research model searches and reads pages itself (web_search, fetch_url, and allowed MCP tools). Default false.
 * @property {string[]} mcpToolIds MCP tool IDs (`mct_...`) the topic may use in agentic search. Empty allows every enabled tool.
 * @property {string|null} voiceProfileId Voice profile that reads the topic's run briefings aloud, or null for the workspace default voice.
 * @property {string|null} lastRunUtc Last run.
 * @property {string|null} lastRefinedUtc Last refinement.
 * @property {boolean} active Active.
 * @property {string} createdUtc Created.
 * @property {string} lastUpdateUtc Updated.
 */

/**
 * A registered MCP server.
 * @typedef {object} McpServer
 * @property {string} id Identifier (`mcs_...`).
 * @property {string} tenantId Tenant.
 * @property {string} name Name.
 * @property {string} url Streamable HTTP or SSE URL.
 * @property {string} transport `StreamableHttp` or `Sse`.
 * @property {string|null} vaultCredentialId Vault credential sent to the server, or null.
 * @property {string} headerName Header that carries the credential. Default `Authorization`.
 * @property {string} headerPrefix Prefix before the credential. Default `Bearer `.
 * @property {boolean} enabled Whether agentic research may use the server.
 * @property {number} toolCount Tools found by the last discovery.
 * @property {string|null} lastDiscoveredUtc Last discovery.
 * @property {string|null} lastError Last connection error, or null.
 * @property {string} createdUtc Created.
 * @property {string} lastUpdateUtc Updated.
 */

/**
 * A tool discovered on an MCP server.
 * @typedef {object} McpServerTool
 * @property {string} id Identifier (`mct_...`).
 * @property {string} tenantId Tenant.
 * @property {string} mcpServerId Server.
 * @property {string} name Tool name.
 * @property {string|null} description Description.
 * @property {string|null} inputSchema JSON schema of the arguments, as text.
 * @property {boolean} enabled Whether agentic research may call it. New tools start disabled.
 * @property {string} createdUtc Created.
 * @property {string} lastUpdateUtc Updated.
 */

/**
 * An MCP server with its tools.
 * @typedef {object} McpServerDetail
 * @property {McpServer} server Server.
 * @property {McpServerTool[]} tools Tools, sorted by name.
 */

/**
 * Result of testing an MCP server connection (nothing is saved).
 * @typedef {object} McpServerTestResult
 * @property {boolean} success Whether the server connected and listed its tools.
 * @property {number} [toolCount] Number of tools, on success.
 * @property {string[]} [tools] Tool names, on success.
 * @property {string} [error] Why the connection failed, on failure.
 */

/**
 * One agentic tool call made during a run.
 * @typedef {object} RunToolCall
 * @property {string} id Identifier (`rtc_...`).
 * @property {string} runId Run.
 * @property {string} topicId Topic.
 * @property {string} tool Tool name.
 * @property {string|null} argumentsSummary Short summary of the arguments.
 * @property {boolean} succeeded Whether the call succeeded.
 * @property {string|null} errorMessage Error, when it failed.
 * @property {number} resultCharacters Characters returned.
 * @property {number} durationMs Duration.
 * @property {number} sortOrder Position in the run.
 * @property {string} createdUtc Created.
 */

/**
 * My usage against my plan's limits. A null limit means unlimited.
 * @typedef {object} UsageMeter
 * @property {string} planKey Plan key.
 * @property {string} planName Plan name.
 * @property {number} monthToDateCostUsd Model cost this month (UTC).
 * @property {number|null} [monthlyCostCapUsd] Monthly cost cap.
 * @property {number|null} [costUsedFraction] Share of the cap used, 0 to 1.
 * @property {string} resetUtc When the monthly counters reset.
 * @property {number} topics Topics.
 * @property {number|null} [topicsMax] Topic limit.
 * @property {number} runsToday Runs today.
 * @property {number|null} [runsDailyMax] Daily run limit.
 * @property {number|null} [scheduleMinIntervalMinutes] Shortest schedule interval allowed.
 * @property {number} audioMinutes Audio minutes this month.
 * @property {number|null} [audioMinutesMax] Monthly audio limit.
 * @property {boolean} audioEnabled Whether the plan includes audio.
 * @property {boolean} customEndpoints Whether the plan allows custom model endpoints.
 */

/**
 * A topic with its dimensions, constraints, and recipients.
 * @typedef {object} TopicDetail
 * @property {Topic} topic Topic.
 * @property {TopicDimension[]} dimensions Dimensions.
 * @property {TopicConstraint[]} constraints Constraints.
 * @property {TopicRecipient[]} recipients Email recipients.
 */

/**
 * A topic dimension.
 * @typedef {object} TopicDimension
 * @property {string} id Identifier.
 * @property {string} topicId Topic.
 * @property {string} name Name.
 * @property {string|null} description Description.
 * @property {string|null} rationale Rationale.
 * @property {string} weight `Exclude`, `Low`, `Normal`, or `High`.
 * @property {string[]} includeTerms Terms to include.
 * @property {string[]} excludeTerms Terms to exclude.
 * @property {boolean} isLocked Kept on regeneration.
 * @property {string} origin Origin.
 * @property {number} sortOrder Sort order.
 */

/**
 * A topic constraint.
 * @typedef {object} TopicConstraint
 * @property {string} id Identifier.
 * @property {string} topicId Topic.
 * @property {string|null} dimensionId Dimension.
 * @property {string} kind Constraint kind (for example `IncludeKeyword`, `FeedUrl`).
 * @property {string} value Value.
 * @property {string|null} note Note.
 * @property {boolean} isLocked Locked.
 * @property {string} origin Origin.
 */

/**
 * A topic email recipient.
 * @typedef {object} TopicRecipient
 * @property {string} id Identifier.
 * @property {string} topicId Topic.
 * @property {string} email Address.
 * @property {boolean} active Active.
 */

/**
 * Refinement wizard state.
 * @typedef {object} RefinementState
 * @property {object} session Session (`id`, `status`, `step`, `round`, `seedText`, ...).
 * @property {Topic} topic Topic being refined.
 * @property {TopicDimension[]} dimensions Dimensions.
 * @property {TopicConstraint[]} constraints Constraints.
 * @property {object[]} questions Questions with options and answers.
 * @property {object[]} messages Conversation messages.
 */

/**
 * A one-time research request with its plan.
 * @typedef {object} ResearchRequestResponse
 * @property {object} request Request (`id`, `status`, `requestText`, `title`, `runAtUtc`, ...).
 * @property {Topic|null} topic Backing topic.
 * @property {TopicDimension[]} dimensions Planned dimensions.
 * @property {object[]} questions Clarifying questions.
 * @property {string|null} refinementSessionId Session.
 */

/**
 * A schedule.
 * @typedef {object} Schedule
 * @property {string} id Identifier.
 * @property {string} ownerType `Topic`, `Edition`, or `Request`.
 * @property {string} ownerId Owner.
 * @property {string} userId User.
 * @property {string|null} name Name.
 * @property {string} intervalType Interval type (for example `EveryNDays`, `DaysOfWeek`, `Cron`).
 * @property {number} intervalValue Interval value.
 * @property {number} minuteOffset Minute offset.
 * @property {number} daysOfWeekMask Days of week bit mask.
 * @property {number[]} daysOfMonth Days of month.
 * @property {string[]} timesOfDay Times of day (HH:mm).
 * @property {string|null} cronExpression Cron expression.
 * @property {string} timeZone IANA time zone.
 * @property {boolean} enabled Enabled.
 * @property {string} missedRunPolicy `Skip` or `RunOnceOnRecovery`.
 * @property {string|null} nextRunUtc Next run.
 * @property {string|null} lastRunUtc Last run.
 */

/**
 * Schedule preview.
 * @typedef {object} SchedulePreview
 * @property {boolean} valid Valid.
 * @property {string[]} errors Validation errors.
 * @property {string[]} nextRunsUtc Next runs (UTC).
 * @property {string[]} nextRunsLocal Next runs in the schedule's time zone.
 */

/**
 * A research run.
 * @typedef {object} Run
 * @property {string} id Identifier.
 * @property {string} topicId Topic.
 * @property {string} userId User.
 * @property {string|null} scheduleId Schedule.
 * @property {string} trigger Trigger.
 * @property {string} status `Queued`, `Running`, `Completed`, `PartiallySucceeded`, `Failed`, `Cancelled`, or `Skipped`.
 * @property {boolean} isDryRun Dry run.
 * @property {boolean} cancelRequested Cancel requested.
 * @property {string} queuedUtc Queued.
 * @property {string|null} startedUtc Started.
 * @property {string|null} completedUtc Completed.
 * @property {string|null} errorMessage Error.
 * @property {string|null} summary Summary.
 * @property {number} durationMs Duration.
 * @property {number} newFindingCount New findings.
 * @property {number} sourceCount Sources fetched.
 * @property {number} inputTokens Input tokens across the run's model calls.
 * @property {number} outputTokens Output tokens.
 * @property {number} cachedInputTokens Input tokens read from the provider's prompt cache.
 * @property {number} reasoningTokens Reasoning (thinking) tokens.
 * @property {number} llmCallCount Model calls made during the run.
 * @property {number} unpricedCallCount Model calls with no price (endpoint without prices, or a subscription CLI), left out of the cost.
 * @property {number|null} estimatedCostUsd Estimated cost in US dollars, or null when no call was priced. Updated after every stage.
 * @property {string|null} resumeFromStage Stage a retried run resumes from, or null for a full run.
 */

/**
 * A server-sent event from `runs.events`.
 * @typedef {object} RunEvent
 * @property {string} event Event type: `run`, `stage`, `ping`, `done`, or `timeout` (`message` when the server names none).
 * @property {string|null} id Event ID, or null.
 * @property {any} data Data parsed as JSON (the raw text when it is not JSON).
 * @property {string} raw Raw data text.
 */

/**
 * A run stage.
 * @typedef {object} RunStage
 * @property {string} id Identifier.
 * @property {string} runId Run.
 * @property {string} stage Stage name.
 * @property {string} status Stage status.
 * @property {number} durationMs Duration.
 * @property {number} itemsIn Items in.
 * @property {number} itemsOut Items out.
 * @property {string|null} errorMessage Error.
 * @property {number} llmCallCount Model calls made during the stage.
 * @property {number} unpricedCallCount Model calls with no price, left out of the cost.
 * @property {number} inputTokens Input tokens.
 * @property {number} outputTokens Output tokens.
 * @property {number} cachedInputTokens Input tokens read from the provider's prompt cache.
 * @property {number} reasoningTokens Reasoning (thinking) tokens.
 * @property {number|null} estimatedCostUsd Estimated cost in US dollars, or null when no call was priced.
 */

/**
 * Cost and token totals for a run, a stage, a model, or a task. Amounts are US dollar estimates from the endpoints'
 * prices, null when no call was priced.
 * @typedef {object} CostTotals
 * @property {number|null} estimatedCostUsd Estimated cost.
 * @property {number|null} inputCostUsd Input share of the cost (uncached, cached, and cache-write input).
 * @property {number|null} outputCostUsd Output share of the cost.
 * @property {number} llmCallCount Model calls.
 * @property {number} unpricedCallCount Calls with no price, left out of the amounts.
 * @property {number} failedCallCount Failed calls.
 * @property {number} inputTokens Input tokens.
 * @property {number} outputTokens Output tokens.
 * @property {number} cachedInputTokens Input tokens read from the prompt cache.
 * @property {number} cacheWriteTokens Input tokens written to the prompt cache.
 * @property {number} reasoningTokens Reasoning tokens.
 * @property {number} modelDurationMs Time spent in model calls, in milliseconds.
 */

/**
 * Cost of one run stage.
 * @typedef {CostTotals & { runStageId: string, stage: string, status: string, sortOrder: number, durationMs: number, itemsIn: number, itemsOut: number, shareOfCost: number|null }} StageCost
 */

/**
 * Cost of one model endpoint and model in a run.
 * @typedef {CostTotals & { modelEndpointId: string|null, endpointName: string|null, provider: string|null, model: string|null }} ModelCost
 */

/**
 * Cost of one model task in a run.
 * @typedef {CostTotals & { task: string }} TaskCost
 */

/**
 * What a run cost, from `runs.costs`.
 * @typedef {object} RunCostReport
 * @property {string} runId Run.
 * @property {string} topicId Topic.
 * @property {string} status Run status.
 * @property {string} currency Always `USD`.
 * @property {CostTotals} totals Totals across the run.
 * @property {number|null} costPerNewFindingUsd Cost divided by new findings, or null.
 * @property {StageCost[]} stages One row per stage, in run order.
 * @property {CostTotals|null} unattributed Calls with no stage, or null when every call has one.
 * @property {ModelCost[]} byModel By endpoint and model, most expensive first.
 * @property {TaskCost[]} byTask By task, most expensive first.
 */

/**
 * A ranked finding.
 * @typedef {object} Finding
 * @property {string} id Identifier.
 * @property {string} runId Run.
 * @property {string} topicId Topic.
 * @property {string|null} dimensionId Dimension.
 * @property {string} title Title.
 * @property {string|null} summary Summary.
 * @property {string|null} whyItMatters Why it matters.
 * @property {string} category Category.
 * @property {number} relevanceScore Relevance score (0 to 100).
 * @property {string} tier Tier (for example `MustRead`, `WorthALook`, `BelowThreshold`).
 * @property {boolean} isDuplicate Duplicate.
 * @property {boolean} pinned Pinned by the user.
 * @property {boolean} hidden Hidden by the user (left out of lists, briefings, and emails).
 * @property {string|null} publishedUtc Published.
 */

/**
 * A finding with its evidence.
 * @typedef {object} FindingDetail
 * @property {Finding} finding Finding.
 * @property {object[]} evidence Evidence with source URL, title, domain, and role.
 * @property {TopicDimension|null} dimension Dimension.
 * @property {string|null} topicName Topic name.
 */

/**
 * One piece of evidence with its source.
 * @typedef {object} EvidenceView
 * @property {object} evidence Evidence (`id`, `sourceItemId`, `runId`, `quote`, `startOffset`, `endOffset`, `contextText`, `verified`, ...).
 * @property {string|null} sourceUrl Source URL.
 * @property {string|null} sourceTitle Source title.
 * @property {string|null} sourceDomain Source domain.
 * @property {string|null} sourcePublishedUtc Source published time.
 * @property {string} role Role (`Supports` by default).
 */

/**
 * A breakdown row in an analytics report.
 * @typedef {object} BreakdownRow
 * @property {string} key Key.
 * @property {string|null} label Label.
 * @property {number} count Count.
 * @property {number} positive Positive count.
 * @property {number|null} rate Rate.
 * @property {number|null} value Value.
 */

/**
 * Overview analytics report.
 * @typedef {object} OverviewReport
 * @property {string} fromUtc Window start.
 * @property {string} toUtc Window end.
 * @property {number} activeTopics Active topics.
 * @property {number} runs Runs.
 * @property {number} runsCompleted Completed runs.
 * @property {number} runsPartiallySucceeded Partially succeeded runs.
 * @property {number} runsFailed Failed runs.
 * @property {number} findingsShown Findings shown.
 * @property {number} findingsRated Findings rated.
 * @property {number|null} relevancePrecision Relevance precision.
 * @property {number} briefingsReady Ready briefings.
 * @property {number} emailsSent Emails sent.
 * @property {number} emailsFailed Emails failed.
 * @property {number} predictionsOpen Open predictions.
 * @property {number} predictionsResolved Resolved predictions.
 * @property {number|null} predictionAccuracy Prediction accuracy.
 * @property {number|null} meanBrier Mean Brier score.
 * @property {number} feedbackTotal Feedback count.
 * @property {number} feedbackWithReason Feedback with a reason.
 * @property {number} suggestionsNew New suggestions.
 * @property {number} suggestionsAccepted Accepted suggestions.
 * @property {number|null} quoteFailureRate Quote verification failure rate.
 * @property {number} modelCalls Model calls.
 * @property {number} modelCostUsd Model cost.
 */

/**
 * Email analytics report.
 * @typedef {object} EmailReport
 * @property {number} total Emails.
 * @property {number} sent Sent.
 * @property {number} failed Failed.
 * @property {number} pending Pending.
 * @property {number} skipped Skipped.
 * @property {number|null} deliveryRate Delivery rate.
 * @property {number|null} meanSecondsToSend Mean seconds from queue to send.
 * @property {BreakdownRow[]} byTemplate By template.
 * @property {BreakdownRow[]} byDay By day.
 * @property {BreakdownRow[]} byStatus By status.
 * @property {number} feedbackLinkOpens Feedback link opens.
 * @property {number} feedbackLinkSubmits Feedback link submits.
 * @property {number} unsubscribes Unsubscribes.
 */

/**
 * A prediction.
 * @typedef {object} Prediction
 * @property {string} id Identifier.
 * @property {string} topicId Topic.
 * @property {string} statement Statement.
 * @property {string|null} rationale Rationale.
 * @property {number} confidence Confidence (0 to 1).
 * @property {string} horizonUtc Horizon.
 * @property {string} status Status.
 * @property {number|null} brierScore Brier score.
 */

/**
 * A suggestion.
 * @typedef {object} Suggestion
 * @property {string} id Identifier.
 * @property {string} topicId Topic.
 * @property {string} title Title.
 * @property {string|null} description Description.
 * @property {string} priority Priority.
 * @property {string} effort Effort.
 * @property {string} status `New`, `Accepted`, `Snoozed`, `Dismissed`, or `Done`.
 * @property {string|null} snoozedUntilUtc Snoozed until.
 */

/**
 * A fetched source.
 * @typedef {object} SourceItem
 * @property {string} id Identifier.
 * @property {string} canonicalUrl URL.
 * @property {string|null} domain Domain.
 * @property {string|null} title Title.
 * @property {string|null} publishedUtc Published.
 * @property {string} fetchedUtc Fetched.
 * @property {string|null} contentText Extracted text (detail only).
 */

/**
 * Feedback on an item.
 * @typedef {object} Feedback
 * @property {string} id Identifier.
 * @property {string} targetType Target type (for example `Finding`, `Prediction`, `LearnedPreference`).
 * @property {string} targetId Target.
 * @property {string|null} topicId Topic.
 * @property {string|null} relevance Relevance rating.
 * @property {string|null} accuracy Accuracy rating.
 * @property {string|null} usefulness Usefulness rating.
 * @property {string|null} reason Reason.
 * @property {string} channel Channel (`Dashboard`, `Email`, `Mobile`, `Api`, or `Mcp`).
 * @property {boolean} isCurrent Current rating.
 */

/**
 * A learned preference.
 * @typedef {object} LearnedPreference
 * @property {string} id Identifier.
 * @property {string|null} topicId Topic, or null for all topics.
 * @property {string} kind `Prefer`, `Avoid`, `PreferSource`, `AvoidSource`, or `StyleNote`.
 * @property {string} statement Statement.
 * @property {string} status Status (`Proposed`, `Active`, `Rejected`, `Disabled`).
 * @property {string} origin Origin.
 * @property {number} confidence Confidence.
 */

/**
 * An edition with its topics and schedules.
 * @typedef {object} EditionResponse
 * @property {object} edition Edition (`id`, `name`, `description`, `briefingTemplateId`, `audioEnabled`, `emailEnabled`, ...).
 * @property {object[]} topics Ordered topic links.
 * @property {Schedule[]} schedules Schedules.
 * @property {string[]} coveredTopicIds Topics the edition covers now.
 */

/**
 * A briefing.
 * @typedef {object} Briefing
 * @property {string} id Identifier.
 * @property {string} kind Kind.
 * @property {string|null} editionId Edition.
 * @property {string|null} topicId Topic.
 * @property {string|null} runId Run.
 * @property {string} title Title.
 * @property {string|null} markdown Markdown (detail and preview only).
 * @property {string} status `Queued`, `Composing`, `Ready`, or `Failed`.
 * @property {string|null} markdownArtifactId Markdown file.
 * @property {string|null} htmlArtifactId HTML file.
 * @property {number} itemCount Items.
 * @property {number} wordCount Words.
 * @property {boolean} pinned Pinned past retention.
 * @property {boolean} isDryRun Dry run or preview.
 */

/**
 * A briefing with its sections, items, files, and audio.
 * @typedef {object} BriefingDetail
 * @property {Briefing} briefing Briefing.
 * @property {object[]} sections Sections.
 * @property {object[]} items Items.
 * @property {Artifact[]} files Stored files.
 * @property {boolean} filesExpired Files passed retention.
 * @property {string|null} editionName Edition name.
 * @property {string|null} topicName Topic name.
 * @property {Record<string, string>} itemTitles Item titles by item ID.
 * @property {MediaAsset|null} audio Latest audio.
 */

/**
 * A stored file.
 * @typedef {object} Artifact
 * @property {string} id Identifier.
 * @property {string} kind Kind.
 * @property {string|null} briefingId Briefing.
 * @property {string} fileName File name.
 * @property {string} contentType Content type.
 * @property {number} bytes Size.
 * @property {string} sha256 SHA-256.
 * @property {string|null} expiresUtc Retention expiry.
 * @property {boolean} pinned Pinned.
 * @property {string|null} deletedUtc Deleted by retention.
 */

/**
 * An audio episode.
 * @typedef {object} MediaAsset
 * @property {string} id Identifier.
 * @property {string} briefingId Briefing.
 * @property {string|null} editionId Edition.
 * @property {string} kind Kind.
 * @property {string} status Status.
 * @property {string} title Title.
 * @property {string|null} artifactId Audio file.
 * @property {string|null} chaptersArtifactId Chapters file.
 * @property {number} durationSeconds Duration.
 */

/**
 * A briefing template with its current content.
 * @typedef {object} TemplateDetail
 * @property {object} template Template (`id`, `name`, `description`, `currentVersion`, `customized`, ...).
 * @property {string} content Current Markdown.
 * @property {object[]} problems Validation problems.
 * @property {object[]} sections Parsed sections.
 * @property {boolean} shared Shared with the tenant.
 * @property {string} title Title line.
 * @property {object} audio Audio options.
 */

/**
 * A prompt template with its content.
 * @typedef {object} PromptTemplateResponse
 * @property {object} template Template (`id`, `key`, `topicId`, `name`, `role`, `currentVersion`, `isCustomized`, ...).
 * @property {string} content Current content.
 * @property {string[]} variables Variables.
 * @property {string} defaultContent Shipped default.
 */

/**
 * A model endpoint.
 * @typedef {object} ModelEndpoint
 * @property {string} id Identifier.
 * @property {string} name Name.
 * @property {string} provider Provider: `Anthropic`, `OpenAi`, `OpenAiCompatible`, `Ollama`, `Gemini`, `XAi`, `OllamaCloud`,
 *     or an agent CLI in the server: `ClaudeCode`, `Codex`, `GeminiCli`, `Mux`.
 * @property {string|null} endpoint Base URL.
 * @property {string|null} model Chat model.
 * @property {string|null} embeddingModel Embedding model.
 * @property {boolean} supportsChat Serves chat.
 * @property {boolean} supportsEmbeddings Serves embeddings.
 * @property {number} timeoutMs Timeout.
 * @property {boolean} active Active.
 * @property {boolean|null} [lastValidationSucceeded] Whether the endpoint last proved it works: true after a successful
 *     validation or model call, false after a failed validation, null when never checked or after its connection
 *     settings changed.
 * @property {'ApiKey'|'AccessToken'|'AuthFile'|'ServerSignIn'|'None'|null} [authMethod] How the endpoint signs in. Null
 *     means the provider's usual method: an API key for hosted APIs, none for Ollama and OpenAI-compatible servers
 *     without a key, and the server's own sign-in for agent CLIs.
 */

/**
 * One thing the workspace needs before people can use it, and whether it is ready.
 * @typedef {object} SetupCheck
 * @property {string} key Stable key: `model:{endpointId}` (or `model:none`), `search`, `embeddings`, `email`, or `speech`.
 * @property {'Model'|'Search'|'Embeddings'|'Email'|'Speech'} kind What the check covers. Model and Search are required;
 *     Embeddings, Email, and Speech (the text to speech endpoint) are optional and sent to administrators only.
 * @property {boolean} required Whether the workspace is blocked until this check is ready.
 * @property {'Ready'|'Missing'|'Unverified'|'Failed'} state State.
 * @property {string|null} [message] Plain-language detail, such as the provider's error text.
 * @property {string|null} [resourceId] Model endpoint or search provider ID, when the check is about one.
 * @property {string|null} [resourceName] Model endpoint or search provider name.
 * @property {string|null} [provider] Provider name.
 * @property {string|null} [model] Model name, for model, embedding, and speech checks.
 * @property {string|null} [voice] Default voice, for the speech check. Null otherwise.
 * @property {string|null} [baseUrl] Server address, for Ollama and OpenAI-compatible model checks and the speech check,
 *     shown to administrators. Null otherwise.
 * @property {string[]} tasks Tasks the endpoint serves, for model checks.
 * @property {'ApiKey'|'AccessToken'|'AuthFile'|'ServerSignIn'|'None'|null} [authMethod] How the endpoint signs in, for
 *     model checks.
 * @property {boolean} needsApiKey Whether the sign-in method needs a stored secret (an API key, a token, or a sign-in
 *     file).
 * @property {boolean} hasApiKey Whether a key is stored or supplied by the platform.
 * @property {boolean} usesPlatformKey Whether the endpoint uses the hosted service's key, which users never enter.
 * @property {string|null} [verifiedUtc] When the resource was last checked.
 */

/**
 * A voice someone can choose for a topic: a workspace voice profile, or a voice the speech server offers.
 * @typedef {object} AvailableVoice
 * @property {string} voice The speech server's voice ID, such as `af_heart`.
 * @property {string} label A readable label, such as "Heart (American English, female)"; the voice ID when it follows no
 *     known naming.
 * @property {string|null} voiceProfileId The workspace voice profile that uses this voice, or null for a speech server
 *     voice that has no profile yet (choosing it for a topic creates one).
 * @property {string|null} name The voice profile's name, or null.
 * @property {boolean} isDefault True for the workspace's default voice profile.
 */

/**
 * The voices someone can choose for a topic.
 * @typedef {object} AvailableVoicesResponse
 * @property {boolean} enabled Whether speech is turned on for this server.
 * @property {boolean} listed True when the speech server listed its voices; false when the list is only the configured
 *     default voices.
 * @property {string|null} defaultVoiceProfileId The workspace's default voice profile, or null.
 * @property {AvailableVoice[]} voices Voice profiles first, then the speech server's other voices.
 */

/**
 * Whether the workspace has everything it needs, checked with live calls.
 * @typedef {object} SetupStatusResponse
 * @property {boolean} ready True when every required check is ready.
 * @property {boolean} canConfigure Whether the caller can change the configuration (a global or tenant administrator).
 * @property {SetupCheck[]} checks Checks, required ones first.
 * @property {string} checkedUtc When the status was computed.
 * @property {SetupConnectionResult|null} [connection] Result of the connection attempt, on responses to
 *     `setup.connect`. Null otherwise.
 */

/**
 * Outcome of a Setup page connection attempt.
 * @typedef {object} SetupConnectionResult
 * @property {string|null} [modelEndpointId] Endpoint that was connected or tried.
 * @property {boolean} succeeded Whether the live test prompt succeeded.
 * @property {boolean} keptPrevious Whether a working model was kept because the new one failed its test. The
 *     workspace keeps using it.
 * @property {string|null} [message] The test's result in plain language (the provider's error text on failure).
 */

/**
 * One way a provider can sign in, as offered on the Setup page.
 * @typedef {object} SetupAuthOption
 * @property {'ApiKey'|'AccessToken'|'AuthFile'|'ServerSignIn'|'None'} method Method.
 * @property {'Text'|'File'|'None'} input What the user supplies: a key or token, a sign-in file's contents, or nothing.
 * @property {string|null} [fileName] File name the CLI keeps the sign-in in, for File inputs (`auth.json`,
 *     `oauth_creds.json`, `endpoints.json`).
 * @property {boolean} recommended Whether this is the suggested method for the provider.
 */

/**
 * A model provider the Setup page can connect, with the ways it can sign in.
 * @typedef {object} SetupProviderOption
 * @property {string} provider Provider.
 * @property {'Api'|'AgentCli'|'SelfHosted'} group Group for display: a hosted API, an agent CLI in the server image, or a
 *     model server you run yourself.
 * @property {boolean} available Whether the provider can be used on this server now (for agent CLIs: allowed and
 *     installed).
 * @property {boolean} installed For agent CLIs: whether the executable is installed in the server. Always true for other
 *     providers.
 * @property {boolean} turnedOff For agent CLIs: whether the server's operator turned agent CLIs off
 *     (`Models.AllowAgentClis`).
 * @property {string|null} [unavailableReason] Why the provider is unavailable, when it is.
 * @property {boolean} recommended Whether this is the suggested provider.
 * @property {string|null} [defaultModel] Model filled in by default; `default` lets an agent CLI choose; null means the
 *     user must name one.
 * @property {boolean} needsBaseUrl Whether a base URL is asked for (Ollama and OpenAI-compatible servers).
 * @property {string|null} [defaultBaseUrl] Base URL filled in by default.
 * @property {SetupAuthOption[]} methods Sign-in methods, recommended first.
 */

/**
 * Model providers the Setup page can connect on this server.
 * @typedef {object} SetupOptionsResponse
 * @property {SetupProviderOption[]} providers Providers, recommended first within each group.
 * @property {string} serverContainer Name of the server container, for sign-in commands such as
 *     `docker exec -it {container} claude`.
 */

/**
 * Connect the workspace's chat model: choose a provider and how it signs in, then verify it with a live call.
 * @typedef {object} SetupConnectRequest
 * @property {string} provider Provider. Required.
 * @property {'ApiKey'|'AccessToken'|'AuthFile'|'ServerSignIn'|'None'} [authMethod] Sign-in method. Omitted uses the
 *     provider's recommended method.
 * @property {string} [secret] The API key, the access token (`claude setup-token`), or the sign-in file's contents
 *     (`auth.json`, `oauth_creds.json`, `endpoints.json`). Required unless the method is `ServerSignIn` or `None`, or the
 *     endpoint already has one stored for the same method. Stored encrypted and never returned.
 * @property {string} [model] Model. Omitted uses the provider's default (an agent CLI's own default when it has one).
 * @property {string} [baseUrl] Base URL for Ollama and OpenAI-compatible servers.
 * @property {string} [modelEndpointId] Endpoint to change. Omitted reuses the workspace's endpoint for the same provider,
 *     or creates one.
 */

/**
 * A search provider.
 * @typedef {object} SearchProvider
 * @property {string} id Identifier.
 * @property {string} name Name.
 * @property {string} kind Kind.
 * @property {string|null} endpoint Endpoint.
 * @property {boolean} isDefault Default for topics.
 * @property {number} maxResults Maximum results.
 * @property {boolean} active Active.
 */

/**
 * A voice profile.
 * @typedef {object} VoiceProfile
 * @property {string} id Identifier.
 * @property {string} name Name.
 * @property {string} provider Provider.
 * @property {string|null} endpoint Endpoint.
 * @property {string} model Model.
 * @property {string} voice Voice.
 * @property {string|null} secondaryVoice Second host voice.
 * @property {number} speed Speed.
 * @property {boolean} isDefault Default.
 */

/**
 * A podcast feed token (the secret is only returned once, at creation).
 * @typedef {object} FeedToken
 * @property {string} id Identifier.
 * @property {string|null} editionId Edition.
 * @property {string} name Name.
 * @property {string} tokenPrefix Prefix.
 * @property {string|null} lastUsedUtc Last used.
 * @property {string|null} revokedUtc Revoked.
 */

/**
 * A created feed token with its secret and URL.
 * @typedef {object} FeedTokenCreated
 * @property {FeedToken} token Token.
 * @property {string} secret Secret (shown once).
 * @property {string} feedUrl Feed URL.
 */

/**
 * An outbox email.
 * @typedef {object} EmailMessage
 * @property {string} id Identifier.
 * @property {string} template Template.
 * @property {string|null} runId Run.
 * @property {string|null} briefingId Briefing.
 * @property {string} recipient Recipient.
 * @property {string} subject Subject.
 * @property {string} status Status.
 * @property {string|null} renderedHtml HTML (detail only).
 * @property {string|null} renderedText Text (detail only).
 */

/**
 * Email preferences.
 * @typedef {object} EmailPreferences
 * @property {string} address Address.
 * @property {boolean} runEmails Run emails on.
 * @property {boolean} briefingEmails Briefing emails on.
 * @property {boolean} failureEmails Failure emails on.
 * @property {string[]} unsubscribedTopicIds Topics unsubscribed from run emails.
 */

/**
 * A vault credential (never carries the secret).
 * @typedef {object} VaultCredential
 * @property {string} id Identifier.
 * @property {string} name Name.
 * @property {string} kind Kind.
 * @property {string} provider Provider.
 * @property {string|null} headerName Header name.
 * @property {string|null} username Username.
 * @property {string} secretPreview Masked preview.
 * @property {string|null} description Description.
 * @property {boolean} active Active.
 */

/**
 * A push device.
 * @typedef {object} Device
 * @property {string} id Identifier.
 * @property {string} expoPushToken Expo push token.
 * @property {string} platform `ios` or `android`.
 * @property {string|null} name Name.
 * @property {string|null} locale Locale.
 */

/**
 * An in-app notification.
 * @typedef {object} Notification
 * @property {string} id Identifier.
 * @property {string} kind `RunResults`, `BriefingReady`, `AudioReady`, `RunFailed`, `CapReached`, or `PredictionDue`.
 * @property {string} title What it is about (topic name, briefing title).
 * @property {string|null} body A short sentence.
 * @property {string|null} topicId Topic to open.
 * @property {string|null} runId Run to open.
 * @property {string|null} briefingId Briefing to open.
 * @property {string|null} mediaAssetId Audio to open.
 * @property {string|null} predictionId Prediction to open.
 * @property {string|null} readUtc When it was read; absent while unread.
 * @property {string} createdUtc When it was created.
 */

/**
 * One cell of the notification preference matrix.
 * @typedef {object} NotificationPreferenceCell
 * @property {string} kind Kind of notification.
 * @property {string} channel `Email`, `Push`, or `InApp`.
 * @property {boolean} enabled Whether the user wants it.
 */

/**
 * My notification preferences.
 * @typedef {object} NotificationPreferences
 * @property {NotificationPreferenceCell[]} preferences Every kind on every channel (18 cells).
 * @property {{ channel: string, available: boolean, reason?: string }[]} channels Whether each channel can deliver right now.
 * @property {string} address My email address, or empty.
 * @property {string[]} unsubscribedTopicIds Topics whose run emails I unsubscribed from.
 */

/**
 * A recorded model call.
 * @typedef {object} LlmCall
 * @property {string} id Identifier.
 * @property {string} task Task.
 * @property {string|null} runId Run.
 * @property {string|null} runStageId Run stage the call was made in.
 * @property {string|null} model Model.
 * @property {string|null} promptKey Prompt key.
 * @property {number} inputTokens Input tokens.
 * @property {number} outputTokens Output tokens.
 * @property {number} durationMs Duration.
 * @property {boolean} success Success.
 * @property {number|null} estimatedCostUsd Estimated cost.
 */

/**
 * The caller's effective plan.
 * @typedef {object} EffectivePlan
 * @property {object} plan Plan (`id`, `key`, `name`, ...).
 * @property {Record<string, string>} features Feature limits.
 * @property {object|null} assignment Current assignment.
 * @property {string|null} endsUtc End of the current plan.
 * @property {object[]} upcoming Upcoming assignments.
 * @property {Record<string, string>} planNames Plan names by ID.
 */

// ---------------------------------------------------------------------------------------------------------------------
// Errors
// ---------------------------------------------------------------------------------------------------------------------

/**
 * Error raised for any failed request: non-2xx responses, timeouts (`code` `Timeout`, `status` 0), and network
 * failures (`code` `NetworkError`, `status` 0).
 */
export class MyMorningNewsError extends Error {
    /**
     * Create an error.
     * @param {string} message Message.
     * @param {object} [info] Error details.
     * @param {number} [info.status] HTTP status, or 0 when no response was received.
     * @param {string|null} [info.code] Server error code (for example `NotFound`, `ValidationFailed`).
     * @param {string|null} [info.requestId] Server request identifier.
     * @param {Record<string, string>|null} [info.details] Details map.
     * @param {any} [info.body] Parsed response body (object or text).
     * @param {string} [info.method] HTTP method.
     * @param {string} [info.path] Request path.
     * @param {unknown} [info.cause] Underlying error.
     */
    constructor(message, info = {}) {
        super(message, info.cause !== undefined ? { cause: info.cause } : undefined);
        this.name = 'MyMorningNewsError';
        /** @type {number} */
        this.status = info.status ?? 0;
        /** @type {string|null} */
        this.code = info.code ?? null;
        /** @type {string|null} */
        this.requestId = info.requestId ?? null;
        /** @type {Record<string, string>|null} */
        this.details = info.details ?? null;
        /** @type {any} */
        this.body = info.body ?? null;
        /** @type {string|null} */
        this.method = info.method ?? null;
        /** @type {string|null} */
        this.path = info.path ?? null;
    }
}

/**
 * Raised by `login` when the email matches users in several tenants (HTTP 409). Retry with `{ tenantId }`.
 */
export class TenantSelectionRequiredError extends MyMorningNewsError {
    /**
     * Create the error.
     * @param {string} message Message.
     * @param {object} info Error details, as for {@link MyMorningNewsError}.
     * @param {{ id: string, name: string }[]} tenants Tenants to choose from.
     */
    constructor(message, info, tenants) {
        super(message, info);
        this.name = 'TenantSelectionRequiredError';
        /** @type {{ id: string, name: string }[]} */
        this.tenants = Array.isArray(tenants) ? tenants : [];
    }
}

/**
 * Raised for HTTP 402 `CapReached`: a plan limit was reached. `key` names the limit (for example `topics.max`,
 * `runs.daily.max`, `usage.monthlyCostUsd.max`, `audio.enabled`, `endpoints.custom`), `limit` is the plan's value, and
 * `resetUtc` is when the counter resets, or null.
 */
export class CapReachedError extends MyMorningNewsError {
    /**
     * Create the error.
     * @param {string} message Message.
     * @param {object} info Error details, as for {@link MyMorningNewsError}.
     */
    constructor(message, info) {
        super(message, info);
        this.name = 'CapReachedError';
        const d = this.details ?? {};
        /** @type {string|null} Limit key from `details.key`. */
        this.key = d.key ?? null;
        /** @type {string|null} The plan's value for the limit from `details.limit`. */
        this.limit = d.limit ?? null;
        /** @type {Date|null} When the limit resets, from `details.resetUtc`. */
        this.resetUtc = d.resetUtc && !Number.isNaN(Date.parse(d.resetUtc)) ? new Date(d.resetUtc) : null;
    }
}

/**
 * Raised by `login` for HTTP 403 `EmailNotVerified`: the account signed up publicly and has not confirmed its email
 * yet. Open the emailed link (`account.verifyEmail`) or ask for a new one (`account.resendVerification`).
 */
export class EmailNotVerifiedError extends MyMorningNewsError {
    /**
     * Create the error.
     * @param {string} message Message.
     * @param {object} info Error details, as for {@link MyMorningNewsError}.
     */
    constructor(message, info) {
        super(message, info);
        this.name = 'EmailNotVerifiedError';
    }
}

/**
 * Raised for HTTP 409 `SetupRequired`: the workspace is missing something it needs, such as a working model key.
 * Refinement sessions and `requests.create` raise it when the model endpoint for refinement has no key or failed its
 * last check. `check` names the setup check that is not ready (it matches a `SetupCheck.key`); read `setup.status()`,
 * and an administrator can fix it with `setup.setModelKey` or `setup.verify`.
 */
export class SetupRequiredError extends MyMorningNewsError {
    /**
     * Create the error.
     * @param {string} message Message.
     * @param {object} info Error details, as for {@link MyMorningNewsError}.
     */
    constructor(message, info) {
        super(message, info);
        this.name = 'SetupRequiredError';
        /** @type {string|null} Setup check key from `details.check`, for example `model:mdl_abc`. */
        this.check = (this.details ?? {}).check ?? null;
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------------------------------------------------

function requireValue(value, name) {
    if (value === undefined || value === null || value === '') throw new TypeError(name + ' is required.');
    return value;
}

function seg(value, name) {
    return encodeURIComponent(String(requireValue(value, name)));
}

function queryValue(value) {
    if (value instanceof Date) return value.toISOString();
    return String(value);
}

function formatRange(range) {
    if (!range || typeof range !== 'object') throw new TypeError('range must be an object such as { start, end }.');
    if (range.start === undefined || range.start === null) {
        if (!Number.isInteger(range.suffix) || range.suffix <= 0) throw new TypeError('range needs start, or a positive suffix.');
        return 'bytes=-' + range.suffix;
    }
    if (!Number.isInteger(range.start) || range.start < 0) throw new TypeError('range.start must be a non-negative integer.');
    if (range.end === undefined || range.end === null) return 'bytes=' + range.start + '-';
    if (!Number.isInteger(range.end) || range.end < range.start) throw new TypeError('range.end must be an integer at or after range.start.');
    return 'bytes=' + range.start + '-' + range.end;
}

function parseFileName(disposition) {
    if (!disposition) return null;
    const star = /filename\*\s*=\s*([^']*)'[^']*'([^;]+)/i.exec(disposition);
    if (star) {
        try {
            return decodeURIComponent(star[2].trim());
        } catch {
            return star[2].trim();
        }
    }
    const plain = /filename\s*=\s*"([^"]*)"|filename\s*=\s*([^;]+)/i.exec(disposition);
    if (plain) return (plain[1] ?? plain[2] ?? '').trim() || null;
    return null;
}

function bindAll(instance) {
    let proto = Object.getPrototypeOf(instance);
    while (proto && proto !== Object.prototype) {
        for (const name of Object.getOwnPropertyNames(proto)) {
            if (name === 'constructor' || name.startsWith('_')) continue;
            const desc = Object.getOwnPropertyDescriptor(proto, name);
            if (desc && typeof desc.value === 'function' && !Object.prototype.hasOwnProperty.call(instance, name)) {
                instance[name] = desc.value.bind(instance);
            }
        }
        proto = Object.getPrototypeOf(proto);
    }
}

function delay(ms, signal) {
    return new Promise((resolve, reject) => {
        if (signal?.aborted) {
            reject(signal.reason ?? new Error('Aborted.'));
            return;
        }
        const timer = setTimeout(() => {
            signal?.removeEventListener('abort', onAbort);
            resolve();
        }, ms);
        function onAbort() {
            clearTimeout(timer);
            reject(signal.reason ?? new Error('Aborted.'));
        }
        signal?.addEventListener('abort', onAbort, { once: true });
    });
}

/**
 * Build the string a signed request signs.
 * @param {object} input Parts of the request.
 * @param {string} input.method HTTP method (uppercased).
 * @param {string} input.pathAndQuery Path and query exactly as sent, for example `/v1.0/api/topics?pageSize=1`.
 * @param {string} input.tenantId Tenant sent in `x-tenant-guid`.
 * @param {string} input.date Value of `x-date` (ISO 8601 UTC).
 * @param {string} input.nonce Value of `x-nonce` (8 to 128 characters).
 * @param {string|Uint8Array|null} [input.body] Body exactly as sent; empty when omitted.
 * @returns {string} `MMN1-HMAC-SHA256\n{METHOD}\n{pathAndQuery}\n{tenant}\n{date}\n{nonce}\n{sha256 hex of body}`.
 * @throws {TypeError} A required part is missing.
 */
export function stringToSign(input) {
    requireValue(input, 'input');
    const bodyHash = createHash('sha256').update(input.body ?? '').digest('hex');
    return [
        SIGNATURE_ALGORITHM,
        String(requireValue(input.method, 'method')).toUpperCase(),
        requireValue(input.pathAndQuery, 'pathAndQuery'),
        requireValue(input.tenantId, 'tenantId'),
        requireValue(input.date, 'date'),
        requireValue(input.nonce, 'nonce'),
        bodyHash
    ].join('\n');
}

/**
 * Compute a request signature: lowercase hex HMAC-SHA256 of {@link stringToSign}, keyed with the UTF-8 secret.
 * @param {object} input Parts of the request, as for {@link stringToSign}, plus the secret.
 * @param {string} input.secretKey Secret key.
 * @returns {string} Signature for `x-signature`.
 * @throws {TypeError} A required part is missing.
 */
export function signRequest(input) {
    requireValue(input, 'input');
    const secret = requireValue(input.secretKey, 'secretKey');
    return createHmac('sha256', Buffer.from(String(secret), 'utf8')).update(stringToSign(input), 'utf8').digest('hex');
}

function signingDate(now = new Date()) {
    return now.toISOString().replace(/\.\d{3}Z$/, 'Z');
}

/**
 * Parse a server-sent event stream (the `text/event-stream` format).
 *
 * Handles CRLF, LF, and CR line endings split across chunks, comment lines (starting with `:`), multi-line `data`
 * fields (joined with a line feed), one optional space after the colon, and a final event without a trailing blank
 * line. Events without data are not dispatched, as in the HTML specification.
 *
 * @param {AsyncIterable<Uint8Array|string>|Iterable<Uint8Array|string>} chunks Stream chunks.
 * @returns {AsyncGenerator<RunEvent, void, unknown>} Parsed events.
 * @throws {TypeError} chunks is not iterable.
 */
export async function* parseServerSentEvents(chunks) {
    if (!chunks || (typeof chunks[Symbol.asyncIterator] !== 'function' && typeof chunks[Symbol.iterator] !== 'function')) {
        throw new TypeError('chunks must be an iterable or async iterable.');
    }
    const decoder = new TextDecoder('utf-8');
    let buffer = '';
    let first = true;
    let eventType = '';
    let dataLines = [];
    let lastId = null;

    function* dispatchLine(line) {
        if (line === '') {
            if (dataLines.length > 0) {
                const raw = dataLines.join('\n');
                let data = raw;
                try {
                    data = JSON.parse(raw);
                } catch {
                    data = raw;
                }
                yield { event: eventType || 'message', id: lastId, data, raw };
            }
            eventType = '';
            dataLines = [];
            return;
        }
        if (line.startsWith(':')) return;
        const colon = line.indexOf(':');
        const field = colon < 0 ? line : line.slice(0, colon);
        let value = colon < 0 ? '' : line.slice(colon + 1);
        if (value.startsWith(' ')) value = value.slice(1);
        if (field === 'event') eventType = value;
        else if (field === 'data') dataLines.push(value);
        else if (field === 'id') lastId = value.includes('\0') ? lastId : value;
    }

    function* drain(final) {
        for (;;) {
            const idx = buffer.search(/\r\n|\r|\n/);
            if (idx < 0) break;
            if (buffer[idx] === '\r' && idx === buffer.length - 1 && !final) break;
            const sepLength = buffer[idx] === '\r' && buffer[idx + 1] === '\n' ? 2 : 1;
            const line = buffer.slice(0, idx);
            buffer = buffer.slice(idx + sepLength);
            yield* dispatchLine(line);
        }
    }

    for await (const chunk of chunks) {
        let piece = typeof chunk === 'string' ? chunk : decoder.decode(chunk, { stream: true });
        if (first && piece.length > 0) {
            if (piece.charCodeAt(0) === 0xfeff) piece = piece.slice(1);
            first = false;
        }
        buffer += piece;
        yield* drain(false);
    }
    buffer += decoder.decode();
    yield* drain(true);
    if (buffer.length > 0) {
        const rest = buffer;
        buffer = '';
        yield* dispatchLine(rest);
    }
    yield* dispatchLine('');
}

/**
 * Iterate over every item of a paged list, fetching pages on demand.
 *
 * Works with any list method that takes list options and returns an {@link EnumerationResult}. A list method that
 * returns a plain array yields that array once.
 *
 * @template T
 * @param {(options: object) => Promise<EnumerationResult<T>|T[]>} listFn List method, for example `client.topics.list`.
 * @param {object} [options] List options and filters. `pageNumber` sets the first page (default 1). `pageSize` defaults to 100.
 * @param {object} [control] Iteration control.
 * @param {number} [control.maxItems] Stop after this many items.
 * @returns {AsyncGenerator<T, void, unknown>} Items across all pages.
 * @throws {TypeError} listFn is not a function.
 */
export async function* paginate(listFn, options = {}, control = {}) {
    if (typeof listFn !== 'function') throw new TypeError('listFn must be a function.');
    let pageNumber = Number.isInteger(options.pageNumber) && options.pageNumber > 0 ? options.pageNumber : 1;
    const pageSize = Number.isInteger(options.pageSize) && options.pageSize > 0 ? options.pageSize : 100;
    const maxItems = Number.isInteger(control.maxItems) && control.maxItems >= 0 ? control.maxItems : Infinity;
    let yielded = 0;
    while (yielded < maxItems) {
        const page = await listFn({ ...options, pageNumber, pageSize });
        const items = Array.isArray(page) ? page : (page?.items ?? []);
        for (const item of items) {
            if (yielded >= maxItems) return;
            yield item;
            yielded++;
        }
        if (Array.isArray(page) || items.length === 0) return;
        const totalPages = Number(page.totalPages ?? 0);
        if (pageNumber >= totalPages) return;
        pageNumber++;
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Resource groups
// ---------------------------------------------------------------------------------------------------------------------

/**
 * Base class for resource groups. Methods are bound to the instance, so `client.topics.list` can be passed around.
 */
export class ResourceGroup {
    /**
     * Create a group.
     * @param {MyMorningNewsClient} client Owning client.
     * @param {string} basePath Collection path, for example `/v1.0/api/topics`.
     */
    constructor(client, basePath) {
        /** @protected */
        this._client = client;
        /** @protected */
        this._base = basePath;
        bindAll(this);
    }

    /** @protected */
    _path(id, suffix = '') {
        return this._base + '/' + seg(id, 'id') + suffix;
    }

    /** @protected */
    _req(method, path, options) {
        return this._client.request(method, path, options);
    }
}

/**
 * A group with list, get, create, update, and remove.
 */
export class CrudResource extends ResourceGroup {
    /**
     * List a page of items.
     * @param {ListOptions & Record<string, any>} [options] List options and typed filters.
     * @returns {Promise<EnumerationResult<any>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read one item.
     * @param {string} id Identifier.
     * @returns {Promise<any>} Item.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }

    /**
     * Create an item.
     * @param {object} body Request body.
     * @returns {Promise<any>} Created item.
     */
    create(body) {
        return this._req('POST', this._base, { body: requireValue(body, 'body') });
    }

    /**
     * Update an item (fields left out are unchanged).
     * @param {string} id Identifier.
     * @param {object} body Request body.
     * @returns {Promise<any>} Updated item.
     */
    update(id, body) {
        return this._req('PUT', this._path(id), { body: requireValue(body, 'body') });
    }

    /**
     * Delete an item.
     * @param {string} id Identifier.
     * @returns {Promise<null>} Resolves when deleted.
     */
    remove(id) {
        return this._req('DELETE', this._path(id));
    }
}

/**
 * Topics: CRUD, status changes, runs, dimensions, constraints, recipients, and search providers.
 */
export class TopicsResource extends CrudResource {
    /**
     * List topics. Filters: `status`, `isOneTime`, `userId` (administrators), `mine`.
     * @param {ListOptions & { status?: string, isOneTime?: boolean, userId?: string, mine?: boolean }} [options] Options.
     * @returns {Promise<EnumerationResult<Topic>>} Page.
     */
    list(options = {}) {
        return super.list(options);
    }

    /**
     * Read a topic with its dimensions, constraints, and recipients.
     * @param {string} id Topic.
     * @returns {Promise<TopicDetail>} Detail.
     */
    get(id) {
        return super.get(id);
    }

    /**
     * Create a topic without the wizard (status Draft).
     * @param {{ name: string, description?: string, brief?: string, seedQueries?: string[] } & Record<string, any>} body Body.
     * @returns {Promise<Topic>} Topic.
     */
    create(body) {
        return super.create(body);
    }

    /**
     * Update a topic.
     * @param {string} id Topic.
     * @param {object} body Fields to change.
     * @returns {Promise<Topic>} Topic.
     */
    update(id, body) {
        return super.update(id, body);
    }

    /**
     * Pause a topic.
     * @param {string} id Topic.
     * @returns {Promise<Topic>} Topic.
     */
    pause(id) {
        return this._req('POST', this._path(id, '/pause'));
    }

    /**
     * Resume a paused or archived topic (409 for a draft without a brief).
     * @param {string} id Topic.
     * @returns {Promise<Topic>} Topic.
     */
    resume(id) {
        return this._req('POST', this._path(id, '/resume'));
    }

    /**
     * Archive a topic.
     * @param {string} id Topic.
     * @returns {Promise<Topic>} Topic.
     */
    archive(id) {
        return this._req('POST', this._path(id, '/archive'));
    }

    /**
     * Set a topic's status with the matching verb (`Paused`, `Active`, or `Archived`).
     * @param {string} id Topic.
     * @param {'Paused'|'Active'|'Archived'} status Status.
     * @returns {Promise<Topic>} Topic.
     * @throws {TypeError} The status has no verb.
     */
    setStatus(id, status) {
        if (status === 'Paused') return this.pause(id);
        if (status === 'Active') return this.resume(id);
        if (status === 'Archived') return this.archive(id);
        throw new TypeError('status must be Paused, Active, or Archived.');
    }

    /**
     * Duplicate a topic as a draft.
     * @param {string} id Topic.
     * @returns {Promise<TopicDetail>} Copy.
     */
    duplicate(id) {
        return this._req('POST', this._path(id, '/duplicate'));
    }

    /**
     * Start a refinement session for an existing topic.
     * @param {string} id Topic.
     * @returns {Promise<RefinementState>} Session state.
     */
    refine(id) {
        return this._req('POST', this._path(id, '/refine'));
    }

    /**
     * Run a topic now (202). This performs real research and model calls.
     * @param {string} id Topic.
     * @param {{ dryRun?: boolean }} [options] `dryRun` sends no email and is not counted in history.
     * @returns {Promise<Run>} Queued run.
     */
    run(id, options = {}) {
        return this._req('POST', this._path(id, '/run'), { query: { dryRun: options.dryRun ? true : undefined } });
    }

    /**
     * Queue a dry run (shorthand for `run(id, { dryRun: true })`).
     * @param {string} id Topic.
     * @returns {Promise<Run>} Queued run.
     */
    dryRun(id) {
        return this.run(id, { dryRun: true });
    }

    /**
     * Run a learning pass for a topic now.
     * @param {string} id Topic.
     * @returns {Promise<{ proposed: number, preferences: LearnedPreference[] }>} Result.
     */
    learn(id) {
        return this._req('POST', this._path(id, '/learn'));
    }

    /**
     * Suggested ranking weights from the user's ratings.
     * @param {string} id Topic.
     * @returns {Promise<object>} Recommendation.
     */
    rankingRecommendation(id) {
        return this._req('GET', this._path(id, '/ranking-recommendation'));
    }

    /**
     * List dimensions.
     * @param {string} id Topic.
     * @returns {Promise<TopicDimension[]>} Dimensions.
     */
    listDimensions(id) {
        return this._req('GET', this._path(id, '/dimensions'));
    }

    /**
     * Add a dimension.
     * @param {string} id Topic.
     * @param {{ name: string, description?: string, rationale?: string, weight?: string, includeTerms?: string[], excludeTerms?: string[], isLocked?: boolean, sortOrder?: number }} body Body.
     * @returns {Promise<TopicDimension>} Dimension.
     */
    addDimension(id, body) {
        return this._req('POST', this._path(id, '/dimensions'), { body: requireValue(body, 'body') });
    }

    /**
     * Update a dimension.
     * @param {string} id Topic.
     * @param {string} dimensionId Dimension.
     * @param {object} body Fields to change.
     * @returns {Promise<TopicDimension>} Dimension.
     */
    updateDimension(id, dimensionId, body) {
        return this._req('PUT', this._path(id, '/dimensions/' + seg(dimensionId, 'dimensionId')), { body: requireValue(body, 'body') });
    }

    /**
     * Delete a dimension.
     * @param {string} id Topic.
     * @param {string} dimensionId Dimension.
     * @returns {Promise<null>} Resolves when deleted.
     */
    removeDimension(id, dimensionId) {
        return this._req('DELETE', this._path(id, '/dimensions/' + seg(dimensionId, 'dimensionId')));
    }

    /**
     * List constraints.
     * @param {string} id Topic.
     * @param {{ kind?: string }} [options] Filter by kind.
     * @returns {Promise<TopicConstraint[]>} Constraints.
     */
    listConstraints(id, options = {}) {
        return this._req('GET', this._path(id, '/constraints'), { query: { kind: options.kind } });
    }

    /**
     * Add a constraint.
     * @param {string} id Topic.
     * @param {{ kind: string, value: string, note?: string, dimensionId?: string, isLocked?: boolean }} body Body.
     * @returns {Promise<TopicConstraint>} Constraint.
     */
    addConstraint(id, body) {
        return this._req('POST', this._path(id, '/constraints'), { body: requireValue(body, 'body') });
    }

    /**
     * Update a constraint.
     * @param {string} id Topic.
     * @param {string} constraintId Constraint.
     * @param {object} body Fields to change.
     * @returns {Promise<TopicConstraint>} Constraint.
     */
    updateConstraint(id, constraintId, body) {
        return this._req('PUT', this._path(id, '/constraints/' + seg(constraintId, 'constraintId')), { body: requireValue(body, 'body') });
    }

    /**
     * Delete a constraint.
     * @param {string} id Topic.
     * @param {string} constraintId Constraint.
     * @returns {Promise<null>} Resolves when deleted.
     */
    removeConstraint(id, constraintId) {
        return this._req('DELETE', this._path(id, '/constraints/' + seg(constraintId, 'constraintId')));
    }

    /**
     * List email recipients.
     * @param {string} id Topic.
     * @returns {Promise<TopicRecipient[]>} Recipients.
     */
    listRecipients(id) {
        return this._req('GET', this._path(id, '/recipients'));
    }

    /**
     * Add an email recipient (an existing address is updated and returned with 200).
     * @param {string} id Topic.
     * @param {{ email: string, active?: boolean }} body Body.
     * @returns {Promise<TopicRecipient>} Recipient.
     */
    addRecipient(id, body) {
        return this._req('POST', this._path(id, '/recipients'), { body: requireValue(body, 'body') });
    }

    /**
     * Remove an email recipient.
     * @param {string} id Topic.
     * @param {string} recipientId Recipient.
     * @returns {Promise<null>} Resolves when removed.
     */
    removeRecipient(id, recipientId) {
        return this._req('DELETE', this._path(id, '/recipients/' + seg(recipientId, 'recipientId')));
    }

    /**
     * Search providers a topic uses.
     * @param {string} id Topic.
     * @returns {Promise<object[]>} Providers.
     */
    getSearchProviders(id) {
        return this._req('GET', this._path(id, '/search-providers'));
    }

    /**
     * Choose a topic's search providers (an empty list uses the tenant defaults).
     * @param {string} id Topic.
     * @param {{ searchProviderId: string, enabled?: boolean, maxResults?: number }[]} providers Providers.
     * @returns {Promise<object[]>} Providers now in effect.
     */
    setSearchProviders(id, providers) {
        if (!Array.isArray(providers)) throw new TypeError('providers must be an array.');
        return this._req('PUT', this._path(id, '/search-providers'), { body: providers });
    }
}

/**
 * Refinement wizard sessions.
 */
export class RefinementResource extends ResourceGroup {
    /**
     * Start the wizard for a new topic (creates a Draft topic).
     * @param {{ seedText: string, goal?: string, depth?: string, horizon?: string, known?: string, modelEndpointId?: string }} body Body.
     * @returns {Promise<RefinementState>} State.
     */
    start(body) {
        return this._req('POST', this._base, { body: requireValue(body, 'body') });
    }

    /**
     * Alias of {@link RefinementResource#start}.
     * @param {object} body Body.
     * @returns {Promise<RefinementState>} State.
     */
    create(body) {
        return this.start(body);
    }

    /**
     * List my sessions. Filters: `status`, `topicId`.
     * @param {ListOptions & { status?: string, topicId?: string }} [options] Options.
     * @returns {Promise<EnumerationResult<object>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read the session state.
     * @param {string} id Session.
     * @returns {Promise<RefinementState>} State.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }

    /**
     * Propose dimensions (model call).
     * @param {string} id Session.
     * @param {{ guidance?: string }} [body] Optional guidance.
     * @returns {Promise<RefinementState>} State.
     */
    generateDimensions(id, body = {}) {
        return this._req('POST', this._path(id, '/dimensions'), { body });
    }

    /**
     * Ask the next round of questions (model call).
     * @param {string} id Session.
     * @param {{ guidance?: string }} [body] Optional guidance.
     * @returns {Promise<RefinementState>} State.
     */
    generateQuestions(id, body = {}) {
        return this._req('POST', this._path(id, '/questions'), { body });
    }

    /**
     * Answer or skip a question.
     * @param {string} id Session.
     * @param {string} questionId Question.
     * @param {{ answerText?: string, selectedOptionIds?: string[], skipped?: boolean }} body Answer.
     * @returns {Promise<RefinementState>} State.
     */
    answer(id, questionId, body) {
        return this._req('PUT', this._path(id, '/questions/' + seg(questionId, 'questionId')), { body: requireValue(body, 'body') });
    }

    /**
     * Draft the brief and constraints (model call).
     * @param {string} id Session.
     * @returns {Promise<RefinementState>} State.
     */
    generateContext(id) {
        return this._req('POST', this._path(id, '/context'));
    }

    /**
     * Suggest feeds and seed pages, checked with a live fetch (model call).
     * @param {string} id Session.
     * @returns {Promise<object[]>} Source checks.
     */
    suggestSources(id) {
        return this._req('POST', this._path(id, '/sources'));
    }

    /**
     * Add accepted sources to the topic.
     * @param {string} id Session.
     * @param {{ feedUrls?: string[], seedUrls?: string[] }} body Sources.
     * @returns {Promise<RefinementState>} State.
     */
    acceptSources(id, body) {
        return this._req('POST', this._path(id, '/sources/accept'), { body: requireValue(body, 'body') });
    }

    /**
     * Apply the topic (Draft becomes Active). Needs a brief and at least one dimension.
     * @param {string} id Session.
     * @returns {Promise<RefinementState>} State.
     */
    complete(id) {
        return this._req('POST', this._path(id, '/complete'));
    }

    /**
     * Abandon the session (deletes a new draft topic).
     * @param {string} id Session.
     * @returns {Promise<null>} Resolves when abandoned.
     */
    abandon(id) {
        return this._req('POST', this._path(id, '/abandon'));
    }
}

/**
 * One-time research requests.
 */
export class RequestsResource extends ResourceGroup {
    /**
     * Create a one-time request. The server plans it with a model call.
     * @param {{ requestText: string, deliverables?: string[], length?: string, runAtUtc?: string, emailRecipients?: string[], publishToFeed?: boolean, skipQuestions?: boolean, modelEndpointId?: string }} body Body.
     * @returns {Promise<ResearchRequestResponse>} Request with plan.
     */
    create(body) {
        return this._req('POST', this._base, { body: requireValue(body, 'body') });
    }

    /**
     * List my requests. Filter: `status`.
     * @param {ListOptions & { status?: string }} [options] Options.
     * @returns {Promise<EnumerationResult<object>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read a request with its plan and questions.
     * @param {string} id Request.
     * @returns {Promise<ResearchRequestResponse>} Request.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }

    /**
     * Answer a clarifying question.
     * @param {string} id Request.
     * @param {string} questionId Question.
     * @param {{ answerText?: string, selectedOptionIds?: string[], skipped?: boolean }} body Answer.
     * @returns {Promise<ResearchRequestResponse>} Request.
     */
    answer(id, questionId, body) {
        return this._req('PUT', this._path(id, '/questions/' + seg(questionId, 'questionId')), { body: requireValue(body, 'body') });
    }

    /**
     * Plan again with the answers given (model call).
     * @param {string} id Request.
     * @returns {Promise<ResearchRequestResponse>} Request.
     */
    replan(id) {
        return this._req('POST', this._path(id, '/replan'));
    }

    /**
     * Confirm: run once now, or at `runAtUtc`.
     * @param {string} id Request.
     * @param {{ runAtUtc?: string|Date }} [options] Options.
     * @returns {Promise<ResearchRequestResponse>} Request.
     */
    confirm(id, options = {}) {
        const body = options.runAtUtc ? { runAtUtc: queryValue(options.runAtUtc) } : undefined;
        return this._req('POST', this._path(id, '/confirm'), { body });
    }

    /**
     * Cancel a request that has not started.
     * @param {string} id Request.
     * @returns {Promise<ResearchRequestResponse>} Request.
     */
    cancel(id) {
        return this._req('POST', this._path(id, '/cancel'));
    }

    /**
     * Make the request a recurring topic.
     * @param {string} id Request.
     * @returns {Promise<ResearchRequestResponse>} Request.
     */
    convert(id) {
        return this._req('POST', this._path(id, '/convert'));
    }

    /**
     * Delete a request.
     * @param {string} id Request.
     * @returns {Promise<null>} Resolves when deleted.
     */
    remove(id) {
        return this._req('DELETE', this._path(id));
    }
}

/**
 * Schedules for topics and editions.
 */
export class SchedulesResource extends CrudResource {
    /**
     * List schedules. Filters: `ownerType`, `ownerId`.
     * @param {ListOptions & { ownerType?: string, ownerId?: string }} [options] Options.
     * @returns {Promise<EnumerationResult<Schedule>>} Page.
     */
    list(options = {}) {
        return super.list(options);
    }

    /**
     * Validate a schedule and preview its next runs.
     * @param {object} body Schedule fields, plus optional `previewCount` (1 to 20, default 5).
     * @returns {Promise<SchedulePreview>} Preview.
     */
    preview(body) {
        return this._req('POST', this._base + '/preview', { body: requireValue(body, 'body') });
    }
}

/**
 * Research runs. `events` streams progress as server-sent events; `wait` polls `GET /runs/{id}` instead.
 */
export class RunsResource extends ResourceGroup {
    /**
     * Whether a run status is terminal.
     * @param {string} status Status.
     * @returns {boolean} True for Completed, PartiallySucceeded, Failed, Cancelled, and Skipped.
     */
    static isTerminal(status) {
        return TERMINAL_RUN_STATUSES.includes(status);
    }

    /**
     * List runs. Filters: `topicId`, `status`, `trigger`. `sortBy: 'estimatedCostUsd'` (unpriced runs last) or
     * `sortBy: 'durationMs'` ranks runs by cost or duration.
     * @param {ListOptions & { topicId?: string, status?: string, trigger?: string }} [options] Options.
     * @returns {Promise<EnumerationResult<Run>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read a run.
     * @param {string} id Run.
     * @returns {Promise<Run>} Run.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }

    /**
     * Queue a run for a topic (same as `topics.run`). This performs real research and model calls.
     * @param {string} topicId Topic.
     * @param {{ dryRun?: boolean }} [options] Options.
     * @returns {Promise<Run>} Queued run.
     */
    start(topicId, options = {}) {
        return this._client.topics.run(topicId, options);
    }

    /**
     * What a run cost: totals, each stage, each model, and each task. Computed on request, so it is current while the run
     * is going. Amounts are US dollar estimates; unpriced calls are counted and left out of the amounts.
     * @param {string} id Run.
     * @returns {Promise<RunCostReport>} Cost report.
     */
    costs(id) {
        return this._req('GET', this._path(id, '/costs'));
    }

    /**
     * A run's stages in order.
     * @param {string} id Run.
     * @returns {Promise<RunStage[]>} Stages.
     */
    stages(id) {
        return this._req('GET', this._path(id, '/stages'));
    }

    /**
     * A run's search queries.
     * @param {string} id Run.
     * @returns {Promise<object[]>} Queries.
     */
    queries(id) {
        return this._req('GET', this._path(id, '/queries'));
    }

    /**
     * A run's search results with dispositions.
     * @param {string} id Run.
     * @param {{ disposition?: string }} [options] Filter.
     * @returns {Promise<object[]>} Results.
     */
    results(id, options = {}) {
        return this._req('GET', this._path(id, '/results'), { query: { disposition: options.disposition } });
    }

    /**
     * Retry a finished run (202). With `fromStage` set to Synthesize, ReviewPredictions, Predict, Suggest,
     * ComposeBriefing, or Notify, the same run is queued again from that stage (`resumeFromStage` is set). Otherwise,
     * or without `fromStage`, a new run of the topic is queued. 409 for a run that is not finished, 400 for an unknown
     * stage. This performs real research and model calls.
     * @param {string} id Run.
     * @param {{ fromStage?: string }} [options] Stage to resume from.
     * @returns {Promise<Run>} The queued run.
     */
    retry(id, options = {}) {
        return this._req('POST', this._path(id, '/retry'), { query: { fromStage: options.fromStage } });
    }

    /**
     * Stream a run's progress as server-sent events. The current run and all its stages come first, then `run` and
     * `stage` events on every change, `ping` every 15 seconds, and `done` with the final run (the stream then ends).
     * A `timeout` event means the stream reached its maximum length; call again to reconnect. Uses fetch streaming so
     * the bearer token or request signature is sent. Break out of the loop, or abort the signal, to stop.
     * @param {string} id Run.
     * @param {{ signal?: AbortSignal, connectTimeoutMs?: number }} [options] Abort signal, and the time allowed for the
     *   response headers (default: the client timeout).
     * @returns {AsyncGenerator<RunEvent, void, unknown>} Events.
     * @throws {MyMorningNewsError} Non-2xx response (for example 404 for an unknown run), or a response that is not an event stream.
     */
    async *events(id, options = {}) {
        const path = this._path(id, '/events');
        const { response, close } = await this._client._openStream('GET', path, options);
        try {
            const contentType = response.headers.get('content-type') ?? '';
            if (!contentType.includes('text/event-stream')) {
                const body = await response.text().catch(() => '');
                throw new MyMorningNewsError('GET ' + path + ' did not return an event stream (content type ' + (contentType || 'none') + ').', {
                    status: response.status, code: 'NotEventStream', body, method: 'GET', path, requestId: response.headers.get('x-request-id')
                });
            }
            if (!response.body) return;
            try {
                yield* parseServerSentEvents(response.body);
            } catch (err) {
                if (err instanceof MyMorningNewsError) throw err;
                const aborted = options.signal?.aborted || err?.name === 'AbortError';
                throw new MyMorningNewsError('GET ' + path + (aborted ? ' was aborted.' : ' stream failed: ' + (err?.message ?? String(err))), {
                    status: 0, code: aborted ? 'Aborted' : 'NetworkError', method: 'GET', path, cause: err
                });
            }
        } finally {
            close();
        }
    }

    /**
     * Follow a run until it finishes, reconnecting after `timeout` events.
     * @param {string} id Run.
     * @param {(event: RunEvent) => void} [onEvent] Called for every event, pings included.
     * @param {{ signal?: AbortSignal, maxReconnects?: number }} [options] Abort signal, and reconnects allowed (default 20, minimum 0).
     * @returns {Promise<Run>} The final run from the `done` event, or read with `get` when the stream ends without one.
     * @throws {MyMorningNewsError} `code` `StreamEnded` when the reconnect budget runs out before the run finishes.
     */
    async follow(id, onEvent, options = {}) {
        const maxReconnects = Math.max(0, Number.isInteger(options.maxReconnects) ? options.maxReconnects : 20);
        for (let attempt = 0; attempt <= maxReconnects; attempt++) {
            let reconnect = false;
            for await (const ev of this.events(id, { signal: options.signal })) {
                if (typeof onEvent === 'function') onEvent(ev);
                if (ev.event === 'done') {
                    if (ev.data && typeof ev.data === 'object' && ev.data.status) return ev.data;
                    return this.get(id);
                }
                if (ev.event === 'timeout') {
                    reconnect = true;
                    break;
                }
            }
            if (!reconnect) {
                const run = await this.get(id);
                if (RunsResource.isTerminal(run.status)) return run;
            }
        }
        throw new MyMorningNewsError('Run ' + id + ' did not finish within ' + maxReconnects + ' reconnects.', { status: 0, code: 'StreamEnded', method: 'GET', path: this._path(id, '/events') });
    }

    /**
     * A run's model calls, oldest first unless `sortDirection` is given.
     * @param {string} id Run.
     * @param {ListOptions} [options] Paging options.
     * @returns {Promise<EnumerationResult<LlmCall>>} Page.
     */
    llmCalls(id, options = {}) {
        return this._req('GET', this._path(id, '/llm-calls'), { query: options });
    }

    /**
     * A run's agentic tool calls (web_search, fetch_url, and MCP tools), in call order. Empty for runs without agentic
     * search.
     * @param {string} id Run.
     * @returns {Promise<RunToolCall[]>} Tool calls.
     * @throws {MyMorningNewsError} 404 when the run is not found.
     */
    toolCalls(id) {
        return this._req('GET', this._path(id, '/tool-calls'));
    }

    /**
     * Render the run digest email without sending it.
     * @param {string} id Run.
     * @returns {Promise<object>} Rendered email.
     */
    emailPreview(id) {
        return this._req('GET', this._path(id, '/email-preview'));
    }

    /**
     * Cancel a queued or running run.
     * @param {string} id Run.
     * @returns {Promise<Run>} Run.
     */
    cancel(id) {
        return this._req('POST', this._path(id, '/cancel'));
    }

    /**
     * Poll a run until it reaches a terminal status.
     * @param {string} id Run.
     * @param {object} [options] Options.
     * @param {number} [options.timeoutMs] Give up after this long. Default 600000 (10 minutes). Minimum 1.
     * @param {number} [options.intervalMs] Poll interval. Default 2000. Minimum 100.
     * @param {AbortSignal} [options.signal] Abort signal.
     * @param {(run: Run) => void} [options.onPoll] Called with each polled run.
     * @returns {Promise<Run>} The run in its terminal status.
     * @throws {MyMorningNewsError} `code` `Timeout` when the run does not finish in time.
     */
    async wait(id, options = {}) {
        requireValue(id, 'id');
        const timeoutMs = Math.max(1, Number.isFinite(options.timeoutMs) ? options.timeoutMs : 600000);
        const intervalMs = Math.max(100, Number.isFinite(options.intervalMs) ? options.intervalMs : 2000);
        const deadline = Date.now() + timeoutMs;
        for (;;) {
            const run = await this.get(id);
            if (typeof options.onPoll === 'function') options.onPoll(run);
            if (RunsResource.isTerminal(run.status)) return run;
            const remaining = deadline - Date.now();
            if (remaining <= 0) {
                throw new MyMorningNewsError('Run ' + id + ' did not finish within ' + timeoutMs + ' ms (last status ' + run.status + ').', {
                    status: 0, code: 'Timeout', body: run, method: 'GET', path: this._path(id)
                });
            }
            await delay(Math.min(intervalMs, remaining), options.signal);
        }
    }
}

/**
 * Findings (read only).
 */
export class FindingsResource extends ResourceGroup {
    /**
     * List findings. Filters: `topicId`, `runId`, `tier`, `category`, `includeDuplicates`, `includeDryRun`, `minScore`,
     * `dimensionId`, `includeBelowThreshold`, `includeHidden`, `pinned`. Hidden findings are left out unless `includeHidden`.
     * @param {ListOptions & Record<string, any>} [options] Options.
     * @returns {Promise<EnumerationResult<Finding>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read a finding with its evidence and score breakdown.
     * @param {string} id Finding.
     * @returns {Promise<FindingDetail>} Detail.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }

    /**
     * Pin or hide a finding. Hidden findings are left out of lists (unless `includeHidden`), briefings, and emails.
     * @param {string} id Finding.
     * @param {{ pinned?: boolean, hidden?: boolean }} body At least one of `pinned` and `hidden`.
     * @returns {Promise<Finding>} Finding.
     */
    update(id, body) {
        return this._req('PUT', this._path(id), { body: requireValue(body, 'body') });
    }

    /**
     * Pin or unpin a finding.
     * @param {string} id Finding.
     * @param {boolean} [pinned] Default true.
     * @returns {Promise<Finding>} Finding.
     */
    pin(id, pinned = true) {
        return this.update(id, { pinned: pinned !== false });
    }

    /**
     * Unpin a finding.
     * @param {string} id Finding.
     * @returns {Promise<Finding>} Finding.
     */
    unpin(id) {
        return this.pin(id, false);
    }

    /**
     * Hide or show a finding.
     * @param {string} id Finding.
     * @param {boolean} [hidden] Default true.
     * @returns {Promise<Finding>} Finding.
     */
    hide(id, hidden = true) {
        return this.update(id, { hidden: hidden !== false });
    }

    /**
     * Show a hidden finding.
     * @param {string} id Finding.
     * @returns {Promise<Finding>} Finding.
     */
    unhide(id) {
        return this.hide(id, false);
    }

    /**
     * Delete a finding.
     * @param {string} id Finding.
     * @returns {Promise<null>} Resolves when deleted.
     */
    remove(id) {
        return this._req('DELETE', this._path(id));
    }

    /**
     * Read one piece of evidence with its source (same as `evidence.get`).
     * @param {string} evidenceId Evidence.
     * @returns {Promise<EvidenceView>} Evidence.
     */
    getEvidence(evidenceId) {
        return this._client.evidence.get(evidenceId);
    }
}

/**
 * Evidence (read only, owner only).
 */
export class EvidenceResource extends ResourceGroup {
    /**
     * Read one piece of evidence with its source.
     * @param {string} id Evidence.
     * @returns {Promise<EvidenceView>} Evidence.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }
}

/**
 * Fetched sources (read only).
 */
export class SourcesResource extends ResourceGroup {
    /**
     * List fetched sources for my topics. Filters: `topicId`, `runId`, `search`.
     * @param {ListOptions & { topicId?: string, runId?: string }} [options] Options.
     * @returns {Promise<EnumerationResult<SourceItem>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read a fetched source with its text.
     * @param {string} id Source.
     * @returns {Promise<SourceItem>} Source.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }
}

/**
 * Predictions and their proposed resolutions.
 */
export class PredictionsResource extends ResourceGroup {
    /**
     * List predictions. Filters: `topicId`, `status`, `includeDryRun`.
     * @param {ListOptions & { topicId?: string, status?: string, includeDryRun?: boolean }} [options] Options.
     * @returns {Promise<EnumerationResult<Prediction>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read a prediction with its reviews.
     * @param {string} id Prediction.
     * @returns {Promise<{ prediction: Prediction, reviews: object[] }>} Detail.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }

    /**
     * Resolve a prediction.
     * @param {string} id Prediction.
     * @param {{ status: 'Accurate'|'PartiallyAccurate'|'Inaccurate'|'Unresolvable'|'Withdrawn', note?: string }} body Resolution.
     * @returns {Promise<Prediction>} Prediction.
     */
    resolve(id, body) {
        return this._req('POST', this._path(id, '/resolve'), { body: requireValue(body, 'body') });
    }

    /**
     * Accept a proposed resolution.
     * @param {string} reviewId Prediction review.
     * @returns {Promise<Prediction>} Prediction.
     */
    acceptReview(reviewId) {
        return this._req('POST', '/v1.0/api/prediction-reviews/' + seg(reviewId, 'reviewId') + '/accept');
    }

    /**
     * Reject a proposed resolution.
     * @param {string} reviewId Prediction review.
     * @returns {Promise<Prediction>} Prediction.
     */
    rejectReview(reviewId) {
        return this._req('POST', '/v1.0/api/prediction-reviews/' + seg(reviewId, 'reviewId') + '/reject');
    }
}

/**
 * Suggestions.
 */
export class SuggestionsResource extends ResourceGroup {
    /**
     * List suggestions. Filters: `topicId`, `status`, `priority`, `includeDryRun`.
     * @param {ListOptions & Record<string, any>} [options] Options.
     * @returns {Promise<EnumerationResult<Suggestion>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read a suggestion.
     * @param {string} id Suggestion.
     * @returns {Promise<Suggestion>} Suggestion.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }

    /**
     * Accept, snooze, dismiss, or complete a suggestion.
     * @param {string} id Suggestion.
     * @param {'New'|'Accepted'|'Snoozed'|'Dismissed'|'Done'} status Status.
     * @param {{ snoozedUntilUtc?: string|Date }} [options] A future time is required for `Snoozed`.
     * @returns {Promise<Suggestion>} Suggestion.
     */
    setStatus(id, status, options = {}) {
        const body = { status: requireValue(status, 'status') };
        if (options.snoozedUntilUtc) body.snoozedUntilUtc = queryValue(options.snoozedUntilUtc);
        return this._req('PUT', this._path(id, '/status'), { body });
    }
}

/**
 * Feedback on findings, predictions, suggestions, briefings, and preferences.
 */
export class FeedbackResource extends ResourceGroup {
    /**
     * Give feedback. Send an `action` (relevant, somewhat, not-relevant, accurate, partially, inaccurate, too-early,
     * useful, not-useful, done, snooze, apply, dismiss) or explicit ratings, with an optional `reason`.
     * @param {{ targetType: string, targetId: string, action?: string, relevance?: string, accuracy?: string, usefulness?: string, reason?: string, channel?: string }} body Feedback.
     * @returns {Promise<Feedback>} Saved feedback.
     */
    submit(body) {
        return this._req('POST', this._base, { body: requireValue(body, 'body') });
    }

    /**
     * Alias of {@link FeedbackResource#submit}.
     * @param {object} body Feedback.
     * @returns {Promise<Feedback>} Saved feedback.
     */
    create(body) {
        return this.submit(body);
    }

    /**
     * List feedback (current ratings unless `includeHistory`). Filters: `targetType`, `targetId`, `topicId`, `channel`, `includeHistory`.
     * @param {ListOptions & Record<string, any>} [options] Options.
     * @returns {Promise<EnumerationResult<Feedback>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Edit the reason of my feedback.
     * @param {string} id Feedback.
     * @param {{ reason: string|null }} body Body.
     * @returns {Promise<Feedback>} Feedback.
     */
    update(id, body) {
        return this._req('PUT', this._path(id), { body: requireValue(body, 'body') });
    }

    /**
     * Retract my feedback (the earlier rating, if any, becomes current).
     * @param {string} id Feedback.
     * @returns {Promise<null>} Resolves when retracted.
     */
    remove(id) {
        return this._req('DELETE', this._path(id));
    }
}

/**
 * Learned preferences.
 */
export class PreferencesResource extends CrudResource {
    /**
     * List preferences. Filters: `topicId`, `status`, `origin`.
     * @param {ListOptions & { topicId?: string, status?: string, origin?: string }} [options] Options.
     * @returns {Promise<EnumerationResult<LearnedPreference>>} Page.
     */
    list(options = {}) {
        return super.list(options);
    }

    /**
     * Read a preference with its supporting feedback.
     * @param {string} id Preference.
     * @returns {Promise<{ preference: LearnedPreference, support: Feedback[] }>} Detail.
     */
    get(id) {
        return super.get(id);
    }

    /**
     * Write a preference (active at once).
     * @param {{ statement: string, kind?: string, topicId?: string }} body Body.
     * @returns {Promise<LearnedPreference>} Preference.
     */
    create(body) {
        return super.create(body);
    }

    /**
     * Accept a proposed preference (409 unless Proposed).
     * @param {string} id Preference.
     * @returns {Promise<LearnedPreference>} Preference.
     */
    accept(id) {
        return this._req('POST', this._path(id, '/accept'));
    }

    /**
     * Reject a proposed preference (409 unless Proposed).
     * @param {string} id Preference.
     * @returns {Promise<LearnedPreference>} Preference.
     */
    reject(id) {
        return this._req('POST', this._path(id, '/reject'));
    }

    /**
     * Enable a preference.
     * @param {string} id Preference.
     * @returns {Promise<LearnedPreference>} Preference.
     */
    enable(id) {
        return this._req('POST', this._path(id, '/enable'));
    }

    /**
     * Disable a preference.
     * @param {string} id Preference.
     * @returns {Promise<LearnedPreference>} Preference.
     */
    disable(id) {
        return this._req('POST', this._path(id, '/disable'));
    }
}

/**
 * Editions (named briefings across topics).
 */
export class EditionsResource extends CrudResource {
    /**
     * Read an edition with its topics and schedules.
     * @param {string} id Edition.
     * @returns {Promise<EditionResponse>} Edition.
     */
    get(id) {
        return super.get(id);
    }

    /**
     * Create an edition.
     * @param {{ name: string, description?: string, briefingTemplateId?: string, audioEnabled?: boolean, emailEnabled?: boolean, enabled?: boolean, freshResearchBeforeCompose?: boolean, freshResearchLeadMinutes?: number, lookbackMode?: string, lookbackHours?: number }} body Body.
     * @returns {Promise<EditionResponse>} Edition.
     */
    create(body) {
        return super.create(body);
    }

    /**
     * Set an edition's topics in order (an empty list means all of the owner's active recurring topics).
     * @param {string} id Edition.
     * @param {string[]} topicIds Topics.
     * @returns {Promise<EditionResponse>} Edition.
     */
    setTopics(id, topicIds) {
        if (!Array.isArray(topicIds)) throw new TypeError('topicIds must be an array.');
        return this._req('PUT', this._path(id, '/topics'), { body: { topicIds } });
    }

    /**
     * Compose an edition now (202 with the queued briefing; 409 when one is already on the way).
     * @param {string} id Edition.
     * @returns {Promise<Briefing>} Queued briefing.
     */
    compose(id) {
        return this._req('POST', this._path(id, '/compose'));
    }

    /**
     * Preview an edition from the latest data without saving.
     * @param {string} id Edition.
     * @param {{ templateContent?: string }} [body] Optional unsaved template Markdown.
     * @returns {Promise<Briefing>} Preview briefing with Markdown.
     */
    preview(id, body) {
        return this._req('POST', this._path(id, '/preview'), { body: body && Object.keys(body).length > 0 ? body : undefined });
    }
}

/**
 * Briefings and their files.
 */
export class BriefingsResource extends ResourceGroup {
    /**
     * List briefings. Filters: `kind`, `editionId`, `topicId`, `runId`, `status`, `pinned`, `includeDryRun`.
     * @param {ListOptions & Record<string, any>} [options] Options.
     * @returns {Promise<EnumerationResult<Briefing>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read a briefing with its sections, items, files, and audio.
     * @param {string} id Briefing.
     * @returns {Promise<BriefingDetail>} Detail.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }

    /**
     * Delete a briefing and its files.
     * @param {string} id Briefing.
     * @returns {Promise<null>} Resolves when deleted.
     */
    remove(id) {
        return this._req('DELETE', this._path(id));
    }

    /**
     * Pin or unpin a briefing past the retention window.
     * @param {string} id Briefing.
     * @param {boolean} [pinned] Default true.
     * @returns {Promise<Briefing>} Briefing.
     */
    pin(id, pinned = true) {
        return this._req('PUT', this._path(id, '/pin'), { body: { pinned: pinned !== false } });
    }

    /**
     * Unpin a briefing.
     * @param {string} id Briefing.
     * @returns {Promise<Briefing>} Briefing.
     */
    unpin(id) {
        return this.pin(id, false);
    }

    /**
     * Where the caller stopped listening (position 0 and not completed when nothing is saved).
     * @param {string} id Briefing.
     * @returns {Promise<BriefingProgress>} Progress.
     */
    getProgress(id) {
        return this._req('GET', this._path(id, '/progress'));
    }

    /**
     * Save where the caller stopped listening.
     * @param {string} id Briefing.
     * @param {{ positionSeconds: number, durationSeconds?: number, completed?: boolean }} progress Position.
     * @returns {Promise<BriefingProgress>} Progress.
     */
    saveProgress(id, progress) {
        return this._req('PUT', this._path(id, '/progress'), { body: progress });
    }

    /**
     * Store fresh Markdown and HTML files from the saved briefing.
     * @param {string} id Briefing.
     * @returns {Promise<Briefing>} Briefing.
     */
    regenerate(id) {
        return this._req('POST', this._path(id, '/regenerate'));
    }

    /**
     * Make (or remake) audio for a ready briefing (202 with the queued episode). Uses the TTS server and may use a model for the script.
     * @param {string} id Briefing.
     * @param {{ voiceProfileId?: string }} [body] Voice.
     * @returns {Promise<MediaAsset>} Queued episode.
     */
    generateAudio(id, body = {}) {
        return this._req('POST', this._path(id, '/audio'), { body });
    }

    /**
     * Download a briefing file.
     * @param {string} id Briefing.
     * @param {{ format?: 'md'|'markdown'|'html', range?: ByteRange, signal?: AbortSignal }} [options] Format (default md) and range.
     * @returns {Promise<DownloadResult>} File.
     */
    download(id, options = {}) {
        return this._req('GET', this._path(id, '/download'), { query: { format: options.format ?? 'md' }, raw: true, range: options.range, signal: options.signal });
    }

    /**
     * Download the briefing Markdown as text.
     * @param {string} id Briefing.
     * @returns {Promise<string>} Markdown.
     */
    async markdown(id) {
        const r = await this.download(id, { format: 'md' });
        return new TextDecoder('utf-8').decode(r.data);
    }

    /**
     * Download the briefing HTML as text.
     * @param {string} id Briefing.
     * @returns {Promise<string>} HTML.
     */
    async html(id) {
        const r = await this.download(id, { format: 'html' });
        return new TextDecoder('utf-8').decode(r.data);
    }

    /**
     * Download the latest audio for a briefing.
     * @param {string} id Briefing.
     * @param {{ range?: ByteRange, signal?: AbortSignal }} [options] Range.
     * @returns {Promise<DownloadResult>} Audio.
     * @throws {MyMorningNewsError} `code` `AudioNotAvailable` (status 404) when the briefing has no finished audio.
     */
    async downloadAudio(id, options = {}) {
        const detail = await this.get(id);
        const artifactId = detail?.audio?.artifactId;
        if (!artifactId) {
            throw new MyMorningNewsError('Briefing ' + id + ' has no finished audio.', { status: 404, code: 'AudioNotAvailable', method: 'GET', path: this._path(id) });
        }
        return this._client.artifacts.content(artifactId, options);
    }
}

/**
 * Briefing templates.
 */
export class TemplatesResource extends CrudResource {
    /**
     * List templates I can use. Filters: `scope` (`all`, `shared`, `mine`), `search`, `userId` (administrators).
     * @param {ListOptions & { scope?: string }} [options] Options.
     * @returns {Promise<EnumerationResult<object>>} Page.
     */
    list(options = {}) {
        return super.list(options);
    }

    /**
     * Read a template with its current content.
     * @param {string} id Template.
     * @returns {Promise<TemplateDetail>} Detail.
     */
    get(id) {
        return super.get(id);
    }

    /**
     * Create (import) a template from Markdown.
     * @param {{ content: string, name?: string, shared?: boolean }} body Body.
     * @returns {Promise<TemplateDetail>} Detail.
     */
    create(body) {
        return super.create(body);
    }

    /**
     * Save a new template version.
     * @param {string} id Template.
     * @param {{ content: string, changeNote?: string }} body Body.
     * @returns {Promise<TemplateDetail>} Detail.
     */
    update(id, body) {
        return super.update(id, body);
    }

    /**
     * List versions.
     * @param {string} id Template.
     * @returns {Promise<object[]>} Versions.
     */
    versions(id) {
        return this._req('GET', this._path(id, '/versions'));
    }

    /**
     * Validate template Markdown without saving.
     * @param {string|{ content: string }} content Markdown, or a body.
     * @returns {Promise<object>} Validation (`valid`, `problems`, `sections`, ...).
     */
    validate(content) {
        const body = typeof content === 'string' ? { content } : requireValue(content, 'content');
        return this._req('POST', this._base + '/validate', { body });
    }

    /**
     * Restore an earlier version.
     * @param {string} id Template.
     * @param {number} version Version number.
     * @returns {Promise<TemplateDetail>} Detail.
     */
    rollback(id, version) {
        return this._req('POST', this._path(id, '/rollback'), { body: { version: requireValue(version, 'version') } });
    }

    /**
     * Reset a seeded template to the shipped default (409 for templates that did not come from a default).
     * @param {string} id Template.
     * @returns {Promise<TemplateDetail>} Detail.
     */
    reset(id) {
        return this._req('POST', this._path(id, '/reset'));
    }

    /**
     * Export the current version as a Markdown file.
     * @param {string} id Template.
     * @returns {Promise<DownloadResult & { text: string }>} File with its text.
     */
    async export(id) {
        const r = await this._req('GET', this._path(id, '/export'), { raw: true });
        return { ...r, text: new TextDecoder('utf-8').decode(r.data) };
    }
}

/**
 * Prompt templates, topic overrides, and the prompt lab.
 */
export class PromptsResource extends ResourceGroup {
    /**
     * List prompt templates. Filters: `key`, `topicId`, `isCustomized`, `tenantOnly`, `role`.
     * @param {ListOptions & Record<string, any>} [options] Options.
     * @returns {Promise<EnumerationResult<object>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read a prompt template with its content.
     * @param {string} id Prompt template.
     * @returns {Promise<PromptTemplateResponse>} Template.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }

    /**
     * Save a new version.
     * @param {string} id Prompt template.
     * @param {{ content: string, changeNote?: string }} body Body.
     * @returns {Promise<PromptTemplateResponse>} Template.
     */
    update(id, body) {
        return this._req('PUT', this._path(id), { body: requireValue(body, 'body') });
    }

    /**
     * Delete a topic override (409 for tenant prompts; reset those instead).
     * @param {string} id Prompt template.
     * @returns {Promise<null>} Resolves when deleted.
     */
    remove(id) {
        return this._req('DELETE', this._path(id));
    }

    /**
     * Version history.
     * @param {string} id Prompt template.
     * @returns {Promise<object[]>} Versions.
     */
    versions(id) {
        return this._req('GET', this._path(id, '/versions'));
    }

    /**
     * Restore an earlier version.
     * @param {string} id Prompt template.
     * @param {number} version Version number.
     * @returns {Promise<PromptTemplateResponse>} Template.
     */
    rollback(id, version) {
        return this._req('POST', this._path(id, '/rollback'), { query: { version: requireValue(version, 'version') } });
    }

    /**
     * Restore the shipped default.
     * @param {string} id Prompt template.
     * @returns {Promise<PromptTemplateResponse>} Template.
     */
    reset(id) {
        return this._req('POST', this._path(id, '/reset'));
    }

    /**
     * Create a topic override of a prompt.
     * @param {{ key: string, topicId: string, content?: string }} body Body.
     * @returns {Promise<PromptTemplateResponse>} Override.
     */
    createOverride(body) {
        return this._req('POST', this._base + '/overrides', { body: requireValue(body, 'body') });
    }

    /**
     * Run a prompt in the prompt lab (model call, recorded as a dry run).
     * @param {{ key: string, content?: string, variables?: Record<string, string>, modelEndpointId?: string, model?: string, topicId?: string }} body Body.
     * @returns {Promise<object>} Lab result.
     */
    lab(body) {
        return this._req('POST', this._base + '/lab', { body: requireValue(body, 'body') });
    }
}

/**
 * Audio episodes.
 */
export class MediaResource extends ResourceGroup {
    /**
     * List audio episodes. Filters: `briefingId`, `editionId`, `status`, `kind`.
     * @param {ListOptions & Record<string, any>} [options] Options.
     * @returns {Promise<EnumerationResult<MediaAsset>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read an episode with its chapters and file paths.
     * @param {string} id Episode.
     * @returns {Promise<{ media: MediaAsset, chapters: object[], audioPath: string|null, chaptersPath: string|null }>} Detail.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }

    /**
     * Delete an episode and its files.
     * @param {string} id Episode.
     * @returns {Promise<null>} Resolves when deleted.
     */
    remove(id) {
        return this._req('DELETE', this._path(id));
    }

    /**
     * Download an episode's audio.
     * @param {string} id Episode.
     * @param {{ range?: ByteRange, signal?: AbortSignal }} [options] Range.
     * @returns {Promise<DownloadResult>} Audio.
     * @throws {MyMorningNewsError} `code` `AudioNotAvailable` (status 404) when the episode has no audio file yet.
     */
    async download(id, options = {}) {
        const detail = await this.get(id);
        const artifactId = detail?.media?.artifactId;
        if (!artifactId) throw new MyMorningNewsError('Episode ' + id + ' has no audio file yet.', { status: 404, code: 'AudioNotAvailable', method: 'GET', path: this._path(id) });
        return this._client.artifacts.content(artifactId, options);
    }
}

/**
 * Stored files.
 */
export class ArtifactsResource extends ResourceGroup {
    /**
     * List stored files. Filters: `briefingId`, `kind`, `pinned`.
     * @param {ListOptions & Record<string, any>} [options] Options.
     * @returns {Promise<EnumerationResult<Artifact>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read file metadata.
     * @param {string} id File.
     * @returns {Promise<Artifact>} Metadata.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }

    /**
     * Stream a stored file (supports a single byte range).
     * @param {string} id File.
     * @param {{ range?: ByteRange, download?: boolean, signal?: AbortSignal }} [options] Range, and `download` to request an attachment.
     * @returns {Promise<DownloadResult>} File.
     */
    content(id, options = {}) {
        return this._req('GET', this._path(id, '/content'), { query: { download: options.download ? true : undefined }, raw: true, range: options.range, signal: options.signal });
    }

    /**
     * Download a stored file as an attachment.
     * @param {string} id File.
     * @param {{ range?: ByteRange, signal?: AbortSignal }} [options] Range.
     * @returns {Promise<DownloadResult>} File.
     */
    download(id, options = {}) {
        return this.content(id, { ...options, download: true });
    }

    /**
     * A presigned download URL when the store offers one, and the content path otherwise.
     * @param {string} id File.
     * @returns {Promise<{ url: string|null, expiresUtc: string|null, contentPath: string }>} URL.
     */
    downloadUrl(id) {
        return this._req('GET', this._path(id, '/download-url'));
    }
}

/**
 * Private podcast feed tokens.
 */
export class FeedTokensResource extends ResourceGroup {
    /**
     * List my feed tokens (hashes are redacted).
     * @returns {Promise<FeedToken[]>} Tokens.
     */
    list() {
        return this._req('GET', this._base);
    }

    /**
     * Create a private podcast feed. The secret and feed URL are returned once.
     * @param {{ name?: string, editionId?: string }} [body] Body.
     * @returns {Promise<FeedTokenCreated>} Token, secret, and URL.
     */
    create(body = {}) {
        return this._req('POST', this._base, { body });
    }

    /**
     * Revoke a token; with `purge`, delete an already revoked one.
     * @param {string} id Token.
     * @param {{ purge?: boolean }} [options] Options.
     * @returns {Promise<null>} Resolves when done.
     */
    remove(id, options = {}) {
        return this._req('DELETE', this._path(id), { query: { purge: options.purge ? true : undefined } });
    }

    /**
     * Revoke a token (same as `remove`).
     * @param {string} id Token.
     * @returns {Promise<null>} Resolves when revoked.
     */
    revoke(id) {
        return this.remove(id);
    }

    /**
     * Delete an already revoked token.
     * @param {string} id Token.
     * @returns {Promise<null>} Resolves when deleted.
     */
    purge(id) {
        return this.remove(id, { purge: true });
    }
}

/**
 * Email outbox, test email, run digest preview, and email preferences.
 */
export class EmailResource extends ResourceGroup {
    /**
     * List the outbox (bodies left out). Filters: `status`, `template`, `runId`, `briefingId`, `topicId`, `recipient` (administrators).
     * @param {ListOptions & Record<string, any>} [options] Options.
     * @returns {Promise<EnumerationResult<EmailMessage>>} Page.
     */
    listMessages(options = {}) {
        return this._req('GET', '/v1.0/api/email-messages', { query: options });
    }

    /**
     * Alias of {@link EmailResource#listMessages}.
     * @param {ListOptions & Record<string, any>} [options] Options.
     * @returns {Promise<EnumerationResult<EmailMessage>>} Page.
     */
    list(options = {}) {
        return this.listMessages(options);
    }

    /**
     * Read an email with its rendered HTML and text.
     * @param {string} id Email.
     * @returns {Promise<EmailMessage>} Email.
     */
    getMessage(id) {
        return this._req('GET', '/v1.0/api/email-messages/' + seg(id, 'id'));
    }

    /**
     * Alias of {@link EmailResource#getMessage}.
     * @param {string} id Email.
     * @returns {Promise<EmailMessage>} Email.
     */
    get(id) {
        return this.getMessage(id);
    }

    /**
     * Send an email again (202; 409 while it is on its way).
     * @param {string} id Email.
     * @returns {Promise<EmailMessage>} Email.
     */
    resend(id) {
        return this._req('POST', '/v1.0/api/email-messages/' + seg(id, 'id') + '/resend');
    }

    /**
     * Delete an email from the outbox.
     * @param {string} id Email.
     * @returns {Promise<null>} Resolves when deleted.
     */
    removeMessage(id) {
        return this._req('DELETE', '/v1.0/api/email-messages/' + seg(id, 'id'));
    }

    /**
     * Alias of {@link EmailResource#removeMessage}.
     * @param {string} id Email.
     * @returns {Promise<null>} Resolves when deleted.
     */
    remove(id) {
        return this.removeMessage(id);
    }

    /**
     * Send a test email to me (202).
     * @returns {Promise<EmailMessage>} Queued email.
     */
    test() {
        return this._req('POST', '/v1.0/api/email/test');
    }

    /**
     * Render a run digest without sending it.
     * @param {string} runId Run.
     * @returns {Promise<object>} Rendered email.
     */
    runPreview(runId) {
        return this._client.runs.emailPreview(runId);
    }

    /**
     * My email preferences.
     * @returns {Promise<EmailPreferences>} Preferences.
     */
    getPreferences() {
        return this._req('GET', '/v1.0/api/email-preferences');
    }

    /**
     * Change my email preferences.
     * @param {{ runEmails?: boolean, briefingEmails?: boolean, failureEmails?: boolean, resubscribeTopicIds?: string[], unsubscribeTopicIds?: string[] }} body Changes.
     * @returns {Promise<EmailPreferences>} Preferences.
     */
    updatePreferences(body) {
        return this._req('PUT', '/v1.0/api/email-preferences', { body: requireValue(body, 'body') });
    }
}

/**
 * Analytics reports. Options: `topicId`, `from`, `to`, `userId` (administrators), `all` (administrators).
 */
export class AnalyticsResource extends ResourceGroup {
    /**
     * Read a named report.
     * @param {'overview'|'relevance'|'accuracy'|'feedback'|'reasons'|'suggestions'|'quality'|'email'} report Report.
     * @param {{ topicId?: string, from?: string|Date, to?: string|Date, userId?: string, all?: boolean }} [options] Options.
     * @returns {Promise<object>} Report.
     */
    report(report, options = {}) {
        return this._req('GET', this._base + '/' + seg(report, 'report'), { query: options });
    }

    /**
     * Overview analytics (runs, findings, briefings, email, predictions, feedback, suggestions, model cost).
     * @param {object} [options] Options.
     * @returns {Promise<OverviewReport>} Report.
     */
    overview(options = {}) {
        return this.report('overview', options);
    }

    /**
     * Email delivery and engagement analytics.
     * @param {object} [options] Options.
     * @returns {Promise<EmailReport>} Report.
     */
    email(options = {}) {
        return this.report('email', options);
    }

    /**
     * Relevance analytics.
     * @param {object} [options] Options.
     * @returns {Promise<object>} Report.
     */
    relevance(options = {}) {
        return this.report('relevance', options);
    }

    /**
     * Prediction accuracy analytics.
     * @param {object} [options] Options.
     * @returns {Promise<object>} Report.
     */
    accuracy(options = {}) {
        return this.report('accuracy', options);
    }

    /**
     * Feedback analytics (includes `byChannelDay`).
     * @param {object} [options] Options.
     * @returns {Promise<object>} Report.
     */
    feedback(options = {}) {
        return this.report('feedback', options);
    }

    /**
     * Feedback reason analytics.
     * @param {object} [options] Options.
     * @returns {Promise<object>} Report.
     */
    reasons(options = {}) {
        return this.report('reasons', options);
    }

    /**
     * Suggestion analytics.
     * @param {object} [options] Options.
     * @returns {Promise<object>} Report.
     */
    suggestions(options = {}) {
        return this.report('suggestions', options);
    }

    /**
     * Quality analytics.
     * @param {object} [options] Options.
     * @returns {Promise<object>} Report.
     */
    quality(options = {}) {
        return this.report('quality', options);
    }
}

/**
 * Model usage and cost. Options: `from`, `to`, `groupBy`, `userId`, `topicId`, `modelEndpointId`, `task`, `includeDryRun`, `top`, `bucketMinutes`, `allTenants`.
 */
export class UsageResource extends ResourceGroup {
    /**
     * The caller's own usage (grouped by task unless `groupBy` is given).
     * @param {Record<string, any>} [options] Options.
     * @returns {Promise<object>} Report.
     */
    me(options = {}) {
        return this._req('GET', this._base + '/me', { query: options });
    }

    /**
     * Usage and cost report.
     * @param {Record<string, any>} [options] Options.
     * @returns {Promise<object>} Report.
     */
    report(options = {}) {
        return this._req('GET', this._base + '/report', { query: options });
    }

    /**
     * Usage and cost time series.
     * @param {Record<string, any>} [options] Options (`bucketMinutes` default 1440).
     * @returns {Promise<object>} Series.
     */
    timeseries(options = {}) {
        return this._req('GET', this._base + '/timeseries', { query: options });
    }
}

/**
 * Recorded model calls.
 */
export class LlmCallsResource extends ResourceGroup {
    /**
     * List model calls (payloads left out). Filters: `runId`, `runStageId`, `refinementSessionId`, `briefingId`, `modelEndpointId`, `model`, `promptKey`, `userId`, `topicId`, `success`, `isDryRun`, `task`.
     * @param {ListOptions & Record<string, any>} [options] Options.
     * @returns {Promise<EnumerationResult<LlmCall>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Read a model call with its payloads.
     * @param {string} id Call.
     * @returns {Promise<LlmCall>} Call.
     */
    get(id) {
        return this._req('GET', this._path(id));
    }
}

/**
 * Model endpoints.
 */
export class ModelEndpointsResource extends CrudResource {
    /**
     * List endpoints. Filters: `active`, `supportsChat`, `supportsEmbeddings`, `provider`.
     * @param {ListOptions & Record<string, any>} [options] Options.
     * @returns {Promise<EnumerationResult<ModelEndpoint>>} Page.
     */
    list(options = {}) {
        return super.list(options);
    }

    /**
     * Validate connectivity, tool support, and embeddings. `succeeded` is true when every check that applies to the
     * endpoint passed, and the endpoint's `lastValidationSucceeded` records it.
     * @param {string} id Endpoint.
     * @returns {Promise<{ reachable: boolean, succeeded: boolean, modelFound: boolean, supportsTools: boolean|null, embeddingDimensions: number|null, messages: string[], validatedUtc: string }>} Validation.
     */
    validate(id) {
        return this._req('POST', this._path(id, '/validate'));
    }

    /**
     * List models offered by an endpoint (502 UpstreamError when it cannot be reached).
     * @param {string} id Endpoint.
     * @returns {Promise<object[]>} Models.
     */
    models(id) {
        return this._req('GET', this._path(id, '/models'));
    }

    /**
     * Send a sample prompt (a real model call on that endpoint).
     * @param {string} id Endpoint.
     * @param {{ prompt?: string, model?: string }} [body] Body.
     * @returns {Promise<{ success: boolean, text: string|null, model: string|null, inputTokens: number, outputTokens: number, durationMs: number, error: string|null }>} Result.
     */
    test(id, body = {}) {
        return this._req('POST', this._path(id, '/test'), { body });
    }
}

/**
 * Model task role assignments.
 */
export class ModelAssignmentsResource extends ResourceGroup {
    /**
     * List assignments. Filters: `topicId`, `task`.
     * @param {ListOptions & { topicId?: string, task?: string }} [options] Options.
     * @returns {Promise<EnumerationResult<object>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Assign a task role to an endpoint (201 when created, 200 when updated).
     * @param {{ task: string, modelEndpointId: string, topicId?: string, modelOverride?: string, reasoningEffort?: string }} body Body.
     * @returns {Promise<object>} Assignment.
     */
    upsert(body) {
        return this._req('PUT', this._base, { body: requireValue(body, 'body') });
    }

    /**
     * Remove a topic assignment override (409 for tenant defaults).
     * @param {string} id Assignment.
     * @returns {Promise<null>} Resolves when removed.
     */
    remove(id) {
        return this._req('DELETE', this._path(id));
    }
}

/**
 * Search providers.
 */
export class SearchProvidersResource extends CrudResource {
    /**
     * List providers. Filter: `kind`.
     * @param {ListOptions & { kind?: string }} [options] Options.
     * @returns {Promise<EnumerationResult<SearchProvider>>} Page.
     */
    list(options = {}) {
        return super.list(options);
    }

    /**
     * Run a test query.
     * @param {string} id Provider.
     * @param {{ query?: string, feedUrls?: string[], maxResults?: number }} [body] Query.
     * @returns {Promise<object>} Search outcome.
     */
    test(id, body = {}) {
        return this._req('POST', this._path(id, '/test'), { body: Object.keys(body).length > 0 ? body : undefined });
    }

    /**
     * Fetch a page as the research pipeline would.
     * @param {string} url Page URL.
     * @returns {Promise<object>} Fetch result (text truncated to 20000 characters).
     */
    fetchTest(url) {
        return this._req('POST', '/v1.0/api/fetch/test', { body: { url: requireValue(url, 'url') } });
    }
}

/**
 * Voice profiles.
 */
export class VoicesResource extends CrudResource {
    /**
     * Speak a sample with a voice.
     * @param {string} id Voice.
     * @param {{ text?: string }} [body] Sample text.
     * @returns {Promise<DownloadResult>} Audio (audio/wav).
     */
    test(id, body = {}) {
        return this._req('POST', this._path(id, '/test'), { body, raw: true });
    }

    /**
     * The voices a topic can use: the workspace's voice profiles, then the voices the speech server offers that have no
     * profile yet, each with a readable label. Anyone who can read topics may call it. To use one for a topic, send its
     * `voiceProfileId`, or its `voice` when it has no profile yet, to `topics.update`.
     * @returns {Promise<AvailableVoicesResponse>} Voices.
     */
    available() {
        return this._req('GET', this._base + '/available');
    }

    /**
     * Speak a short sample (up to 300 characters) with a speech server voice or a voice profile. Limited to 20 previews
     * a minute per user: 409 when speech is turned off, 429 `TooManyRequests` over the limit, 502 `TtsError` when the
     * speech endpoint fails.
     * @param {{ voice?: string, voiceProfileId?: string, text?: string }} body A voice from `available()` or a voice
     *     profile, and optional text.
     * @returns {Promise<DownloadResult>} Audio (audio/wav).
     */
    preview(body) {
        return this._req('POST', this._base + '/preview', { body: requireValue(body, 'body'), raw: true });
    }
}

/**
 * Vault credentials. Secrets are never returned (reveal is available in the C# SDK only).
 */
export class VaultResource extends CrudResource {
    /**
     * List credentials. Filters: `active`, `kind`, `provider`.
     * @param {ListOptions & Record<string, any>} [options] Options.
     * @returns {Promise<EnumerationResult<VaultCredential>>} Page.
     */
    list(options = {}) {
        return super.list(options);
    }

    /**
     * Store a credential.
     * @param {{ name: string, kind: string, provider: string, secret: string, headerName?: string, username?: string, description?: string, expiresUtc?: string, active?: boolean }} body Body.
     * @returns {Promise<VaultCredential>} Credential (no secret).
     */
    create(body) {
        return super.create(body);
    }

    /**
     * Records that use a credential.
     * @param {string} id Credential.
     * @returns {Promise<object[]>} Usage.
     */
    usage(id) {
        return this._req('GET', this._path(id, '/usage'));
    }
}

/**
 * In-app notifications and notification preferences (which kinds I want by email, push, and in-app).
 */
export class NotificationsResource extends ResourceGroup {
    /**
     * List my in-app notifications, newest first.
     * @param {{ unread?: boolean, kind?: string, pageNumber?: number, pageSize?: number }} [options] Filters and paging.
     * @returns {Promise<EnumerationResult<Notification>>} Page.
     */
    list(options = {}) {
        return this._req('GET', this._base, { query: options });
    }

    /**
     * Count my unread in-app notifications.
     * @returns {Promise<{ count: number }>} Count.
     */
    unreadCount() {
        return this._req('GET', this._base + '/unread-count');
    }

    /**
     * Mark a notification as read.
     * @param {string} id Notification.
     * @returns {Promise<Notification>} Notification.
     */
    markRead(id) {
        return this._req('POST', this._path(id, '/read'));
    }

    /**
     * Mark all my notifications as read.
     * @returns {Promise<{ count: number }>} How many were marked.
     */
    markAllRead() {
        return this._req('POST', this._base + '/read-all');
    }

    /**
     * Delete a notification.
     * @param {string} id Notification.
     * @returns {Promise<null>} Resolves when deleted.
     */
    remove(id) {
        return this._req('DELETE', this._path(id));
    }

    /**
     * My notification preferences: every kind on every channel, and whether each channel can deliver.
     * @returns {Promise<NotificationPreferences>} Preferences.
     */
    getPreferences() {
        return this._req('GET', this._base + '/preferences');
    }

    /**
     * Change my notification preferences. Only the cells that are sent change.
     * @param {{ preferences?: NotificationPreferenceCell[], unsubscribeTopicIds?: string[], resubscribeTopicIds?: string[] }} body Changes.
     * @returns {Promise<NotificationPreferences>} Preferences.
     */
    updatePreferences(body) {
        return this._req('PUT', this._base + '/preferences', { body: requireValue(body, 'body') });
    }
}

/**
 * Push devices.
 */
export class DevicesResource extends ResourceGroup {
    /**
     * List my devices.
     * @returns {Promise<Device[]>} Devices.
     */
    list() {
        return this._req('GET', this._base);
    }

    /**
     * Register a device (201 new, 200 when the token is already registered).
     * @param {{ expoPushToken: string, platform: 'ios'|'android', name?: string, locale?: string }} body Body.
     * @returns {Promise<Device>} Device.
     */
    register(body) {
        return this._req('POST', this._base, { body: requireValue(body, 'body') });
    }

    /**
     * Alias of {@link DevicesResource#register}.
     * @param {object} body Body.
     * @returns {Promise<Device>} Device.
     */
    create(body) {
        return this.register(body);
    }

    /**
     * Remove a device.
     * @param {string} id Device.
     * @returns {Promise<null>} Resolves when removed.
     */
    remove(id) {
        return this._req('DELETE', this._path(id));
    }

    /**
     * Send a test notification to a device.
     * @param {string} id Device.
     * @returns {Promise<object>} Push result.
     */
    test(id) {
        return this._req('POST', this._path(id, '/test'));
    }
}

/**
 * Workspace setup: whether the workspace has a chat model with a working key and web search, live checks that prove it,
 * the model providers and sign-in methods the server offers, connecting one, and storing a model API key. Until `ready` is true, refinement sessions and research requests fail with
 * {@link SetupRequiredError} (409 `SetupRequired`).
 */
export class SetupResource extends ResourceGroup {
    /**
     * The workspace setup status. `ready` is false until every required check is `Ready`; `canConfigure` tells whether
     * the caller (a global or tenant administrator) can change the configuration.
     * @returns {Promise<SetupStatusResponse>} Status.
     */
    status() {
        return this._req('GET', this._base);
    }

    /**
     * Re-check setup with live calls (administrators only, 403 otherwise). Sends a short probe prompt to each chat
     * endpoint, a test query to each default web search provider, one embedding request, and one short speech request.
     * @param {'Model'|'Search'|'Embeddings'|'Speech'} [check] Re-check only this kind; the other checks are reported
     *     from stored results.
     * @returns {Promise<SetupStatusResponse>} Status after the checks.
     */
    verify(check) {
        return this._req('POST', this._base + '/verify', check ? { query: { check } } : undefined);
    }

    /**
     * Model providers and sign-in methods the Setup page can connect on this server (administrators only, 403
     * otherwise): hosted APIs (API key), agent CLIs in the server image (Claude Code, Codex, Gemini CLI, Mux), and
     * self-hosted servers, each with whether it is installed and allowed and its sign-in methods, recommended first.
     * @returns {Promise<SetupOptionsResponse>} Options.
     */
    options() {
        return this._req('GET', this._base + '/options');
    }

    /**
     * Connect the workspace's chat model (administrators only, 403 otherwise). Stores the secret for the chosen sign-in
     * method (an API key, a `claude setup-token` token, or a sign-in file's contents) encrypted in the vault, tests it
     * with a live call, and points every chat task at the endpoint. When the workspace already has a working model, the
     * new sign-in is tested first and the working one is kept if the test fails. `connection` on the result says what
     * happened (`succeeded`, `keptPrevious`, `message`). The secret is never returned. 400 `ValidationFailed` (with
     * `details.field`: `provider`, `authMethod`, `secret`, `model`, or `baseUrl`) for a missing provider or secret, a
     * method the provider does not accept, or an agent CLI that is not allowed or not installed; 404 when the named
     * endpoint does not exist.
     * @param {SetupConnectRequest} request Provider, sign-in method, secret, and optional model, base URL, and endpoint.
     * @returns {Promise<SetupStatusResponse>} Status after the endpoint was checked.
     */
    connect(request) {
        requireValue(request, 'request');
        requireValue(request.provider, 'provider');
        return this._req('POST', this._base + '/connect', { body: request });
    }

    /**
     * Store an API key for a model endpoint and verify it with a live call (administrators only, 403 otherwise). The key
     * is stored encrypted in the vault, attached to the endpoint, and never returned. 400 `ValidationFailed` for a missing
     * value, 404 when the endpoint does not exist.
     * @param {string} modelEndpointId Model endpoint.
     * @param {string} apiKey API key.
     * @returns {Promise<SetupStatusResponse>} Status after the key was checked.
     */
    setModelKey(modelEndpointId, apiKey) {
        requireValue(modelEndpointId, 'modelEndpointId');
        requireValue(apiKey, 'apiKey');
        return this._req('POST', this._base + '/model-key', { body: { modelEndpointId, apiKey } });
    }
}

/**
 * The caller's account: plan, promotional codes, usage meter, data export, and deletion. Also the public sign-up, email
 * verification, and password reset routes, which never send the client's credentials.
 */
export class AccountResource extends ResourceGroup {
    /**
     * My usage against my plan's limits. Needs a signed-in user, not an access key (400 otherwise).
     * @returns {Promise<UsageMeter>} Meter.
     */
    usage() {
        return this._req('GET', '/v1.0/api/account/usage');
    }

    /**
     * Download my data as a ZIP of JSON and Markdown. Needs a signed-in user.
     * @param {{ signal?: AbortSignal }} [options] Options.
     * @returns {Promise<DownloadResult>} ZIP file.
     */
    export(options = {}) {
        return this._req('GET', '/v1.0/api/account/export', { raw: true, signal: options.signal });
    }

    /**
     * Delete my account. Signs out every session and schedules the purge after the grace period (default 7 days);
     * signing in again before then cancels it. Administrators and protected accounts get 409.
     * @returns {Promise<{ purgeAfterUtc: string, message: string }>} When the purge happens.
     */
    delete() {
        return this._req('POST', '/v1.0/api/account/delete');
    }

    /**
     * Sign up for the hosted service (public). Always 202 with the same body whether or not the email has an account;
     * 404 `SignupClosed` when sign-up is off, 400 for validation problems, 429 when one address signs up too often.
     * @param {{ email: string, password: string, acceptTerms: boolean, confirmAge: boolean, displayName?: string, timeZone?: string, locale?: string, promoCode?: string, turnstileToken?: string }} body Sign-up request.
     * @returns {Promise<{ message: string, verificationRequired: boolean }>} Answer.
     */
    signup(body) {
        return this._req('POST', '/v1.0/public/signup', { body: requireValue(body, 'body'), auth: false });
    }

    /**
     * Confirm an email address with the token from the emailed link (public). 400 when the link is not valid, used, or
     * expired.
     * @param {string} token Token from the link.
     * @returns {Promise<{ verified: boolean, email: string }>} Answer.
     */
    verifyEmail(token) {
        return this._req('POST', '/v1.0/public/verify-email', { body: { token: requireValue(token, 'token') }, auth: false });
    }

    /**
     * Send a new verification link (public). Always 202.
     * @param {string} email Email.
     * @returns {Promise<{ message: string }>} Answer.
     */
    resendVerification(email) {
        return this._req('POST', '/v1.0/public/verify-email/resend', { body: { email: requireValue(email, 'email') }, auth: false });
    }

    /**
     * Email a password reset link (public). Always 202.
     * @param {string} email Email.
     * @returns {Promise<{ message: string }>} Answer.
     */
    forgotPassword(email) {
        return this._req('POST', '/v1.0/public/password/forgot', { body: { email: requireValue(email, 'email') }, auth: false });
    }

    /**
     * Set a new password with the token from a reset link (public). Signs out every session. 400 when the link is not
     * valid or the password is too weak (minimum 8 characters).
     * @param {string} token Token from the link.
     * @param {string} password New password.
     * @returns {Promise<{ reset: boolean, email: string }>} Answer.
     */
    resetPassword(token, password) {
        return this._req('POST', '/v1.0/public/password/reset', { body: { token: requireValue(token, 'token'), password: requireValue(password, 'password') }, auth: false });
    }

    /**
     * My plan and its limits.
     * @returns {Promise<EffectivePlan>} Plan.
     */
    plan() {
        return this._req('GET', '/v1.0/api/account/plan');
    }

    /**
     * Redeem a promotional code (409 `PromoCodeInvalid` with `details.reason` when it cannot be redeemed).
     * @param {string} code Code.
     * @returns {Promise<EffectivePlan>} Plan after redemption.
     */
    redeem(code) {
        return this._req('POST', '/v1.0/api/promo-codes/redeem', { body: { code: requireValue(code, 'code') } });
    }
}

/**
 * MCP server registry for agentic research: register remote MCP servers (Streamable HTTP or SSE URLs; stdio is not
 * supported), test them, discover their tools, and turn tools on or off. Tenant administrators manage it; members read it.
 */
export class McpServersResource extends CrudResource {
    /**
     * List registered MCP servers.
     * @param {ListOptions & { enabled?: boolean }} [options] Paging and the `enabled` filter.
     * @returns {Promise<EnumerationResult<McpServer>>} Page.
     */
    list(options = {}) {
        return super.list(options);
    }

    /**
     * Read a server with its tools.
     * @param {string} id Server.
     * @returns {Promise<McpServerDetail>} Server and tools.
     */
    get(id) {
        return super.get(id);
    }

    /**
     * Register a server (201). `name` and `url` are required; 400 `ValidationFailed` otherwise.
     * @param {{ name: string, url: string, transport?: 'StreamableHttp'|'Sse', vaultCredentialId?: string, headerName?: string, headerPrefix?: string, enabled?: boolean }} body Server.
     * @returns {Promise<McpServer>} Server.
     */
    create(body) {
        return super.create(body);
    }

    /**
     * Change a server; fields left out are unchanged. Send `vaultCredentialId: ''` to remove the credential.
     * @param {string} id Server.
     * @param {object} body Changes.
     * @returns {Promise<McpServer>} Server.
     */
    update(id, body) {
        return super.update(id, body);
    }

    /**
     * Connect and list tools without saving them. A failed connection is still HTTP 200 with `success: false`.
     * @param {string} id Server.
     * @returns {Promise<McpServerTestResult>} Result.
     */
    test(id) {
        return this._req('POST', this._path(id, '/test'));
    }

    /**
     * Discover the server's tools and save them. New tools start disabled, existing tools keep their setting, and tools the
     * server no longer lists are removed. 502 `McpServerUnavailable` when the server cannot be reached.
     * @param {string} id Server.
     * @returns {Promise<McpServerDetail>} Server and tools.
     */
    discover(id) {
        return this._req('POST', this._path(id, '/discover'));
    }

    /**
     * Turn a tool on or off for agentic research.
     * @param {string} id Server.
     * @param {string} toolId Tool (`mct_...`).
     * @param {boolean} enabled Whether agentic research may call it.
     * @returns {Promise<McpServerTool>} Tool.
     */
    setToolEnabled(id, toolId, enabled) {
        return this._req('PUT', this._path(id, '/tools/' + seg(toolId, 'toolId')), { body: { enabled: Boolean(enabled) } });
    }
}

/**
 * Server settings (administrators).
 */
export class SettingsResource extends ResourceGroup {
    /**
     * Read server settings (secrets masked).
     * @returns {Promise<{ settings: object, fields: object[], pendingRestart: boolean }>} Settings.
     */
    get() {
        return this._req('GET', '/v1.0/api/settings');
    }
}

/**
 * Anonymous public routes: promotional code check, podcast feeds, and artwork. No credentials are sent.
 */
export class PublicResource extends ResourceGroup {
    /**
     * Check a promotional code.
     * @param {string} code Code.
     * @returns {Promise<object>} Check result.
     */
    checkPromoCode(code) {
        return this._req('GET', '/v1.0/public/promo-codes/' + seg(code, 'code'), { auth: false });
    }

    /**
     * Read a private podcast feed (RSS 2.0).
     * @param {string} feedSecret Feed token secret.
     * @returns {Promise<string>} Feed XML.
     */
    async podcastFeed(feedSecret) {
        const r = await this._req('GET', '/v1.0/public/feeds/' + seg(feedSecret, 'feedSecret') + '/podcast.xml', { auth: false, raw: true });
        return new TextDecoder('utf-8').decode(r.data);
    }

    /**
     * Download a podcast episode.
     * @param {string} feedSecret Feed token secret.
     * @param {string} file Episode file name from the feed.
     * @param {{ range?: ByteRange, signal?: AbortSignal }} [options] Range.
     * @returns {Promise<DownloadResult>} Audio.
     */
    episode(feedSecret, file, options = {}) {
        return this._req('GET', '/v1.0/public/feeds/' + seg(feedSecret, 'feedSecret') + '/episodes/' + seg(file, 'file'), { auth: false, raw: true, range: options.range, signal: options.signal });
    }

    /**
     * Download podcast episode chapters (Podcasting 2.0 JSON).
     * @param {string} feedSecret Feed token secret.
     * @param {string} file Chapters file name from the feed.
     * @returns {Promise<object>} Chapters.
     */
    chapters(feedSecret, file) {
        return this._req('GET', '/v1.0/public/feeds/' + seg(feedSecret, 'feedSecret') + '/chapters/' + seg(file, 'file'), { auth: false });
    }

    /**
     * Download public artwork (`glyph.png` or `logo.png`).
     * @param {string} name Asset name.
     * @returns {Promise<DownloadResult>} Image.
     */
    asset(name) {
        return this._req('GET', '/v1.0/public/assets/' + seg(name, 'name'), { auth: false, raw: true });
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Client
// ---------------------------------------------------------------------------------------------------------------------

/**
 * My Morning News REST client.
 *
 * Instances are safe to share across concurrent requests. Signing in (`login`, `loginWithAccessKey`) stores the
 * session token on the instance, and `logout` clears it.
 */
export class MyMorningNewsClient {
    /**
     * Create a client.
     * @param {ClientOptions} [options] Options.
     * @throws {TypeError} No fetch implementation is available, or `accessKey` is given without `secretKey`.
     */
    constructor(options = {}) {
        const {
            baseUrl = DEFAULT_BASE_URL, token, accessKey, secretKey, apiKey, tenantId, timeoutMs, fetch: fetchImpl, headers, signedRequests = false
        } = options;
        const f = fetchImpl ?? globalThis.fetch;
        if (typeof f !== 'function') throw new TypeError('No fetch implementation is available. Use Node 20 or later, or pass options.fetch.');
        if (accessKey && !secretKey) throw new TypeError('accessKey requires secretKey.');
        if (signedRequests && !(accessKey && secretKey && tenantId)) throw new TypeError('signedRequests requires accessKey, secretKey, and tenantId.');
        /** @private */
        this._fetch = fetchImpl ? fetchImpl : (...args) => globalThis.fetch(...args);
        /** @type {string} Server base URL without a trailing slash. */
        this.baseUrl = String(baseUrl).replace(/\/+$/, '');
        /** @type {string|null} Session token. */
        this.token = token ?? null;
        /** @type {string|null} Access key. */
        this.accessKey = accessKey ?? null;
        /** @private */
        this._secretKey = secretKey ?? null;
        /** @private */
        this._apiKey = apiKey ?? null;
        /** @type {string|null} Tenant. */
        this.tenantId = tenantId ?? null;
        /** @private */
        this._timeoutMs = DEFAULT_TIMEOUT_MS;
        this.timeoutMs = timeoutMs ?? DEFAULT_TIMEOUT_MS;
        /** @private */
        this._headers = { ...(headers ?? {}) };
        /** @type {boolean} Sign requests with the access key instead of sending the secret. */
        this.signedRequests = Boolean(signedRequests);

        /** @type {TopicsResource} */
        this.topics = new TopicsResource(this, '/v1.0/api/topics');
        /** @type {RefinementResource} */
        this.refinement = new RefinementResource(this, '/v1.0/api/refinement-sessions');
        /** @type {RequestsResource} */
        this.requests = new RequestsResource(this, '/v1.0/api/requests');
        /** @type {SchedulesResource} */
        this.schedules = new SchedulesResource(this, '/v1.0/api/schedules');
        /** @type {RunsResource} */
        this.runs = new RunsResource(this, '/v1.0/api/runs');
        /** @type {FindingsResource} */
        this.findings = new FindingsResource(this, '/v1.0/api/findings');
        /** @type {EvidenceResource} */
        this.evidence = new EvidenceResource(this, '/v1.0/api/evidence');
        /** @type {SourcesResource} */
        this.sources = new SourcesResource(this, '/v1.0/api/sources');
        /** @type {PredictionsResource} */
        this.predictions = new PredictionsResource(this, '/v1.0/api/predictions');
        /** @type {SuggestionsResource} */
        this.suggestions = new SuggestionsResource(this, '/v1.0/api/suggestions');
        /** @type {FeedbackResource} */
        this.feedback = new FeedbackResource(this, '/v1.0/api/feedback');
        /** @type {PreferencesResource} */
        this.preferences = new PreferencesResource(this, '/v1.0/api/learned-preferences');
        /** @type {EditionsResource} */
        this.editions = new EditionsResource(this, '/v1.0/api/editions');
        /** @type {BriefingsResource} */
        this.briefings = new BriefingsResource(this, '/v1.0/api/briefings');
        /** @type {TemplatesResource} */
        this.templates = new TemplatesResource(this, '/v1.0/api/briefing-templates');
        /** @type {PromptsResource} */
        this.prompts = new PromptsResource(this, '/v1.0/api/prompts');
        /** @type {MediaResource} */
        this.media = new MediaResource(this, '/v1.0/api/media');
        /** @type {ArtifactsResource} */
        this.artifacts = new ArtifactsResource(this, '/v1.0/api/artifacts');
        /** @type {FeedTokensResource} */
        this.feedTokens = new FeedTokensResource(this, '/v1.0/api/feed-tokens');
        /** @type {EmailResource} */
        this.email = new EmailResource(this, '/v1.0/api/email');
        /** @type {AnalyticsResource} */
        this.analytics = new AnalyticsResource(this, '/v1.0/api/analytics');
        /** @type {UsageResource} */
        this.usage = new UsageResource(this, '/v1.0/api/usage');
        /** @type {LlmCallsResource} */
        this.llmCalls = new LlmCallsResource(this, '/v1.0/api/llm-calls');
        /** @type {ModelEndpointsResource} */
        this.modelEndpoints = new ModelEndpointsResource(this, '/v1.0/api/model-endpoints');
        /** @type {ModelAssignmentsResource} */
        this.modelAssignments = new ModelAssignmentsResource(this, '/v1.0/api/model-assignments');
        /** @type {SearchProvidersResource} */
        this.searchProviders = new SearchProvidersResource(this, '/v1.0/api/search-providers');
        /** @type {VoicesResource} */
        this.voices = new VoicesResource(this, '/v1.0/api/voices');
        /** @type {VaultResource} */
        this.vault = new VaultResource(this, '/v1.0/api/vault/credentials');
        /** @type {DevicesResource} */
        this.devices = new DevicesResource(this, '/v1.0/api/devices');
        /** @type {NotificationsResource} */
        this.notifications = new NotificationsResource(this, '/v1.0/api/notifications');
        /** @type {AccountResource} */
        this.account = new AccountResource(this, '/v1.0/api/account');
        /** @type {McpServersResource} */
        this.mcpServers = new McpServersResource(this, '/v1.0/api/mcp-servers');
        /** @type {SetupResource} */
        this.setup = new SetupResource(this, '/v1.0/api/setup');
        /** @type {SettingsResource} */
        this.settings = new SettingsResource(this, '/v1.0/api/settings');
        /** @type {PublicResource} */
        this.publicApi = new PublicResource(this, '/v1.0/public');
    }

    /**
     * Per-request timeout in milliseconds. Minimum 1.
     * @type {number}
     */
    get timeoutMs() {
        return this._timeoutMs;
    }

    set timeoutMs(value) {
        const n = Number(value);
        if (!Number.isFinite(n) || n < 1) throw new RangeError('timeoutMs must be a number of at least 1.');
        this._timeoutMs = Math.floor(n);
    }

    /**
     * Whether the client holds credentials (a token, an access key pair, or an API key).
     * @type {boolean}
     */
    get isAuthenticated() {
        return Boolean(this.token || (this.accessKey && this._secretKey) || this._apiKey);
    }

    /**
     * Sign in with email and password and store the session token.
     * @param {string} email Email.
     * @param {string} password Password.
     * @param {{ tenantId?: string }} [options] Tenant, required when the email exists in several tenants.
     * @returns {Promise<TokenResponse>} Token.
     * @throws {TenantSelectionRequiredError} The email exists in several tenants and no tenant was given.
     * @throws {EmailNotVerifiedError} The account signed up publicly and has not confirmed its email (403).
     * @throws {MyMorningNewsError} Sign-in failed (401, 429, ...).
     */
    async login(email, password, options = {}) {
        requireValue(email, 'email');
        requireValue(password, 'password');
        const headers = { 'x-email': email, 'x-password': password };
        const tenantId = options.tenantId ?? this.tenantId;
        if (tenantId) headers['x-tenant-guid'] = tenantId;
        const result = await this.request('POST', '/v1.0/token', { headers, auth: false });
        this._adopt(result);
        return result;
    }

    /**
     * Exchange an access key and secret for a session token and store it.
     * @param {string} accessKey Access key.
     * @param {string} secretKey Secret key.
     * @param {{ tenantId?: string }} [options] Tenant (defaults to the client's tenantId).
     * @returns {Promise<TokenResponse>} Token.
     * @throws {MyMorningNewsError} Sign-in failed.
     */
    async loginWithAccessKey(accessKey, secretKey, options = {}) {
        requireValue(accessKey, 'accessKey');
        requireValue(secretKey, 'secretKey');
        const headers = { 'x-access-key': accessKey, 'x-secret-key': secretKey };
        const tenantId = options.tenantId ?? this.tenantId;
        if (tenantId) headers['x-tenant-guid'] = tenantId;
        const result = await this.request('POST', '/v1.0/token', { headers, auth: false });
        this._adopt(result);
        return result;
    }

    /**
     * Revoke the current session token and forget it.
     * @returns {Promise<null>} Resolves when revoked.
     * @throws {MyMorningNewsError} The server refused (for example 400 when the client is not using a session token).
     */
    async logout() {
        const result = await this.request('DELETE', '/v1.0/token');
        this.token = null;
        return result;
    }

    /**
     * Refresh the current session token and store the new one.
     * @returns {Promise<TokenResponse>} Token.
     */
    async refreshToken() {
        const result = await this.request('POST', '/v1.0/token/refresh');
        this._adopt(result);
        return result;
    }

    /**
     * Validate the current token.
     * @returns {Promise<TokenDetails>} Details.
     */
    validateToken() {
        return this.request('GET', '/v1.0/token');
    }

    /**
     * Token and principal details, with issue and expiry times.
     * @returns {Promise<TokenDetails>} Details.
     */
    tokenDetails() {
        return this.request('GET', '/v1.0/token/details');
    }

    /**
     * Server health (anonymous). Returns the body for 503 as well, with `status` `Unhealthy`.
     * @returns {Promise<HealthResponse>} Health.
     */
    health() {
        return this.request('GET', '/v1.0/api/health', { auth: false, okStatuses: [503] });
    }

    /**
     * Public client configuration (anonymous).
     * @returns {Promise<object>} Configuration.
     */
    clientConfig() {
        return this.request('GET', '/v1.0/api/client-config', { auth: false });
    }

    /**
     * Server information and bundled tool links.
     * @returns {Promise<object>} Information.
     */
    serverInfo() {
        return this.request('GET', '/v1.0/api/server-info');
    }

    /**
     * My profile.
     * @returns {Promise<object>} Profile.
     */
    me() {
        return this.request('GET', '/v1.0/api/me');
    }

    /**
     * Update my profile.
     * @param {{ firstName?: string, lastName?: string, timeZone?: string, locale?: string }} body Changes.
     * @returns {Promise<object>} Profile.
     */
    updateMe(body) {
        return this.request('PUT', '/v1.0/api/me', { body: requireValue(body, 'body') });
    }

    /**
     * Change my password.
     * @param {string} currentPassword Current password.
     * @param {string} newPassword New password.
     * @returns {Promise<any>} Server response.
     */
    changePassword(currentPassword, newPassword) {
        return this.request('PUT', '/v1.0/api/me/password', { body: { currentPassword: requireValue(currentPassword, 'currentPassword'), newPassword: requireValue(newPassword, 'newPassword') } });
    }

    /**
     * Iterate over every item of a paged list. See {@link paginate}.
     * @template T
     * @param {(options: object) => Promise<EnumerationResult<T>|T[]>} listFn List method.
     * @param {object} [options] List options.
     * @param {{ maxItems?: number }} [control] Iteration control.
     * @returns {AsyncGenerator<T, void, unknown>} Items.
     */
    paginate(listFn, options = {}, control = {}) {
        return paginate(listFn, options, control);
    }

    /**
     * Send a request. Use this for routes the SDK does not wrap.
     * @param {string} method HTTP method.
     * @param {string} path Path starting with `/`.
     * @param {RequestOptions} [options] Options.
     * @returns {Promise<any>} Parsed JSON, text for non-JSON bodies, null for empty bodies, or a {@link DownloadResult} with `raw`.
     * @throws {MyMorningNewsError} Non-2xx response, timeout, or network failure.
     */
    async request(method, path, options = {}) {
        requireValue(method, 'method');
        requireValue(path, 'path');
        const { query, body, headers, auth = true, raw = false, range, signal, okStatuses } = options;
        const timeoutMs = options.timeoutMs !== undefined ? Math.max(1, Number(options.timeoutMs)) : this._timeoutMs;
        const { url, headers: h, payload } = this._prepare(method, path, { query, body, headers, auth, range, accept: raw ? '*/*' : 'application/json' });

        const controller = new AbortController();
        let timedOut = false;
        const timer = setTimeout(() => {
            timedOut = true;
            controller.abort();
        }, timeoutMs);
        const onAbort = () => controller.abort(signal.reason);
        if (signal) {
            if (signal.aborted) controller.abort(signal.reason);
            else signal.addEventListener('abort', onAbort, { once: true });
        }

        try {
            let response;
            try {
                response = await this._fetch(url, { method: method.toUpperCase(), headers: h, body: payload, signal: controller.signal });
            } catch (err) {
                throw this._transportError(err, timedOut, timeoutMs, method, path);
            }

            if (!response.ok && !(okStatuses && okStatuses.includes(response.status))) {
                throw await this._errorFrom(response, method, path);
            }

            try {
                if (raw) return await this._download(response);
                if (response.status === 204) return null;
                const text = await response.text();
                if (!text) return null;
                const contentType = response.headers.get('content-type') ?? '';
                if (contentType.includes('json') || /^[\s]*[{[]/.test(text)) {
                    try {
                        return JSON.parse(text);
                    } catch {
                        return text;
                    }
                }
                return text;
            } catch (err) {
                if (err instanceof MyMorningNewsError) throw err;
                throw this._transportError(err, timedOut, timeoutMs, method, path);
            }
        } finally {
            clearTimeout(timer);
            if (signal) signal.removeEventListener('abort', onAbort);
        }
    }

    /** @private */
    _prepare(method, path, { query, body, headers, auth = true, range, accept }) {
        const url = this._url(path, query);
        const h = { Accept: accept, ...this._headers };
        let payload;
        if (body !== undefined && body !== null) {
            h['Content-Type'] = 'application/json';
            payload = JSON.stringify(body);
        }
        if (auth) Object.assign(h, this._authHeaders(method, url, payload));
        if (headers) Object.assign(h, headers);
        if (range) h.Range = formatRange(range);
        return { url, headers: h, payload };
    }

    /**
     * Open a streaming response. The connect timeout covers only the response headers.
     * @private
     */
    async _openStream(method, path, options = {}) {
        const { signal } = options;
        const connectTimeoutMs = Number.isFinite(options.connectTimeoutMs) ? Math.max(1, options.connectTimeoutMs) : this._timeoutMs;
        const { url, headers, payload } = this._prepare(method, path, { query: options.query, headers: options.headers, auth: options.auth ?? true, accept: 'text/event-stream' });
        headers['Cache-Control'] = 'no-cache';
        const controller = new AbortController();
        let timedOut = false;
        const timer = setTimeout(() => {
            timedOut = true;
            controller.abort();
        }, connectTimeoutMs);
        const onAbort = () => controller.abort(signal.reason);
        if (signal) {
            if (signal.aborted) controller.abort(signal.reason);
            else signal.addEventListener('abort', onAbort, { once: true });
        }
        const close = () => {
            clearTimeout(timer);
            if (signal) signal.removeEventListener('abort', onAbort);
            controller.abort();
        };
        let response;
        try {
            response = await this._fetch(url, { method: method.toUpperCase(), headers, body: payload, signal: controller.signal });
        } catch (err) {
            close();
            throw this._transportError(err, timedOut, connectTimeoutMs, method, path);
        } finally {
            clearTimeout(timer);
        }
        if (!response.ok) {
            try {
                throw await this._errorFrom(response, method, path);
            } finally {
                close();
            }
        }
        return { response, close };
    }

    /** @private */
    _adopt(result) {
        if (result && typeof result.token === 'string') this.token = result.token;
        if (result && typeof result.tenantId === 'string') this.tenantId = result.tenantId;
    }

    /** @private */
    _authHeaders(method, url, payload) {
        const h = {};
        if (this.token) {
            h.Authorization = 'Bearer ' + this.token;
            return h;
        }
        if (this.signedRequests && this.accessKey && this._secretKey && this.tenantId) {
            const u = new URL(url);
            const date = signingDate();
            const nonce = randomBytes(16).toString('hex');
            h['x-access-key'] = this.accessKey;
            h['x-tenant-guid'] = this.tenantId;
            h['x-date'] = date;
            h['x-nonce'] = nonce;
            h['x-signature'] = signRequest({
                method, pathAndQuery: u.pathname + u.search, tenantId: this.tenantId, date, nonce, body: payload ?? '', secretKey: this._secretKey
            });
            return h;
        }
        if (this.accessKey && this._secretKey) {
            h['x-access-key'] = this.accessKey;
            h['x-secret-key'] = this._secretKey;
            if (this.tenantId) h['x-tenant-guid'] = this.tenantId;
            return h;
        }
        if (this._apiKey) {
            h['x-api-key'] = this._apiKey;
            if (this.tenantId) h['x-tenant-guid'] = this.tenantId;
        }
        return h;
    }

    /** @private */
    _url(path, query) {
        const p = path.startsWith('/') ? path : '/' + path;
        const params = new URLSearchParams();
        if (query && typeof query === 'object') {
            for (const [key, value] of Object.entries(query)) {
                if (value === undefined || value === null || value === '') continue;
                if (Array.isArray(value)) {
                    for (const v of value) if (v !== undefined && v !== null) params.append(key, queryValue(v));
                } else {
                    params.append(key, queryValue(value));
                }
            }
        }
        const qs = params.toString();
        return this.baseUrl + p + (qs ? '?' + qs : '');
    }

    /** @private */
    _transportError(err, timedOut, timeoutMs, method, path) {
        if (timedOut) {
            return new MyMorningNewsError(method.toUpperCase() + ' ' + path + ' timed out after ' + timeoutMs + ' ms.', { status: 0, code: 'Timeout', method, path, cause: err });
        }
        if (err && err.name === 'AbortError') {
            return new MyMorningNewsError(method.toUpperCase() + ' ' + path + ' was aborted.', { status: 0, code: 'Aborted', method, path, cause: err });
        }
        const detail = err && err.cause && err.cause.code ? ' (' + err.cause.code + ')' : '';
        return new MyMorningNewsError(method.toUpperCase() + ' ' + path + ' failed: ' + (err?.message ?? String(err)) + detail, { status: 0, code: 'NetworkError', method, path, cause: err });
    }

    /** @private */
    async _errorFrom(response, method, path) {
        let text = '';
        try {
            text = await response.text();
        } catch {
            text = '';
        }
        let parsed = null;
        if (text) {
            try {
                parsed = JSON.parse(text);
            } catch {
                parsed = null;
            }
        }
        const body = parsed ?? (text || null);
        const obj = parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : {};
        const requestId = obj.requestId ?? response.headers.get('x-request-id') ?? null;
        const code = obj.error ?? (response.statusText || null);
        const message = obj.message ?? (text && !parsed ? text.slice(0, 500) : 'HTTP ' + response.status + ' ' + (response.statusText || ''));
        const info = { status: response.status, code, requestId, details: obj.details ?? null, body, method, path };
        if (response.status === 409 && Array.isArray(obj.tenants)) {
            return new TenantSelectionRequiredError(message || 'Choose a tenant to sign in.', { ...info, code: obj.error ?? 'TenantSelectionRequired' }, obj.tenants);
        }
        if (response.status === 402 && obj.error === 'CapReached') return new CapReachedError(String(message).trim(), info);
        if (response.status === 403 && obj.error === 'EmailNotVerified') return new EmailNotVerifiedError(String(message).trim(), info);
        if (response.status === 409 && obj.error === 'SetupRequired') return new SetupRequiredError(String(message).trim(), info);
        return new MyMorningNewsError(String(message).trim(), info);
    }

    /** @private */
    async _download(response) {
        const data = await response.arrayBuffer();
        const contentRange = response.headers.get('content-range');
        const lengthHeader = response.headers.get('content-length');
        let totalBytes = null;
        if (contentRange) {
            const m = /\/(\d+)\s*$/.exec(contentRange);
            if (m) totalBytes = Number(m[1]);
        } else if (lengthHeader) {
            totalBytes = Number(lengthHeader);
        } else {
            totalBytes = data.byteLength;
        }
        return {
            data,
            contentType: response.headers.get('content-type'),
            fileName: parseFileName(response.headers.get('content-disposition')),
            status: response.status,
            contentLength: lengthHeader ? Number(lengthHeader) : data.byteLength,
            contentRange,
            totalBytes,
            etag: response.headers.get('etag')
        };
    }
}

export default MyMorningNewsClient;
