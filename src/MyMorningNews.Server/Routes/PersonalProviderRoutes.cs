namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Audio;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Exceptions;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>Account-owned, write-only API keys and explicit BYOK selection.</summary>
    public class PersonalProviderRoutes : RouteBase
    {
        private readonly MyMorningNews.Core.Services.LoginRateLimiter _ProbeLimiter = new MyMorningNews.Core.Services.LoginRateLimiter(6);
        /// <summary>Construct the account provider routes.</summary>
        public PersonalProviderRoutes(ServerContext server) : base(server) { }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            const string path = "/v1.0/api/account/providers";
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, path, ListAsync, null, openApiMetadata: Doc("My private provider connections", "Account providers", "Returns masked keys only. Connections are isolated even between users in the same tenant."));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.PUT, path, SaveAsync, null, openApiMetadata: Doc("Connect or rotate my model or speech API key", "Account providers", "One connection per kind. API keys are write-only and encrypted. Saving clears prior validation.").WithRequestBody(Body<ProviderConnectionRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, path + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Remove my provider connection", "Account providers", "BYOK requests fail closed after removal; platform keys are never a fallback."));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, path + "/{id}/test", TestAsync, null, openApiMetadata: Doc("Test my private model or speech connection", "Account providers", "Makes a fixed small provider-funded probe, up to six per account per minute. Success marks only the unchanged connection as validated."));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.PUT, "/v1.0/api/account/funding", FundingAsync, null, openApiMetadata: Doc("Choose BYOK or my assigned package", "Account providers").WithRequestBody(Body<FundingModeRequest>()));
        }

        private Task ListAsync(HttpContextBase ctx) => GuardAsync(ctx, async () =>
        {
            await SendJsonAsync(ctx, 200, new ProviderConnectionsResponse
            {
                Connections = await Server.PersonalProviders.ListAsync(Context(ctx).TenantId!, Context(ctx).UserId!, ctx.Token).ConfigureAwait(false),
                LocalSpeechAvailable = Server.Settings.Tts.Enabled && Server.Settings.Tts.AllowLocalForByok,
                Plan = await Server.Entitlements.GetEffectiveAsync(Context(ctx).TenantId!, Context(ctx).UserId!, ctx.Token).ConfigureAwait(false)
            }).ConfigureAwait(false);
        });

        private Task SaveAsync(HttpContextBase ctx) => GuardAsync(ctx, async () =>
        {
            ProviderConnectionRequest? request = await ReadBodyAsync<ProviderConnectionRequest>(ctx).ConfigureAwait(false);
            if (request != null) await SendJsonAsync(ctx, 200, await Server.PersonalProviders.SaveAsync(Context(ctx).TenantId!, Context(ctx).UserId!, request, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        });

        private Task DeleteAsync(HttpContextBase ctx) => GuardAsync(ctx, async () =>
        {
            await Server.PersonalProviders.DeleteAsync(Context(ctx).TenantId!, Context(ctx).UserId!, ctx.Request.Url.Parameters["id"] ?? String.Empty, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, new { deleted = true }).ConfigureAwait(false);
        });

        private Task TestAsync(HttpContextBase ctx) => GuardAsync(ctx, async () =>
        {
            ProviderConnection connection = await Server.PersonalProviders.ReadAsync(Context(ctx).TenantId!, Context(ctx).UserId!, ctx.Request.Url.Parameters["id"] ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (!_ProbeLimiter.TryAcquire(connection.TenantId + ":" + connection.UserId))
            {
                await SendErrorAsync(ctx, 429, "TooManyRequests", "Too many connection tests. Try again in a minute.").ConfigureAwait(false);
                return;
            }
            if (connection.Kind == "Speech")
                await Server.Audio.ProbePersonalSpeechAsync(await Server.PersonalProviders.SpeechProbeAsync(connection.TenantId, connection.UserId, connection.Id, ctx.Token).ConfigureAwait(false), ctx.Token).ConfigureAwait(false);
            else await Server.Models.ProbePersonalAsync(connection.TenantId, connection.UserId, connection.Id, connection.Kind == "Embedding", ctx.Token).ConfigureAwait(false);
            bool unchanged = await Server.Database.ProviderConnections.MarkValidatedAsync(connection, ctx.Token).ConfigureAwait(false);
            if (!unchanged) throw new InvalidOperationException("The connection changed during the test. Test the updated connection again.");
            await SendJsonAsync(ctx, 200, new { validated = true }).ConfigureAwait(false);
        });

        private Task FundingAsync(HttpContextBase ctx) => GuardAsync(ctx, async () =>
        {
            FundingModeRequest? request = await ReadBodyAsync<FundingModeRequest>(ctx).ConfigureAwait(false);
            if (request != null) await SendJsonAsync(ctx, 200, await Server.PersonalProviders.SetFundingAsync(Context(ctx).TenantId!, Context(ctx).UserId!, request.Mode, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        });

        private async Task GuardAsync(HttpContextBase ctx, Func<Task> action)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.PromoCodeRedeem).ConfigureAwait(false)) return;
            try { await action().ConfigureAwait(false); }
            catch (KeyNotFoundException) { await SendErrorAsync(ctx, 404, "NotFound", "Provider connection not found.").ConfigureAwait(false); }
            catch (ModelCallException) { await SendErrorAsync(ctx, 502, "ProviderConnectionFailed", "The provider call failed. Check your model, URL and API key.").ConfigureAwait(false); }
            catch (TtsException) { await SendErrorAsync(ctx, 502, "ProviderConnectionFailed", "The speech call failed. Check your model, voice, URL and API key.").ConfigureAwait(false); }
            catch (ArgumentException ex) { await SendErrorAsync(ctx, 400, "ValidationFailed", ex.Message).ConfigureAwait(false); }
            catch (InvalidOperationException ex) { await SendErrorAsync(ctx, 409, "ProviderConnectionConflict", ex.Message).ConfigureAwait(false); }
        }
    }
}
