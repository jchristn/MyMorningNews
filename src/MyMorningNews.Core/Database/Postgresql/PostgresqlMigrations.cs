namespace MyMorningNews.Core.Database.Postgresql
{
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Database.Postgresql.Queries;

    /// <summary>
    /// Ordered PostgreSQL schema migrations. Add new versions at the end; never edit an applied version.
    /// </summary>
    public static class PostgresqlMigrations
    {
        /// <summary>
        /// All migrations in ascending version order.
        /// </summary>
        public static IReadOnlyList<SchemaMigration> All { get; } = new List<SchemaMigration>
        {
            new SchemaMigration(1, "Tenancy, security, audit, and request history", Concat(
                TenantQueries.Schema,
                UserQueries.Schema,
                CredentialQueries.Schema,
                AuthSessionQueries.Schema,
                UserRoleQueries.Schema,
                PermissionQueries.Schema,
                RolePermissionMapQueries.Schema,
                UserRoleAssignmentQueries.Schema,
                CredentialScopeAssignmentQueries.Schema,
                AuditRecordQueries.Schema,
                RequestHistoryQueries.Schema,
                new List<string>
                {
                    "CREATE INDEX IF NOT EXISTS ix_permissions_resourcetypes ON permissions USING GIN (resourcetypes)",
                    "CREATE INDEX IF NOT EXISTS ix_permissions_operationtypes ON permissions USING GIN (operationtypes)",
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_userroles_builtin_name ON userroles (name) WHERE tenantid IS NULL",
                    "CREATE INDEX IF NOT EXISTS ix_credentials_accesskey ON credentials (accesskey)",
                    "CREATE INDEX IF NOT EXISTS ix_auditrecords_tenantid_createdutc ON auditrecords (tenantid, createdutc)"
                })),
            new SchemaMigration(2, "Credential vault, model endpoints, assignments, model call log, and prompt templates", Concat(
                VaultCredentialQueries.Schema,
                ModelEndpointQueries.Schema,
                ModelAssignmentQueries.Schema,
                LlmCallQueries.Schema,
                PromptTemplateQueries.Schema,
                PromptTemplateVersionQueries.Schema,
                new List<string>
                {
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_modelassignments_scope ON modelassignments (tenantid, task, COALESCE(topicid, ''))",
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_prompttemplates_scope ON prompttemplates (tenantid, key, COALESCE(topicid, ''))"
                })),
            new SchemaMigration(3, "Full model call accounting: attribution, token types, streaming timings, tool calls, split cost", new List<string>
            {
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS userid VARCHAR(64) NULL",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS topicid VARCHAR(64) NULL",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS streamed BOOLEAN NOT NULL DEFAULT FALSE",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS attempts INTEGER NOT NULL DEFAULT 1",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS cachedinputtokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS cachewritetokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS reasoningtokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS totaltokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS timetofirsttokenms DOUBLE PRECISION NULL",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS streamingdurationms DOUBLE PRECISION NULL",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS chunkcount INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS outputtokenspersecond DOUBLE PRECISION NULL",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS toolcallcount INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS toolnames TEXT[] NOT NULL DEFAULT '{}'",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS inputcostusd NUMERIC(18,6) NULL",
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS outputcostusd NUMERIC(18,6) NULL",
                "ALTER TABLE modelendpoints ADD COLUMN IF NOT EXISTS cachedinputcostpermtok NUMERIC(18,6) NULL",
                "ALTER TABLE modelendpoints ADD COLUMN IF NOT EXISTS cachewritecostpermtok NUMERIC(18,6) NULL",
                "CREATE INDEX IF NOT EXISTS ix_llmcalls_tenantid_userid_createdutc ON llmcalls (tenantid, userid, createdutc)",
                "CREATE INDEX IF NOT EXISTS ix_llmcalls_tenantid_topicid ON llmcalls (tenantid, topicid)",
                "CREATE INDEX IF NOT EXISTS ix_llmcalls_createdutc ON llmcalls (createdutc)"
            }),
            new SchemaMigration(4, "Mobile refresh tokens and push devices", Concat(
                RefreshTokenQueries.Schema,
                DeviceQueries.Schema,
                new List<string>
                {
                    "CREATE INDEX IF NOT EXISTS ix_users_email_lower ON users (LOWER(email))"
                })),
            new SchemaMigration(5, "Topics, refinement wizard, and one-time requests", Concat(
                TopicQueries.Schema,
                TopicDimensionQueries.Schema,
                TopicConstraintQueries.Schema,
                TopicRecipientQueries.Schema,
                RefinementSessionQueries.Schema,
                RefinementMessageQueries.Schema,
                RefinementQuestionQueries.Schema,
                RefinementQuestionOptionQueries.Schema,
                ResearchRequestQueries.Schema,
                new List<string>
                {
                    "CREATE INDEX IF NOT EXISTS ix_topics_tenantid_userid_status ON topics (tenantid, userid, status)",
                    "CREATE INDEX IF NOT EXISTS ix_researchrequests_due ON researchrequests (status, runatutc)"
                })),
            new SchemaMigration(6, "Schedules, runs, and search providers", Concat(
                ScheduleQueries.Schema,
                RunQueries.Schema,
                SearchProviderQueries.Schema,
                TopicSearchProviderQueries.Schema,
                new List<string>
                {
                    "CREATE INDEX IF NOT EXISTS ix_schedules_due ON schedules (enabled, nextrunutc)",
                    "CREATE INDEX IF NOT EXISTS ix_runs_queue ON runs (status, queuedutc)",
                    "CREATE INDEX IF NOT EXISTS ix_runs_topic_status ON runs (tenantid, topicid, status)"
                })),
            new SchemaMigration(7, "Research pipeline: stages, queries, results, sources, evidence, findings, embeddings, predictions, suggestions", Concat(
                RunStageQueries.Schema,
                SearchQueryQueries.Schema,
                SearchResultQueries.Schema,
                SourceItemQueries.Schema,
                EvidenceQueries.Schema,
                FindingQueries.Schema,
                FindingEvidenceQueries.Schema,
                PredictionQueries.Schema,
                PredictionReviewQueries.Schema,
                SuggestionQueries.Schema,
                new List<string>
                {
                    "ALTER TABLE runs ADD COLUMN IF NOT EXISTS durationms DOUBLE PRECISION NOT NULL DEFAULT 0",
                    "ALTER TABLE runs ADD COLUMN IF NOT EXISTS newfindingcount INTEGER NOT NULL DEFAULT 0",
                    "ALTER TABLE runs ADD COLUMN IF NOT EXISTS duplicatecount INTEGER NOT NULL DEFAULT 0",
                    "ALTER TABLE runs ADD COLUMN IF NOT EXISTS sourcecount INTEGER NOT NULL DEFAULT 0",
                    "ALTER TABLE runs ADD COLUMN IF NOT EXISTS inputtokens INTEGER NOT NULL DEFAULT 0",
                    "ALTER TABLE runs ADD COLUMN IF NOT EXISTS outputtokens INTEGER NOT NULL DEFAULT 0",
                    "ALTER TABLE runs ADD COLUMN IF NOT EXISTS estimatedcostusd NUMERIC(18,6) NULL",
                    "ALTER TABLE runs ADD COLUMN IF NOT EXISTS contextsnapshothash VARCHAR(64) NULL",
                    "CREATE EXTENSION IF NOT EXISTS vector",
                    "CREATE TABLE IF NOT EXISTS embeddings (id VARCHAR(64) PRIMARY KEY, tenantid VARCHAR(64) NOT NULL, ownertype VARCHAR(32) NOT NULL, ownerid VARCHAR(64) NOT NULL, topicid VARCHAR(64) NULL, " +
                        "modelendpointid VARCHAR(64) NULL, model VARCHAR(256) NOT NULL, dimensions INTEGER NOT NULL, vector vector NOT NULL, contenthash VARCHAR(64) NULL, createdutc TIMESTAMPTZ NOT NULL)",
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_embeddings_owner_model ON embeddings (tenantid, ownertype, ownerid, model)",
                    "CREATE INDEX IF NOT EXISTS ix_embeddings_topic ON embeddings (tenantid, topicid, ownertype, model, dimensions, createdutc)",
                    "CREATE INDEX IF NOT EXISTS ix_findings_topic_created ON findings (tenantid, topicid, createdutc)",
                    "CREATE INDEX IF NOT EXISTS ix_searchresults_topic_hash ON searchresults (tenantid, topicid, canonicalurlhash)"
                })),
            new SchemaMigration(8, "Briefings, templates, editions, and stored artifacts", Concat(
                ArtifactQueries.Schema,
                BriefingTemplateQueries.Schema,
                BriefingTemplateVersionQueries.Schema,
                EditionQueries.Schema,
                EditionTopicQueries.Schema,
                BriefingQueries.Schema,
                BriefingSectionQueries.Schema,
                BriefingItemQueries.Schema,
                new List<string>
                {
                    "CREATE INDEX IF NOT EXISTS ix_artifacts_expiry ON artifacts (pinned, deletedutc, expiresutc)",
                    "CREATE INDEX IF NOT EXISTS ix_briefings_queue ON briefings (status, composeafterutc)"
                })),
            new SchemaMigration(9, "Audio: voice profiles, media assets, chapters, and podcast feed tokens", Concat(
                VoiceProfileQueries.Schema,
                MediaAssetQueries.Schema,
                MediaChapterQueries.Schema,
                FeedTokenQueries.Schema,
                new List<string>
                {
                    "CREATE INDEX IF NOT EXISTS ix_mediaassets_queue ON mediaassets (status, createdutc)"
                })),
            new SchemaMigration(10, "Email outbox, action tokens, suppressions, and feedback", Concat(
                EmailMessageQueries.Schema,
                EmailActionTokenQueries.Schema,
                EmailSuppressionQueries.Schema,
                FeedbackQueries.Schema,
                new List<string>
                {
                    "CREATE INDEX IF NOT EXISTS ix_emailmessages_queue ON emailmessages (status, nextattemptutc)",
                    "CREATE INDEX IF NOT EXISTS ix_feedback_target ON feedback (tenantid, targettype, targetid, iscurrent)"
                })),
            new SchemaMigration(11, "Learned preferences, reason tags, and learning checkpoints", Concat(
                LearnedPreferenceQueries.Schema,
                LearnedPreferenceFeedbackQueries.Schema,
                FeedbackReasonTagQueries.Schema,
                LearningCheckpointQueries.Schema,
                new List<string>
                {
                    "CREATE INDEX IF NOT EXISTS ix_feedback_topic_created ON feedback (tenantid, topicid, createdutc)"
                }))
        };

        private static List<string> Concat(params IReadOnlyList<string>[] parts)
        {
            return parts.SelectMany(p => p).ToList();
        }
    }
}
