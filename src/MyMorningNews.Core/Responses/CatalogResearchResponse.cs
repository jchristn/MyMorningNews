namespace MyMorningNews.Core.Responses
{
    using MyMorningNews.Core.Models;

    /// <summary>System-administrator-only canonical research settings.</summary>
    public class CatalogResearchResponse
    {
        /// <summary>Canonical topic. Never included in member catalog projections.</summary>
        public Topic Topic { get; set; } = new Topic();

        /// <summary>Primary research schedule, or null before configuration.</summary>
        public Schedule? Schedule { get; set; } = null;
    }
}
