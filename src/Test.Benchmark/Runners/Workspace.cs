namespace MyMorningNews.Benchmark.Runners
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Benchmark.Stub;

    /// <summary>
    /// A provisioned workspace: a tenant with one reader and one topic for a corpus and arm, the runs made for each
    /// round, and the quotes the stub returned while they ran. Saved under benchmarks/.run/workspaces/ so a later
    /// command can reuse it.
    /// </summary>
    public class Workspace
    {
        /// <summary>
        /// Deterministic name: bench-{dataset}-{corpus}-{arm}-{hash}.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Configuration hash (dataset hash, corpus, arm settings, stub version, and model).
        /// </summary>
        public string Hash { get; set; } = String.Empty;

        /// <summary>
        /// Dataset name.
        /// </summary>
        public string Dataset { get; set; } = String.Empty;

        /// <summary>
        /// Corpus.
        /// </summary>
        public string Corpus { get; set; } = String.Empty;

        /// <summary>
        /// Arm.
        /// </summary>
        public string Arm { get; set; } = String.Empty;

        /// <summary>
        /// Tenant.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Reader.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Reader email.
        /// </summary>
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// Reader password (a generated bench password, stored only in the git-ignored .run directory).
        /// </summary>
        public string Password { get; set; } = String.Empty;

        /// <summary>
        /// Topic.
        /// </summary>
        public string TopicId { get; set; } = String.Empty;

        /// <summary>
        /// Topic name as created.
        /// </summary>
        public string TopicName { get; set; } = String.Empty;

        /// <summary>
        /// Search provider.
        /// </summary>
        public string ProviderId { get; set; } = String.Empty;

        /// <summary>
        /// Model endpoints the runs may use (stub, plus the paid endpoint in paid mode).
        /// </summary>
        public List<string> AllowedEndpointIds { get; set; } = new List<string>();

        /// <summary>
        /// Run per round.
        /// </summary>
        public SortedDictionary<int, string> Runs { get; set; } = new SortedDictionary<int, string>();

        /// <summary>
        /// Ledger tag for this workspace's model requests.
        /// </summary>
        public string Tag { get; set; } = String.Empty;

        /// <summary>
        /// Quotes the stub returned during this workspace's runs.
        /// </summary>
        public List<StubQuoteRecord> Quotes { get; set; } = new List<StubQuoteRecord>();

        /// <summary>
        /// Model requests the stub saw during this workspace's runs.
        /// </summary>
        public List<StubChatRecord> Chats { get; set; } = new List<StubChatRecord>();

        /// <summary>
        /// Notes from ingestion (feedback that could not be applied, and so on).
        /// </summary>
        public List<string> Notes { get; set; } = new List<string>();

        /// <summary>
        /// Creation time.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
