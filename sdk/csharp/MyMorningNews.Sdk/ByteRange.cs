namespace MyMorningNews.Sdk
{
    using System;
    using System.Globalization;

    /// <summary>
    /// A single HTTP byte range for partial downloads (sent as Range: bytes=start-end).
    /// </summary>
    public class ByteRange
    {
        /// <summary>
        /// First byte, zero-based. Minimum 0; negative values are raised to 0.
        /// </summary>
        public long Start
        {
            get
            {
                return _Start;
            }
            set
            {
                _Start = Math.Max(0, value);
            }
        }

        /// <summary>
        /// Last byte, inclusive. Null reads to the end of the file. When set, it must not be below Start.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is below Start.</exception>
        public long? End
        {
            get
            {
                return _End;
            }
            set
            {
                if (value.HasValue && value.Value < _Start) throw new ArgumentOutOfRangeException(nameof(End), "End must not be below Start.");
                _End = value;
            }
        }

        private long _Start = 0;
        private long? _End = null;

        /// <summary>
        /// Instantiate a range that reads the whole file from the start.
        /// </summary>
        public ByteRange()
        {
        }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="start">First byte (minimum 0).</param>
        /// <param name="end">Last byte, inclusive, or null for the end of the file.</param>
        /// <exception cref="ArgumentOutOfRangeException">end is below start.</exception>
        public ByteRange(long start, long? end = null)
        {
            Start = start;
            End = end;
        }

        /// <summary>
        /// The Range header value.
        /// </summary>
        /// <returns>For example bytes=0-1023.</returns>
        public string ToHeaderValue()
        {
            return "bytes=" + _Start.ToString(CultureInfo.InvariantCulture) + "-" + (_End.HasValue ? _End.Value.ToString(CultureInfo.InvariantCulture) : String.Empty);
        }
    }
}
