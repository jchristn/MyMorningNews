namespace MyMorningNews.Core.Pipeline.Stages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Models.Results;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Telemetry;

    /// <summary>
    /// Stage 6: research.extract reads each source and returns candidate findings with verbatim quotes. Every quote is
    /// verified against the stored text; unverifiable quotes are dropped, and a candidate with no verified quote is
    /// discarded (plan section 19). Fails only when every extraction call failed.
    /// </summary>
    public class ExtractStage : IRunStage
    {
        /// <inheritdoc />
        public string Name
        {
            get { return "Extract"; }
        }

        /// <inheritdoc />
        public bool Critical
        {
            get { return true; }
        }

        /// <inheritdoc />
        public async Task<StageOutcome> ExecuteAsync(RunContext ctx, PipelineServices services, CancellationToken token)
        {
            int failures = 0;
            int quotes = 0;
            int verified = 0;
            string? lastError = null;
            object sync = new object();
            using (SemaphoreSlim gate = new SemaphoreSlim(Math.Max(1, services.Settings.Pipeline.MaxConcurrentModelCalls)))
            {
                await Task.WhenAll(ctx.Sources.Select(async source =>
                {
                    await gate.WaitAsync(token).ConfigureAwait(false);
                    try
                    {
                        string text = source.ContentText ?? String.Empty;
                        int max = services.Settings.Pipeline.MaxDocumentCharacters;
                        StructuredCallResult<ExtractionResult> result = await services.CallAsync<ExtractionResult>(ctx, "research.extract", new Dictionary<string, object?>
                        {
                            { "document.title", source.Title },
                            { "document.url", source.CanonicalUrl },
                            { "document.published", source.PublishedUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) },
                            { "document.text", text.Length > max ? text.Substring(0, max) : text }
                        }, token).ConfigureAwait(false);

                        foreach (ExtractedFinding f in result.Value.Findings)
                        {
                            Candidate c = new Candidate { Extracted = f, Source = source };
                            c.Dimension = f.Dimension == null ? null : ctx.Dimensions.FirstOrDefault(d => String.Equals(d.Name, f.Dimension.Trim(), StringComparison.OrdinalIgnoreCase));
                            foreach (string q in f.Quotes.Where(q => !String.IsNullOrWhiteSpace(q)).Take(3))
                            {
                                Interlocked.Increment(ref quotes);
                                VerifiedQuote v = QuoteVerifier.Verify(text, q);
                                if (!v.Found) continue;
                                Interlocked.Increment(ref verified);
                                c.Evidence.Add(await services.Database.Evidence.CreateAsync(new Evidence
                                {
                                    TenantId = ctx.Run.TenantId,
                                    SourceItemId = source.Id,
                                    RunId = ctx.Run.Id,
                                    Quote = v.Text,
                                    StartOffset = v.Start,
                                    EndOffset = v.End,
                                    ContextText = v.Context,
                                    QuoteHash = QuoteVerifier.Hash(v.Text),
                                    Verified = true
                                }, token).ConfigureAwait(false));
                            }

                            if (c.Evidence.Count > 0) lock (sync) ctx.Candidates.Add(c);
                        }
                    }
                    catch (ModelCallException ex)
                    {
                        Interlocked.Increment(ref failures);
                        lastError = ex.Message;
                    }
                    finally
                    {
                        gate.Release();
                    }
                })).ConfigureAwait(false);
            }

            MyMorningNewsTelemetry.Add(MyMorningNewsTelemetry.EvidenceQuotes, verified, "outcome", "verified");
            MyMorningNewsTelemetry.Add(MyMorningNewsTelemetry.EvidenceQuotes, quotes - verified, "outcome", "unverified");
            if (ctx.Sources.Count > 0 && failures == ctx.Sources.Count) throw new InvalidOperationException("Extraction failed for every source: " + lastError);
            string? note = quotes > verified ? (quotes - verified) + " of " + quotes + " quotes could not be verified and were dropped." : (quotes > 0 ? "All " + quotes + " quotes verified." : null);
            return StageOutcome.Of(ctx.Sources.Count, ctx.Candidates.Count, note);
        }
    }
}
