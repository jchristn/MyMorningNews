namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of refinement.synthesize: the research brief and constraints.
    /// </summary>
    public class TopicContextResult : IStructuredResult
    {
        /// <summary>
        /// Topic name.
        /// </summary>
        [Documentation("A short topic name, 2 to 8 words.")]
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Research brief in Markdown.
        /// </summary>
        [Documentation("Markdown brief, 3 to 8 short paragraphs or bullet groups.")]
        public string Brief { get; set; } = String.Empty;

        /// <summary>
        /// Include keywords.
        /// </summary>
        [Documentation("Keywords to include.")]
        public List<string> IncludeKeywords { get; set; } = new List<string>();

        /// <summary>
        /// Exclude keywords.
        /// </summary>
        [Documentation("Keywords to exclude.")]
        public List<string> ExcludeKeywords { get; set; } = new List<string>();

        /// <summary>
        /// Entities to always include.
        /// </summary>
        [Documentation("Entities (companies, projects, people) to always include.")]
        public List<string> IncludeEntities { get; set; } = new List<string>();

        /// <summary>
        /// Entities to ignore.
        /// </summary>
        [Documentation("Entities to ignore.")]
        public List<string> ExcludeEntities { get; set; } = new List<string>();

        /// <summary>
        /// Preferred domains.
        /// </summary>
        [Documentation("Preferred source domains, for example example.com.")]
        public List<string> PreferredDomains { get; set; } = new List<string>();

        /// <summary>
        /// Blocked domains.
        /// </summary>
        [Documentation("Domains to never use.")]
        public List<string> BlockedDomains { get; set; } = new List<string>();

        /// <summary>
        /// Questions to answer over time.
        /// </summary>
        [Documentation("Questions the reader wants answered over time.")]
        public List<string> TrackedQuestions { get; set; } = new List<string>();

        /// <summary>
        /// Audience level.
        /// </summary>
        [Documentation("Beginner, Practitioner, or Expert.")]
        public AudienceLevelEnum AudienceLevel { get; set; } = AudienceLevelEnum.Practitioner;

        /// <summary>
        /// Horizon.
        /// </summary>
        [Documentation("Days, Weeks, Months, or Years.")]
        public HorizonEnum Horizon { get; set; } = HorizonEnum.Weeks;

        /// <summary>
        /// Novelty preference, 0 to 100.
        /// </summary>
        [Documentation("0 repeats important items, 100 shows only new items.")]
        public int NoveltyPreference { get; set; } = 50;

        /// <summary>
        /// Seed search queries.
        /// </summary>
        [Documentation("5 to 10 seed search queries.")]
        public List<string> SeedQueries { get; set; } = new List<string>();

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (String.IsNullOrWhiteSpace(Brief)) errors.Add("brief must not be empty.");
            if (SeedQueries == null || SeedQueries.Count == 0) errors.Add("seedQueries must contain at least one query.");
            NoveltyPreference = Math.Clamp(NoveltyPreference, 0, 100);
            return errors;
        }
    }
}
