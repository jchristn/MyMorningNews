namespace MyMorningNews.Sdk.TestHarness
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Shared setup for cases: records the harness creates, each registered for cleanup.
    /// </summary>
    public static class Fixtures
    {
        /// <summary>
        /// Create a draft topic with a brief, registered for cleanup under "topic:{id}".
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="client">Client to create it with.</param>
        /// <param name="label">Name label.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The topic.</returns>
        /// <exception cref="ArgumentNullException">context or client is null.</exception>
        public static async Task<Topic> CreateTopicAsync(TestContext context, MyMorningNewsClient client, string label, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(client);
            Topic topic = await client.Topics.CreateAsync(new TopicUpsertRequest
            {
                Name = context.Name(label),
                Description = "Created by the My Morning News C# SDK harness.",
                Brief = "Follow open source database releases. Harness data; safe to delete."
            }, token).ConfigureAwait(false);
            context.Cleanup.Add("topic:" + topic.Id, ct => client.Topics.DeleteAsync(topic.Id, ct));
            return topic;
        }

        /// <summary>
        /// Delete a topic created by CreateTopicAsync and drop its cleanup entry.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="client">Client.</param>
        /// <param name="topicId">Topic.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static async Task DeleteTopicAsync(TestContext context, MyMorningNewsClient client, string topicId, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(topicId);
            await client.Topics.DeleteAsync(topicId, token).ConfigureAwait(false);
            context.Cleanup.Remove("topic:" + topicId);
        }

        /// <summary>
        /// Create an ordinary member in the harness tenant, registered for cleanup under "user:{id}".
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="client">Administrator client.</param>
        /// <param name="password">Password for the new user.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The user.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static async Task<UserResponse> CreateUserAsync(TestContext context, MyMorningNewsClient client, string password, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(password);
            string tenant = context.Environment.Tenant;
            UserResponse user = await client.Admin.Users.CreateAsync(tenant, new UserCreateRequest
            {
                Email = "sdk-csharp-" + Helpers.Unique() + "@example.com",
                Password = password,
                FirstName = "SDK",
                LastName = "Harness",
                Active = true
            }, token).ConfigureAwait(false);
            context.Cleanup.Add("user:" + user.Id, async ct =>
            {
                await DeleteEditionsOfAsync(client, user.Id, ct).ConfigureAwait(false);
                await client.Admin.Users.DeleteAsync(tenant, user.Id, ct).ConfigureAwait(false);
            });
            return user;
        }

        /// <summary>
        /// Delete a user created by CreateUserAsync and drop its cleanup entry.
        /// </summary>
        /// <param name="context">Shared state.</param>
        /// <param name="client">Administrator client.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static async Task DeleteUserAsync(TestContext context, MyMorningNewsClient client, string userId, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(userId);
            await DeleteEditionsOfAsync(client, userId, token).ConfigureAwait(false);
            await client.Admin.Users.DeleteAsync(context.Environment.Tenant, userId, token).ConfigureAwait(false);
            context.Cleanup.Remove("user:" + userId);
        }

        /// <summary>
        /// Delete a user's editions (and with them their schedules). Creating a user gives them a default edition with an
        /// enabled schedule, and deleting the user leaves both behind, so the harness removes them first.
        /// </summary>
        /// <param name="client">Administrator client.</param>
        /// <param name="userId">User.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static async Task DeleteEditionsOfAsync(MyMorningNewsClient client, string userId, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(client);
            ArgumentNullException.ThrowIfNull(userId);
            EnumerationResult<Edition> editions = await client.Editions.ListAsync(new EditionListOptions { UserId = userId, PageSize = 100 }, token).ConfigureAwait(false);
            foreach (Edition e in editions.Items.Where(x => x.UserId == userId))
                await client.Editions.DeleteAsync(e.Id, token).ConfigureAwait(false);
        }
    }
}
