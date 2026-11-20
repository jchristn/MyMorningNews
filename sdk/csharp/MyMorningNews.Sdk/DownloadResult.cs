namespace MyMorningNews.Sdk
{
    using System;

    /// <summary>
    /// A downloaded file: its bytes (when buffered) and the response metadata.
    /// </summary>
    public class DownloadResult
    {
        /// <summary>
        /// File bytes. Empty when the content was written to a caller-supplied stream.
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Bytes received (the length of Data, or the bytes written to the stream).
        /// </summary>
        public long Length { get; set; } = 0;

        /// <summary>
        /// Content type reported by the server, or null.
        /// </summary>
        public string? ContentType { get; set; } = null;

        /// <summary>
        /// File name from Content-Disposition, or null.
        /// </summary>
        public string? FileName { get; set; } = null;

        /// <summary>
        /// HTTP status: 200 for the whole file, 206 for a range.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Content-Range header for a partial response, or null.
        /// </summary>
        public string? ContentRange { get; set; } = null;

        /// <summary>
        /// Total file size from Content-Range, when the server sent it; otherwise null.
        /// </summary>
        public long? TotalLength { get; set; } = null;

        /// <summary>
        /// Entity tag, or null.
        /// </summary>
        public string? ETag { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DownloadResult()
        {
        }

        /// <summary>
        /// Decode Data as UTF-8 text.
        /// </summary>
        /// <returns>Text.</returns>
        public string AsText()
        {
            return System.Text.Encoding.UTF8.GetString(Data);
        }
    }
}
