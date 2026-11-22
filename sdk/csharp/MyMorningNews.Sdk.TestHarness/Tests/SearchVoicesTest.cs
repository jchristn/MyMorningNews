namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Search providers and voice profiles pointed at an unreachable local address: CRUD, a failing test query, a typed 502
    /// from the voice test, and 404 after delete.
    /// </summary>
    public class SearchVoicesTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Search providers and voices CRUD and test";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SearchVoicesTest()
        {
        }

        /// <summary>
        /// Run the case.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="result">Result.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public override async Task RunAsync(TestContext context, TestResult result, CancellationToken token)
        {
            MyMorningNewsClient c = context.RequireAdmin();
            await StepAsync(context, result, "POST search provider Tavily without a key (400)", () => Helpers.ExpectErrorAsync(() => c.SearchProviders.CreateAsync(new SearchProviderUpsertRequest
            {
                Name = context.Name("tavily"),
                Kind = SearchProviderKindEnum.Tavily
            }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            SearchProvider p = await StepAsync(context, result, "POST search provider", () => c.SearchProviders.CreateAsync(new SearchProviderUpsertRequest
            {
                Name = context.Name("searxng"),
                Kind = SearchProviderKindEnum.Searxng,
                Endpoint = "http://127.0.0.1:9",
                IsDefault = false,
                Active = false,
                TimeoutMs = 2000
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("search:" + p.Id, ct => c.SearchProviders.DeleteAsync(p.Id, ct));
            SearchProvider sp = await StepAsync(context, result, "GET search provider", () => c.SearchProviders.GetAsync(p.Id, token)).ConfigureAwait(false);
            Helpers.Equal(SearchProviderKindEnum.Searxng, sp.Kind, "kind");
            SearchProvider spu = await StepAsync(context, result, "PUT search provider", () => c.SearchProviders.UpdateAsync(p.Id, new SearchProviderUpsertRequest { MaxResults = 7 }, token)).ConfigureAwait(false);
            Helpers.Equal(7, spu.MaxResults, "max results");
            EnumerationResult<SearchProvider> list = await StepAsync(context, result, "GET search providers (kind filter)",
                () => c.SearchProviders.ListAsync(new SearchProviderListOptions { Kind = SearchProviderKindEnum.Searxng, PageSize = 100 }, token)).ConfigureAwait(false);
            Helpers.Contains(list.Items, x => x.Id == p.Id, "the provider");
            SearchOutcome outcome = await StepAsync(context, result, "POST search provider test (unreachable)", () => c.SearchProviders.TestAsync(p.Id, new SearchTestRequest { Query = "postgres", MaxResults = 3 }, token)).ConfigureAwait(false);
            Helpers.True(!outcome.Success, "an unreachable provider fails its test");
            await StepAsync(context, result, "DELETE search provider", () => c.SearchProviders.DeleteAsync(p.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("search:" + p.Id);
            await StepAsync(context, result, "GET deleted search provider (404)", () => Helpers.ExpectErrorAsync(() => c.SearchProviders.GetAsync(p.Id, token), 404)).ConfigureAwait(false);

            await StepAsync(context, result, "POST voice without a voice name (400)", () => Helpers.ExpectErrorAsync(() => c.Voices.CreateAsync(new VoiceProfileUpsertRequest { Name = "x" }, token), 400)).ConfigureAwait(false);
            VoiceProfile v = await StepAsync(context, result, "POST voice", () => c.Voices.CreateAsync(new VoiceProfileUpsertRequest
            {
                Name = context.Name("voice"),
                Provider = TtsProviderEnum.OpenAiCompatibleSpeech,
                Endpoint = "http://127.0.0.1:9",
                Model = "harness-tts",
                Voice = "alloy",
                IsDefault = false
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("voice:" + v.Id, ct => c.Voices.DeleteAsync(v.Id, ct));
            VoiceProfile vu = await StepAsync(context, result, "PUT voice", () => c.Voices.UpdateAsync(v.Id, new VoiceProfileUpsertRequest { Speed = 1.25 }, token)).ConfigureAwait(false);
            Helpers.True(System.Math.Abs(vu.Speed - 1.25) < 0.001, "speed");
            VoiceProfile vr = await StepAsync(context, result, "GET voice", () => c.Voices.GetAsync(v.Id, token)).ConfigureAwait(false);
            Helpers.Equal("alloy", vr.Voice, "voice");
            EnumerationResult<VoiceProfile> voices = await StepAsync(context, result, "GET voices", () => c.Voices.ListAsync(new VoiceListOptions { PageSize = 100 }, token)).ConfigureAwait(false);
            Helpers.Contains(voices.Items, x => x.Id == v.Id, "the voice");
            await StepAsync(context, result, "POST voice test (unreachable, 502)", () => Helpers.ExpectErrorAsync(() => c.Voices.TestAsync(v.Id, new VoiceTestRequest { Text = "Hello." }, token), 502, "TtsError")).ConfigureAwait(false);
            AvailableVoicesResponse available = await StepAsync(context, result, "GET voices available", () => c.Voices.GetAvailableAsync(token)).ConfigureAwait(false);
            Helpers.Contains(available.Voices, x => x.VoiceProfileId == v.Id && x.Voice == "alloy", "the voice profile among the available voices");
            await StepAsync(context, result, "POST voice preview without a voice (400)", () => Helpers.ExpectErrorAsync(() => c.Voices.PreviewAsync(new VoicePreviewRequest(), token), 400)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE voice", () => c.Voices.DeleteAsync(v.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("voice:" + v.Id);
            await StepAsync(context, result, "GET deleted voice (404)", () => Helpers.ExpectErrorAsync(() => c.Voices.GetAsync(v.Id, token), 404)).ConfigureAwait(false);
        }
    }
}
