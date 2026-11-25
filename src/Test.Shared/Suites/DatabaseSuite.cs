namespace Test.Shared.Suites
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Security;
    using SyslogLogging;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Migrations, seeding, and tenant-scoped data access against a dedicated database.
    /// </summary>
    public static class DatabaseSuite
    {
        private const string S = "Database";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Database driver, migrations, and seeding", new[]
            {
                Cases.Case(S, "Migrations.Idempotent", "Migrations apply once and re-initialization is a no-op", MigrationsAsync, "database"),
                Cases.Case(S, "Seed.Idempotent", "Seeding creates the default tenant, admin, and built-in roles once and keeps role IDs stable", SeedAsync, "database"),
                Cases.Case(S, "Scoping.NoCrossTenantRead", "Tenant-scoped reads never return another tenant's rows", ScopingAsync, "database"),
                Cases.Case(S, "Cascade.UserDeletesCredentials", "Deleting a user deletes their credentials", CascadeAsync, "database"),
                Cases.Case(S, "Paging.SortAndCount", "Enumeration pages, sorts, and counts", PagingAsync, "database")
            });
        }

        private static async Task<T> WithDatabaseAsync<T>(Func<DatabaseDriverBase, Task<T>> body, CancellationToken token)
        {
            DatabaseSettings settings = TestDatabase.NewSettings("mmn_db");
            DatabaseDriverBase driver = await DatabaseDriverFactory.CreateAndInitializeAsync(settings, token).ConfigureAwait(false);
            try
            {
                return await body(driver).ConfigureAwait(false);
            }
            finally
            {
                await driver.DisposeAsync().ConfigureAwait(false);
                await TestDatabase.DropAsync(settings, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static async Task MigrationsAsync(CancellationToken token)
        {
            DatabaseSettings settings = TestDatabase.NewSettings("mmn_db");
            try
            {
                int first;
                using (DatabaseDriverBase a = await DatabaseDriverFactory.CreateAndInitializeAsync(settings, token).ConfigureAwait(false))
                {
                    first = a.SchemaVersion;
                    Assert.True(first >= 1, "schema version applied");
                }

                using (DatabaseDriverBase b = await DatabaseDriverFactory.CreateAndInitializeAsync(settings, token).ConfigureAwait(false))
                {
                    Assert.Equal(first, b.SchemaVersion, "same version after re-initialization");
                    System.Data.DataTable t = await b.ExecuteQueryAsync("SELECT COUNT(*) FROM schema_migrations", false, token).ConfigureAwait(false);
                    // Versions may have gaps (a number can be reserved for work still on another branch), so compare with
                    // the number of defined migrations rather than the highest version.
                    Assert.Equal(MyMorningNews.Core.Database.Postgresql.PostgresqlMigrations.All.Count, Convert.ToInt32(t.Rows[0][0]), "one tracking row per version");
                }
            }
            finally
            {
                await TestDatabase.DropAsync(settings, CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static async Task SeedAsync(CancellationToken token)
        {
            await WithDatabaseAsync(async db =>
            {
                using (LoggingModule logging = new LoggingModule())
                {
                    logging.Settings.EnableConsole = false;
                    SeedService seed = new SeedService(db, logging);
                    await seed.EnsureAsync(token).ConfigureAwait(false);
                    EnumerationResult<UserRole> rolesBefore = await db.Roles.EnumerateAsync(new UserRoleFilter { TenantId = null, PageSize = 100 }, token).ConfigureAwait(false);
                    await seed.EnsureAsync(token).ConfigureAwait(false);
                    EnumerationResult<UserRole> rolesAfter = await db.Roles.EnumerateAsync(new UserRoleFilter { TenantId = null, PageSize = 100 }, token).ConfigureAwait(false);
                    Assert.Equal(BuiltInRoles.All.Count, (int)rolesAfter.TotalRecords, "one row per built-in role");
                    foreach (UserRole r in rolesBefore.Objects)
                        Assert.True(rolesAfter.Objects.Exists(x => x.Id == r.Id), "role " + r.Name + " keeps its id");
                    Assert.Equal(1L, await db.Users.CountAsync(new UserFilter { TenantId = "default" }, token).ConfigureAwait(false), "one default admin");
                    Assert.True(await db.Tenants.ExistsAsync("default", token).ConfigureAwait(false), "default tenant");
                }

                return true;
            }, token).ConfigureAwait(false);
        }

        private static async Task ScopingAsync(CancellationToken token)
        {
            await WithDatabaseAsync(async db =>
            {
                Tenant a = await db.Tenants.CreateAsync(new Tenant { Name = "A" }, token).ConfigureAwait(false);
                Tenant b = await db.Tenants.CreateAsync(new Tenant { Name = "B" }, token).ConfigureAwait(false);
                User ua = await db.Users.CreateAsync(new User { TenantId = a.Id, Email = "a@example.test", PasswordSha256 = SecretHasher.Sha256Hex("x") }, token).ConfigureAwait(false);
                await db.Users.CreateAsync(new User { TenantId = b.Id, Email = "b@example.test", PasswordSha256 = SecretHasher.Sha256Hex("x") }, token).ConfigureAwait(false);
                Assert.True(await db.Users.ReadAsync(b.Id, ua.Id, token).ConfigureAwait(false) == null, "user A not readable in tenant B");
                EnumerationResult<User> inB = await db.Users.EnumerateAsync(new UserFilter { TenantId = b.Id }, token).ConfigureAwait(false);
                Assert.True(inB.Objects.TrueForAll(u => u.TenantId == b.Id) && inB.TotalRecords == 1, "enumeration stays in tenant");
                Assert.True(!await db.Users.DeleteAsync(b.Id, ua.Id, token).ConfigureAwait(false), "cross-tenant delete affects nothing");
                return true;
            }, token).ConfigureAwait(false);
        }

        private static async Task CascadeAsync(CancellationToken token)
        {
            await WithDatabaseAsync(async db =>
            {
                Tenant t = await db.Tenants.CreateAsync(new Tenant { Name = "T" }, token).ConfigureAwait(false);
                User u = await db.Users.CreateAsync(new User { TenantId = t.Id, Email = "c@example.test", PasswordSha256 = SecretHasher.Sha256Hex("x") }, token).ConfigureAwait(false);
                Credential c = await db.Credentials.CreateAsync(new Credential { TenantId = t.Id, UserId = u.Id, Name = "k", AccessKey = SecretGenerator.AccessKey(), SecretKeyEncrypted = "x" }, token).ConfigureAwait(false);
                await db.Users.DeleteAsync(t.Id, u.Id, token).ConfigureAwait(false);
                Assert.True(await db.Credentials.ReadAsync(t.Id, c.Id, token).ConfigureAwait(false) == null, "credential removed with user");
                await db.Tenants.DeleteAsync(t.Id, token).ConfigureAwait(false);
                Assert.Equal(0L, await db.Users.CountAsync(new UserFilter { TenantId = t.Id }, token).ConfigureAwait(false), "tenant delete cascades");
                return true;
            }, token).ConfigureAwait(false);
        }

        private static async Task PagingAsync(CancellationToken token)
        {
            await WithDatabaseAsync(async db =>
            {
                Tenant t = await db.Tenants.CreateAsync(new Tenant { Name = "P" }, token).ConfigureAwait(false);
                for (int i = 0; i < 7; i++)
                    await db.Users.CreateAsync(new User { TenantId = t.Id, Email = "p" + i + "@example.test", PasswordSha256 = "x" }, token).ConfigureAwait(false);
                EnumerationResult<User> page = await db.Users.EnumerateAsync(new UserFilter { TenantId = t.Id, PageSize = 3, PageNumber = 2, SortBy = "email", SortDirection = MyMorningNews.Core.Enums.SortDirectionEnum.Ascending }, token).ConfigureAwait(false);
                Assert.Equal(7L, page.TotalRecords, "total");
                Assert.Equal(3, page.TotalPages, "pages");
                Assert.Equal(3, page.Objects.Count, "page size");
                Assert.Equal("p3@example.test", page.Objects[0].Email, "sorted second page starts at p3");
                EnumerationResult<User> search = await db.Users.EnumerateAsync(new UserFilter { TenantId = t.Id, Search = "p5@" }, token).ConfigureAwait(false);
                Assert.Equal(1L, search.TotalRecords, "search");
                return true;
            }, token).ConfigureAwait(false);
        }
    }
}
