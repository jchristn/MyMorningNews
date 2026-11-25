namespace MyMorningNews.Core.Email
{
    using System;

    /// <summary>
    /// One labeled value in an email metrics table.
    /// </summary>
    public class EmailMetric
    {
        /// <summary>
        /// Label.
        /// </summary>
        public string Label { get; set; } = String.Empty;

        /// <summary>
        /// Formatted value.
        /// </summary>
        public string Value { get; set; } = String.Empty;

        /// <summary>
        /// Small note after the value (a comparison), or null.
        /// </summary>
        public string? Note { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EmailMetric()
        {
        }

        /// <summary>
        /// Instantiate with values.
        /// </summary>
        /// <param name="label">Label.</param>
        /// <param name="value">Formatted value.</param>
        /// <param name="note">Note, or null.</param>
        public EmailMetric(string label, string value, string? note = null)
        {
            Label = label ?? String.Empty;
            Value = value ?? String.Empty;
            Note = note;
        }
    }
}
