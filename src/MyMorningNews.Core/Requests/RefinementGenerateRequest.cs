namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Generate the next wizard output with optional guidance.
    /// </summary>
    public class RefinementGenerateRequest
    {
        /// <summary>
        /// Guidance for this generation, for example "more about pricing".
        /// </summary>
        public string? Guidance { get; set; } = null;
    }
}
