namespace MyMorningNews.Core.Requests
{
    using System;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Resolve a prediction yourself.
    /// </summary>
    public class PredictionResolveRequest
    {
        /// <summary>
        /// Accurate, PartiallyAccurate, Inaccurate, Unresolvable, or Withdrawn.
        /// </summary>
        public PredictionStatusEnum Status { get; set; } = PredictionStatusEnum.Unresolvable;

        /// <summary>
        /// Note.
        /// </summary>
        public string? Note { get; set; } = null;
    }
}
