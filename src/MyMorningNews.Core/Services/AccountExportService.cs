namespace MyMorningNews.Core.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Serialization;

    /// <summary>
    /// Builds a user's data export (GET /v1.0/api/account/export, plan section 33.1): a ZIP of the profile, topics with
    /// their dimensions and constraints, findings, predictions, suggestions, feedback, learned preferences as JSON, and
    /// every briefing as Markdown. Thread safe (stateless beyond the database).
    /// </summary>
    public class AccountExportService
    {
        #region Private-Members

        private const int _Page = 1000;
        private readonly DatabaseDriverBase _Database;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the service.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <exception cref="ArgumentNullException">Database is null.</exception>
        public AccountExportService(DatabaseDriverBase database)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build the export ZIP for a user.
        /// </summary>
        /// <param name="user">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>ZIP bytes.</returns>
        /// <exception cref="ArgumentNullException">User is null.</exception>
        public async Task<byte[]> ExportAsync(User user, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(user);
            string t = user.TenantId;
            List<Topic> topics = (await _Database.Topics.EnumerateAsync(new TopicFilter { TenantId = t, UserId = user.Id, PageSize = _Page }, token).ConfigureAwait(false)).Objects;
            List<Dictionary<string, object?>> topicDocs = new List<Dictionary<string, object?>>();
            foreach (Topic topic in topics)
            {
                topicDocs.Add(new Dictionary<string, object?>
                {
                    { "topic", topic },
                    { "dimensions", (await _Database.TopicDimensions.EnumerateAsync(new TopicDimensionFilter { TenantId = t, TopicId = topic.Id, PageSize = _Page }, token).ConfigureAwait(false)).Objects },
                    { "constraints", (await _Database.TopicConstraints.EnumerateAsync(new TopicConstraintFilter { TenantId = t, TopicId = topic.Id, PageSize = _Page }, token).ConfigureAwait(false)).Objects }
                });
            }

            using (MemoryStream ms = new MemoryStream())
            {
                using (ZipArchive zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    Write(zip, "README.txt", "My Morning News data export for " + user.Email + ", created " + DateTime.UtcNow.ToString("u") + ".\nJSON files hold your records; the briefings folder holds each briefing as Markdown.\n(c) 2026 Lattice AI LLC\n");
                    Write(zip, "profile.json", Serializer.Serialize(new Dictionary<string, object?>
                    {
                        { "id", user.Id }, { "email", user.Email }, { "firstName", user.FirstName }, { "lastName", user.LastName }, { "timeZone", user.TimeZone }, { "locale", user.Locale },
                        { "createdUtc", user.CreatedUtc }, { "termsVersion", user.TermsVersion }, { "termsAcceptedUtc", user.TermsAcceptedUtc }
                    }));
                    Write(zip, "topics.json", Serializer.Serialize(topicDocs));
                    Write(zip, "findings.json", Serializer.Serialize((await _Database.Findings.EnumerateAsync(new FindingFilter { TenantId = t, UserId = user.Id, PageSize = 5000 }, token).ConfigureAwait(false)).Objects));
                    Write(zip, "predictions.json", Serializer.Serialize((await _Database.Predictions.EnumerateAsync(new PredictionFilter { TenantId = t, UserId = user.Id, PageSize = 5000 }, token).ConfigureAwait(false)).Objects));
                    Write(zip, "suggestions.json", Serializer.Serialize((await _Database.Suggestions.EnumerateAsync(new SuggestionFilter { TenantId = t, UserId = user.Id, PageSize = 5000 }, token).ConfigureAwait(false)).Objects));
                    Write(zip, "feedback.json", Serializer.Serialize((await _Database.Feedback.EnumerateAsync(new FeedbackFilter { TenantId = t, UserId = user.Id, PageSize = 5000 }, token).ConfigureAwait(false)).Objects));
                    Write(zip, "preferences.json", Serializer.Serialize((await _Database.LearnedPreferences.EnumerateAsync(new LearnedPreferenceFilter { TenantId = t, UserId = user.Id, PageSize = 5000 }, token).ConfigureAwait(false)).Objects));
                    List<Briefing> briefings = (await _Database.Briefings.EnumerateAsync(new BriefingFilter { TenantId = t, UserId = user.Id, PageSize = 5000 }, token).ConfigureAwait(false)).Objects;
                    HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (Briefing b in briefings.Where(b => !String.IsNullOrWhiteSpace(b.Markdown)))
                    {
                        string name = "briefings/" + (b.ComposedUtc ?? b.CreatedUtc).ToString("yyyy-MM-dd") + "-" + Slug(b.Title) + ".md";
                        if (!names.Add(name)) name = "briefings/" + b.Id + ".md";
                        names.Add(name);
                        Write(zip, name, b.Markdown!);
                    }
                }

                return ms.ToArray();
            }
        }

        #endregion

        #region Private-Methods

        private static void Write(ZipArchive zip, string name, string content)
        {
            ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (Stream s = entry.Open())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                s.Write(bytes, 0, bytes.Length);
            }
        }

        private static string Slug(string? title)
        {
            string s = new string((title ?? "briefing").ToLowerInvariant().Select(c => Char.IsLetterOrDigit(c) ? c : '-').ToArray());
            while (s.Contains("--", StringComparison.Ordinal)) s = s.Replace("--", "-", StringComparison.Ordinal);
            s = s.Trim('-');
            if (s.Length == 0) s = "briefing";
            return s.Length > 60 ? s.Substring(0, 60) : s;
        }

        #endregion
    }
}
