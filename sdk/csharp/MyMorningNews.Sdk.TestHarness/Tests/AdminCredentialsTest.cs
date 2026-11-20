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
    /// Credentials: create for a new user (secret returned once), log in with the access key, call with direct access-key
    /// headers, read, update, list, scope assignment, effective permissions, delete, and 401 for the deleted key.
    /// </summary>
    public class AdminCredentialsTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Admin credentials and access key login";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AdminCredentialsTest()
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
            string tenant = context.Environment.Tenant;
            UserResponse user = await StepAsync(context, result, "POST user", () => Fixtures.CreateUserAsync(context, c, "Harness-" + Helpers.Unique(), token)).ConfigureAwait(false);

            CredentialCreatedResponse created = await StepAsync(context, result, "POST credential", () => c.Admin.Credentials.CreateAsync(tenant, new CredentialCreateRequest
            {
                UserId = user.Id,
                Name = context.Name("key"),
                AuthMode = CredentialAuthModeEnum.Hybrid
            }, token)).ConfigureAwait(false);
            CredentialResponse cred = created.Credential;
            context.Cleanup.Add("credential:" + cred.Id, ct => c.Admin.Credentials.DeleteAsync(tenant, cred.Id, ct));
            Helpers.NotEmpty(created.SecretKey, "secret key is returned once");
            Helpers.NotEmpty(cred.AccessKey, "access key");
            Helpers.True(!cred.SecretKeyPreview.Contains(created.SecretKey, System.StringComparison.Ordinal), "the preview does not reveal the secret");

            using (MyMorningNewsClient keyClient = context.NewClient())
            {
                TokenResponse login = await StepAsync(context, result, "POST token (access key)", () => keyClient.LoginWithAccessKeyAsync(cred.AccessKey, created.SecretKey, tenant, token)).ConfigureAwait(false);
                Helpers.Equal(cred.Id, login.CredentialId, "credential session");
                TokenDetailsResponse details = await StepAsync(context, result, "GET token/details (credential session)", () => keyClient.GetTokenDetailsAsync(token)).ConfigureAwait(false);
                Helpers.Equal(PrincipalTypeEnum.Credential, details.PrincipalType, "principal type");
                await keyClient.LogoutAsync(token).ConfigureAwait(false);
            }

            using (MyMorningNewsClient direct = context.NewClient())
            {
                direct.AccessKey = cred.AccessKey;
                direct.SecretKey = created.SecretKey;
                direct.TenantId = tenant;
                TokenDetailsResponse details = await StepAsync(context, result, "GET token/details (direct access-key headers)", () => direct.GetTokenDetailsAsync(token)).ConfigureAwait(false);
                Helpers.Equal(cred.Id, details.CredentialId, "direct headers authenticate as the credential");
                direct.SecretKey = "wrong-" + Helpers.Unique();
                await StepAsync(context, result, "GET token/details with a wrong secret (401)", () => Helpers.ExpectErrorAsync(() => direct.GetTokenDetailsAsync(token), 401)).ConfigureAwait(false);
            }

            CredentialResponse read = await StepAsync(context, result, "GET credential", () => c.Admin.Credentials.GetAsync(tenant, cred.Id, token)).ConfigureAwait(false);
            Helpers.Equal(cred.AccessKey, read.AccessKey, "access key read back");
            CredentialResponse updated = await StepAsync(context, result, "PUT credential", () => c.Admin.Credentials.UpdateAsync(tenant, cred.Id, new CredentialUpdateRequest { Name = cred.Name + "-edited" }, token)).ConfigureAwait(false);
            Helpers.Equal(cred.Name + "-edited", updated.Name, "credential name");
            EnumerationResult<CredentialResponse> list = await StepAsync(context, result, "GET credentials (user filter)", () => c.Admin.Credentials.ListAsync(tenant, new CredentialListOptions { UserId = user.Id }, token)).ConfigureAwait(false);
            Helpers.Contains(list.Items, x => x.Id == cred.Id, "the credential");

            CredentialScopeAssignment scope = await StepAsync(context, result, "POST credential scope", () => c.Admin.Assignments.CreateCredentialScopeAsync(tenant, cred.Id, new AssignmentUpsertRequest
            {
                ResourceScope = ResourceScopeEnum.Tenant,
                ResourceTypes = new List<string> { "Topic" },
                OperationTypes = new List<string> { "Read" }
            }, token)).ConfigureAwait(false);
            EnumerationResult<CredentialScopeAssignment> scopes = await StepAsync(context, result, "GET credential scopes", () => c.Admin.Assignments.ListCredentialScopesAsync(tenant, cred.Id, null, token)).ConfigureAwait(false);
            Helpers.Contains(scopes.Items, s => s.Id == scope.Id, "the scope");
            EffectivePermissionsResponse effective = await StepAsync(context, result, "GET credential effective permissions", () => c.Admin.Credentials.GetEffectivePermissionsAsync(tenant, cred.Id, token)).ConfigureAwait(false);
            Helpers.Equal(PrincipalTypeEnum.Credential, effective.PrincipalType, "principal type");
            await StepAsync(context, result, "DELETE credential scope", () => c.Admin.Assignments.DeleteCredentialScopeAsync(tenant, cred.Id, scope.Id, token)).ConfigureAwait(false);

            await StepAsync(context, result, "DELETE credential", () => c.Admin.Credentials.DeleteAsync(tenant, cred.Id, token)).ConfigureAwait(false);
            context.Cleanup.Remove("credential:" + cred.Id);
            using (MyMorningNewsClient gone = context.NewClient())
            {
                await StepAsync(context, result, "POST token with the deleted key (401)", () => Helpers.ExpectErrorAsync(() => gone.LoginWithAccessKeyAsync(cred.AccessKey, created.SecretKey, tenant, token), 401)).ConfigureAwait(false);
            }

            await StepAsync(context, result, "DELETE user", () => Fixtures.DeleteUserAsync(context, c, user.Id, token)).ConfigureAwait(false);
        }
    }
}
