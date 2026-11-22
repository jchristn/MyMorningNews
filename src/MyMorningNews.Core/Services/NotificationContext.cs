namespace MyMorningNews.Core.Services
{
    using System.Collections.Generic;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;

    /// <summary>
    /// What a notification is about. Set the items that apply to the kind being sent; the rest stay null.
    /// </summary>
    public class NotificationContext
    {
        /// <summary>
        /// The run (run results, run failed). May be null.
        /// </summary>
        public Run? Run { get; set; } = null;

        /// <summary>
        /// The briefing (briefing ready, audio ready). May be null.
        /// </summary>
        public Briefing? Briefing { get; set; } = null;

        /// <summary>
        /// The edition the briefing belongs to. May be null.
        /// </summary>
        public Edition? Edition { get; set; } = null;

        /// <summary>
        /// The audio (audio ready). May be null.
        /// </summary>
        public MediaAsset? Media { get; set; } = null;

        /// <summary>
        /// The plan limit that was reached. May be null.
        /// </summary>
        public CapReachedException? Cap { get; set; } = null;

        /// <summary>
        /// The prediction that is due, when the notice is about one prediction. May be null.
        /// </summary>
        public Prediction? Prediction { get; set; } = null;

        /// <summary>
        /// How many predictions are due, when one notice covers several. Default 0.
        /// </summary>
        public int PredictionCount { get; set; } = 0;

        /// <summary>
        /// Name of the topic, when known. May be null; it is read from the run's topic when needed.
        /// </summary>
        public string? TopicName { get; set; } = null;

        /// <summary>
        /// Recipients of a briefing email; null sends to the briefing's owner.
        /// </summary>
        public IReadOnlyList<string>? EmailRecipients { get; set; } = null;

        /// <summary>
        /// Further filter on email, for example the edition's email switch or a one-time request without the email
        /// deliverable. Default true.
        /// </summary>
        public bool EmailAllowed { get; set; } = true;

        /// <summary>
        /// Further filter on push, for example the edition's push switch. Default true.
        /// </summary>
        public bool PushAllowed { get; set; } = true;
    }
}
