namespace MyMorningNews.Core.Responses
{
    using System;

    /// <summary>
    /// A model reported by an endpoint.
    /// </summary>
    public class ModelListItem
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Display name.
        /// </summary>
        public string? DisplayName { get; set; } = null;

        /// <summary>
        /// Input token limit.
        /// </summary>
        public int? InputTokenLimit { get; set; } = null;

        /// <summary>
        /// Output token limit.
        /// </summary>
        public int? OutputTokenLimit { get; set; } = null;

        /// <summary>
        /// Tool support, when reported.
        /// </summary>
        public bool? SupportsTools { get; set; } = null;
    }
}
