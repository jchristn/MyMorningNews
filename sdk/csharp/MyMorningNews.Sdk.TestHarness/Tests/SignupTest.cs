namespace MyMorningNews.Sdk.TestHarness.Tests
{
    using System;
    using System.Linq;
    using System.Text.Json.Nodes;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Sdk.Enums;
    using MyMorningNews.Sdk.Models;
    using MyMorningNews.Sdk.Options;
    using MyMorningNews.Sdk.Requests;

    /// <summary>
    /// Public sign-up, email verification, and password reset. Sign-up is off by default, so the case checks the 404, then
    /// turns Hosting.PublicSignupEnabled (and email verification) on through the settings API as the administrator and
    /// restores the original settings afterward. It signs up, checks the typed EmailNotVerifiedException at sign-in, reads
    /// the verification and reset links from the personal tenant's outbox through an administrator-made access key,
    /// verifies, resets the password, and deletes the personal tenant. Bad links answer 400 with the link code: LinkInvalid for
    /// a token the server never issued or one issued for the other purpose, LinkUsed for a link used before; a weak
    /// password is ValidationFailed.
    /// </summary>
    public class SignupTest : TestBase
    {
        /// <summary>
        /// Case name.
        /// </summary>
        public override string Name => "Public sign-up, EmailNotVerified, verify, resend, forgot, reset";

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SignupTest()
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
            SettingsResponse settings = await StepAsync(context, result, "GET settings", () => c.Admin.Settings.GetAsync(token)).ConfigureAwait(false);
            JsonObject original = (JsonObject)settings.Settings.DeepClone();
            JsonObject? hosting = settings.Settings["hosting"] as JsonObject;
            if (hosting == null) throw new SkipTestException("the server's settings have no hosting group");
            if (settings.Fields.Any(f => f.Path == "hosting.publicSignupEnabled" && f.OverriddenByEnvironment))
                throw new SkipTestException("Hosting.PublicSignupEnabled is set by the environment, so the harness cannot toggle it");

            string unique = Helpers.Unique();
            string email = "sdk-csharp-signup-" + unique + "@example.com";
            string display = context.Environment.Prefix + "signup-" + unique;
            string password = "Harness signup " + unique;
            SignupRequest signup = new SignupRequest { Email = email, Password = password, DisplayName = display, TimeZone = "UTC", AcceptTerms = true, ConfirmAge = true };

            using (MyMorningNewsClient anon = context.NewClient())
            {
                bool wasOpen = hosting["publicSignupEnabled"]?.GetValue<bool>() ?? false;
                if (!wasOpen)
                    await StepAsync(context, result, "POST public/signup while closed (404)", () => Helpers.ExpectErrorAsync(() => anon.Account.SignUpAsync(signup, token), 404, "SignupClosed")).ConfigureAwait(false);

                hosting["publicSignupEnabled"] = true;
                hosting["requireEmailVerification"] = true;
                hosting["signupsPerIpPerHour"] = 1000;
                context.Cleanup.Add("settings:signup", ct => c.Admin.Settings.UpdateAsync(original, ct));
                SettingsUpdateResult opened = await StepAsync(context, result, "PUT settings (open sign-up)", () => c.Admin.Settings.UpdateAsync(settings.Settings, token)).ConfigureAwait(false);
                Helpers.True(opened.Success, "settings accepted");
                string? tenantId = null;
                try
                {
                    SignupResponse first = await StepAsync(context, result, "POST public/signup (202)", () => anon.Account.SignUpAsync(signup, token)).ConfigureAwait(false);
                    Helpers.True(first.VerificationRequired, "verification required");
                    Helpers.NotEmpty(first.Message, "message");
                    SignupResponse again = await StepAsync(context, result, "POST public/signup again looks the same", () => anon.Account.SignUpAsync(signup, token)).ConfigureAwait(false);
                    Helpers.Equal(first.Message, again.Message, "same answer for an existing account");
                    await StepAsync(context, result, "POST public/signup without terms (400)", () => Helpers.ExpectErrorAsync(() => anon.Account.SignUpAsync(new SignupRequest { Email = "x" + email, Password = password, ConfirmAge = true }, token), 400, "ValidationFailed")).ConfigureAwait(false);

                    MyMorningNewsException blocked = await StepAsync(context, result, "login before verifying (403 EmailNotVerified)", () => Helpers.ExpectErrorAsync(() => anon.LoginAsync(email, password, null, token), 403, "EmailNotVerified")).ConfigureAwait(false);
                    Helpers.True(blocked is EmailNotVerifiedException, "403 EmailNotVerified is an EmailNotVerifiedException, got " + blocked.GetType().Name);
                    Helpers.True(anon.Token == null, "no session after a refused sign-in");

                    AccountMessageResponse resend = await StepAsync(context, result, "POST public/verify-email/resend (202)", () => anon.Account.ResendVerificationAsync(email, token)).ConfigureAwait(false);
                    Helpers.NotEmpty(resend.Message, "resend message");
                    await StepAsync(context, result, "POST public/verify-email/resend for nobody (202)", () => anon.Account.ResendVerificationAsync("nobody-" + unique + "@example.com", token)).ConfigureAwait(false);
                    await StepAsync(context, result, "POST public/password/forgot for nobody (202)", () => anon.Account.ForgotPasswordAsync("nobody-" + unique + "@example.com", token)).ConfigureAwait(false);
                    await StepAsync(context, result, "POST public/verify-email bad token (400 LinkInvalid)", () => Helpers.ExpectErrorAsync(() => anon.Account.VerifyEmailAsync("not-a-token", token), 400, "LinkInvalid")).ConfigureAwait(false);
                    await StepAsync(context, result, "POST public/password/reset bad token (400 LinkInvalid)", () => Helpers.ExpectErrorAsync(() => anon.Account.ResetPasswordAsync("not-a-token", "a long new password", token), 400, "LinkInvalid")).ConfigureAwait(false);

                    EnumerationResult<Tenant> tenants = await StepAsync(context, result, "GET tenants (find the personal tenant)", () => c.Admin.Tenants.ListAsync(new TenantListOptions { PageSize = 1000 }, token)).ConfigureAwait(false);
                    Tenant? personal = tenants.Items.FirstOrDefault(t => t.Name == display + " (personal)");
                    if (personal == null) throw new SkipTestException("could not find the personal tenant in the first 1000 tenants");
                    tenantId = personal.Id;
                    string tid = personal.Id;
                    context.Cleanup.Add("tenant:" + tid, ct => c.Admin.Tenants.DeleteAsync(tid, ct));
                    Helpers.True(personal.IsPersonal, "personal tenant");

                    EnumerationResult<UserResponse> users = await StepAsync(context, result, "GET personal tenant users", () => c.Admin.Users.ListAsync(tid, null, token)).ConfigureAwait(false);
                    UserResponse signedUp = users.Items.Single(u => u.Email == email);
                    CredentialCreatedResponse key = await StepAsync(context, result, "POST credential for the new user", () => c.Admin.Credentials.CreateAsync(tid, new CredentialCreateRequest
                    {
                        UserId = signedUp.Id,
                        Name = context.Name("outbox"),
                        AuthMode = CredentialAuthModeEnum.Hybrid
                    }, token)).ConfigureAwait(false);

                    using (MyMorningNewsClient outbox = new MyMorningNewsClient(context.Environment.BaseUrl) { AccessKey = key.Credential.AccessKey, SecretKey = key.SecretKey, TenantId = tid })
                    {
                        string verifyToken = await StepAsync(context, result, "read the verification link", () => LinkTokenAsync(outbox, EmailTemplateEnum.VerifyEmail, token)).ConfigureAwait(false);
                        EmailVerifiedResponse verified = await StepAsync(context, result, "POST public/verify-email", () => anon.Account.VerifyEmailAsync(verifyToken, token)).ConfigureAwait(false);
                        Helpers.True(verified.Verified, "verified");
                        Helpers.Equal(email, verified.Email, "verified email");
                        await StepAsync(context, result, "POST public/verify-email again (400 LinkUsed)", () => Helpers.ExpectErrorAsync(() => anon.Account.VerifyEmailAsync(verifyToken, token), 400, "LinkUsed")).ConfigureAwait(false);

                        TokenResponse session = await StepAsync(context, result, "login after verifying", () => anon.LoginAsync(email, password, null, token)).ConfigureAwait(false);
                        Helpers.Equal(tid, session.TenantId, "signed in to the personal tenant");
                        UsageMeter meter = await StepAsync(context, result, "GET account/usage (new account)", () => anon.Account.GetUsageAsync(token)).ConfigureAwait(false);
                        Helpers.NotEmpty(meter.PlanKey, "plan key");
                        Helpers.Equal(0L, meter.Topics, "no topics yet");
                        await anon.LogoutAsync(token).ConfigureAwait(false);

                        AccountMessageResponse forgot = await StepAsync(context, result, "POST public/password/forgot (202)", () => anon.Account.ForgotPasswordAsync(email, token)).ConfigureAwait(false);
                        Helpers.NotEmpty(forgot.Message, "forgot message");
                        string resetToken = await StepAsync(context, result, "read the reset link", () => LinkTokenAsync(outbox, EmailTemplateEnum.PasswordReset, token)).ConfigureAwait(false);
                        await StepAsync(context, result, "POST public/password/reset with the verification link (400 LinkInvalid)", () => Helpers.ExpectErrorAsync(() => anon.Account.ResetPasswordAsync(verifyToken, "a long new password", token), 400, "LinkInvalid")).ConfigureAwait(false);
                        await StepAsync(context, result, "POST public/password/reset weak password (400 ValidationFailed)", () => Helpers.ExpectErrorAsync(() => anon.Account.ResetPasswordAsync(resetToken, "short", token), 400, "ValidationFailed")).ConfigureAwait(false);
                        string newPassword = password + " renewed";
                        PasswordResetResponse reset = await StepAsync(context, result, "POST public/password/reset", () => anon.Account.ResetPasswordAsync(resetToken, newPassword, token)).ConfigureAwait(false);
                        Helpers.True(reset.Reset, "reset");
                        await StepAsync(context, result, "POST public/password/reset again (400 LinkUsed)", () => Helpers.ExpectErrorAsync(() => anon.Account.ResetPasswordAsync(resetToken, newPassword + " again", token), 400, "LinkUsed")).ConfigureAwait(false);
                        await StepAsync(context, result, "login with the old password (401)", () => Helpers.ExpectErrorAsync(() => anon.LoginAsync(email, password, null, token), 401)).ConfigureAwait(false);
                        await StepAsync(context, result, "login with the new password", () => anon.LoginAsync(email, newPassword, null, token)).ConfigureAwait(false);
                        await anon.LogoutAsync(token).ConfigureAwait(false);
                    }

                    await StepAsync(context, result, "DELETE personal tenant", () => c.Admin.Tenants.DeleteAsync(tid, token)).ConfigureAwait(false);
                    context.Cleanup.Remove("tenant:" + tid);
                }
                finally
                {
                    await c.Admin.Settings.UpdateAsync(original, CancellationToken.None).ConfigureAwait(false);
                    context.Cleanup.Remove("settings:signup");
                }

                if (!wasOpen)
                    await StepAsync(context, result, "POST public/signup after restoring (404)", () => Helpers.ExpectErrorAsync(() => anon.Account.SignUpAsync(signup, token), 404, "SignupClosed")).ConfigureAwait(false);
                Helpers.True(tenantId != null, "the personal tenant was found");
            }
        }

        private static async Task<string> LinkTokenAsync(MyMorningNewsClient outbox, EmailTemplateEnum template, CancellationToken token)
        {
            EnumerationResult<EmailMessage> mail = await outbox.Email.ListAsync(new EmailMessageListOptions { Template = template, PageSize = 20 }, token).ConfigureAwait(false);
            EmailMessage? latest = mail.Items.OrderByDescending(m => m.CreatedUtc).FirstOrDefault();
            if (latest == null) throw new HarnessAssertionException("no " + template + " email in the personal tenant's outbox");
            EmailMessage full = await outbox.Email.GetAsync(latest.Id, token).ConfigureAwait(false);
            Match match = Regex.Match(full.RenderedText ?? String.Empty, "token=([A-Za-z0-9%_\\-\\.~]+)");
            if (!match.Success) throw new HarnessAssertionException("the " + template + " email carries no link token");
            return Uri.UnescapeDataString(match.Groups[1].Value);
        }
    }
}
