namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class PlatformWireHandler : HttpMessageHandler
    {
        internal string Reply { get; set; } = "{}";
        internal HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        internal string Body { get; private set; } = String.Empty;
        internal string Path { get; private set; } = String.Empty;
        internal string? Authorization { get; private set; }
        internal string? Range { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.PathAndQuery;
            Body = request.Content == null ? String.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            Authorization = request.Headers.Authorization?.ToString();
            Range = request.Headers.Range?.ToString();
            return new HttpResponseMessage(Status) { Content = new StringContent(Reply, System.Text.Encoding.UTF8, "application/json") };
        }
    }
}
