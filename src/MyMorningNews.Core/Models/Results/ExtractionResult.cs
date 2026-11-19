namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of research.extract.
    /// </summary>
    public class ExtractionResult : IStructuredResult
    {
        /// <summary>
        /// Findings.
        /// </summary>
        [Documentation("Up to 5 findings; empty when the document has nothing relevant.")]
        public List<ExtractedFinding> Findings { get; set; } = new List<ExtractedFinding>();

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (Findings == null) Findings = new List<ExtractedFinding>();
            Findings = Findings.Where(f => f != null && !String.IsNullOrWhiteSpace(f.Title)).Take(5).ToList();
            foreach (ExtractedFinding f in Findings) if (f.Quotes == null) f.Quotes = new List<string>();
            return errors;
        }
    }
}
