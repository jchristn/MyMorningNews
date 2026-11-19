namespace MyMorningNews.Core.Models.Results
{
    using System.Collections.Generic;

    /// <summary>
    /// A structured model output that can validate and normalize itself after parsing.
    /// </summary>
    public interface IStructuredResult
    {
        /// <summary>
        /// Clamp values into range and report problems that need a repair turn.
        /// </summary>
        /// <returns>Validation errors; empty when valid.</returns>
        List<string> Validate();
    }
}
