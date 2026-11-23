namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Vault credentials (CRUD, reveal, usage, validation details) and push devices (register, re-register, list, delete).
    /// Secrets are compared, never printed.
    /// </summary>
    public class VaultDevicesTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Vault credentials and devices";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public VaultDevicesTest()
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
            MyMorningNewsException invalid = await StepAsync(context, result, "POST vault credential without a secret (400)", () => Helpers.ExpectErrorAsync(() => c.Vault.CreateAsync(new VaultCredentialUpsertRequest
            {
                Name = context.Name("vault"),
                Kind = VaultCredentialKindEnum.ApiKey
            }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            Helpers.True(invalid.Details.ContainsKey("secret"), "details name the missing field");

            string secret = "sk-harness-" + Helpers.Unique();
            VaultCredentialResponse v = await StepAsync(context, result, "POST vault credential", () => c.Vault.CreateAsync(new VaultCredentialUpsertRequest
            {
                Name = context.Name("vault"),
                Kind = VaultCredentialKindEnum.ApiKey,
                Provider = VaultProviderEnum.Custom,
                Secret = secret
            }, token)).ConfigureAwait(false);
            context.Cleanup.Add("vault:" + v.Id, ct => c.Vault.DeleteAsync(v.Id, ct));
            VaultCredentialResponse read = await StepAsync(context, result, "GET vault credential", () => c.Vault.GetAsync(v.Id, token)).ConfigureAwait(false);
            Helpers.Equal(v.Id, read.Id, "vault id");
            VaultCredentialResponse updated = await StepAsync(context, result, "PUT vault credential", () => c.Vault.UpdateAsync(v.Id, new VaultCredentialUpsertRequest { Description = "harness" }, token)).ConfigureAwait(false);
            Helpers.Equal("harness", updated.Description, "description");
            VaultCredentialResponse cleared = await StepAsync(context, result, "PUT vault credential (clearFields)", () => c.Vault.UpdateAsync(v.Id, new VaultCredentialUpsertRequest { ClearFields = new List<string> { "description" } }, token)).ConfigureAwait(false);
            Helpers.True(cleared.Description == null, "clearFields removes the description");
            await StepAsync(context, result, "PUT vault credential clearFields=secret (400)", () => Helpers.ExpectErrorAsync(() => c.Vault.UpdateAsync(v.Id, new VaultCredentialUpsertRequest { ClearFields = new List<string> { "secret" } }, token), 400, "ValidationFailed")).ConfigureAwait(false);
            EnumerationResult<VaultCredentialResponse> list = await StepAsync(context, result, "GET vault credentials (kind filter)",
                () => c.Vault.ListAsync(new VaultCredentialListOptions { Kind = VaultCredentialKindEnum.ApiKey, PageSize = 100 }, token)).ConfigureAwait(false);
            Helpers.Contains(list.Items, x => x.Id == v.Id, "the credential");
            VaultRevealResponse revealed = await StepAsync(context, result, "POST reveal", () => c.Vault.RevealAsync(v.Id, token)).ConfigureAwait(false);
            Helpers.True(revealed.Secret == secret, "revealed secret matches");
            List<VaultUsageItem> usage = await StepAsync(context, result, "GET usage", () => c.Vault.GetUsageAsync(v.Id, token)).ConfigureAwait(false);
            Helpers.Equal(0, usage.Count, "unused credential");
            await StepAsync(context, result, "DELETE vault credential", () => c.Vault.DeleteAsync(v.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("vault:" + v.Id);
            await StepAsync(context, result, "GET deleted vault credential (404)", () => Helpers.ExpectErrorAsync(() => c.Vault.GetAsync(v.Id, token), 404)).ConfigureAwait(false);

            await StepAsync(context, result, "POST device with a bad platform (400)", () => Helpers.ExpectErrorAsync(() => c.Devices.RegisterAsync(new DeviceRegisterRequest
            {
                ExpoPushToken = "ExponentPushToken[harness]",
                Platform = "windows"
            }, token), 400)).ConfigureAwait(false);
            string pushToken = "ExponentPushToken[sdk-csharp-" + Helpers.Unique() + "]";
            Device d = await StepAsync(context, result, "POST device", () => c.Devices.RegisterAsync(new DeviceRegisterRequest { ExpoPushToken = pushToken, Platform = "ios", Name = context.Name("device") }, token)).ConfigureAwait(false);
            context.Cleanup.Add("device:" + d.Id, ct => c.Devices.DeleteAsync(d.Id, ct));
            Device again = await StepAsync(context, result, "POST device again (same token)", () => c.Devices.RegisterAsync(new DeviceRegisterRequest { ExpoPushToken = pushToken, Platform = "android" }, token)).ConfigureAwait(false);
            Helpers.Equal(d.Id, again.Id, "re-registering returns the same device");
            List<Device> devices = await StepAsync(context, result, "GET devices", () => c.Devices.ListAsync(token)).ConfigureAwait(false);
            Helpers.Contains(devices, x => x.Id == d.Id && x.Platform == "android", "the device, updated");
            await StepAsync(context, result, "DELETE device", () => c.Devices.DeleteAsync(d.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("device:" + d.Id);
            await StepAsync(context, result, "DELETE deleted device (404)", () => Helpers.ExpectErrorAsync(() => c.Devices.DeleteAsync(d.Id, token), 404)).ConfigureAwait(false);
        }
    }
}
