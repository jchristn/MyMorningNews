namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Editions: create, duplicate-name conflict, read, update, set topics, preview without saving, list, delete, 404.
    /// </summary>
    public class EditionTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Editions CRUD, topics, preview";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public EditionTest()
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
            Topic topic = await StepAsync(context, result, "POST topic", () => Fixtures.CreateTopicAsync(context, c, "edition-topic", token)).ConfigureAwait(false);
            string name = context.Name("edition");
            EditionResponse e = await StepAsync(context, result, "POST edition", () => c.Editions.CreateAsync(new EditionUpsertRequest
            {
                Name = name,
                Description = "Harness edition",
                AudioEnabled = false,
                EmailEnabled = false,
                Enabled = false
            }, token)).ConfigureAwait(false);
            string id = e.Edition.Id;
            context.Cleanup.Add("edition:" + id, ct => c.Editions.DeleteAsync(id, ct));
            Helpers.Equal(name, e.Edition.Name, "edition name");
            await StepAsync(context, result, "POST edition with a duplicate name (409)", () => Helpers.ExpectErrorAsync(() => c.Editions.CreateAsync(new EditionUpsertRequest { Name = name }, token), 409, "Conflict")).ConfigureAwait(false);

            EditionResponse read = await StepAsync(context, result, "GET edition", () => c.Editions.GetAsync(id, token)).ConfigureAwait(false);
            Helpers.Equal(id, read.Edition.Id, "edition id");
            EditionResponse updated = await StepAsync(context, result, "PUT edition", () => c.Editions.UpdateAsync(id, new EditionUpsertRequest { Description = "Updated", LookbackHours = 48 }, token)).ConfigureAwait(false);
            Helpers.Equal("Updated", updated.Edition.Description, "description");
            EditionResponse withTopics = await StepAsync(context, result, "PUT edition topics", () => c.Editions.SetTopicsAsync(id, new EditionTopicsRequest { TopicIds = new List<string> { topic.Id } }, token)).ConfigureAwait(false);
            Helpers.Contains(withTopics.Topics, t => t.TopicId == topic.Id, "the edition topic");

            Briefing preview = await StepAsync(context, result, "POST edition preview", () => c.Editions.PreviewAsync(id, null, token)).ConfigureAwait(false);
            Helpers.NotEmpty(preview.Title, "preview title");

            EnumerationResult<Edition> list = await StepAsync(context, result, "GET editions", () => c.Editions.ListAsync(new EditionListOptions { Mine = true, PageSize = 100 }, token)).ConfigureAwait(false);
            Helpers.Contains(list.Objects, x => x.Id == id, "the edition");

            await StepAsync(context, result, "DELETE edition", () => c.Editions.DeleteAsync(id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("edition:" + id);
            await StepAsync(context, result, "GET deleted edition (404)", () => Helpers.ExpectErrorAsync(() => c.Editions.GetAsync(id, token), 404)).ConfigureAwait(false);
            await StepAsync(context, result, "DELETE topic", () => Fixtures.DeleteTopicAsync(context, c, topic.Id, token)).ConfigureAwait(false);
        }
    }
}
