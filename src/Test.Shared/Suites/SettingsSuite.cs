namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Serialization;
    using MyMorningNews.Core.Services;
    using MyMorningNews.Core.Settings;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Settings loading, environment overrides, masking, and validated updates (file only, no database).
    /// </summary>
    public static class SettingsSuite
    {
        private const string S = "Settings";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Settings service", new[]
            {
                Cases.Case(S, "CreatesDefaultFile", "A missing settings file is created with defaults", CreatesFileAsync, "fast"),
                Cases.Case(S, "EnvironmentOverrides", "Environment variables override the file and are reported as overridden", EnvironmentAsync, "fast"),
                Cases.Case(S, "SecretsMasked", "Secrets are masked when described", MaskedAsync, "fast"),
                Cases.Case(S, "UpdateLiveAndRestart", "Live fields apply immediately; restart-required fields are pending", UpdateAsync, "fast"),
                Cases.Case(S, "MaskNeverOverwritesSecret", "Submitting the mask leaves the secret unchanged", MaskPreservedAsync, "fast"),
                Cases.Case(S, "OverriddenNotWritten", "Environment-overridden values are not written to the file", OverriddenNotWrittenAsync, "fast"),
                Cases.Case(S, "Clamping", "Numeric settings are clamped to their documented ranges", ClampAsync, "fast")
            });
        }

        private static string TempFile()
        {
            return Path.Combine(Path.GetTempPath(), "mmn-settings-" + Guid.NewGuid().ToString("N") + ".json");
        }

        private static Task CreatesFileAsync(CancellationToken token)
        {
            string file = TempFile();
            try
            {
                SettingsService s = new SettingsService(file, n => null);
                Assert.True(File.Exists(file), "file created");
                Assert.Equal(8000, s.Current.Rest.Port, "default port");
            }
            finally
            {
                File.Delete(file);
            }

            return Task.CompletedTask;
        }

        private static Task EnvironmentAsync(CancellationToken token)
        {
            string file = TempFile();
            try
            {
                Dictionary<string, string> env = new Dictionary<string, string> { { "MYMORNINGNEWS_REST_PORT", "12345" }, { "MYMORNINGNEWS_DB_PASSWORD", "envpw" } };
                SettingsService s = new SettingsService(file, n => env.TryGetValue(n, out string? v) ? v : null);
                Assert.Equal(12345, s.Current.Rest.Port, "port overridden");
                Assert.Equal("envpw", s.Current.Database.Password, "password overridden");
                Assert.True(s.OverriddenPaths.Contains("rest.port") && s.OverriddenPaths.Contains("database.password"), "overrides reported");
                SettingFieldDescriptor port = s.Describe().Fields.First(f => f.Path == "rest.port");
                Assert.True(port.OverriddenByEnvironment && port.RestartRequired, "port descriptor flags");
            }
            finally
            {
                File.Delete(file);
            }

            return Task.CompletedTask;
        }

        private static Task MaskedAsync(CancellationToken token)
        {
            string file = TempFile();
            try
            {
                SettingsService s = new SettingsService(file, n => null);
                SettingsResponse d = s.Describe();
                Assert.Equal(SettingsService.SecretMask, d.Settings.Auth.SigningKey, "signing key masked");
                Assert.True(d.Fields.First(f => f.Path == "auth.signingKey").IsSet, "signing key reported as set");
                Assert.True(s.Current.Auth.SigningKey != SettingsService.SecretMask, "current value untouched");
            }
            finally
            {
                File.Delete(file);
            }

            return Task.CompletedTask;
        }

        private static async Task UpdateAsync(CancellationToken token)
        {
            string file = TempFile();
            try
            {
                SettingsService s = new SettingsService(file, n => null);
                ServerSettings incoming = s.Describe().Settings;
                incoming.RequestHistory.RetentionDays = 7;
                incoming.Rest.Port = 9001;
                SettingsUpdateResult r = await s.UpdateAsync(incoming, token).ConfigureAwait(false);
                Assert.True(r.Success, "update succeeded");
                Assert.True(r.AppliedLive.Contains("requestHistory.retentionDays"), "retention applied live");
                Assert.True(r.PendingRestart.Contains("rest.port"), "port pending restart");
                Assert.Equal(7, s.Current.RequestHistory.RetentionDays, "live value");
                Assert.Equal(8000, s.Current.Rest.Port, "restart-required value not applied");
                ServerSettings onDisk = Serializer.Deserialize<ServerSettings>(File.ReadAllText(file))!;
                Assert.Equal(9001, onDisk.Rest.Port, "port written to file");
            }
            finally
            {
                File.Delete(file);
            }
        }

        private static async Task MaskPreservedAsync(CancellationToken token)
        {
            string file = TempFile();
            try
            {
                SettingsService s = new SettingsService(file, n => null);
                string original = s.Current.Auth.SigningKey;
                ServerSettings incoming = s.Describe().Settings;
                SettingsUpdateResult r = await s.UpdateAsync(incoming, token).ConfigureAwait(false);
                Assert.True(r.Success && !r.Changed.Contains("auth.signingKey"), "mask not treated as a change");
                Assert.True(r.Changed.Count == 0, "an unchanged round trip changes nothing (lists compare by content): " + String.Join(",", r.Changed));
                Assert.Equal("list", s.Describe().Fields.Single(x => x.Path == "hosting.allowedPrivateHosts").Type, "list settings are described as lists");
                ServerSettings onDisk = Serializer.Deserialize<ServerSettings>(File.ReadAllText(file))!;
                Assert.Equal(original, onDisk.Auth.SigningKey, "file keeps the real secret");
            }
            finally
            {
                File.Delete(file);
            }
        }

        private static async Task OverriddenNotWrittenAsync(CancellationToken token)
        {
            string file = TempFile();
            try
            {
                Dictionary<string, string> env = new Dictionary<string, string> { { "MYMORNINGNEWS_DB_PASSWORD", "from-env" } };
                SettingsService s = new SettingsService(file, n => env.TryGetValue(n, out string? v) ? v : null);
                ServerSettings incoming = s.Describe().Settings;
                incoming.RequestHistory.RetentionDays = 9;
                await s.UpdateAsync(incoming, token).ConfigureAwait(false);
                ServerSettings onDisk = Serializer.Deserialize<ServerSettings>(File.ReadAllText(file))!;
                Assert.True(onDisk.Database.Password != "from-env", "environment password is not persisted");
            }
            finally
            {
                File.Delete(file);
            }
        }

        private static Task ClampAsync(CancellationToken token)
        {
            RequestHistorySettings r = new RequestHistorySettings { RetentionDays = 0, MaxRequestBodyBytes = -5 };
            Assert.Equal(1, r.RetentionDays, "retention clamped to minimum");
            Assert.Equal(0, r.MaxRequestBodyBytes, "body bytes clamped to minimum");
            AuthSettings a = new AuthSettings { SessionLifetimeMinutes = 999999 };
            Assert.Equal(10080, a.SessionLifetimeMinutes, "session lifetime clamped to maximum");
            return Task.CompletedTask;
        }
    }
}
