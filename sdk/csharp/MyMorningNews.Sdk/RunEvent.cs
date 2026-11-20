namespace MyMorningNews.Sdk
{
    using System;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Serialization;

    /// <summary>
    /// One server-sent event from <c>GET /v1.0/api/runs/{id}/events</c>.
    /// </summary>
    public class RunEvent
    {
        /// <summary>
        /// Event name: <c>run</c>, <c>stage</c>, <c>ping</c>, <c>done</c>, or <c>timeout</c>. Default <c>message</c>.
        /// </summary>
        public string Event { get; set; } = "message";

        /// <summary>
        /// Event ID, or null.
        /// </summary>
        public string? Id { get; set; } = null;

        /// <summary>
        /// Raw data (JSON).
        /// </summary>
        public string Data { get; set; } = String.Empty;

        /// <summary>
        /// True for <c>done</c> (the run finished) and <c>timeout</c> (reconnect to continue).
        /// </summary>
        public bool IsFinal
        {
            get { return Event == "done" || Event == "timeout"; }
        }

        /// <summary>
        /// The run carried by a <c>run</c> or <c>done</c> event, or null.
        /// </summary>
        /// <returns>Run or null.</returns>
        public Run? AsRun()
        {
            if (Event != "run" && Event != "done") return null;
            try
            {
                Run? run = MyMorningNewsSerializer.Deserialize<Run>(Data);
                return run != null && !String.IsNullOrEmpty(run.Id) ? run : null;
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// The stage carried by a <c>stage</c> event, or null.
        /// </summary>
        /// <returns>Stage or null.</returns>
        public RunStage? AsStage()
        {
            if (Event != "stage") return null;
            try
            {
                return MyMorningNewsSerializer.Deserialize<RunStage>(Data);
            }
            catch (System.Text.Json.JsonException)
            {
                return null;
            }
        }
    }
}
