namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Benchmark.Client;
    using MyMorningNews.Benchmark.Datasets;
    using MyMorningNews.Benchmark.Metrics;
    using MyMorningNews.Benchmark.Reporting;
    using MyMorningNews.Benchmark.Stub;

    /// <summary>
    /// Creates and reuses workspaces over the public API: a tenant named deterministically from the dataset, corpus, arm,
    /// and a hash of everything that affects stored state; a tenant administrator who owns the topic; the stub model
    /// assigned to every task (checked after assignment and after every run); a SearXNG provider pointed at the stub;
    /// and the topic with its dimensions and constraints. Runs rounds one at a time so the stub's ledger can attribute
    /// every model request. Not thread safe.
    /// </summary>
    public class Provisioner
    {
        #region Public-Members

        /// <summary>
        /// Total recorded model cost of the runs this provisioner made, in USD.
        /// </summary>
        public decimal SpentUsd { get; private set; } = 0;

        #endregion

        #region Private-Members

        private readonly BenchmarkContext _Ctx;
        private readonly BenchStubServer _Stub;
        private readonly DatasetIndex _Index;
        private readonly string _Dataset;
        private readonly string _DatasetSha;
        private readonly Dictionary<string, MyMorningNewsClient> _Sessions = new Dictionary<string, MyMorningNewsClient>(StringComparer.Ordinal);

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="ctx">Context.</param>
        /// <param name="stub">Running stub.</param>
        /// <param name="dataset">Dataset name.</param>
        /// <param name="datasetSha">Dataset hash.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">The stub serves no dataset.</exception>
        public Provisioner(BenchmarkContext ctx, BenchStubServer stub, string dataset, string datasetSha)
        {
            _Ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _Stub = stub ?? throw new ArgumentNullException(nameof(stub));
            _Index = stub.Index() ?? throw new InvalidOperationException("The stub must serve the dataset.");
            _Dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));
            _DatasetSha = datasetSha ?? throw new ArgumentNullException(nameof(datasetSha));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// The workspace name and hash for a corpus and arm.
        /// </summary>
        /// <param name="corpus">Corpus.</param>
        /// <param name="arm">Arm.</param>
        /// <param name="suffix">Optional suffix (load and resilience workspaces), or empty.</param>
        /// <returns>Name.</returns>
        public string NameFor(BenchmarkCorpus corpus, ProductArm arm, string suffix)
        {
            ArgumentNullException.ThrowIfNull(corpus);
            ArgumentNullException.ThrowIfNull(arm);
            return "bench-" + _Dataset + "-" + corpus.Id + "-" + arm.Name + (suffix.Length > 0 ? "-" + suffix : String.Empty) + "-" + HashFor(corpus, arm, suffix);
        }

        /// <summary>
        /// Load a saved workspace when its tenant and every round's run still exist.
        /// </summary>
        /// <param name="name">Workspace name.</param>
        /// <param name="rounds">Rounds expected.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Workspace, or null.</returns>
        public async Task<Workspace?> TryReuseAsync(string name, int rounds, CancellationToken token)
        {
            string path = FilePath(name);
            if (!File.Exists(path)) return null;
            Workspace ws;
            try
            {
                ws = Json.Deserialize<Workspace>(File.ReadAllText(path));
            }
            catch (JsonException)
            {
                return null;
            }

            if (ws.Runs.Count < rounds) return null;
            try
            {
                MyMorningNewsClient user = await SessionAsync(ws, token).ConfigureAwait(false);
                foreach (string runId in ws.Runs.Values)
                {
                    ApiResult r = await user.GetAsync("/v1.0/api/runs/" + runId, token).ConfigureAwait(false);
                    if (!r.Success || !r.Json<RunDto>().Finished) return null;
                }
            }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException || ex is JsonException)
            {
                return null;
            }

            return ws;
        }

        /// <summary>
        /// Create a workspace, deleting any earlier tenant with the same name.
        /// </summary>
        /// <param name="corpus">Corpus.</param>
        /// <param name="arm">Arm.</param>
        /// <param name="suffix">Optional suffix, or empty.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Workspace.</returns>
        /// <exception cref="ModelGuardException">A task is not assigned to an allowed endpoint.</exception>
        public async Task<Workspace> CreateAsync(BenchmarkCorpus corpus, ProductArm arm, string suffix, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(corpus);
            ArgumentNullException.ThrowIfNull(arm);
            string hash = HashFor(corpus, arm, suffix);
            string name = NameFor(corpus, arm, suffix);
            foreach (IdDto old in (await _Ctx.Admin.ListTenantsAsync(token).ConfigureAwait(false)).Where(t => t.Name == name))
                await _Ctx.Admin.DeleteTenantAsync(old.Id, token).ConfigureAwait(false);

            Workspace ws = new Workspace
            {
                Name = name,
                Hash = hash,
                Dataset = _Dataset,
                Corpus = corpus.Id,
                Arm = arm.Name,
                Tag = name,
                Email = "reader-" + hash + "@bench.example.test",
                Password = "bench-" + StableHash.Sha256(name + "|password").Substring(0, 20)
            };

            IdDto tenant = await _Ctx.Admin.CreateTenantAsync(name, token).ConfigureAwait(false);
            ws.TenantId = tenant.Id;
            IdDto user = await _Ctx.Admin.CreateUserAsync(tenant.Id, ws.Email, ws.Password, token).ConfigureAwait(false);
            ws.UserId = user.Id;
            MyMorningNewsClient client = await SessionAsync(ws, token).ConfigureAwait(false);

            IdDto stubEndpoint = await client.CreateModelEndpointAsync(new
            {
                name = "bench stub",
                provider = "OpenAiCompatible",
                endpoint = _Stub.ModelUrl,
                model = BenchStubServer.ChatModel,
                embeddingModel = BenchStubServer.EmbeddingModel,
                supportsChat = true,
                supportsEmbeddings = true,
                timeoutMs = 60000,
                inputCostPerMTok = 0.0,
                outputCostPerMTok = 0.0
            }, token).ConfigureAwait(false);
            ws.AllowedEndpointIds.Add(stubEndpoint.Id);
            string chatEndpoint = stubEndpoint.Id;
            if (_Ctx.Paid.Enabled)
            {
                IdDto paid = await client.CreateModelEndpointAsync(new
                {
                    name = "bench paid " + _Ctx.Paid.Model,
                    provider = _Ctx.Paid.Provider,
                    endpoint = _Ctx.Paid.Endpoint,
                    apiKey = _Ctx.Paid.ApiKey,
                    model = _Ctx.Paid.Model,
                    supportsChat = true,
                    supportsEmbeddings = false,
                    reasoningEffort = _Ctx.Paid.ReasoningEffort,
                    inputCostPerMTok = _Ctx.Paid.InputCostPerMTok,
                    outputCostPerMTok = _Ctx.Paid.OutputCostPerMTok
                }, token).ConfigureAwait(false);
                ws.AllowedEndpointIds.Add(paid.Id);
                chatEndpoint = paid.Id;
                await CapSpendAsync(ws, token).ConfigureAwait(false);
            }

            foreach (string task in MyMorningNewsClient.ModelTasks)
                await client.AssignModelAsync(task, task == "Embedding" ? stubEndpoint.Id : chatEndpoint, token).ConfigureAwait(false);
            await VerifyAssignmentsAsync(client, ws, token).ConfigureAwait(false);

            IdDto provider = await client.CreateSearchProviderAsync("bench stub search", _Index.SearchEndpoint(corpus.Id, 1), token).ConfigureAwait(false);
            ws.ProviderId = provider.Id;
            Dictionary<string, object?> topic = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                { "name", corpus.Topic.Name },
                { "brief", corpus.Topic.Brief },
                { "status", "Active" },
                { "noveltyPreference", corpus.Topic.NoveltyPreference },
                { "historyMode", "PreviousIssue" },
                { "seedQueries", corpus.Topic.SeedQueries },
                { "emailOnRun", true },
                { "emailWhenEmpty", true }
            };
            foreach (KeyValuePair<string, double> w in arm.Weights) topic[w.Key] = w.Value;
            TopicDto created = await client.CreateTopicAsync(topic, token).ConfigureAwait(false);
            ws.TopicId = created.Id;
            ws.TopicName = created.Name;
            int order = 0;
            foreach (BenchmarkDimension d in corpus.Topic.Dimensions)
                await client.AddDimensionAsync(created.Id, new { name = d.Name, weight = d.Weight, description = d.Description, sortOrder = order++ }, token).ConfigureAwait(false);
            foreach (BenchmarkConstraint k in corpus.Topic.Constraints) await client.AddConstraintAsync(created.Id, k.Kind, k.Value, token).ConfigureAwait(false);
            await client.SetTopicProviderAsync(created.Id, provider.Id, token).ConfigureAwait(false);
            Save(ws);
            return ws;
        }

        /// <summary>
        /// Run one round: point search at the round, queue a run, wait for it, check every model call stayed on an allowed
        /// endpoint, record the ledger entries, and give the round's feedback.
        /// </summary>
        /// <param name="ws">Workspace.</param>
        /// <param name="corpus">Corpus.</param>
        /// <param name="round">Round.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The finished run.</returns>
        /// <exception cref="SpendCapReachedException">A paid run would exceed the cap.</exception>
        /// <exception cref="ModelGuardException">A model call left the allowed endpoints.</exception>
        public async Task<RunDto> RunRoundAsync(Workspace ws, BenchmarkCorpus corpus, int round, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(ws);
            ArgumentNullException.ThrowIfNull(corpus);
            if (_Ctx.Paid.Enabled && SpentUsd >= _Ctx.Paid.MaxSpendUsd)
                throw new SpendCapReachedException("The spend cap of $" + _Ctx.Paid.MaxSpendUsd.ToString("0.00", CultureInfo.InvariantCulture) + " was reached ($" + SpentUsd.ToString("0.0000", CultureInfo.InvariantCulture) + " spent); no further runs were started.");
            MyMorningNewsClient client = await SessionAsync(ws, token).ConfigureAwait(false);
            await client.UpdateSearchProviderAsync(ws.ProviderId, _Index.SearchEndpoint(corpus.Id, round), token).ConfigureAwait(false);
            int chatsBefore = _Stub.Ledger.Chats.Count;
            int quotesBefore = _Stub.Ledger.Quotes.Count;
            _Stub.Ledger.CurrentTag = ws.Tag;
            RunDto run;
            try
            {
                ApiResult queued = (await client.RunTopicAsync(ws.TopicId, false, token).ConfigureAwait(false)).Expect(202);
                RunDto q = queued.Json<RunDto>();
                run = await client.WaitForRunAsync(q.Id, _Ctx.RunTimeout, 500, token).ConfigureAwait(false);
            }
            finally
            {
                _Stub.Ledger.CurrentTag = String.Empty;
            }

            ws.Runs[round] = run.Id;
            ws.Chats.AddRange(_Stub.Ledger.Chats.Skip(chatsBefore).Where(c => c.Tag == ws.Tag));
            ws.Quotes.AddRange(_Stub.Ledger.Quotes.Skip(quotesBefore).Where(c => c.Tag == ws.Tag));
            SpentUsd += run.EstimatedCostUsd ?? 0;
            await GuardAsync(client, ws, run.Id, token).ConfigureAwait(false);

            foreach (BenchmarkFeedback f in corpus.Feedback.Where(f => f.AfterRound == round))
            {
                List<FindingDto> findings = await client.GetRunFindingsAsync(run.Id, token).ConfigureAwait(false);
                List<SearchResultDto> results = await client.GetResultsAsync(run.Id, token).ConfigureAwait(false);
                FindingDto? target = findings.Where(x => !x.IsDuplicate).FirstOrDefault(x => ItemOf(corpus.Id, x, results) == f.Item);
                if (target == null)
                {
                    ws.Notes.Add("Feedback after round " + round + " on " + f.Item + " was not given: the item produced no finding.");
                    continue;
                }

                ApiResult rated = await client.RateFindingAsync(target.Id, f.Relevance, f.Reason, token).ConfigureAwait(false);
                if (!rated.Success) ws.Notes.Add("Feedback on " + f.Item + " failed: " + rated.Status + " " + rated.Body);
            }

            Save(ws);
            return run;
        }

        /// <summary>
        /// Check that every model call of a run went to an allowed endpoint, and add the run's cost to the total.
        /// </summary>
        /// <param name="ws">Workspace.</param>
        /// <param name="run">Finished run.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ModelGuardException">A model call left the allowed endpoints.</exception>
        public async Task GuardRunAsync(Workspace ws, RunDto run, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(ws);
            ArgumentNullException.ThrowIfNull(run);
            MyMorningNewsClient client = await SessionAsync(ws, token).ConfigureAwait(false);
            SpentUsd += run.EstimatedCostUsd ?? 0;
            await GuardAsync(client, ws, run.Id, token).ConfigureAwait(false);
        }

        /// <summary>
        /// A signed-in client for a workspace's reader.
        /// </summary>
        /// <param name="ws">Workspace.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Client.</returns>
        public async Task<MyMorningNewsClient> SessionAsync(Workspace ws, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(ws);
            if (_Sessions.TryGetValue(ws.Name, out MyMorningNewsClient? c)) return c;
            TokenDto t = await _Ctx.Admin.LoginAsync(ws.TenantId, ws.Email, ws.Password, token).ConfigureAwait(false);
            MyMorningNewsClient client = _Ctx.Admin.ForUser(t.Token);
            _Sessions[ws.Name] = client;
            return client;
        }

        /// <summary>
        /// The item behind a finding, through the run's search results.
        /// </summary>
        /// <param name="corpusId">Corpus.</param>
        /// <param name="finding">Finding.</param>
        /// <param name="results">The run's search results.</param>
        /// <returns>Item id, or null.</returns>
        public string? ItemOf(string corpusId, FindingDto finding, List<SearchResultDto> results)
        {
            ArgumentNullException.ThrowIfNull(finding);
            ArgumentNullException.ThrowIfNull(results);
            if (finding.SourceItemId == null) return null;
            SearchResultDto? r = results.Where(x => x.SourceItemId == finding.SourceItemId).OrderBy(x => x.Disposition == "Fetched" ? 0 : 1).FirstOrDefault();
            string? item = r == null ? null : _Index.ItemForUrl(corpusId, r.Url);
            if (item == null) return null;
            BenchmarkItem? i = _Index.Item(corpusId, item);
            return i?.AliasOf ?? item;
        }

        #endregion

        #region Private-Methods

        private string HashFor(BenchmarkCorpus corpus, ProductArm arm, string suffix)
        {
            string model = _Ctx.Paid.Enabled ? _Ctx.Paid.Provider + "/" + _Ctx.Paid.Model : "stub";
            return StableHash.Sha256(_DatasetSha + "|" + corpus.Id + "|" + arm.Fingerprint() + "|" + suffix + "|" + BenchStubServer.Version + "|" + model).Substring(0, 10);
        }

        private static string FilePath(string name)
        {
            return Path.Combine(RepositoryPaths.Run(), "workspaces", name + ".json");
        }

        private static void Save(Workspace ws)
        {
            Json.WriteFile(FilePath(ws.Name), ws);
        }

        private static async Task VerifyAssignmentsAsync(MyMorningNewsClient client, Workspace ws, CancellationToken token)
        {
            ApiResult r = (await client.ModelAssignmentsAsync(token).ConfigureAwait(false)).Expect(200);
            Dictionary<string, string> byTask = new Dictionary<string, string>(StringComparer.Ordinal);
            using (JsonDocument doc = JsonDocument.Parse(r.Body))
            {
                JsonElement items = doc.RootElement.GetProperty("objects");
                foreach (JsonElement a in items.EnumerateArray())
                {
                    string task = a.TryGetProperty("task", out JsonElement t) ? t.GetString() ?? String.Empty : String.Empty;
                    string endpoint = a.TryGetProperty("modelEndpointId", out JsonElement e) ? e.GetString() ?? String.Empty : String.Empty;
                    if (task.Length > 0) byTask[task] = endpoint;
                }
            }

            foreach (string task in MyMorningNewsClient.ModelTasks)
                if (!byTask.TryGetValue(task, out string? ep) || !ws.AllowedEndpointIds.Contains(ep))
                    throw new ModelGuardException("Task " + task + " is not assigned to a benchmark endpoint; refusing to run so no unplanned model call is made.");
        }

        private static async Task GuardAsync(MyMorningNewsClient client, Workspace ws, string runId, CancellationToken token)
        {
            List<LlmCallDto> calls = await client.GetRunCallsAsync(runId, token).ConfigureAwait(false);
            List<LlmCallDto> outside = calls.Where(c => c.ModelEndpointId == null || !ws.AllowedEndpointIds.Contains(c.ModelEndpointId)).ToList();
            if (outside.Count > 0)
                throw new ModelGuardException("Run " + runId + " made " + outside.Count + " model calls outside the benchmark endpoints (" + String.Join(", ", outside.Select(c => c.Model).Distinct()) + "). Stopping.");
        }

        private async Task CapSpendAsync(Workspace ws, CancellationToken token)
        {
            decimal remaining = Math.Max(0.01m, _Ctx.Paid.MaxSpendUsd - SpentUsd);
            Dictionary<string, string> features = new Dictionary<string, string>(StringComparer.Ordinal) { { "usage.monthlyCostUsd.max", remaining.ToString("0.00", CultureInfo.InvariantCulture) } };
            ApiResult plan = await _Ctx.Admin.ForTenant(ws.TenantId).CreatePlanAsync("bench-cap-" + ws.Hash, "Benchmark cap " + ws.Hash, features, token).ConfigureAwait(false);
            if (!plan.Success) throw new SpendCapReachedException("Could not create the spend-cap plan (" + plan.Status + "); refusing a paid run without a server-side cap.");
            string planId;
            using (JsonDocument doc = JsonDocument.Parse(plan.Body))
            {
                planId = doc.RootElement.GetProperty("plan").GetProperty("id").GetString() ?? String.Empty;
            }

            ApiResult assigned = await _Ctx.Admin.ForTenant(ws.TenantId).AssignPlanAsync(ws.TenantId, ws.UserId, planId, token).ConfigureAwait(false);
            if (!assigned.Success) throw new SpendCapReachedException("Could not assign the spend-cap plan (" + assigned.Status + "); refusing a paid run without a server-side cap.");
        }

        #endregion
    }
}
