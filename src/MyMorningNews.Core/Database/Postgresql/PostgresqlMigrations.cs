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
                })),
            new SchemaMigration(12, "Plans, plan assignments, promotional codes, and redemptions", Concat(
                PlanQueries.Schema,
                PlanFeatureQueries.Schema,
                PlanAssignmentQueries.Schema,
                PromoCodeQueries.Schema,
                PromoRedemptionQueries.Schema,
                new List<string>
                {
                    "CREATE INDEX IF NOT EXISTS ix_planassignments_user ON planassignments (tenantid, userid, startsutc)"
                })),
            new SchemaMigration(13, "Pinned and hidden findings", new List<string>
            {
                "ALTER TABLE findings ADD COLUMN IF NOT EXISTS pinned BOOLEAN NOT NULL DEFAULT FALSE",
                "ALTER TABLE findings ADD COLUMN IF NOT EXISTS hidden BOOLEAN NOT NULL DEFAULT FALSE"
            }),
            new SchemaMigration(14, "Retry a run from a stage", new List<string>
            {
                "ALTER TABLE runs ADD COLUMN IF NOT EXISTS resumefromstage VARCHAR(64)"
            }),
            new SchemaMigration(15, "MCP server registry, agentic search, and run tool calls", Concat(
                McpServerQueries.Schema,
                McpServerToolQueries.Schema,
                RunToolCallQueries.Schema,
                new List<string>
                {
                    "ALTER TABLE topics ADD COLUMN IF NOT EXISTS agenticsearch BOOLEAN NOT NULL DEFAULT FALSE",
                    "ALTER TABLE topics ADD COLUMN IF NOT EXISTS mcptoolids TEXT[]",
                    "CREATE INDEX IF NOT EXISTS ix_runtoolcalls_run ON runtoolcalls (tenantid, runid, sortorder)"
                })),
            new SchemaMigration(16, "Public sign-up, email verification, password reset, and account deletion", Concat(
                AccountTokenQueries.Schema,
                new List<string>
                {
                    "ALTER TABLE users ADD COLUMN IF NOT EXISTS selfsignup BOOLEAN NOT NULL DEFAULT FALSE",
                    "ALTER TABLE users ADD COLUMN IF NOT EXISTS emailverifiedutc TIMESTAMPTZ",
                    "ALTER TABLE users ADD COLUMN IF NOT EXISTS termsversion VARCHAR(32)",
                    "ALTER TABLE users ADD COLUMN IF NOT EXISTS termsacceptedutc TIMESTAMPTZ",
                    "ALTER TABLE users ADD COLUMN IF NOT EXISTS deletionrequestedutc TIMESTAMPTZ",
                    "ALTER TABLE tenants ADD COLUMN IF NOT EXISTS ispersonal BOOLEAN NOT NULL DEFAULT FALSE",
                    "ALTER TABLE tenants ADD COLUMN IF NOT EXISTS purgeafterutc TIMESTAMPTZ",
                    "CREATE INDEX IF NOT EXISTS ix_accounttokens_hash ON accounttokens (tokenhash)",
                    "ALTER TABLE modelendpoints DROP CONSTRAINT IF EXISTS modelendpoints_credentialid_fkey"
                })),
            new SchemaMigration(17, "Listening progress and the push channel per edition", Concat(
                BriefingProgressQueries.Schema,
                new List<string>
                {
                    "CREATE UNIQUE INDEX IF NOT EXISTS ux_briefingprogress_user ON briefingprogress (tenantid, userid, briefingid)",
                    "ALTER TABLE editions ADD COLUMN IF NOT EXISTS pushenabled BOOLEAN NOT NULL DEFAULT TRUE"
                })),
            new SchemaMigration(18, "Prediction due notices", new List<string>
            {
                "ALTER TABLE predictions ADD COLUMN IF NOT EXISTS duenotifiedutc TIMESTAMPTZ",
                "CREATE INDEX IF NOT EXISTS ix_predictions_due ON predictions (horizonutc) WHERE status = 'Open' AND duenotifiedutc IS NULL"
            }),
            new SchemaMigration(19, "Model endpoint verification", new List<string>
            {
                "ALTER TABLE modelendpoints ADD COLUMN IF NOT EXISTS lastvalidationsucceeded BOOLEAN"
            }),
            new SchemaMigration(20, "Model endpoint sign-in method", new List<string>
            {
                "ALTER TABLE modelendpoints ADD COLUMN IF NOT EXISTS authmethod VARCHAR(32)"
            }),
            new SchemaMigration(21, "Run and stage cost", new List<string>
            {
                "ALTER TABLE llmcalls ADD COLUMN IF NOT EXISTS runstageid VARCHAR(64)",
                "CREATE INDEX IF NOT EXISTS ix_llmcalls_runstageid ON llmcalls (runstageid)",
                "CREATE INDEX IF NOT EXISTS ix_llmcalls_runid ON llmcalls (runid)",
                "ALTER TABLE runstages ADD COLUMN IF NOT EXISTS llmcallcount INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE runstages ADD COLUMN IF NOT EXISTS unpricedcallcount INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE runstages ADD COLUMN IF NOT EXISTS inputtokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE runstages ADD COLUMN IF NOT EXISTS outputtokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE runstages ADD COLUMN IF NOT EXISTS cachedinputtokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE runstages ADD COLUMN IF NOT EXISTS reasoningtokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE runstages ADD COLUMN IF NOT EXISTS estimatedcostusd NUMERIC(18,6)",
                "ALTER TABLE runs ADD COLUMN IF NOT EXISTS llmcallcount INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE runs ADD COLUMN IF NOT EXISTS unpricedcallcount INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE runs ADD COLUMN IF NOT EXISTS cachedinputtokens INTEGER NOT NULL DEFAULT 0",
                "ALTER TABLE runs ADD COLUMN IF NOT EXISTS reasoningtokens INTEGER NOT NULL DEFAULT 0",
                // Earlier calls carry no stage: stages run one at a time, so attribute each call to the stage that was running when it was logged.
                @"UPDATE llmcalls c SET runstageid = s.id FROM runstages s
WHERE c.runid = s.runid AND c.runstageid IS NULL AND s.startedutc IS NOT NULL
AND c.createdutc >= s.startedutc AND c.createdutc <= COALESCE(s.completedutc, s.startedutc + INTERVAL '1 day')",
                @"UPDATE runstages s SET llmcallcount = a.calls, unpricedcallcount = a.unpriced, inputtokens = a.input, outputtokens = a.output,
cachedinputtokens = a.cached, reasoningtokens = a.reasoning, estimatedcostusd = a.cost
FROM (SELECT runstageid, COUNT(*) AS calls, COUNT(*) FILTER (WHERE estimatedcostusd IS NULL) AS unpriced, COALESCE(SUM(inputtokens), 0) AS input,
COALESCE(SUM(outputtokens), 0) AS output, COALESCE(SUM(cachedinputtokens), 0) AS cached, COALESCE(SUM(reasoningtokens), 0) AS reasoning, SUM(estimatedcostusd) AS cost
FROM llmcalls WHERE runstageid IS NOT NULL GROUP BY runstageid) a WHERE s.id = a.runstageid",
                @"UPDATE runs r SET llmcallcount = a.calls, unpricedcallcount = a.unpriced, cachedinputtokens = a.cached, reasoningtokens = a.reasoning
FROM (SELECT runid, COUNT(*) AS calls, COUNT(*) FILTER (WHERE estimatedcostusd IS NULL) AS unpriced, COALESCE(SUM(cachedinputtokens), 0) AS cached,
COALESCE(SUM(reasoningtokens), 0) AS reasoning FROM llmcalls WHERE runid IS NOT NULL GROUP BY runid) a WHERE r.id = a.runid"
            })
        };

        private static List<string> Concat(params IReadOnlyList<string>[] parts)
        {
            return parts.SelectMany(p => p).ToList();
        }
    }
}
