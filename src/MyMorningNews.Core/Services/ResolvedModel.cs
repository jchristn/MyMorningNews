namespace MyMorningNews.Core.Services
{
    using System;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// Endpoint, model, and effort resolved for a task.
    /// </summary>
    public class ResolvedModel
    {
        /// <summary>
        /// Endpoint.
        /// </summary>
        public ModelEndpoint Endpoint { get; }

        /// <summary>
        /// Model name.
        /// </summary>
        public string Model { get; }

        /// <summary>
        /// Reasoning effort, when set.
        /// </summary>
        public ReasoningEffortEnum? ReasoningEffort { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="endpoint">Endpoint.</param>
        /// <param name="model">Model.</param>
        /// <param name="effort">Effort.</param>
        public ResolvedModel(ModelEndpoint endpoint, string model, ReasoningEffortEnum? effort)
        {
            Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            Model = model ?? String.Empty;
            ReasoningEffort = effort;
        }
    }
}
