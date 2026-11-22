namespace MyMorningNews.Core.Email
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// A short notice email about one event: a heading, a few paragraphs, and one main action.
    /// </summary>
    public class EmailNotice
    {
        /// <summary>
        /// Template recorded on the message. Default AudioReady.
        /// </summary>
        public EmailTemplateEnum Template { get; set; } = EmailTemplateEnum.AudioReady;

        /// <summary>
        /// Category for preferences and the unsubscribe link. Default AudioEmails.
        /// </summary>
        public EmailCategoryEnum Category { get; set; } = EmailCategoryEnum.AudioEmails;

        /// <summary>
        /// Idempotency key without the recipient; one message is sent per key and recipient.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Key
        {
            get { return _Key; }
            set { _Key = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(Key)) : value; }
        }

        /// <summary>
        /// Heading and subject.
        /// </summary>
        /// <exception cref="ArgumentNullException">Value is null or empty.</exception>
        public string Title
        {
            get { return _Title; }
            set { _Title = String.IsNullOrWhiteSpace(value) ? throw new ArgumentNullException(nameof(Title)) : value; }
        }

        /// <summary>
        /// Line under the heading. May be null.
        /// </summary>
        public string? Subtitle { get; set; } = null;

        /// <summary>
        /// Body paragraphs. Never null.
        /// </summary>
        public List<string> Paragraphs
        {
            get { return _Paragraphs; }
            set { _Paragraphs = value ?? new List<string>(); }
        }

        /// <summary>
        /// Main action. May be null.
        /// </summary>
        public EmailLink? Button { get; set; } = null;

        /// <summary>
        /// Footer links shown before the preferences and unsubscribe links. Never null.
        /// </summary>
        public List<EmailLink> Links
        {
            get { return _Links; }
            set { _Links = value ?? new List<EmailLink>(); }
        }

        /// <summary>
        /// Footer note saying why the reader got this email. May be null.
        /// </summary>
        public string? Note { get; set; } = null;

        /// <summary>
        /// Topic the notice is about. May be null.
        /// </summary>
        public string? TopicId { get; set; } = null;

        /// <summary>
        /// Run the notice is about. May be null.
        /// </summary>
        public string? RunId { get; set; } = null;

        /// <summary>
        /// Briefing the notice is about. May be null.
        /// </summary>
        public string? BriefingId { get; set; } = null;

        private string _Key = "notice";
        private string _Title = "My Morning News";
        private List<string> _Paragraphs = new List<string>();
        private List<EmailLink> _Links = new List<EmailLink>();
    }
}
