namespace MyMorningNews.Core.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core.Database;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Models;
    using MyMorningNews.Core.Security;
    using SyslogLogging;

    /// <summary>
    /// Writes security audit records. Failures are logged and never thrown to callers.
    /// Thread safety: safe for concurrent use.
    /// </summary>
    public class AuditService
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly LoggingModule _Logging;
        private readonly string _Header = "[AuditService] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database.</param>
        /// <param name="logging">Logging.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public AuditService(DatabaseDriverBase database, LoggingModule logging)
        {
            ArgumentNullException.ThrowIfNull(database);
            ArgumentNullException.ThrowIfNull(logging);
            _Database = database;
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Record an event built from a request context.
        /// </summary>
        /// <param name="eventType">Event type.</param>
        /// <param name="ctx">Request context, or null for background events.</param>
        /// <param name="statusCode">Response status code.</param>
        /// <param name="reason">Reason or description.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RecordAsync(AuditEventTypeEnum eventType, RequestContext? ctx, int statusCode, string? reason, CancellationToken token = default)
        {
            AuditRecord record = new AuditRecord
            {
                EventType = eventType,
                TenantId = ctx?.TenantId,
                RequestId = ctx?.RequestId,
                CorrelationId = ctx?.CorrelationId,
                TraceId = ctx?.TraceId,
                AuthScheme = ctx?.Authentication.Scheme?.ToString(),
                PrincipalType = ctx?.Authentication.PrincipalType?.ToString(),
                PrincipalId = ctx?.Authentication.PrincipalId,
                SessionId = ctx?.SessionId,
                UserId = ctx?.UserId,
                CredentialId = ctx?.CredentialId,
                ResourceType = ctx != null ? ctx.Authorization.ResourceType.ToString() : null,
                ResourceId = ctx?.Authorization.ResourceId,
                RequestType = ctx?.RequestType.ToString(),
                Method = ctx?.Method,
                Path = ctx?.Path,
                SourceIp = ctx?.SourceIp,
                AuthenticationResult = ctx?.Authentication.Outcome.ToString(),
                AuthorizationResult = ctx?.Authorization.Outcome.ToString(),
                Reason = reason ?? ctx?.Authorization.Reason,
                BypassReason = ctx?.Authorization.BypassReason,
                RequiredPermission = ctx != null ? ctx.Authorization.ResourceType + ":" + ctx.Authorization.Operation : null,
                StatusCode = statusCode
            };

            await RecordAsync(record, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Record an event.
        /// </summary>
        /// <param name="record">Record.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task RecordAsync(AuditRecord record, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(record);
            try
            {
                if (record.TenantId != null && !await _Database.Tenants.ExistsAsync(record.TenantId, token).ConfigureAwait(false)) record.TenantId = null;
                await _Database.AuditRecords.CreateAsync(record, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _Logging.Warn(_Header + "unable to persist audit record " + record.EventType + ": " + ex.Message);
            }
        }

        #endregion
    }
}
