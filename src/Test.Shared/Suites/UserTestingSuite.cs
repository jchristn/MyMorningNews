namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using MyMorningNews.Server.Services;
    using Npgsql;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Regression cases for the server findings of the 2026-11-20 simulated user testing session
    /// (user-testing/2026-11-20-full-session.md). Each case names the finding it guards.
    /// </summary>
    public static class UserTestingSuite
    {
        private const string S = "UserTesting";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "User testing 2026-11-20 regressions", new[]
            {
                Cases.Case(S, "SettingsSaveWithoutRename", "UT-001: settings save in place when the file cannot be replaced by a rename", SettingsInPlaceAsync, "fast"),
                Cases.Case(S, "LongTextIsValidation", "UT-004: over-long names are 400 ValidationFailed with the field and its limit, never 500", LongTextAsync),
                Cases.Case(S, "PlainErrorMessages", "UT-020: errors name fields and values in words, without .NET type or parameter names", PlainErrorsAsync),
                Cases.Case(S, "DuplicateNamesTheField", "UT-021: a duplicate name conflict says which field collided", DuplicateNameAsync),
                Cases.Case(S, "BlankAndDuplicateInputs", "UT-023: blank names are refused or stored as unset, and a repeated constraint is not stored twice", BlankAndDuplicateAsync),
                Cases.Case(S, "StatusCodes", "UT-029: unmatched public routes are 404 and an unreachable database is 503", StatusCodesAsync),
                Cases.Case(S, "NoEchoedHeaders", "UT-032: responses carry no request-style headers such as Host", HeadersAsync),
                Cases.Case(S, "ServerInfoForMembers", "UT-033: members see the product and version; runtime and schema details are for administrators", ServerInfoAsync),
                Cases.Case(S, "PlatformKeyMessage", "UT-011: a missing platform model key tells the user what they can do", PlatformKeyAsync, "fast"),
                Cases.Case(S, "EmailTimeZone", "UT-014 and UT-030: the run email shows the reader's local time with its zone, and counts what it shows", EmailTimeAsync),
                Cases.Case(S, "FetchReadsNothing", "UT-018: a run whose fetch reads no page ends Partially succeeded with a warning, not Failed", FetchNothingAsync)
            });
        }

        private static async Task SettingsInPlaceAsync(CancellationToken token)
        {
            if (OperatingSystem.IsWindows()) return;
            string dir = Path.Combine(Path.GetTempPath(), "mmn-settings-ro-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "mymorningnews.json");
            try
            {
                SettingsService s = new SettingsService(file, n => null);
                // A directory that cannot take a new file stands in for a single-file bind mount: the temporary file and
                // the rename both fail there, and the save must still reach the file.
                File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserExecute);
                ServerSettings incoming = Serializer.Deserialize<ServerSettings>(Serializer.Serialize(s.Current))!;
                incoming.Hosting.PublicSignupEnabled = !s.Current.Hosting.PublicSignupEnabled;
                SettingsUpdateResult result = await s.UpdateAsync(incoming, token).ConfigureAwait(false);
                Assert.True(result.Success && result.Changed.Contains("hosting.publicSignupEnabled"), "saved: " + String.Join(", ", result.Errors.Values));
                ServerSettings onDisk = Serializer.Deserialize<ServerSettings>(File.ReadAllText(file))!;
                Assert.Equal(incoming.Hosting.PublicSignupEnabled, onDisk.Hosting.PublicSignupEnabled, "written to the file");
                Assert.True(!Directory.GetFiles(dir).Any(f => f.EndsWith(".tmp", StringComparison.Ordinal)), "no temporary file left behind");

                File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                Assert.True(await FileReplacer.WriteAllTextAsync(file, "{}", token).ConfigureAwait(false), "a writable directory still gets an atomic rename");
            }
            finally
            {
                File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                Directory.Delete(dir, true);
            }
        }

        private static async Task LongTextAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            string longText = new string('x', 5000);
            ErrorResponse user = (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/users", new { email = "long@example.test", password = "abcdefgh1", firstName = longText }, token).ConfigureAwait(false))
                .Expect(400, "long first name").Json<ErrorResponse>();
            Assert.True(user.Error == "ValidationFailed" && user.Message.Contains("firstName", StringComparison.Ordinal) && user.Message.Contains("128", StringComparison.Ordinal) && user.Details!.ContainsKey("firstName"), "user: " + user.Message);
            ErrorResponse endpoint = (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = longText, provider = "Ollama", model = "m" }, token).ConfigureAwait(false)).Expect(400, "long endpoint name").Json<ErrorResponse>();
            Assert.Equal("name must be 1 to 128 characters.", endpoint.Message, "endpoint message");
            ErrorResponse feed = (await ta.Client.PostAsync("/v1.0/api/feed-tokens", new { name = longText }, token).ConfigureAwait(false)).Expect(400, "long feed token name").Json<ErrorResponse>();
            Assert.True(feed.Error == "ValidationFailed" && feed.Message.Contains("128", StringComparison.Ordinal), "feed token: " + feed.Message);
            ErrorResponse topic = (await ta.Client.PostAsync("/v1.0/api/topics", new { name = longText }, token).ConfigureAwait(false)).Expect(400, "long topic name").Json<ErrorResponse>();
            Assert.True(topic.Message.Contains("200", StringComparison.Ordinal), "topic limit matches its column: " + topic.Message);
            (await ta.Client.PostAsync("/v1.0/api/search-providers", new { name = longText, kind = "Searxng" }, token).ConfigureAwait(false)).Expect(400, "long search provider name");
            (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = new string('y', 128), provider = "Ollama", model = "m" }, token).ConfigureAwait(false)).Expect(201, "a name at the limit is fine");

            Dictionary<string, string> errors = RequestValidator.Validate(new List<TopicSearchProviderRequest> { new TopicSearchProviderRequest() });
            Assert.Equal(0, errors.Count, "a list body with no limits passes");
            Assert.Equal(1, RequestValidator.Validate(new UserUpdateRequest { LastName = longText }).Count, "the validator reads the attributes");
        }

        private static async Task PlainErrorsAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            ErrorResponse wrongEnum = (await ta.Client.PostAsync("/v1.0/api/voices", "{\"name\":\"v\",\"voice\":\"af\",\"provider\":\"NotAProvider\"}", token).ConfigureAwait(false))
                .Expect(400, "unknown enum value").Json<ErrorResponse>();
            Assert.True(wrongEnum.Message.StartsWith("provider must be one of:", StringComparison.Ordinal) && !wrongEnum.Message.Contains("System.", StringComparison.Ordinal), "enum: " + wrongEnum.Message);
            ErrorResponse wrongType = (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/users", "{\"email\":123,\"password\":\"abcdefgh1\"}", token).ConfigureAwait(false))
                .Expect(400, "number for text").Json<ErrorResponse>();
            Assert.Equal("email must be text.", wrongType.Message, "type");
            ErrorResponse syntax = (await admin.PostAsync("/v1.0/tenants", "{\"name\": nope}", token).ConfigureAwait(false)).Expect(400, "bad JSON").Json<ErrorResponse>();
            Assert.True(syntax.Message.StartsWith("The request body is not valid JSON", StringComparison.Ordinal) && !syntax.Message.Contains("Path:", StringComparison.Ordinal), "syntax: " + syntax.Message);

            ModelEndpoint e = (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "No key", provider = "Anthropic", model = "claude-opus-5-5" }, token).ConfigureAwait(false)).Expect(201, "endpoint").Json<ModelEndpoint>();
            ModelTestResponse test = (await ta.Client.PostAsync("/v1.0/api/model-endpoints/" + e.Id + "/test", new { }, token).ConfigureAwait(false)).Expect(200, "test").Json<ModelTestResponse>();
            Assert.True(!test.Success && test.Error!.Contains("needs an API key", StringComparison.Ordinal) && !test.Error.Contains("(Parameter", StringComparison.Ordinal), "no parameter name: " + test.Error);

            Assert.Equal("dimension proposal", ErrorText.Describe(typeof(DimensionProposalResult)), "type described in words");
            Assert.Equal("blocked domain", ErrorText.Words(nameof(ConstraintKindEnum.BlockedDomain)), "enum name in words");
            Assert.Equal("A required value is missing: planId.", ErrorText.Plain(new ArgumentNullException("PlanId")), "a missing value is named");
            Assert.Equal("Too long.", ErrorText.Plain(new ArgumentException("Too long.", "name")), "parameter suffix removed");
        }

        private static async Task DuplicateNameAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "Stub chat", provider = "Ollama", model = "m" }, token).ConfigureAwait(false)).Expect(201, "first");
            ErrorResponse dup = (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "Stub chat", provider = "Ollama", model = "m" }, token).ConfigureAwait(false)).Expect(409, "second").Json<ErrorResponse>();
            Assert.Equal("A model endpoint with that name already exists. Choose a different name.", dup.Message, "message names the record and field");
            Assert.True(dup.Details != null && dup.Details.ContainsKey("name"), "details name the field");
        }

        private static async Task BlankAndDuplicateAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            Tenant t = await TestFixtures.CreateTenantAsync(admin, token).ConfigureAwait(false);
            TestUser ta = await TestFixtures.CreateUserAsync(admin, t.Id, true, token).ConfigureAwait(false);
            UserResponse spaces = (await admin.PostAsync("/v1.0/tenants/" + t.Id + "/users", new { email = "spaces@example.test", password = "abcdefgh1", firstName = "   ", lastName = "  Reader " }, token).ConfigureAwait(false))
                .Expect(201, "user").Json<UserResponse>();
            Assert.True(spaces.FirstName == null && spaces.LastName == "Reader", "a name of spaces is unset and names are trimmed");
            (await ta.Client.PostAsync("/v1.0/api/model-endpoints", new { name = "   ", provider = "Ollama", model = "m" }, token).ConfigureAwait(false)).Expect(400, "blank endpoint name");
            (await admin.PostAsync("/v1.0/tenants", new { name = "  " }, token).ConfigureAwait(false)).Expect(400, "blank tenant name");

            Topic topic = (await ta.Client.PostAsync("/v1.0/api/topics", new { name = "Duplicates" }, token).ConfigureAwait(false)).Expect(201, "topic").Json<Topic>();
            (await ta.Client.PutAsync("/v1.0/api/topics/" + topic.Id, new { name = "   " }, token).ConfigureAwait(false)).Expect(400, "blank topic name on update");
            TopicConstraint first = (await ta.Client.PostAsync("/v1.0/api/topics/" + topic.Id + "/constraints", new { kind = "BlockedDomain", value = "spam.example" }, token).ConfigureAwait(false)).Expect(201, "first").Json<TopicConstraint>();
            TopicConstraint again = (await ta.Client.PostAsync("/v1.0/api/topics/" + topic.Id + "/constraints", new { kind = "BlockedDomain", value = " SPAM.example " }, token).ConfigureAwait(false)).Expect(200, "again").Json<TopicConstraint>();
            Assert.Equal(first.Id, again.Id, "the existing constraint is returned");
            TopicConstraint other = (await ta.Client.PostAsync("/v1.0/api/topics/" + topic.Id + "/constraints", new { kind = "BlockedDomain", value = "other.example" }, token).ConfigureAwait(false)).Expect(201, "another").Json<TopicConstraint>();
            (await ta.Client.PutAsync("/v1.0/api/topics/" + topic.Id + "/constraints/" + other.Id, new { value = "spam.example" }, token).ConfigureAwait(false)).Expect(409, "an update cannot make a duplicate");
            List<TopicConstraint> all = (await ta.Client.GetAsync("/v1.0/api/topics/" + topic.Id + "/constraints?kind=BlockedDomain", token).ConfigureAwait(false)).Json<EnumerationResult<TopicConstraint>>().Objects;
            Assert.Equal(2, all.Count, "two blocked domains stored");
        }

        private static async Task StatusCodesAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            ErrorResponse feed = (await anon.GetAsync("/v1.0/public/feeds//podcast.xml", token).ConfigureAwait(false)).Expect(404, "empty feed token").Json<ErrorResponse>();
            Assert.Equal("RouteNotFound", feed.Error, "route not found, not unauthorized");
            (await anon.GetAsync("/v1.0/public/nothing-here", token).ConfigureAwait(false)).Expect(404, "unknown public path");
            (await anon.GetAsync("/.well-known/unknown.json", token).ConfigureAwait(false)).Expect(404, "unknown well-known path");
            (await anon.GetAsync("/v1.0/api/topics", token).ConfigureAwait(false)).Expect(401, "authenticated routes still ask for credentials");

            Assert.True(DatabaseErrorDescriber.IsUnavailable(new NpgsqlException("Failed to connect to 127.0.0.1:12102")), "a connection failure is unavailable");
            Assert.True(!DatabaseErrorDescriber.IsUnavailable(new InvalidOperationException("x")), "other errors are not");
            using (NpgsqlConnection bad = new NpgsqlConnection("Host=127.0.0.1;Port=1;Username=postgres;Password=x;Timeout=2"))
            {
                bool unavailable = false;
                try
                {
                    await bad.OpenAsync(token).ConfigureAwait(false);
                }
                catch (Exception ex) when (DatabaseErrorDescriber.IsUnavailable(ex) || (ex.InnerException != null && DatabaseErrorDescriber.IsUnavailable(ex.InnerException)))
                {
                    unavailable = true;
                }

                Assert.True(unavailable, "a database that refuses connections maps to 503 DatabaseUnavailable");
            }
        }

        private static async Task HeadersAsync(CancellationToken token)
        {
            TestApiClient anon = await SharedTestServer.AnonymousClientAsync(token).ConfigureAwait(false);
            TestResponse health = (await anon.GetAsync("/v1.0/api/health", token).ConfigureAwait(false)).Expect(200, "health");
            foreach (string h in new[] { "Host", "Accept", "Accept-Language", "Accept-Charset" })
                Assert.True(!health.Headers.ContainsKey(h), "no " + h + " response header");
            Assert.True(health.Headers.ContainsKey("X-Request-Id"), "real response headers remain");
        }

        private static async Task ServerInfoAsync(CancellationToken token)
        {
            TestApiClient admin = await SharedTestServer.AdminClientAsync(token).ConfigureAwait(false);
            TestUser member = await TestFixtures.CreateUserAsync(admin, "default", false, token).ConfigureAwait(false);
            TestResponse mine = (await member.Client.GetAsync("/v1.0/api/server-info", token).ConfigureAwait(false)).Expect(200, "member");
            ServerInfoResponse m = mine.Json<ServerInfoResponse>();
            Assert.True(m.Product == "My Morning News" && !String.IsNullOrEmpty(m.Version), "product and version for members");
            Assert.True(m.OperatingSystem == null && m.RuntimeVersion == null && m.SchemaVersion == null && m.DatabaseType == null && m.ExternalServices.Count == 0, "no runtime, schema, or operator links for members: " + mine.Body);
            ServerInfoResponse a = (await admin.GetAsync("/v1.0/api/server-info", token).ConfigureAwait(false)).Expect(200, "admin").Json<ServerInfoResponse>();
            Assert.True(!String.IsNullOrEmpty(a.OperatingSystem) && !String.IsNullOrEmpty(a.RuntimeVersion) && a.SchemaVersion >= 1 && a.ExternalServices.Count > 0, "administrators see everything");
        }

        private static async Task PlatformKeyAsync(CancellationToken token)
        {
            string variable = MyMorningNews.Core.Constants.EnvironmentPrefix + "ANTHROPIC_API_KEY";
            string? before = Environment.GetEnvironmentVariable(variable);
            try
            {
                Environment.SetEnvironmentVariable(variable, null);
                VaultService vault = (await SharedTestServer.GetServerAsync(token).ConfigureAwait(false)).Context.Vault;
                string message = String.Empty;
                try
                {
                    await vault.ResolveAsync("any-tenant", VaultService.PlatformAnthropicCredentialId, token).ConfigureAwait(false);
                }
                catch (VaultSecretUnavailableException ex)
                {
                    message = ex.Message;
                }

                Assert.True(message.Contains("Models", StringComparison.Ordinal) && message.Contains("API key", StringComparison.Ordinal) && !message.Contains("platform", StringComparison.OrdinalIgnoreCase), "user-facing: " + message);
            }
            finally
            {
                Environment.SetEnvironmentVariable(variable, before);
            }
        }

        private static async Task EmailTimeAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipeline", token).ConfigureAwait(false);
            (await f.User.Client.PutAsync("/v1.0/api/me", new { timeZone = "Pacific/Honolulu" }, token).ConfigureAwait(false)).Expect(200, "profile time zone");
            (await f.User.Client.PutAsync("/v1.0/api/topics/" + f.Topic.Id, new { maxEmailFindings = 1 }, token).ConfigureAwait(false)).Expect(200, "one finding per email");
            (await f.User.Client.PostAsync("/v1.0/api/topics/" + f.Topic.Id + "/recipients", new { email = f.User.User.Email }, token).ConfigureAwait(false)).Expect(201, "owner as recipient");
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            EmailMessage m = (await f.Server.Context.Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = run.TenantId, RunId = run.Id, PageSize = 10 }, token).ConfigureAwait(false)).Objects.Single();
            TimeZoneInfo zone = TimeZoneHelper.TryFind("Pacific/Honolulu")!;
            string local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(run.CompletedUtc!.Value, DateTimeKind.Utc), zone).ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(m.RenderedText!.Contains(local + " HST", StringComparison.Ordinal), "local time with its zone (" + local + " HST) in:\n" + m.RenderedText.Substring(0, Math.Min(400, m.RenderedText.Length)));
            Assert.True(run.NewFindingCount == 2 && m.Subject.Contains(": 1 new (", StringComparison.Ordinal), "the subject counts what the email shows: " + m.Subject);
            Assert.True(m.RenderedText.Contains("shows the top 1 of 2 new findings", StringComparison.Ordinal), "the email says it shows 1 of 2");

            Assert.Equal("HST", TimeZoneHelper.Abbreviation(zone, DateTime.UtcNow), "Honolulu");
            Assert.Equal("PDT", TimeZoneHelper.Abbreviation(TimeZoneHelper.TryFind("America/Los_Angeles")!, new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc)), "Los Angeles in summer");
            Assert.Equal("UTC+5:30", TimeZoneHelper.Abbreviation(TimeZoneHelper.TryFind("Asia/Colombo")!, DateTime.UtcNow), "an unlisted zone shows its offset");
        }

        private static async Task FetchNothingAsync(CancellationToken token)
        {
            PipelineFixture f = await PipelineSuite.FixtureAsync("Track residential heat pump rebates.", "/pipelinemissing", token).ConfigureAwait(false);
            Run run = await PipelineSuite.RunAsync(f, token).ConfigureAwait(false);
            Assert.True(run.Status == RunStatusEnum.PartiallySucceeded, "partially succeeded, not failed: " + run.Status + " " + run.ErrorMessage);
            Assert.True(run.Summary != null && run.Summary.Contains("Nothing new could be read", StringComparison.Ordinal), "the warning is in the summary: " + run.Summary);
            List<RunStage> stages = (await f.User.Client.GetAsync("/v1.0/api/runs/" + run.Id + "/stages?pageSize=1000", token).ConfigureAwait(false)).Json<EnumerationResult<RunStage>>().Objects;
            RunStage fetch = stages.Single(s => s.Stage == "Fetch");
            Assert.True(fetch.Status == StageStatusEnum.Succeeded && fetch.ItemsOut == 0 && fetch.ErrorMessage!.Contains("could be fetched", StringComparison.Ordinal), "fetch stage succeeded with a note");
            Assert.True(stages.Any(s => s.Stage == "Notify"), "the later stages still ran");
        }
    }
}
