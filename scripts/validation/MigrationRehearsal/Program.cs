namespace MigrationRehearsal
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using MyMorningNews.Core.Models;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Entitlements;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Settings;

    /// <summary>Apply migrations and tier seeds twice to an isolated local production backup. Never starts workers or provider clients.</summary>
    public static class Program
    {
        /// <summary>Use MMN_REHEARSAL_DATABASE (must start mmn_rehearsal_) and optional MMN_REHEARSAL_PASSWORD.</summary>
        public static async Task Main()
        {
            string name = Environment.GetEnvironmentVariable("MMN_REHEARSAL_DATABASE") ?? "mmn_rehearsal_tiers";
            if (!name.StartsWith("mmn_rehearsal_", StringComparison.Ordinal)) throw new ArgumentException("An isolated rehearsal database is required.");
            DatabaseSettings settings = new DatabaseSettings { Server = "127.0.0.1", Port = 12102, DatabaseName = name, Username = "postgres", Password = Environment.GetEnvironmentVariable("MMN_REHEARSAL_PASSWORD") ?? "password" };
            ServerSettings server = new ServerSettings();
            server.Hosting.DefaultPlanKey = "free";
            server.Hosting.NewUsersRequireSubscription = false;
            server.Hosting.NonBilledPlanKey = "catalog-only";
            string? policies = null;
            long users = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                using (DatabaseDriverBase database = await DatabaseDriverFactory.CreateAndInitializeAsync(settings, CancellationToken.None).ConfigureAwait(false))
                {
                    EntitlementService entitlements = new EntitlementService(database, server);
                    await entitlements.EnsureDefaultsAsync(CancellationToken.None).ConfigureAwait(false);
                    List<AccountPlanPolicy> captured = new List<AccountPlanPolicy>();
                    long count = 0;
                    foreach (Tenant tenant in (await database.Tenants.EnumerateAsync(new TenantFilter { PageSize = 1000 }, CancellationToken.None).ConfigureAwait(false)).Objects)
                    {
                        captured.AddRange((await database.AccountPlanPolicies.EnumerateAsync(new AccountPlanPolicyFilter { TenantId = tenant.Id, PageSize = 1000 }, CancellationToken.None).ConfigureAwait(false)).Objects);
                        count += await database.Users.CountAsync(new UserFilter { TenantId = tenant.Id }, CancellationToken.None).ConfigureAwait(false);
                    }
                    string current = Serializer.Serialize(captured.OrderBy(p => p.Id).ToList());
                    if (pass == 1 && (current != policies || users != count)) throw new InvalidOperationException("Restart changed captured account policies or user count.");
                    policies = current;
                    users = count;
                    Console.WriteLine("Migration/seed pass " + (pass + 1) + ": " + count + " users; captured policies stable.");
                }
            }
        }
    }
}
