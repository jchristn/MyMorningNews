namespace MyMorningNews.Core.Models.Results
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;

    /// <summary>
    /// Output of research.queries.
    /// </summary>
    public class QueryPlanResult : IStructuredResult
    {
        /// <summary>
        /// Queries.
        /// </summary>
        [Documentation("The planned queries.")]
        public List<PlannedQuery> Queries { get; set; } = new List<PlannedQuery>();

        /// <inheritdoc />
        public List<string> Validate()
        {
            List<string> errors = new List<string>();
            if (Queries == null) Queries = new List<PlannedQuery>();
            Queries = Queries.Where(q => q != null && !String.IsNullOrWhiteSpace(q.Query)).ToList();
            if (Queries.Count == 0) errors.Add("queries must contain at least one query.");
            return errors;
        }
    }
}
