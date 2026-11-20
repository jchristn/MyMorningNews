namespace MyMorningNews.Server.Routes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Requests;
    using MyMorningNews.Core.Responses;
    using MyMorningNews.Core.Security;
    using WatsonWebserver;
    using WatsonWebserver.Core;

    /// <summary>
    /// Topics with their dimensions, constraints, and recipients (plan sections 10.3 and 13). Members work with their own
    /// topics; tenant administrators see every topic in the tenant.
    /// </summary>
    public class TopicRoutes : RouteBase
    {
        private const string B = "/v1.0/api/topics";

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="server">Server context.</param>
        public TopicRoutes(ServerContext server) : base(server)
        {
        }

        /// <inheritdoc />
        public override void Register(Webserver webserver)
        {
            ArgumentNullException.ThrowIfNull(webserver);
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.GET, B, ListAsync, null, openApiMetadata: Paged(Doc("List topics", "Topics", "Members see their own topics; tenant administrators see all unless userId is given.")
                .WithParameter(QueryParam("status", typeof(TopicStatusEnum))).WithParameter(QueryParam("isOneTime", typeof(bool))).WithParameter(QueryParam("userId", typeof(string)))));
            webserver.Routes.PostAuthentication.Static.Add(HttpMethod.POST, B, CreateAsync, null, openApiMetadata: Doc("Create a topic without the wizard", "Topics").WithRequestBody(Body<TopicUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, B + "/{id}", ReadAsync, null, openApiMetadata: Doc("Read a topic with dimensions, constraints, and recipients", "Topics"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, B + "/{id}", UpdateAsync, null, openApiMetadata: Doc("Update a topic", "Topics").WithRequestBody(Body<TopicUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, B + "/{id}", DeleteAsync, null, openApiMetadata: Doc("Delete a topic and everything under it", "Topics"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/pause", PauseAsync, null, openApiMetadata: Doc("Pause a topic", "Topics"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/resume", ResumeAsync, null, openApiMetadata: Doc("Resume a paused or archived topic", "Topics"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/archive", ArchiveAsync, null, openApiMetadata: Doc("Archive a topic", "Topics"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/duplicate", DuplicateAsync, null, openApiMetadata: Doc("Duplicate a topic as a draft", "Topics"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/refine", RefineAsync, null, openApiMetadata: Doc("Refine a topic again (starts a wizard session)", "Topics"));

            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, B + "/{id}/dimensions", ListDimensionsAsync, null, openApiMetadata: Doc("List dimensions", "Topics"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/dimensions", CreateDimensionAsync, null, openApiMetadata: Doc("Add a dimension", "Topics").WithRequestBody(Body<DimensionUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, B + "/{id}/dimensions/{dimensionId}", UpdateDimensionAsync, null, openApiMetadata: Doc("Update a dimension (it becomes yours and is kept on regeneration)", "Topics").WithRequestBody(Body<DimensionUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, B + "/{id}/dimensions/{dimensionId}", DeleteDimensionAsync, null, openApiMetadata: Doc("Delete a dimension", "Topics"));

            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, B + "/{id}/constraints", ListConstraintsAsync, null, openApiMetadata: Doc("List constraints", "Topics").WithParameter(QueryParam("kind", typeof(ConstraintKindEnum))));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/constraints", CreateConstraintAsync, null, openApiMetadata: Doc("Add a constraint", "Topics").WithRequestBody(Body<ConstraintUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.PUT, B + "/{id}/constraints/{constraintId}", UpdateConstraintAsync, null, openApiMetadata: Doc("Update a constraint", "Topics").WithRequestBody(Body<ConstraintUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, B + "/{id}/constraints/{constraintId}", DeleteConstraintAsync, null, openApiMetadata: Doc("Delete a constraint", "Topics"));

            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.GET, B + "/{id}/recipients", ListRecipientsAsync, null, openApiMetadata: Doc("List email recipients", "Topics"));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.POST, B + "/{id}/recipients", AddRecipientAsync, null, openApiMetadata: Doc("Add an email recipient", "Topics").WithRequestBody(Body<RecipientUpsertRequest>()));
            webserver.Routes.PostAuthentication.Parameter.Add(HttpMethod.DELETE, B + "/{id}/recipients/{recipientId}", DeleteRecipientAsync, null, openApiMetadata: Doc("Remove an email recipient", "Topics"));
        }

        private async Task ListAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicRead).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            TopicFilter filter = new TopicFilter { TenantId = tenantId, UserId = ListScope(ctx), Status = QueryEnum<TopicStatusEnum>(ctx, "status"), IsOneTime = QueryBool(ctx, "isOneTime") };
            ApplyPaging(ctx, filter);
            await SendJsonAsync(ctx, 200, await Server.Database.Topics.EnumerateAsync(filter, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return;
            RequestContext rc = Context(ctx);
            if (String.IsNullOrEmpty(rc.UserId))
            {
                await SendErrorAsync(ctx, 400, "BadRequest", "Topics belong to a signed-in user.").ConfigureAwait(false);
                return;
            }

            TopicUpsertRequest? req = await ReadBodyAsync<TopicUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (String.IsNullOrWhiteSpace(req.Name))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "name is required.").ConfigureAwait(false);
                return;
            }

            Topic topic = await Server.Refinement.CreateTopicAsync(tenantId, rc.UserId, req.Name, req.Description, false, ctx.Token).ConfigureAwait(false);
            Apply(topic, req);
            topic = await Server.Database.Topics.UpdateAsync(topic, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, topic).ConfigureAwait(false);
        }

        private async Task ReadAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicRead).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            await SendJsonAsync(ctx, 200, await DetailAsync(topic, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task UpdateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            TopicUpsertRequest? req = await ReadBodyAsync<TopicUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            string oldName = topic.Name;
            Apply(topic, req);
            if (!String.Equals(oldName, topic.Name, StringComparison.Ordinal)) topic.Slug = await Server.Refinement.UniqueSlugAsync(topic.TenantId, topic.Name, ctx.Token).ConfigureAwait(false);
            topic = await Server.Database.Topics.UpdateAsync(topic, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, topic).ConfigureAwait(false);
        }

        private async Task DeleteAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicDelete).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            await Server.Briefings.DeleteTopicFilesAsync(topic.TenantId, topic.Id, ctx.Token).ConfigureAwait(false);
            await Server.Database.Topics.DeleteCascadeAsync(topic.TenantId, topic.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private Task PauseAsync(HttpContextBase ctx)
        {
            return SetStatusAsync(ctx, TopicStatusEnum.Paused);
        }

        private Task ResumeAsync(HttpContextBase ctx)
        {
            return SetStatusAsync(ctx, TopicStatusEnum.Active);
        }

        private Task ArchiveAsync(HttpContextBase ctx)
        {
            return SetStatusAsync(ctx, TopicStatusEnum.Archived);
        }

        private async Task SetStatusAsync(HttpContextBase ctx, TopicStatusEnum status)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            if (topic.Status == TopicStatusEnum.Draft && status == TopicStatusEnum.Active && String.IsNullOrWhiteSpace(topic.Brief))
            {
                await SendErrorAsync(ctx, 409, "Conflict", "Finish refining the topic (it has no brief yet) before activating it.").ConfigureAwait(false);
                return;
            }

            topic.Status = status;
            topic = await Server.Database.Topics.UpdateAsync(topic, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, topic).ConfigureAwait(false);
        }

        private async Task DuplicateAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            Topic? source = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (source == null) return;
            RequestContext rc = Context(ctx);
            Topic copy = await Server.Refinement.CreateTopicAsync(source.TenantId, rc.UserId ?? source.UserId, source.Name + " (copy)", source.Description, false, ctx.Token).ConfigureAwait(false);
            copy.Brief = source.Brief;
            copy.AudienceLevel = source.AudienceLevel;
            copy.Horizon = source.Horizon;
            copy.Language = source.Language;
            copy.NoveltyPreference = source.NoveltyPreference;
            copy.EmailOnRun = source.EmailOnRun;
            copy.EmailWhenEmpty = source.EmailWhenEmpty;
            copy.MaxEmailFindings = source.MaxEmailFindings;
            copy.DisplayThreshold = source.DisplayThreshold;
            copy.SeedQueries = new List<string>(source.SeedQueries);
            copy = await Server.Database.Topics.UpdateAsync(copy, ctx.Token).ConfigureAwait(false);
            Dictionary<string, string> dimensionMap = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (TopicDimension d in await Server.Refinement.DimensionsAsync(source.TenantId, source.Id, ctx.Token).ConfigureAwait(false))
            {
                TopicDimension created = await Server.Database.TopicDimensions.CreateAsync(new TopicDimension
                {
                    TenantId = copy.TenantId, TopicId = copy.Id, Name = d.Name, Description = d.Description, Rationale = d.Rationale, Weight = d.Weight,
                    IncludeTerms = new List<string>(d.IncludeTerms), ExcludeTerms = new List<string>(d.ExcludeTerms), IsLocked = d.IsLocked, Origin = d.Origin, SortOrder = d.SortOrder
                }, ctx.Token).ConfigureAwait(false);
                dimensionMap[d.Id] = created.Id;
            }

            foreach (TopicConstraint c in await Server.Refinement.ConstraintsAsync(source.TenantId, source.Id, ctx.Token).ConfigureAwait(false))
            {
                await Server.Database.TopicConstraints.CreateAsync(new TopicConstraint
                {
                    TenantId = copy.TenantId, TopicId = copy.Id, Kind = c.Kind, Value = c.Value, Note = c.Note, IsLocked = c.IsLocked, Origin = c.Origin,
                    DimensionId = c.DimensionId != null && dimensionMap.TryGetValue(c.DimensionId, out string? mapped) ? mapped : null
                }, ctx.Token).ConfigureAwait(false);
            }

            await SendJsonAsync(ctx, 201, await DetailAsync(copy, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task RefineAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.RefinementWrite).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            RequestContext rc = Context(ctx);
            RefinementStateResponse state = await Server.Refinement.StartForTopicAsync(topic.TenantId, rc.UserId ?? topic.UserId, topic, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, state).ConfigureAwait(false);
        }

        private async Task ListDimensionsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicRead).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            await SendJsonAsync(ctx, 200, await Server.Refinement.DimensionsAsync(topic.TenantId, topic.Id, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task CreateDimensionAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            DimensionUpsertRequest? req = await ReadBodyAsync<DimensionUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (String.IsNullOrWhiteSpace(req.Name))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "name is required.").ConfigureAwait(false);
                return;
            }

            List<TopicDimension> existing = await Server.Refinement.DimensionsAsync(topic.TenantId, topic.Id, ctx.Token).ConfigureAwait(false);
            TopicDimension d = new TopicDimension { TenantId = topic.TenantId, TopicId = topic.Id, Origin = OriginEnum.User, SortOrder = existing.Count == 0 ? 0 : existing.Max(x => x.SortOrder) + 1 };
            Apply(d, req);
            d = await Server.Database.TopicDimensions.CreateAsync(d, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, d).ConfigureAwait(false);
        }

        private async Task UpdateDimensionAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            TopicDimension? d = await Server.Database.TopicDimensions.ReadAsync(topic.TenantId, Param(ctx, "dimensionId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (d == null || d.TopicId != topic.Id) { await SendNotFoundAsync(ctx, "Dimension").ConfigureAwait(false); return; }
            DimensionUpsertRequest? req = await ReadBodyAsync<DimensionUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            Apply(d, req);
            if (req.Name != null || req.Description != null || req.Weight.HasValue || req.IncludeTerms != null || req.ExcludeTerms != null) d.Origin = OriginEnum.User;
            d = await Server.Database.TopicDimensions.UpdateAsync(d, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, d).ConfigureAwait(false);
        }

        private async Task DeleteDimensionAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            TopicDimension? d = await Server.Database.TopicDimensions.ReadAsync(topic.TenantId, Param(ctx, "dimensionId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (d == null || d.TopicId != topic.Id) { await SendNotFoundAsync(ctx, "Dimension").ConfigureAwait(false); return; }
            await Server.Database.TopicDimensions.DeleteAsync(topic.TenantId, d.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task ListConstraintsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicRead).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            ConstraintKindEnum? kind = QueryEnum<ConstraintKindEnum>(ctx, "kind");
            List<TopicConstraint> items = await Server.Refinement.ConstraintsAsync(topic.TenantId, topic.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, kind.HasValue ? items.Where(c => c.Kind == kind.Value).ToList() : items).ConfigureAwait(false);
        }

        private async Task CreateConstraintAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            ConstraintUpsertRequest? req = await ReadBodyAsync<ConstraintUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            if (!req.Kind.HasValue || String.IsNullOrWhiteSpace(req.Value))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "kind and value are required.").ConfigureAwait(false);
                return;
            }

            string? error = await ValidateConstraintAsync(topic, req.Kind.Value, req.Value, req.DimensionId, ctx).ConfigureAwait(false);
            if (error != null) { await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false); return; }
            TopicConstraint c = await Server.Database.TopicConstraints.CreateAsync(new TopicConstraint
            {
                TenantId = topic.TenantId, TopicId = topic.Id, Kind = req.Kind.Value, Value = req.Value.Trim(), Note = req.Note, DimensionId = req.DimensionId,
                IsLocked = req.IsLocked ?? false, Origin = OriginEnum.User
            }, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, c).ConfigureAwait(false);
        }

        private async Task UpdateConstraintAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            TopicConstraint? c = await Server.Database.TopicConstraints.ReadAsync(topic.TenantId, Param(ctx, "constraintId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (c == null || c.TopicId != topic.Id) { await SendNotFoundAsync(ctx, "Constraint").ConfigureAwait(false); return; }
            ConstraintUpsertRequest? req = await ReadBodyAsync<ConstraintUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            ConstraintKindEnum kind = req.Kind ?? c.Kind;
            string value = req.Value ?? c.Value;
            string? error = await ValidateConstraintAsync(topic, kind, value, req.DimensionId ?? c.DimensionId, ctx).ConfigureAwait(false);
            if (error != null) { await SendErrorAsync(ctx, 400, "ValidationFailed", error).ConfigureAwait(false); return; }
            c.Kind = kind;
            c.Value = value.Trim();
            if (req.Note != null) c.Note = req.Note;
            if (req.DimensionId != null) c.DimensionId = req.DimensionId.Length == 0 ? null : req.DimensionId;
            if (req.IsLocked.HasValue) c.IsLocked = req.IsLocked.Value;
            c.Origin = OriginEnum.User;
            c = await Server.Database.TopicConstraints.UpdateAsync(c, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 200, c).ConfigureAwait(false);
        }

        private async Task DeleteConstraintAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            TopicConstraint? c = await Server.Database.TopicConstraints.ReadAsync(topic.TenantId, Param(ctx, "constraintId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (c == null || c.TopicId != topic.Id) { await SendNotFoundAsync(ctx, "Constraint").ConfigureAwait(false); return; }
            await Server.Database.TopicConstraints.DeleteAsync(topic.TenantId, c.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task ListRecipientsAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicRead).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            await SendJsonAsync(ctx, 200, await RecipientsAsync(topic, ctx).ConfigureAwait(false)).ConfigureAwait(false);
        }

        private async Task AddRecipientAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            RecipientUpsertRequest? req = await ReadBodyAsync<RecipientUpsertRequest>(ctx).ConfigureAwait(false);
            if (req == null) return;
            string email = (req.Email ?? String.Empty).Trim();
            if (email.Length < 3 || email.Length > 256 || !email.Contains('@', StringComparison.Ordinal))
            {
                await SendErrorAsync(ctx, 400, "ValidationFailed", "A valid email address is required.").ConfigureAwait(false);
                return;
            }

            TopicRecipient? existing = (await RecipientsAsync(topic, ctx).ConfigureAwait(false)).FirstOrDefault(r => String.Equals(r.Email, email, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.Active = req.Active ?? true;
                await SendJsonAsync(ctx, 200, await Server.Database.TopicRecipients.UpdateAsync(existing, ctx.Token).ConfigureAwait(false)).ConfigureAwait(false);
                return;
            }

            TopicRecipient created = await Server.Database.TopicRecipients.CreateAsync(new TopicRecipient { TenantId = topic.TenantId, TopicId = topic.Id, Email = email, Active = req.Active ?? true }, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 201, created).ConfigureAwait(false);
        }

        private async Task DeleteRecipientAsync(HttpContextBase ctx)
        {
            if (!await AuthorizeAsync(ctx, RequestTypeEnum.TopicWrite).ConfigureAwait(false)) return;
            Topic? topic = await OwnedTopicAsync(ctx).ConfigureAwait(false);
            if (topic == null) return;
            TopicRecipient? r = await Server.Database.TopicRecipients.ReadAsync(topic.TenantId, Param(ctx, "recipientId") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (r == null || r.TopicId != topic.Id) { await SendNotFoundAsync(ctx, "Recipient").ConfigureAwait(false); return; }
            await Server.Database.TopicRecipients.DeleteAsync(topic.TenantId, r.Id, ctx.Token).ConfigureAwait(false);
            await SendJsonAsync(ctx, 204, null).ConfigureAwait(false);
        }

        private async Task<Topic?> OwnedTopicAsync(HttpContextBase ctx)
        {
            string? tenantId = await RequireTenantAsync(ctx).ConfigureAwait(false);
            if (tenantId == null) return null;
            Topic? topic = await Server.Database.Topics.ReadAsync(tenantId, Param(ctx, "id") ?? String.Empty, ctx.Token).ConfigureAwait(false);
            if (topic == null || !IsOwnerOrAdmin(ctx, topic.UserId))
            {
                await SendNotFoundAsync(ctx, "Topic").ConfigureAwait(false);
                return null;
            }

            return topic;
        }

        private async Task<TopicDetailResponse> DetailAsync(Topic topic, HttpContextBase ctx)
        {
            return new TopicDetailResponse
            {
                Topic = topic,
                Dimensions = await Server.Refinement.DimensionsAsync(topic.TenantId, topic.Id, ctx.Token).ConfigureAwait(false),
                Constraints = await Server.Refinement.ConstraintsAsync(topic.TenantId, topic.Id, ctx.Token).ConfigureAwait(false),
                Recipients = await RecipientsAsync(topic, ctx).ConfigureAwait(false)
            };
        }

        private async Task<List<TopicRecipient>> RecipientsAsync(Topic topic, HttpContextBase ctx)
        {
            return (await Server.Database.TopicRecipients.EnumerateAsync(new TopicRecipientFilter { TenantId = topic.TenantId, TopicId = topic.Id, PageSize = 200 }, ctx.Token).ConfigureAwait(false)).Items;
        }

        private async Task<string?> ValidateConstraintAsync(Topic topic, ConstraintKindEnum kind, string value, string? dimensionId, HttpContextBase ctx)
        {
            string v = (value ?? String.Empty).Trim();
            if (v.Length == 0 || v.Length > 2000) return "value must be 1 to 2000 characters.";
            if ((kind == ConstraintKindEnum.FeedUrl || kind == ConstraintKindEnum.SeedUrl) &&
                !(Uri.TryCreate(v, UriKind.Absolute, out Uri? u) && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp)))
                return "Feed and seed URLs must be absolute http or https URLs.";
            if (!String.IsNullOrEmpty(dimensionId))
            {
                TopicDimension? d = await Server.Database.TopicDimensions.ReadAsync(topic.TenantId, dimensionId, ctx.Token).ConfigureAwait(false);
                if (d == null || d.TopicId != topic.Id) return "dimensionId must be a dimension of this topic.";
            }

            return null;
        }

        private static void Apply(Topic topic, TopicUpsertRequest req)
        {
            if (!String.IsNullOrWhiteSpace(req.Name)) topic.Name = req.Name.Trim().Length > 200 ? req.Name.Trim().Substring(0, 200) : req.Name.Trim();
            if (req.Description != null) topic.Description = req.Description;
            if (req.Brief != null) topic.Brief = req.Brief;
            if (req.AudienceLevel.HasValue) topic.AudienceLevel = req.AudienceLevel.Value;
            if (req.Horizon.HasValue) topic.Horizon = req.Horizon.Value;
            if (!String.IsNullOrWhiteSpace(req.Language)) topic.Language = req.Language.Trim();
            if (req.Status.HasValue) topic.Status = req.Status.Value;
            if (req.NoveltyPreference.HasValue) topic.NoveltyPreference = req.NoveltyPreference.Value;
            if (req.EmailOnRun.HasValue) topic.EmailOnRun = req.EmailOnRun.Value;
            if (req.EmailWhenEmpty.HasValue) topic.EmailWhenEmpty = req.EmailWhenEmpty.Value;
            if (req.MaxEmailFindings.HasValue) topic.MaxEmailFindings = req.MaxEmailFindings.Value;
            if (req.DisplayThreshold.HasValue) topic.DisplayThreshold = Math.Clamp(req.DisplayThreshold.Value, 0, 100);
            if (req.WeightLlm.HasValue) topic.WeightLlm = req.WeightLlm.Value < 0 ? null : Math.Clamp(req.WeightLlm.Value, 0, 1);
            if (req.WeightFeedback.HasValue) topic.WeightFeedback = req.WeightFeedback.Value < 0 ? null : Math.Clamp(req.WeightFeedback.Value, 0, 1);
            if (req.WeightNovelty.HasValue) topic.WeightNovelty = req.WeightNovelty.Value < 0 ? null : Math.Clamp(req.WeightNovelty.Value, 0, 1);
            if (req.WeightSource.HasValue) topic.WeightSource = req.WeightSource.Value < 0 ? null : Math.Clamp(req.WeightSource.Value, 0, 1);
            if (req.WeightRecency.HasValue) topic.WeightRecency = req.WeightRecency.Value < 0 ? null : Math.Clamp(req.WeightRecency.Value, 0, 1);
            if (req.AgenticSearch.HasValue) topic.AgenticSearch = req.AgenticSearch.Value;
            if (req.McpToolIds != null) topic.McpToolIds = req.McpToolIds.Where(i => !String.IsNullOrWhiteSpace(i) && i.Trim().StartsWith(IdPrefixes.McpServerTool, StringComparison.Ordinal)).Select(i => i.Trim()).Distinct(StringComparer.Ordinal).Take(100).ToList();
            if (req.SeedQueries != null) topic.SeedQueries = req.SeedQueries.Where(q => !String.IsNullOrWhiteSpace(q)).Select(q => q.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
        }

        private static void Apply(TopicDimension d, DimensionUpsertRequest req)
        {
            if (!String.IsNullOrWhiteSpace(req.Name)) d.Name = req.Name.Trim().Length > 200 ? req.Name.Trim().Substring(0, 200) : req.Name.Trim();
            if (req.Description != null) d.Description = req.Description;
            if (req.Rationale != null) d.Rationale = req.Rationale;
            if (req.Weight.HasValue) d.Weight = req.Weight.Value;
            if (req.IncludeTerms != null) d.IncludeTerms = req.IncludeTerms.Where(t => !String.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Take(30).ToList();
            if (req.ExcludeTerms != null) d.ExcludeTerms = req.ExcludeTerms.Where(t => !String.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Take(30).ToList();
            if (req.IsLocked.HasValue) d.IsLocked = req.IsLocked.Value;
            if (req.SortOrder.HasValue) d.SortOrder = req.SortOrder.Value;
        }
    }
}
