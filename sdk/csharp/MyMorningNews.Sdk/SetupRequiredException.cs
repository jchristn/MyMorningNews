namespace MyMorningNews.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The workspace is missing something it needs before the request can run, such as a working model key (HTTP 409
    /// SetupRequired). Raised by refinement sessions and research requests when the model endpoint for refinement has no
    /// key or failed its last check. Check names the setup check that is not ready; read Setup.GetStatusAsync for the
    /// full picture, and an administrator can fix it with Setup.SetModelKeyAsync or Setup.VerifyAsync.
    /// </summary>
    public class SetupRequiredException : MyMorningNewsException
    {
        /// <summary>
        /// Setup check key from details.check, for example model:mdl_abc. Matches SetupCheck.Key. Empty when the server sent
        /// none.
        /// </summary>
        public string Check { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="requestId">Request ID, or null.</param>
        /// <param name="details">Details, or null.</param>
        /// <param name="responseBody">Raw body, or null.</param>
        public SetupRequiredException(string message, string? requestId, IReadOnlyDictionary<string, string>? details, string? responseBody)
            : base(409, "SetupRequired", message, requestId, details, responseBody)
        {
            Check = Details.TryGetValue("check", out string? check) ? check : String.Empty;
        }
    }
}
