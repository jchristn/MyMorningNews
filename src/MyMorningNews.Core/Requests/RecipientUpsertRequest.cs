namespace MyMorningNews.Core.Requests
{
    using System;
    using System.Collections.Generic;
    using MyMorningNews.Core.Enums;

    /// <summary>
    /// Add or update a topic email recipient.
    /// </summary>
    public class RecipientUpsertRequest
    {
        /// <summary>
        /// Email address.
        /// </summary>
        [TextLimit(256)]
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// Receives email.
        /// </summary>
        public bool? Active { get; set; } = null;
    }
}
