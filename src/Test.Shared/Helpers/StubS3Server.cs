namespace Test.Shared.Helpers
{
    using System;
    using System.Collections.Concurrent;
    using System.IO;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A minimal S3-compatible server for tests (path-style): PUT, GET with Range, HEAD, and DELETE on /{bucket}/{key}.
    /// Signatures are not checked. Decodes aws-chunked uploads.
    /// </summary>
    public sealed class StubS3Server : IDisposable
    {
        /// <summary>
        /// Endpoint URL (no trailing slash).
        /// </summary>
        public string Endpoint { get; }

        /// <summary>
        /// Stored objects by "bucket/key".
        /// </summary>
        public ConcurrentDictionary<string, byte[]> Objects { get; } = new ConcurrentDictionary<string, byte[]>(StringComparer.Ordinal);

        /// <summary>
        /// Requests served.
        /// </summary>
        public int Requests
        {
            get { return _Requests; }
        }

        private readonly HttpListener _Listener = new HttpListener();
        private readonly CancellationTokenSource _Stop = new CancellationTokenSource();
        private int _Requests = 0;

        /// <summary>
        /// Start on a free loopback port.
        /// </summary>
        public StubS3Server()
        {
            int port = PortAllocator.GetFreePort();
            _Listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
            _Listener.Start();
            Endpoint = "http://127.0.0.1:" + port;
            Task.Run(LoopAsync);
        }

        /// <summary>
        /// Stop.
        /// </summary>
        public void Dispose()
        {
            _Stop.Cancel();
            try
            {
                _Listener.Stop();
                _Listener.Close();
            }
            catch (ObjectDisposedException)
            {
            }

            _Stop.Dispose();
        }

        private async Task LoopAsync()
        {
            while (!_Stop.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _Listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                    return;
                }

                _ = Task.Run(() => HandleAsync(ctx));
            }
        }

        private async Task HandleAsync(HttpListenerContext ctx)
        {
            Interlocked.Increment(ref _Requests);
            try
            {
                string key = Uri.UnescapeDataString(ctx.Request.Url!.AbsolutePath.TrimStart('/'));
                string method = ctx.Request.HttpMethod.ToUpperInvariant();
                if (method == "PUT")
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        await ctx.Request.InputStream.CopyToAsync(ms).ConfigureAwait(false);
                        byte[] data = ms.ToArray();
                        string? sha = ctx.Request.Headers["x-amz-content-sha256"];
                        bool chunked = (ctx.Request.Headers["Content-Encoding"] ?? String.Empty).Contains("aws-chunked", StringComparison.OrdinalIgnoreCase)
                            || (sha != null && sha.StartsWith("STREAMING-", StringComparison.Ordinal));
                        byte[] stored = chunked ? Dechunk(data) : data;
                        Objects[key] = stored;
                        ctx.Response.Headers["ETag"] = ETag(stored);
                    }

                    ctx.Response.StatusCode = 200;
                    ctx.Response.Close();
                    return;
                }

                if (method == "DELETE")
                {
                    Objects.TryRemove(key, out _);
                    ctx.Response.StatusCode = 204;
                    ctx.Response.Close();
                    return;
                }

                if (!Objects.TryGetValue(key, out byte[]? obj))
                {
                    byte[] err = Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?><Error><Code>NoSuchKey</Code><Message>The specified key does not exist.</Message><Key>" + WebUtility.HtmlEncode(key) + "</Key></Error>");
                    ctx.Response.StatusCode = 404;
                    ctx.Response.ContentType = "application/xml";
                    if (method != "HEAD") await ctx.Response.OutputStream.WriteAsync(err).ConfigureAwait(false);
                    ctx.Response.Close();
                    return;
                }

                long start = 0;
                long end = obj.Length - 1;
                string? range = ctx.Request.Headers["Range"];
                if (!String.IsNullOrEmpty(range) && range.StartsWith("bytes=", StringComparison.Ordinal))
                {
                    string[] parts = range.Substring(6).Split('-');
                    if (parts[0].Length == 0) start = Math.Max(0, obj.Length - long.Parse(parts[1]));
                    else
                    {
                        start = long.Parse(parts[0]);
                        if (parts.Length > 1 && parts[1].Length > 0) end = Math.Min(end, long.Parse(parts[1]));
                    }

                    ctx.Response.StatusCode = 206;
                    ctx.Response.Headers["Content-Range"] = "bytes " + start + "-" + end + "/" + obj.Length;
                }
                else
                {
                    ctx.Response.StatusCode = 200;
                }

                ctx.Response.ContentType = "application/octet-stream";
                ctx.Response.Headers["ETag"] = ETag(obj);
                ctx.Response.Headers["Last-Modified"] = DateTime.UtcNow.ToString("R");
                ctx.Response.ContentLength64 = end - start + 1;
                if (method != "HEAD") await ctx.Response.OutputStream.WriteAsync(obj.AsMemory((int)start, (int)(end - start + 1))).ConfigureAwait(false);
                ctx.Response.Close();
            }
            catch (Exception)
            {
                try
                {
                    ctx.Response.StatusCode = 500;
                    ctx.Response.Close();
                }
                catch (Exception)
                {
                }
            }
        }

        private static string ETag(byte[] data)
        {
            return "\"" + Convert.ToHexString(System.Security.Cryptography.MD5.HashData(data)).ToLowerInvariant() + "\"";
        }

        private static byte[] Dechunk(byte[] data)
        {
            using (MemoryStream output = new MemoryStream())
            {
                int pos = 0;
                while (pos < data.Length)
                {
                    int lineEnd = IndexOf(data, pos);
                    if (lineEnd < 0) break;
                    string header = Encoding.ASCII.GetString(data, pos, lineEnd - pos);
                    int size = Convert.ToInt32(header.Split(';')[0], 16);
                    pos = lineEnd + 2;
                    if (size == 0) break;
                    output.Write(data, pos, size);
                    pos += size + 2;
                }

                return output.ToArray();
            }
        }

        private static int IndexOf(byte[] data, int start)
        {
            for (int i = start; i < data.Length - 1; i++)
                if (data[i] == '\r' && data[i + 1] == '\n') return i;
            return -1;
        }
    }
}
