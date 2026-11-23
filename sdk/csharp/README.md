# My Morning News C# SDK

`MyMorningNews.Sdk` is a .NET client for the My Morning News REST API. It targets .NET 8 and .NET 10, depends only on the base class library (`System.Net.Http` and `System.Text.Json`), and has no reference to the server projects.

| Path | What it is |
|------|------------|
| `MyMorningNews.Sdk/` | The class library (`PackageId` `MyMorningNews.Sdk`, version `0.1.0-alpha`) |
| `MyMorningNews.Sdk.TestHarness/` | Console harness that exercises the SDK against a live server |
| `MyMorningNews.Sdk.sln` | Solution with both projects |
| `tools/generate_models.py` | Regenerates the data types from `src/MyMorningNews.Core` |
| `run-tests.sh`, `run-tests.bat` | Build and run the harness |

## Install

Reference the project directly:

```xml
<ProjectReference Include="path/to/sdk/csharp/MyMorningNews.Sdk/MyMorningNews.Sdk.csproj" />
```

or pack it and add the package:

```bash
dotnet pack sdk/csharp/MyMorningNews.Sdk -c Release -o ./packages
dotnet add package MyMorningNews.Sdk --version 0.1.0-alpha --source ./packages
```

## Quick start

```csharp
using MyMorningNews.Sdk;
using MyMorningNews.Sdk.Models;
using MyMorningNews.Sdk.Options;
using MyMorningNews.Sdk.Requests;

using (MyMorningNewsClient client = new MyMorningNewsClient("http://127.0.0.1:12100"))
{
    await client.LoginAsync("admin@mymorningnews", "password", "default");

    Topic topic = await client.Topics.CreateAsync(new TopicUpsertRequest
    {
        Name = "Open source databases",
        Brief = "Follow PostgreSQL and SQLite releases."
    });

    EnumerationResult<Finding> findings = await client.Findings.ListAsync(new FindingListOptions { TopicId = topic.Id, MinScore = 70 });
    BriefingDetailResponse? latest = await client.Briefings.GetLatestAsync();

    await client.Topics.DeleteAsync(topic.Id);
    await client.LogoutAsync();
}
```

The default base URL is `http://127.0.0.1:12100`. Every async method takes an optional `CancellationToken`. `Timeout` (default 5 minutes, clamped to 1 second through 1 hour) applies per request and raises `TimeoutException`.

## Authentication

| Method | How |
|--------|-----|
| Email and password | `await client.LoginAsync(email, password, tenantId)` stores the session token in `client.Token` and the tenant in `client.TenantId`. Without a tenant, a login that matches several tenants throws `TenantSelectionRequiredException` with the choices in `Tenants`. |
| Access key login | `await client.LoginWithAccessKeyAsync(accessKey, secretKey, tenantId)` exchanges a credential for a session token. |
| Access key headers | Set `AccessKey`, `SecretKey`, and `TenantId` (and leave `Token` null). Every request carries `x-access-key`, `x-secret-key`, and `x-tenant-guid`. |
| Signed requests | Also set `UseSignedRequests = true`. Each request then carries `x-access-key`, `x-tenant-guid`, `x-date`, `x-nonce`, and an `MMN1-HMAC-SHA256` `x-signature`; the secret never leaves the process. `Security/RequestSigner` exposes the canonical string, body hash, and signature for other tools. The credential needs the `SignedRequest` or `Hybrid` auth mode. |
| Bearer token | Set `client.Token` to a token you already have. |
| Administration key | Set `client.AdminApiKey` (sent as `x-api-key`; with `TenantId`, also `x-tenant-guid`). |

Precedence is administration key, then bearer token, then access key. `LogoutAsync` revokes the session and clears `Token`. `RefreshAsync`, `ValidateTokenAsync`, `GetTokenDetailsAsync`, `GetMeAsync`, `UpdateMeAsync`, and `ChangePasswordAsync` round out the session calls. `GetHealthAsync`, `GetClientConfigAsync`, and `Account.CheckPromoCodeAsync` need no credentials.

## Errors

Any non-2xx response throws `MyMorningNewsException`:

```csharp
try
{
    await client.Topics.GetAsync("top_missing");
}
catch (MyMorningNewsException ex) when (ex.StatusCode == 404)
{
    Console.WriteLine(ex.ErrorCode + " " + ex.Message + " (request " + ex.RequestId + ")");
}
```

| Property | Meaning |
|----------|---------|
| `StatusCode` | HTTP status (0 when the response body could not be read) |
| `ErrorCode` | Server code such as `NotFound`, `ValidationFailed`, `Conflict`, `Forbidden`, `PromoCodeInvalid`, `ArtifactExpired`, `UpstreamError` |
| `Message` | Server message |
| `RequestId` | Request ID from the body or the `X-Request-Id` header |
| `Details` | String map, for example field errors for `ValidationFailed`, `reason` for `PromoCodeInvalid`, and `reason`, `requiredPermission`, `requestType` for 403 |
| `ResponseBody` | Raw body |

Three codes have their own subclasses of `MyMorningNewsException`:

| Exception | When | Extra properties |
|-----------|------|------------------|
| `CapReachedException` | 402 `CapReached`: a plan limit was hit (topics, daily runs, monthly model cost, audio minutes, custom endpoints, shortest schedule) | `Key` (for example `topics.max`), `Limit` (the plan's value as text), `ResetUtc` (or null) |
| `EmailNotVerifiedException` | 403 `EmailNotVerified` from `LoginAsync`: a publicly signed-up account has not confirmed its email | none; call `Account.ResendVerificationAsync` or `Account.VerifyEmailAsync` |
| `SetupRequiredException` | 409 `SetupRequired` from refinement sessions and `Requests.CreateAsync`: the model endpoint for refinement has no key or failed its last check | `Check` (the setup check key, for example `model:mdl_abc`); read `Setup.GetStatusAsync`, and an administrator fixes it with `Setup.SetModelKeyAsync` or `Setup.VerifyAsync` |

```csharp
try
{
    await client.Topics.CreateAsync(new TopicUpsertRequest { Name = "Another topic" });
}
catch (CapReachedException cap)
{
    Console.WriteLine("Plan limit " + cap.Key + " (" + cap.Limit + ") resets " + (cap.ResetUtc?.ToString("u") ?? "never"));
}
```

Network failures surface as `HttpRequestException`, timeouts as `TimeoutException`.

## Pagination and filters

List methods take an options object derived from `ListOptions` (`PageNumber`, `PageSize` 1 to 1000, `SortBy`, `SortDirection`, `Search`, `FromUtc`, `ToUtc`) and return `EnumerationResult<T>` (`Items`, `PageNumber`, `PageSize`, `TotalCount`, `TotalPages`, `DurationMs`). Owned resources use `OwnedListOptions`, which adds `UserId` and `Mine` for administrators. Each resource has typed filters, for example `FindingListOptions.Tier`, `BriefingListOptions.Pinned`, or `RequestHistoryListOptions.StatusClass`.

```csharp
int page = 1;
EnumerationResult<Run> runs;
do
{
    runs = await client.Runs.ListAsync(new RunListOptions { TopicId = topicId, PageNumber = page++, PageSize = 100 });
    foreach (Run r in runs.Items) Console.WriteLine(r.Id + " " + r.Status);
}
while (runs.PageNumber < runs.TotalPages);
```

Analytics take `AnalyticsOptions` (`TopicId`, `FromUtc`, `ToUtc`, `UserId`, `All`); usage takes `UsageOptions`.

## Downloads and byte ranges

Binary routes return `DownloadResult` (`Data`, `Length`, `ContentType`, `FileName`, `StatusCode`, `ContentRange`, `TotalLength`, `ETag`), or write to a stream with the `DownloadToAsync` variants. Pass a `ByteRange` for partial content (HTTP 206).

```csharp
string markdown = await client.Briefings.GetMarkdownAsync(briefingId);
string html = await client.Briefings.GetHtmlAsync(briefingId);
DownloadResult? audio = await client.Briefings.DownloadAudioAsync(briefingId, new ByteRange(0, 65535));
using (FileStream fs = File.Create("briefing.md"))
{
    await client.Briefings.DownloadToAsync(briefingId, fs, "md");
}
```

## Waiting for runs

`Runs.StreamEventsAsync(runId)` follows the run's server-sent event stream (`GET /v1.0/api/runs/{id}/events`) as it happens. To wait without a stream, `Runs.WaitAsync(runId, timeout, pollInterval)` polls `GET /v1.0/api/runs/{id}` (every 2 seconds by default, for up to 30 minutes) until the status is terminal (`Completed`, `PartiallySucceeded`, `Failed`, `Cancelled`, or `Skipped`). `RunsResource.IsTerminal(status)` is public.

```csharp
Run queued = await client.Topics.RunAsync(topicId, dryRun: true);
Run done = await client.Runs.WaitAsync(queued.Id, TimeSpan.FromMinutes(15));
```

## Resource groups

Unless noted, each group offers `ListAsync(options)`, `GetAsync(id)`, `CreateAsync(request)`, `UpdateAsync(id, request)`, and `DeleteAsync(id)`.

| Property | Extra methods | Example |
|----------|---------------|---------|
| `Topics` | `PauseAsync`, `ResumeAsync`, `ArchiveAsync`, `DuplicateAsync`, `RefineAsync`, `RunAsync(id, dryRun)`, `LearnAsync`, `GetRankingRecommendationAsync`, `ListDimensionsAsync`, `CreateDimensionAsync`, `UpdateDimensionAsync`, `DeleteDimensionAsync`, `ListConstraintsAsync(id, kind)`, `CreateConstraintAsync`, `UpdateConstraintAsync`, `DeleteConstraintAsync`, `ListRecipientsAsync`, `AddRecipientAsync`, `DeleteRecipientAsync`, `GetSearchProvidersAsync`, `SetSearchProvidersAsync` | `await client.Topics.PauseAsync(id);` |
| `Refinement` | `StartAsync`, `GetAsync`, `ListAsync`, `GenerateDimensionsAsync`, `GenerateQuestionsAsync`, `AnswerAsync`, `GenerateContextAsync`, `SuggestSourcesAsync`, `AcceptSourcesAsync`, `CompleteAsync`, `AbandonAsync` | `await client.Refinement.StartAsync(new RefinementStartRequest { SeedText = "..." });` |
| `Requests` | One-time requests: `CreateAsync`, `ListAsync`, `GetAsync`, `AnswerAsync`, `ReplanAsync`, `ConfirmAsync`, `CancelAsync`, `ConvertAsync`, `DeleteAsync` | `await client.Requests.CancelAsync(id);` |
| `Schedules` | `PreviewAsync` | `await client.Schedules.PreviewAsync(new ScheduleUpsertRequest { IntervalType = IntervalTypeEnum.Cron, CronExpression = "0 7 * * 1-5" });` |
| `Runs` | `ListAsync` (SortBy `estimatedCostUsd` or `durationMs`), `GetAsync`, `GetCostsAsync` (RunCostReport: totals, stages, models, tasks), `ListStagesAsync`, `ListQueriesAsync`, `ListResultsAsync(id, disposition)`, `ListLlmCallsAsync`, `ListToolCallsAsync` (agentic tool calls in order), `GetEmailPreviewAsync`, `CancelAsync`, `WaitAsync` | `await client.Runs.ListToolCallsAsync(runId);` |
| `Findings` | `ListAsync`, `GetAsync` (with evidence), `UpdateAsync` (pin or hide), `DeleteAsync`, `GetEvidenceAsync` | `await client.Findings.UpdateAsync(id, new FindingUpdateRequest { Pinned = true });` |
| `Sources` | `ListAsync`, `GetAsync` | `await client.Sources.ListAsync(new SourceListOptions { RunId = runId });` |
| `Predictions` | `ListAsync`, `GetAsync`, `ResolveAsync`, `AcceptReviewAsync`, `RejectReviewAsync` | `await client.Predictions.ResolveAsync(id, new PredictionResolveRequest { Status = PredictionStatusEnum.Accurate });` |
| `Suggestions` | `ListAsync`, `GetAsync`, `SetStatusAsync` | `await client.Suggestions.SetStatusAsync(id, new SuggestionStatusRequest { Status = SuggestionStatusEnum.Done });` |
| `Feedback` | `CreateAsync` (alias `SubmitAsync`), `ListAsync`, `UpdateAsync` (reason), `DeleteAsync` (retract) | `await client.Feedback.SubmitAsync(new FeedbackCreateRequest { TargetType = FeedbackTargetTypeEnum.Finding, TargetId = id, Action = "not-relevant", Reason = "Old news." });` |
| `Preferences` | Learned preferences: `AcceptAsync`, `RejectAsync`, `EnableAsync`, `DisableAsync` | `await client.Preferences.DisableAsync(id);` |
| `Editions` | `SetTopicsAsync`, `ComposeAsync`, `PreviewAsync` | `Briefing preview = await client.Editions.PreviewAsync(id);` |
| `Briefings` | `ListAsync`, `GetAsync`, `GetLatestAsync`, `DeleteAsync`, `PinAsync`, `RegenerateAsync`, `GenerateAudioAsync`, `DownloadAsync(id, format, range)`, `DownloadToAsync`, `GetMarkdownAsync`, `GetHtmlAsync`, `DownloadAudioAsync` | `await client.Briefings.PinAsync(id, true);` |
| `Templates` | Briefing templates: `ListVersionsAsync`, `ValidateAsync`, `RollbackAsync(id, version)`, `ResetAsync`, `ExportAsync` | `string md = await client.Templates.ExportAsync(id);` |
| `Prompts` | `ListAsync`, `GetAsync`, `UpdateAsync`, `DeleteAsync` (topic overrides), `ListVersionsAsync`, `RollbackAsync(id, version)`, `ResetAsync`, `CreateOverrideAsync`, `LabAsync` | `await client.Prompts.ResetAsync(id);` |
| `Media` | `ListAsync`, `GetAsync`, `DeleteAsync`, `DownloadAudioAsync` | `await client.Media.DownloadAudioAsync(mediaId);` |
| `Artifacts` | `ListAsync`, `GetAsync`, `DownloadAsync(id, range, attachment)`, `DownloadToAsync`, `GetDownloadUrlAsync` | `await client.Artifacts.DownloadAsync(id, new ByteRange(0, 1023));` |
| `FeedTokens` | `ListAsync`, `CreateAsync` (secret returned once), `DeleteAsync(id, purge)` | `FeedTokenCreated feed = await client.FeedTokens.CreateAsync(null);` |
| `Email` | Outbox: `ListAsync`, `GetAsync`, `ResendAsync`, `DeleteAsync`; `SendTestAsync`, `PreviewRunAsync`, `GetPreferencesAsync`, `UpdatePreferencesAsync` | `await client.Email.UpdatePreferencesAsync(new EmailPreferencesRequest { FailureEmails = false });` |
| `Analytics` | `OverviewAsync`, `RelevanceAsync`, `AccuracyAsync`, `FeedbackAsync`, `SuggestionsAsync`, `ReasonsAsync`, `QualityAsync`, `EmailAsync` | `await client.Analytics.RelevanceAsync(new AnalyticsOptions { TopicId = id });` |
| `Usage` | `GetMineAsync`, `GetReportAsync`, `GetTimeseriesAsync` | `await client.Usage.GetReportAsync(new UsageOptions { GroupBy = UsageGroupByEnum.Model });` Model rows are keyed by endpoint and model (`Key` is `<endpoint id>\|<model>`, with `ModelEndpointId` and `Model`). Buckets carry `CacheWriteTokens`, `ReasoningTokens`, `ToolCalls`, and `P95TimeToFirstTokenMs`; a time series with `GroupBy` (not `Day`) adds `Groups` and per-bucket `Rows`, with `Top` (default 10) and the rest under `(other)`: `await client.Usage.GetTimeseriesAsync(new UsageOptions { GroupBy = UsageGroupByEnum.Task, Top = 5 });` |
| `LlmCalls` | `ListAsync`, `GetAsync` | `await client.LlmCalls.ListAsync(new LlmCallListOptions { RunStageId = stageId });` (RunId, RunStageId, and other filters) |
| `ModelEndpoints` | `ValidateAsync`, `ListModelsAsync`, `TestAsync`, `ListAssignmentsAsync`, `SetAssignmentAsync`, `DeleteAssignmentAsync` | `await client.ModelEndpoints.ValidateAsync(id);` `UpdateAsync` leaves null properties unchanged; `ClearFields` sets optional values back to null: `new ModelEndpointUpsertRequest { ClearFields = new List<string> { "temperature", "contextLength" } }` (a name that also has a value, or any other name, is a 400). |
| `SearchProviders` | `TestAsync`, `FetchTestAsync` | `await client.SearchProviders.TestAsync(id, new SearchTestRequest { Query = "postgres" });` |
| `Voices` | `TestAsync` (returns audio as `DownloadResult`); `GetAvailableAsync` (the voices a topic can use: voice profiles, then the speech server's voices with readable labels); `PreviewAsync(VoicePreviewRequest)` (a short sample of a `Voice` or a `VoiceProfileId`; 409 when speech is off, 429 over 20 a minute, 502 `TtsError`). Set a topic's voice with `TopicUpsertRequest.VoiceProfileId` (an empty string clears it) or `Voice` | `await client.Voices.TestAsync(id, new VoiceTestRequest { Text = "Hello." });` |
| `Vault` | `RevealAsync` (audited), `GetUsageAsync` | `await client.Vault.GetUsageAsync(id);` Reveal is allowed for the credential's creator and for administrators. `UpdateAsync` leaves null properties unchanged; `ClearFields` removes optional values: `new VaultCredentialUpsertRequest { ClearFields = new List<string> { "expiresUtc" } }` (also `headerName`, `username`, `description`). |
| `Devices` | `ListAsync`, `RegisterAsync`, `DeleteAsync`, `TestAsync` | `await client.Devices.RegisterAsync(new DeviceRegisterRequest { ExpoPushToken = "ExponentPushToken[...]", Platform = "ios" });` |
| `Notifications` | `ListAsync(NotificationListOptions { Unread, Kind })`, `UnreadCountAsync`, `MarkReadAsync`, `MarkAllReadAsync`, `DeleteAsync`, `GetPreferencesAsync` (every kind on every channel, and whether each channel can deliver), `UpdatePreferencesAsync` (only the cells sent change) | `await client.Notifications.UpdatePreferencesAsync(new NotificationPreferencesRequest { Preferences = new List<NotificationPreferenceCell> { new NotificationPreferenceCell { Kind = NotificationKindEnum.AudioReady, Channel = NotificationChannelEnum.Push, Enabled = true } } });` |
| `Account` | `GetPlanAsync`, `RedeemAsync(code)`, `CheckPromoCodeAsync(code)`, `GetUsageAsync` (the usage meter), `ExportAsync` and `ExportToAsync` (ZIP of my data), `DeleteAsync` (202 with `PurgeAfterUtc`; signing in again cancels it). Public, sent without credentials: `SignUpAsync`, `VerifyEmailAsync(token)`, `ResendVerificationAsync(email)`, `ForgotPasswordAsync(email)`, `ResetPasswordAsync(token, password)` | `UsageMeter meter = await client.Account.GetUsageAsync();` |
| `Setup` | Workspace setup: `GetStatusAsync` (`Ready`, `CanConfigure`, and `Checks` for the model, search, embeddings, and email), `VerifyAsync` (administrators; live probes of each chat endpoint, the default search providers, embeddings, and speech; `VerifyAsync(SetupCheckKindEnum.Speech)` probes one kind only), `GetOptionsAsync` (administrators; providers grouped as `Api`, `AgentCli`, or `SelfHosted`, whether each agent CLI is installed and allowed, and the sign-in methods, recommended first), `ConnectAsync(SetupConnectRequest)` (administrators; stores the secret for the chosen `ModelAuthMethodEnum`, such as a `claude setup-token` token or Codex's `auth.json`, points every chat task at the endpoint, and checks it live), `SetModelKeyAsync(modelEndpointId, apiKey)` (administrators; stores the key in the vault, attaches it, and checks it live). Non-administrators get 403 from all but `GetStatusAsync` | `SetupStatusResponse s = await client.Setup.GetStatusAsync();` |
| `McpServers` | MCP server registry for agentic research: `ListAsync(McpServerListOptions { Enabled })`, `GetAsync` (with tools), `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `TestAsync` (200 with `Success` false when unreachable), `DiscoverAsync` (new tools start disabled; 502 `McpServerUnavailable`), `SetToolEnabledAsync(id, toolId, enabled)` | `await client.McpServers.DiscoverAsync(id);` |
| `Admin` | Sub-groups below | `await client.Admin.Users.ListAsync("default");` |

`Admin` holds `Tenants`, `Users` (tenant ID first, plus `GetEffectivePermissionsAsync`), `Credentials` (create returns the secret once; `GetEffectivePermissionsAsync`), `Roles`, `Permissions` (no read by ID on the server), `Assignments` (`ListUserAssignmentsAsync`, `CreateUserAssignmentAsync`, `DeleteUserAssignmentAsync`, `ListCredentialScopesAsync`, `CreateCredentialScopeAsync`, `DeleteCredentialScopeAsync`), `PromoCodes` (`GenerateAsync`, `DeleteAsync(id, force)`, `ListRedemptionsAsync`, `RevokeRedemptionAsync`, `ApplyAsync`), `Plans` (`ListAsync`, `CreateAsync`, `UpdateAsync`), `PlanAssignments` (`ListAsync(tenantId, userId)`, `CreateAsync`, `DeleteAsync(id, tenantId)`), `Audit` (`DeleteOlderThanAsync`), `RequestHistory` (`DeleteManyAsync`, `GetSummaryAsync`), and `Settings` (`GetAsync`, `UpdateAsync(JsonObject)`, `RestartAsync`).

Calls that reach real models or start research (`Topics.RunAsync`, `Topics.LearnAsync`, the refinement generate steps, `Requests.CreateAsync`, `Requests.ReplanAsync`, `Requests.ConfirmAsync`, `Editions.ComposeAsync`, `Prompts.LabAsync`, `ModelEndpoints.ValidateAsync` and `TestAsync` on a real endpoint, `Setup.VerifyAsync`, `Setup.ConnectAsync`, and `Setup.SetModelKeyAsync`) are billed by the configured provider.

## Models and serialization

Data types live in `MyMorningNews.Sdk.Models`, `MyMorningNews.Sdk.Requests`, and `MyMorningNews.Sdk.Enums`. They are plain classes with public auto-properties that mirror the server DTOs. JSON is camelCase, nulls are left out when writing, and property names are case-insensitive when reading. Enums travel as names; a name this SDK does not know (from a newer server) reads as the enum's first member instead of failing. `Serialization/MyMorningNewsSerializer.Options` exposes the settings. Server settings are a `JsonObject` because their shape belongs to the server.

The generated files carry a header comment naming their source. To regenerate after the server changes:

```bash
python3 sdk/csharp/tools/generate_models.py          # write
python3 sdk/csharp/tools/generate_models.py --check  # report only
```

The generator reads public classes and enums under `src/MyMorningNews.Core`, keeps only the types the API sends or receives (its `ROOTS` list plus everything they reference), strips the server's validating setters, and replaces defaults the client cannot evaluate. It never overwrites a file without the generated header, so hand-written types such as `Models/LearnResult.cs` are safe.

## Test harness

```bash
./run-tests.sh                       # defaults: http://127.0.0.1:12100, admin@mymorningnews / password, tenant default
./run-tests.sh --verbose             # one line per step
./run-tests.sh --with-run            # also run cases that call real models or start research runs
MYMORNINGNEWS_FRAMEWORK=net8.0 ./run-tests.sh
```

Arguments: `--base-url`, `--email`, `--password`, `--tenant`, `--with-run`, `--verbose`. The environment variables `MYMORNINGNEWS_BASE_URL`, `MYMORNINGNEWS_EMAIL`, and `MYMORNINGNEWS_PASSWORD` override the defaults. On Windows use `run-tests.bat` with the same arguments.

The harness follows the SharpAI SDK test layout: `TestEnvironment` (settings), `ApiDetails` and `TestResult` (step and case records), `Helpers` (assertions and typed-error checks), `Fixtures` (shared setup), `CleanupRegistry` (deletes anything a failed case leaves behind), `Tests/TestBase`, and one class per case in `Tests/`. It prints `PASS`, `FAIL`, or `SKIP` with the case name and milliseconds, then a summary, and exits non-zero on any failure or failed cleanup. Every name it creates starts with `SDK-csharp-`, and it never prints tokens or secrets.

There are 43 cases. Four run offline (the signed-request known vector, tolerant enums and JSON shape, the setup options and connect wire shapes, list option formatting). The live cases cover health, login and logout, typed errors, topics and their children, cross-user isolation (the `demo@mymorningnews` member cannot see the administrator's topic), refinement, schedules, runs, findings, sources, predictions, suggestions, preferences and feedback, editions, briefings, templates, prompts, media and artifacts, feed tokens, email, analytics, usage, model endpoints and calls (including `Succeeded` and `LastValidationSucceeded` after a failed validation), workspace setup (status, the provider options with Claude Code listing `AccessToken` first, connect refusing an API key pasted as a setup-token token and an unknown endpoint before anything is stored, a key stored for an unreachable endpoint, 404 for an unknown endpoint, and 403 from verify, model-key, options, and connect for a plain user; the administrator's live verify is not called), search providers, voices, vault, devices, notifications (a new member's default preference matrix, a round trip, agreement with the older email preferences, and the empty in-app list), account, plan limits (a new user on the Free plan reads the usage meter, exports a ZIP, gets `CapReachedException` on a second topic, and deletes and restores the account), public sign-up (opened through the settings API and restored afterward, with `EmailNotVerifiedException`, verification, resend, and password reset; the link tokens are read from the personal tenant's outbox with an administrator-made access key), the MCP server registry (against a fake MCP server the harness runs on 127.0.0.1, so it skips when the server cannot reach the harness host, for example from a container), agentic topic fields, run tool calls, and administration (tenants, users, roles, permissions, assignments, credentials with access key login, promotional codes with limits and forced delete, plans, audit, request history, settings). Model endpoints, search providers, and voices point at an unreachable local address so nothing is billed. Cases for routes that a server may not have yet (signed requests, overview and email analytics, finding edits) skip cleanly. The three `--with-run` cases (a dry run with `WaitAsync`, the model-backed refinement steps, and a one-time request) skip by default.

(c) 2026 Lattice AI LLC
