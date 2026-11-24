namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Briefings;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Storage;

    /// <summary>
    /// Removes everything a user owns before the user row is deleted: topics with all their research, editions with
    /// their schedules, briefings with their stored files and audio, feed tokens, feedback with its learning links,
    /// learned preferences, learning checkpoints, personal briefing templates, and plan assignments. Sent email records
    /// stay with the user cleared; model call, audit, request history, and promo redemption records stay for accounting.
    /// The database cascades credentials, sessions, refresh tokens, devices, role assignments, refinement sessions,
    /// and one-time requests from the user row. Thread safe (stateless beyond its dependencies).
    /// </summary>
    public class AccountDataService
    {
        #region Private-Members

        private const int _Page = 200;
        private const int _MaxPasses = 500;
        private readonly DatabaseDriverBase _Database;
        private readonly BriefingService _Briefings;
        private readonly ArtifactService _Artifacts;
        private readonly MyMorningNews.Core.Billing.BillingService? _Billing;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the service.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="briefings">Briefing service (deletes briefings, editions, and topic files).</param>
        /// <param name="artifacts">Artifact service (deletes stored file content).</param>
        /// <param name="billing">Optional billing lifecycle, required by the server.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public AccountDataService(DatabaseDriverBase database, BriefingService briefings, ArtifactService artifacts, MyMorningNews.Core.Billing.BillingService? billing = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Briefings = briefings ?? throw new ArgumentNullException(nameof(briefings));
            _Artifacts = artifacts ?? throw new ArgumentNullException(nameof(artifacts));
            _Billing = billing;
        }

        #endregion

        #region Public-Methods

        /// <summary>Stop provider renewals before account deletion; failures retain account binding so retries remain possible.</summary>
        public Task StopBillingAsync(string tenantId, string userId, CancellationToken token = default)
        {
            return _Billing?.PrepareDeletionAsync(tenantId, userId, token) ?? Task.CompletedTask;
        }


        /// <summary>
        /// Delete a user's data, then the user. Safe to call again after a partial failure.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the user row existed.</returns>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        public async Task<bool> DeleteUserAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));
            await StopBillingAsync(tenantId, userId, token).ConfigureAwait(false);
            await DeleteUserDataAsync(tenantId, userId, token).ConfigureAwait(false);
            return await _Database.Users.DeleteAsync(tenantId, userId, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete everything a user owns, keeping the user row.
        /// </summary>
        /// <param name="tenantId">Tenant.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">An argument is null or empty.</exception>
        public async Task DeleteUserDataAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));

            await DrainAsync(async () => (await _Database.Topics.EnumerateAsync(new TopicFilter { TenantId = tenantId, UserId = userId, PageSize = _Page }, token).ConfigureAwait(false)).Objects,
                async t =>
                {
                    await _Briefings.DeleteTopicFilesAsync(tenantId, t.Id, token).ConfigureAwait(false);
                    await _Database.Topics.DeleteCascadeAsync(tenantId, t.Id, token).ConfigureAwait(false);
                }, token).ConfigureAwait(false);
            await DrainAsync(async () => (await _Database.Editions.EnumerateAsync(new EditionFilter { TenantId = tenantId, UserId = userId, PageSize = _Page }, token).ConfigureAwait(false)).Objects,
                e => _Briefings.DeleteEditionAsync(e, token), token).ConfigureAwait(false);
            await DrainAsync(async () => (await _Database.Briefings.EnumerateAsync(new BriefingFilter { TenantId = tenantId, UserId = userId, PageSize = _Page }, token).ConfigureAwait(false)).Objects,
                b => _Briefings.DeleteBriefingAsync(b, token), token).ConfigureAwait(false);
            await DrainAsync(async () => (await _Database.Artifacts.EnumerateAsync(new ArtifactFilter { TenantId = tenantId, UserId = userId, PageSize = _Page }, token).ConfigureAwait(false)).Objects,
                async a =>
                {
                    await _Artifacts.DeleteContentAsync(a, token).ConfigureAwait(false);
                    await _Database.Artifacts.DeleteAsync(tenantId, a.Id, token).ConfigureAwait(false);
                }, token).ConfigureAwait(false);
            await DrainAsync(async () => (await _Database.FeedTokens.EnumerateAsync(new FeedTokenFilter { TenantId = tenantId, UserId = userId, PageSize = _Page }, token).ConfigureAwait(false)).Objects,
                f => _Database.FeedTokens.DeleteAsync(tenantId, f.Id, token), token).ConfigureAwait(false);
            await DrainAsync(async () => (await _Database.EmailActionTokens.EnumerateAsync(new EmailActionTokenFilter { TenantId = tenantId, UserId = userId, PageSize = _Page }, token).ConfigureAwait(false)).Objects,
                x => _Database.EmailActionTokens.DeleteAsync(tenantId, x.Id, token), token).ConfigureAwait(false);
            await DrainAsync(async () => (await _Database.LearnedPreferences.EnumerateAsync(new LearnedPreferenceFilter { TenantId = tenantId, UserId = userId, PageSize = _Page }, token).ConfigureAwait(false)).Objects,
                async p =>
                {
                    foreach (LearnedPreferenceFeedback link in (await _Database.LearnedPreferenceFeedback.EnumerateAsync(new LearnedPreferenceFeedbackFilter { TenantId = tenantId, LearnedPreferenceId = p.Id, PageSize = 1000 }, token).ConfigureAwait(false)).Objects)
                        await _Database.LearnedPreferenceFeedback.DeleteAsync(tenantId, link.Id, token).ConfigureAwait(false);
                    await _Database.LearnedPreferences.DeleteAsync(tenantId, p.Id, token).ConfigureAwait(false);
                }, token).ConfigureAwait(false);
            await DrainAsync(async () => (await _Database.Feedback.EnumerateAsync(new FeedbackFilter { TenantId = tenantId, UserId = userId, PageSize = _Page }, token).ConfigureAwait(false)).Objects,
                async f =>
                {
                    foreach (FeedbackReasonTag tag in (await _Database.FeedbackReasonTags.EnumerateAsync(new FeedbackReasonTagFilter { TenantId = tenantId, FeedbackId = f.Id, PageSize = 100 }, token).ConfigureAwait(false)).Objects)
                        await _Database.FeedbackReasonTags.DeleteAsync(tenantId, tag.Id, token).ConfigureAwait(false);
                    foreach (LearnedPreferenceFeedback link in (await _Database.LearnedPreferenceFeedback.EnumerateAsync(new LearnedPreferenceFeedbackFilter { TenantId = tenantId, FeedbackId = f.Id, PageSize = 100 }, token).ConfigureAwait(false)).Objects)
                        await _Database.LearnedPreferenceFeedback.DeleteAsync(tenantId, link.Id, token).ConfigureAwait(false);
                    await _Database.Feedback.DeleteAsync(tenantId, f.Id, token).ConfigureAwait(false);
                }, token).ConfigureAwait(false);
            await DrainAsync(async () => (await _Database.LearningCheckpoints.EnumerateAsync(new LearningCheckpointFilter { TenantId = tenantId, UserId = userId, PageSize = _Page }, token).ConfigureAwait(false)).Objects,
                c => _Database.LearningCheckpoints.DeleteAsync(tenantId, c.Id, token), token).ConfigureAwait(false);
            await DrainAsync(async () => (await _Database.BriefingTemplates.EnumerateAsync(new BriefingTemplateFilter { TenantId = tenantId, UserId = userId, PageSize = _Page }, token).ConfigureAwait(false)).Objects,
                async t =>
                {
                    foreach (BriefingTemplateVersion v in (await _Database.BriefingTemplateVersions.EnumerateAsync(new BriefingTemplateVersionFilter { TenantId = tenantId, TemplateId = t.Id, PageSize = 1000 }, token).ConfigureAwait(false)).Objects)
                        await _Database.BriefingTemplateVersions.DeleteAsync(tenantId, v.Id, token).ConfigureAwait(false);
                    await _Database.BriefingTemplates.DeleteAsync(tenantId, t.Id, token).ConfigureAwait(false);
                }, token).ConfigureAwait(false);
            await DrainAsync(async () => (await _Database.PlanAssignments.EnumerateAsync(new PlanAssignmentFilter { TenantId = tenantId, UserId = userId, PageSize = _Page }, token).ConfigureAwait(false)).Objects,
                p => _Database.PlanAssignments.DeleteAsync(tenantId, p.Id, token), token).ConfigureAwait(false);
            await DrainAsync(async () => (await _Database.EmailMessages.EnumerateAsync(new EmailMessageFilter { TenantId = tenantId, UserId = userId, PageSize = _Page }, token).ConfigureAwait(false)).Objects,
                async m =>
                {
                    m.UserId = null;
                    await _Database.EmailMessages.UpdateAsync(m, token).ConfigureAwait(false);
                }, token).ConfigureAwait(false);
        }

        #endregion

        #region Private-Methods

        private static async Task DrainAsync<T>(Func<Task<List<T>>> page, Func<T, Task> remove, CancellationToken token)
        {
            for (int pass = 0; pass < _MaxPasses; pass++)
            {
                token.ThrowIfCancellationRequested();
                List<T> items = await page().ConfigureAwait(false);
                if (!items.Any()) return;
                foreach (T item in items) await remove(item).ConfigureAwait(false);
            }

            throw new InvalidOperationException("Account data removal did not finish after " + _MaxPasses + " passes.");
        }

        #endregion
    }
}
