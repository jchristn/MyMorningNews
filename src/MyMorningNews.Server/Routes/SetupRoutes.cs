namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Workspace setup: what the workspace still needs (a working model key, web search) and live checks that prove it.
    /// </summary>
    public class SetupRoutes : RouteBase
    {
        private const string B = "/v1.0/api/setup";

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public SetupRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, B, StatusAsync, null, openApiMetadata: Doc("Workspace setup status", "Setup",
                "Whether the workspace has a chat model with a working key and web search. ready is false until every required check is Ready; the apps ask administrators to finish setup first, and let them skip it. Administrators also get the optional checks: embeddings, email, and speech (the text to speech endpoint, with its model and default voice)."));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, B + "/verify", VerifyAsync, null, openApiMetadata: Doc("Re-check setup with live calls", "Setup",
                "Administrators only. Sends a short probe prompt to each chat endpoint, a test query to each default web search provider, one embedding request, and one short speech request. check limits the live calls to one kind (Model, Search, Embeddings, or Speech); the other checks are reported from stored results.")
                .WithParameter(QueryParam("check", typeof(SetupCheckKindEnum))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, B + "/options", OptionsAsync, null, openApiMetadata: Doc("Model providers and sign-in methods", "Setup",
                "Administrators only. Hosted APIs (API key), agent CLIs in the server image (Claude Code with a claude setup-token token, an Anthropic API key, or the server's sign-in; Codex with auth.json, an OpenAI API key, or the server's sign-in; Gemini CLI with oauth_creds.json, a Gemini API key, or the server's sign-in; Mux with endpoints.json), and self-hosted servers, with whether each is installed and allowed."));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, B + "/connect", ConnectAsync, null, openApiMetadata: Doc("Connect the workspace's chat model", "Setup",
                "Administrators only. Stores the secret for the chosen sign-in method encrypted in the vault, points every chat task at the endpoint, and verifies it with a live call. The secret is never returned.")
                .WithRequestBody(Body<SetupConnectRequest>()));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, B + "/model-key", ModelKeyAsync, null, openApiMetadata: Doc("Store and verify a model API key", "Setup",
                "Administrators only. Stores the key encrypted in the vault, attaches it to the endpoint, and verifies it with a live call. The key is never returned.")
                .WithRequestBody(Body<SetupModelKeyRequest>()));
        }

        private async Task StatusAsync(HttpContextBase ctx)
        {
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            if (!Context(ctx).IsAdmin && !String.IsNullOrEmpty(Context(ctx).UserId))
            {
                MyMorningNews.Core.Entitlements.EffectivePlan plan = await Server.Entitlements.GetEffectiveAsync(tenantId, Context(ctx).UserId!, ctx.Token).ConfigureAwait(false);
                if (plan.FundingMode == "Personal")
                {
                    bool connected = (await Server.PersonalProviders.ListAsync(tenantId, Context(ctx).UserId!, ctx.Token).ConfigureAwait(false)).Any(c => c.Kind == "Chat" && c.Active && c.ValidatedUtc.HasValue);
                    await SendJsonAsync(ctx, 200, new SetupStatusResponse { Ready = connected, CanConfigure = false, Checks = new List<SetupCheck> { new SetupCheck { Key = "personal:chat", Kind = SetupCheckKindEnum.Model, Required = true, State = connected ? SetupCheckStateEnum.Ready : SetupCheckStateEnum.Missing, Message = "Manage and test your personal model key in Account > Your API keys." } } }).ConfigureAwait(false);
                    return;
                }
                if (!MyMorningNews.Core.Entitlements.EntitlementService.Allowed(plan, "customResearch.enabled")
                    && !MyMorningNews.Core.Entitlements.EntitlementService.Allowed(plan, "oneTimeResearch.enabled"))
                {
                    await SendJsonAsync(ctx, 200, new SetupStatusResponse { Ready = true, CanConfigure = false }).ConfigureAwait(false);
                    return;
                }
            }
            await SendJsonAsync(ctx, 200, await Server.Setup.GetStatusAsync(tenantId, CanConfigure(ctx), ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task VerifyAsync(HttpContextBase ctx)
        {
            string? tenantId = await AdminTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            await SendJsonAsync(ctx, 200, await Server.Setup.VerifyAsync(tenantId, QueryEnum<SetupCheckKindEnum>(ctx, "check"), ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task OptionsAsync(HttpContextBase ctx)
        {
            string? tenantId = await AdminTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            await SendJsonAsync(ctx, 200, Server.Setup.GetOptions(Context(ctx).IsAdmin)).ConfigureAwait(false);
        }

        private async Task ConnectAsync(HttpContextBase ctx)
        {
            string? tenantId = await AdminTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            SetupConnectRequest? req = await ReadBodyAsync<SetupConnectRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            RequestContext rc = Context(ctx);
            SetupStatusResponse status = await Server.Setup.ConnectAsync(tenantId, rc.UserId, req, rc.IsAdmin, ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.VaultCredentialChanged, rc, 200, "Chat model connected on the Setup page: " + req.Provider + " (" + (req.AuthMethod?.ToString() ?? "recommended sign-in") + ").", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, status).ConfigureAwait(false);
        }

        private async Task ModelKeyAsync(HttpContextBase ctx)
        {
            string? tenantId = await AdminTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            SetupModelKeyRequest? req = await ReadBodyAsync<SetupModelKeyRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            RequestContext rc = Context(ctx);
            SetupStatusResponse status = await Server.Setup.SetModelKeyAsync(tenantId, rc.UserId, req, ctx.Token).ConfigureAwait(false);
            await Server.Audit.RecordAsync(AuditEventTypeEnum.VaultCredentialChanged, rc, 200, "Model key stored for endpoint " + req.ModelEndpointId + " on the Setup page.", ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, status).ConfigureAwait(false);
        }

        private async Task<string?> AdminTenantAsync(HttpContextBase ctx)
        {
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            if (!CanConfigure(ctx))
            {
                await SendErrorAsync(ctx, 403, "Forbidden", "Only an administrator can change the workspace setup.").ConfigureAwait(false);
                return null;
            }

            return tenantId;
        }

        private bool CanConfigure(HttpContextBase ctx)
        {
            RequestContext rc = Context(ctx);
            return rc.IsAdmin || rc.IsTenantAdmin;
        }
    }
}
