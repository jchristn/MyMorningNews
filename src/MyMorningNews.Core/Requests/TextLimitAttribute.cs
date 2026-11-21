namespace MyMorningNews.Core.Requests
{
    using System;

    /// <summary>
    /// Declares the longest text a request property accepts, matching the database column it is stored in, and whether a
    /// value that is present may be blank. Checked by <see cref="RequestValidator"/> when a request body is read, so an
    /// over-long value is a 400 ValidationFailed instead of a database error. A null (absent) value is never checked here;
    /// routes decide whether a field is required. For a list of strings the limit applies to each item.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class TextLimitAttribute : Attribute
    {
        /// <summary>
        /// Most characters allowed after trimming. Minimum 1.
        /// </summary>
        public int Maximum { get; }

        /// <summary>
        /// Whether a value that is present may be empty or whitespace. Default true. Set false for names and other values
        /// that identify a record.
        /// </summary>
        public bool AllowBlank { get; set; } = true;

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="maximum">Most characters allowed after trimming. Minimum 1.</param>
        /// <exception cref="ArgumentOutOfRangeException">maximum is less than 1.</exception>
        public TextLimitAttribute(int maximum)
        {
            if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum), "The maximum must be at least 1.");
            Maximum = maximum;
        }
    }
}
