namespace Test.Shared.Helpers
{
    using System;
    using MyMorningNews.Core.Responses;

    /// <summary>
    /// A test user and a client logged in as that user.
    /// </summary>
    public class TestUser
    {
        /// <summary>
        /// User.
        /// </summary>
        public UserResponse User { get; }

        /// <summary>
        /// Logged-in client.
        /// </summary>
        public TestApiClient Client { get; }

        /// <summary>
        /// Password.
        /// </summary>
        public string Password { get; }

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="user">User.</param>
        /// <param name="client">Client.</param>
        /// <param name="password">Password.</param>
        public TestUser(UserResponse user, TestApiClient client, string password)
        {
            User = user ?? throw new ArgumentNullException(nameof(user));
            Client = client ?? throw new ArgumentNullException(nameof(client));
            Password = password ?? throw new ArgumentNullException(nameof(password));
        }
    }
}
