namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Topic dimensions, constraints, recipients, and search providers.
    /// </summary>
    public class TopicChildrenTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Topic dimensions, constraints, recipients, search providers";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TopicChildrenTest()
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
            Topic topic = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "children", token)).ConfigureAwait(false);

            TopicDimension dim = await StepAsync(context, result, "POST dimension", () => c.Topics.CreateDimensionAsync(topic.Id, new DimensionUpsertRequest
            {
                Name = "Releases",
                Description = "New versions",
                Weight = DimensionWeightEnum.High,
                IncludeTerms = new List<string> { "release", "changelog" }
            }, token)).ConfigureAwait(false);
            Helpers.Equal(DimensionWeightEnum.High, dim.Weight, "dimension weight");
            TopicDimension dim2 = await StepAsync(context, result, "PUT dimension", () => c.Topics.UpdateDimensionAsync(topic.Id, dim.Id, new DimensionUpsertRequest { Name = "Major releases", IsLocked = true }, token)).ConfigureAwait(false);
            Helpers.Equal("Major releases", dim2.Name, "dimension name");
            Helpers.True(dim2.IsLocked, "dimension locked");
            List<TopicDimension> dims = (await StepAsync(context, result, "GET dimensions", () => c.Topics.ListDimensionsAsync(topic.Id, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            Helpers.Contains(dims, d => d.Id == dim.Id, "the dimension");

            await StepAsync(context, result, "POST constraint with a bad feed URL (400)", () => Helpers.ExpectErrorAsync(() => c.Topics.CreateConstraintAsync(topic.Id,
                new ConstraintUpsertRequest { Kind = ConstraintKindEnum.FeedUrl, Value = "not a url" }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            TopicConstraint con = await StepAsync(context, result, "POST constraint", () => c.Topics.CreateConstraintAsync(topic.Id,
                new ConstraintUpsertRequest { Kind = ConstraintKindEnum.IncludeKeyword, Value = "postgres", DimensionId = dim.Id }, token)).ConfigureAwait(false);
            TopicConstraint con2 = await StepAsync(context, result, "PUT constraint", () => c.Topics.UpdateConstraintAsync(topic.Id, con.Id, new ConstraintUpsertRequest { Value = "postgresql", Note = "spelled out" }, token)).ConfigureAwait(false);
            Helpers.Equal("postgresql", con2.Value, "constraint value");
            List<TopicConstraint> keywords = (await StepAsync(context, result, "GET constraints (kind filter)", () => c.Topics.ListConstraintsAsync(topic.Id, ConstraintKindEnum.IncludeKeyword, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            Helpers.Contains(keywords, k => k.Id == con.Id, "the keyword constraint");
            List<TopicConstraint> feeds = (await StepAsync(context, result, "GET constraints (other kind)", () => c.Topics.ListConstraintsAsync(topic.Id, ConstraintKindEnum.FeedUrl, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            Helpers.DoesNotContain(feeds, k => k.Id == con.Id, "the keyword constraint under FeedUrl");

            string email = "sdk-csharp-" + Helpers.Unique() + "@example.com";
            TopicRecipient rec = await StepAsync(context, result, "POST recipient", () => c.Topics.AddRecipientAsync(topic.Id, new RecipientUpsertRequest { Email = email }, token)).ConfigureAwait(false);
            Helpers.Equal(email, rec.Email, "recipient email");
            await StepAsync(context, result, "POST recipient with a bad address (400)", () => Helpers.ExpectErrorAsync(() => c.Topics.AddRecipientAsync(topic.Id, new RecipientUpsertRequest { Email = "nope" }, token), 400)).ConfigureAwait(false);
            List<TopicRecipient> recs = (await StepAsync(context, result, "GET recipients", () => c.Topics.ListRecipientsAsync(topic.Id, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            Helpers.Contains(recs, r => r.Id == rec.Id, "the recipient");

            List<SearchProvider> providers = (await StepAsync(context, result, "GET topic search providers", () => c.Topics.GetSearchProvidersAsync(topic.Id, options: new MyMorningNews.Sdk.Options.EnumerationQuery { PageSize = 1000 }, token: token)).ConfigureAwait(false)).Objects;
            await StepAsync(context, result, "PUT topic search providers with an unknown id (400)", () => Helpers.ExpectErrorAsync(() => c.Topics.SetSearchProvidersAsync(topic.Id,
                new List<TopicSearchProviderRequest> { new TopicSearchProviderRequest { SearchProviderId = "spr_missing", Enabled = true } }, token), 400)).ConfigureAwait(false);
            if (providers.Count > 0)
            {
                List<SearchProvider> chosen = await StepAsync(context, result, "PUT topic search providers", () => c.Topics.SetSearchProvidersAsync(topic.Id,
                    new List<TopicSearchProviderRequest> { new TopicSearchProviderRequest { SearchProviderId = providers[0].Id, Enabled = true, MaxResults = 5 } }, token)).ConfigureAwait(false);
                Helpers.Contains(chosen, p => p.Id == providers[0].Id, "the chosen provider");
                await StepAsync(context, result, "PUT topic search providers (empty restores defaults)", () => c.Topics.SetSearchProvidersAsync(topic.Id, new List<TopicSearchProviderRequest>(), token)).ConfigureAwait(false);
            }

            await StepAsync(context, result, "DELETE recipient", () => c.Topics.DeleteRecipientAsync(topic.Id, rec.Id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE constraint", () => c.Topics.DeleteConstraintAsync(topic.Id, con.Id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE dimension", () => c.Topics.DeleteDimensionAsync(topic.Id, dim.Id, token)).ConfigureAwait(false);
            await StepAsync(context, result, "PUT deleted dimension (404)", () => Helpers.ExpectErrorAsync(() => c.Topics.UpdateDimensionAsync(topic.Id, dim.Id, new DimensionUpsertRequest { Name = "x" }, token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
        }
    }
}
