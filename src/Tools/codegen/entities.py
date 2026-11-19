"""
Entity and enum specs for codegen.py. Add new entities at the end of ENTITIES (table creation order matters for foreign keys).
Field keys: name, type (string|text|int|long|double|decimal|bool|datetime|strings|ints|time|enum:Name), nullable, required,
default, clamp=(min,max), length, doc, filter, sort, search, index, unique, fk=(table, onDelete), column.
"""

S = 'string'


def f(name, type_=S, doc=None, **kw):
    d = {'name': name, 'type': type_, 'doc': doc or (name + '.')}
    d.update(kw)
    return d


ENTITIES = [
    # ------------------------------------------------------------------ Phase 1: tenancy and security
    {
        'name': 'Tenant', 'table': 'tenants', 'prefix': 'ten_', 'tenant': False,
        'doc': 'Tenant: the hard isolation boundary for all data.',
        'fields': [
            f('Name', doc='Display name.', required=True, length=128, search=True, sort=True),
            f('Description', 'text', doc='Optional description.', nullable=True),
            f('Region', doc='Optional region label.', nullable=True, length=64),
            f('TimeZone', doc='Default IANA time zone for schedules and email.', default='UTC', length=64),
            f('IsProtected', 'bool', doc='Protected records cannot be deleted through the API.', default=False),
        ],
        'default_sort': 'CreatedUtc',
    },
    {
        'name': 'User', 'table': 'users', 'prefix': 'usr_',
        'doc': 'Interactive user within a tenant.',
        'fields': [
            f('FirstName', doc='First name.', nullable=True, length=128, search=True),
            f('LastName', doc='Last name.', nullable=True, length=128, search=True),
            f('Email', doc='Email address, unique within the tenant, used for login.', required=True, length=256, unique=True, search=True, sort=True, filter=True),
            f('PasswordSha256', doc='SHA-256 hash of the password, hex encoded. Never returned by the API.', required=True, length=64),
            f('IsAdmin', 'bool', doc='Global administrator: bypasses all tenant and permission checks.', default=False, filter=True),
            f('IsTenantAdmin', 'bool', doc='Tenant administrator: bypasses RBAC within the user\'s own tenant.', default=False, filter=True),
            f('IsProtected', 'bool', doc='Protected records cannot be deleted through the API.', default=False),
            f('TimeZone', doc='Preferred IANA time zone.', nullable=True, length=64),
            f('Locale', doc='Preferred locale (BCP 47), for example en or ja.', nullable=True, length=16),
            f('LastLoginUtc', 'datetime', doc='UTC time of the last successful login.', nullable=True, sort=True),
        ],
    },
    {
        'name': 'Credential', 'table': 'credentials', 'prefix': 'crd_', 'user': True,
        'doc': 'Non-interactive API credential (access key and secret) owned by a user.',
        'fields': [
            f('Name', doc='Display name, for example "CI pipeline".', required=True, length=128, search=True, sort=True),
            f('AccessKey', doc='Public access key (access_ plus 32 random characters).', required=True, length=64, unique=True, filter=True),
            f('SecretKeyEncrypted', 'text', doc='Encrypted secret key. Never returned by the API.', required=True),
            f('SecretKeyLast4', doc='Last four characters of the secret, for display.', length=8, default=''),
            f('AuthMode', 'enum:CredentialAuthModeEnum', doc='Authentication mode.', default='DirectHeader'),
            f('Scopes', 'strings', doc='Legacy coarse scopes such as read or write. Empty means no coarse restriction.'),
            f('ResourceIds', 'strings', doc='Optional resource allow-list. Empty means all resources in the tenant.'),
            f('LastUsedUtc', 'datetime', doc='UTC time the credential was last used.', nullable=True),
            f('ExpiresUtc', 'datetime', doc='UTC expiration time.', nullable=True),
            f('IsProtected', 'bool', doc='Protected records cannot be deleted through the API.', default=False),
        ],
    },
    {
        # HAND-EDITED after generation (DeleteExpiredAsync): do not --force.
        'name': 'AuthSession', 'table': 'authsessions', 'prefix': 'ses_',
        'doc': 'Revocable server-side session backing an opaque bearer token.',
        'fields': [
            f('UserId', doc='User principal, when the session belongs to a user.', nullable=True, length=64, filter=True, fk=('users', 'CASCADE')),
            f('CredentialId', doc='Credential principal, when the session belongs to a credential.', nullable=True, length=64, filter=True, fk=('credentials', 'CASCADE')),
            f('PrincipalType', 'enum:PrincipalTypeEnum', doc='Principal type.', default='User', filter=True),
            f('AuthScheme', 'enum:AuthSchemeEnum', doc='Scheme used to create the session.', default='PasswordHeaders'),
            f('TokenId', doc='Random token identifier embedded in the token.', required=True, length=64, unique=True),
            f('SourceIp', doc='Source IP at issuance.', nullable=True, length=64),
            f('UserAgent', 'text', doc='User agent at issuance.', nullable=True),
            f('ExpiresUtc', 'datetime', doc='UTC expiration time.', sort=True),
            f('LastUsedUtc', 'datetime', doc='UTC time the session was last used.', nullable=True),
            f('RevokedUtc', 'datetime', doc='UTC time the session was revoked.', nullable=True),
            f('RevocationReason', doc='Reason for revocation.', nullable=True, length=256),
            f('IsProtected', 'bool', doc='Protected records cannot be deleted through the API.', default=False),
        ],
    },
    {
        'name': 'UserRole', 'table': 'userroles', 'prefix': 'rol_', 'tenant_nullable': True,
        'doc': 'Role grouping permissions. Built-in roles have a null tenant and are protected.',
        'fields': [
            f('Name', doc='Role name.', required=True, length=128, search=True, sort=True, filter=True),
            f('Description', 'text', doc='Description.', nullable=True),
            f('IsBuiltIn', 'bool', doc='Seeded by the platform.', default=False, filter=True),
            f('IsProtected', 'bool', doc='Protected roles cannot be edited or deleted through the API.', default=False),
            f('DefaultScope', 'enum:ResourceScopeEnum', doc='Scope the role is intended for.', default='Tenant'),
        ],
        'uniques': [['tenantid', 'name']],
    },
    {
        'name': 'Permission', 'table': 'permissions', 'prefix': 'prm_', 'tenant_nullable': True,
        'doc': 'Permission: which operations are permitted or denied on which resource types.',
        'fields': [
            f('Name', doc='Display name.', required=True, length=128, search=True, sort=True, filter=True),
            f('ResourceTypes', 'strings', doc='Resource types covered; ["All"] is a wildcard.'),
            f('OperationTypes', 'strings', doc='Operation types covered; ["All"] is a wildcard.'),
            f('PermissionType', 'enum:PermissionTypeEnum', doc='Permit or Deny.', default='Permit', filter=True),
            f('IsBuiltIn', 'bool', doc='Seeded by the platform.', default=False),
            f('IsProtected', 'bool', doc='Protected records cannot be edited or deleted through the API.', default=False),
        ],
        'indexes': [],
    },
    {
        'name': 'RolePermissionMap', 'table': 'rolepermissionmaps', 'prefix': 'rpm_', 'tenant_nullable': True,
        'doc': 'Association between a role and a permission.',
        'fields': [
            f('RoleId', doc='Role identifier.', required=True, length=64, filter=True, fk=('userroles', 'CASCADE')),
            f('PermissionId', doc='Permission identifier.', required=True, length=64, filter=True, fk=('permissions', 'CASCADE')),
            f('IsProtected', 'bool', doc='Protected records cannot be deleted through the API.', default=False),
        ],
        'uniques': [['roleid', 'permissionid']],
    },
    {
        'name': 'UserRoleAssignment', 'table': 'userroleassignments', 'prefix': 'ura_',
        'doc': 'Assignment of a role to a user at tenant or resource scope.',
        'fields': [
            f('UserId', doc='User identifier.', required=True, length=64, filter=True, fk=('users', 'CASCADE')),
            f('RoleId', doc='Role identifier; null when RoleName is used.', nullable=True, length=64, filter=True, fk=('userroles', 'CASCADE')),
            f('RoleName', doc='Built-in role name fallback.', nullable=True, length=128),
            f('ResourceScope', 'enum:ResourceScopeEnum', doc='Tenant or Resource.', default='Tenant', filter=True),
            f('ResourceId', doc='Target resource identifier when resource scoped.', nullable=True, length=64, filter=True, index=True),
            f('InheritsToChildren', 'bool', doc='Whether a tenant-scoped grant applies to resources inside the tenant.', default=True),
            f('IsProtected', 'bool', doc='Protected records cannot be deleted through the API.', default=False),
        ],
    },
    {
        'name': 'CredentialScopeAssignment', 'table': 'credentialscopeassignments', 'prefix': 'csa_',
        'doc': 'Scope assignment for a credential, narrowing it below its owning user.',
        'fields': [
            f('CredentialId', doc='Credential identifier.', required=True, length=64, filter=True, fk=('credentials', 'CASCADE')),
            f('RoleId', doc='Optional role identifier.', nullable=True, length=64, fk=('userroles', 'CASCADE')),
            f('RoleName', doc='Optional built-in role name.', nullable=True, length=128),
            f('ResourceScope', 'enum:ResourceScopeEnum', doc='Tenant or Resource.', default='Tenant'),
            f('ResourceId', doc='Target resource identifier when resource scoped.', nullable=True, length=64),
            f('OperationTypes', 'strings', doc='Direct operation grants (role-less).'),
            f('ResourceTypes', 'strings', doc='Direct resource-type grants (role-less).'),
            f('IsProtected', 'bool', doc='Protected records cannot be deleted through the API.', default=False),
        ],
    },
    {
        # HAND-EDITED after generation (DeleteOlderThanAsync): do not --force.
        'name': 'AuditRecord', 'table': 'auditrecords', 'prefix': 'aud_', 'tenant_nullable': True,
        'doc': 'Append-only security event: authentication, sessions, RBAC changes, denials, bypasses, settings changes.',
        'fields': [
            f('EventType', 'enum:AuditEventTypeEnum', doc='Event type.', default='AuthSuccess', filter=True, sort=True),
            f('RequestId', doc='Request identifier.', nullable=True, length=64),
            f('CorrelationId', doc='Correlation identifier.', nullable=True, length=128),
            f('TraceId', doc='Trace identifier.', nullable=True, length=64),
            f('AuthScheme', doc='Authentication scheme name.', nullable=True, length=64),
            f('PrincipalType', doc='Principal type name.', nullable=True, length=32),
            f('PrincipalId', doc='Principal identifier.', nullable=True, length=64, filter=True),
            f('SessionId', doc='Session identifier.', nullable=True, length=64),
            f('UserId', doc='User identifier.', nullable=True, length=64, filter=True),
            f('CredentialId', doc='Credential identifier.', nullable=True, length=64),
            f('ResourceType', doc='Resource type.', nullable=True, length=64),
            f('ResourceId', doc='Resource identifier.', nullable=True, length=64),
            f('RequestType', doc='Classified request type.', nullable=True, length=64),
            f('Method', doc='HTTP method.', nullable=True, length=16),
            f('Path', 'text', doc='URL path.', nullable=True, search=True),
            f('SourceIp', doc='Source IP.', nullable=True, length=64),
            f('AuthenticationResult', doc='Authentication outcome.', nullable=True, length=32),
            f('AuthorizationResult', doc='Authorization outcome.', nullable=True, length=32, filter=True),
            f('Reason', 'text', doc='Denial or event reason.', nullable=True, search=True),
            f('BypassReason', 'text', doc='Bypass reason for administrative paths.', nullable=True),
            f('RequiredPermission', doc='Permission the request required.', nullable=True, length=64),
            f('StatusCode', 'int', doc='Response status code.', default=0),
            f('Description', 'text', doc='Short description.', nullable=True),
        ],
    },
]

# Prefixes for identifiers that are not codegen entities.
EXTRA_PREFIXES = [
    ('RequestHistory', 'req_', 'Prefix for request history entries.'),
    ('Request', 'rqi_', 'Prefix for per-request trace identifiers.'),
]

ENUMS = [
    ('SortDirectionEnum', 'Sort direction for enumeration.', [('Descending', 'Newest or largest first.'), ('Ascending', 'Oldest or smallest first.')]),
    ('PrincipalTypeEnum', 'Authenticated principal type.', [('Administrator', 'System administrator.'), ('User', 'Interactive user.'), ('Credential', 'API credential.')]),
    ('AuthSchemeEnum', 'Authentication scheme.', [
        ('BearerToken', 'Authorization: Bearer session token.'), ('XToken', 'x-token session token header.'),
        ('PasswordHeaders', 'x-email, x-password, x-tenant-guid login headers.'), ('AccessKeySecret', 'x-access-key and x-secret-key headers.'),
        ('AccessKeySignature', 'Signed request with x-access-key and x-signature.'), ('AdminApiKey', 'x-api-key system administration key.'),
        ('EmailActionToken', 'Tokenized email feedback link.'), ('FeedToken', 'Tokenized podcast feed URL.')]),
    ('CredentialAuthModeEnum', 'How a credential authenticates.', [
        ('DirectHeader', 'Access key and secret sent as headers.'), ('SignedRequest', 'HMAC-signed requests.'),
        ('SessionExchange', 'Exchanged for a session token.'), ('Hybrid', 'Any of the above.')]),
    ('PermissionTypeEnum', 'Permission effect.', [('Permit', 'Allows the operation.'), ('Deny', 'Explicitly denies the operation; wins over Permit.')]),
    ('ResourceScopeEnum', 'Breadth of a role assignment.', [('Tenant', 'Applies to the whole tenant.'), ('Resource', 'Applies to one resource.')]),
    ('AuthenticationOutcomeEnum', 'Authentication outcome.', [
        ('Success', 'Authenticated.'), ('NotFound', 'Principal or session not found.'), ('Inactive', 'Principal, tenant, or session inactive or expired.'),
        ('Invalid', 'Malformed, conflicting, or incorrect credentials.'), ('NotAttempted', 'No credentials were supplied.')]),
    ('AuthorizationOutcomeEnum', 'Authorization outcome.', [
        ('Permitted', 'Allowed.'), ('DeniedExplicit', 'A Deny permission matched.'), ('DeniedImplicit', 'No Permit permission matched.'),
        ('NotFound', 'The target resource does not exist in the tenant.'), ('Conflict', 'Conflicting tenant or resource hints.')]),
    ('AuditEventTypeEnum', 'Security event type.', [
        ('AuthSuccess', 'Authentication succeeded.'), ('AuthFailure', 'Authentication failed.'), ('SessionIssued', 'Session token issued.'),
        ('SessionRefreshed', 'Session token refreshed.'), ('SessionRevoked', 'Session token revoked.'), ('CredentialCreated', 'Credential created.'),
        ('CredentialUsed', 'Credential used.'), ('CredentialRevoked', 'Credential revoked or deleted.'), ('AuthorizationDenied', 'Authorization denied.'),
        ('AuthorizationBypass', 'Authorization skipped by an administrative bypass.'), ('RoleChanged', 'Role created, updated, or deleted.'),
        ('PermissionChanged', 'Permission or role mapping changed.'), ('AssignmentChanged', 'Role assignment changed.'),
        ('UserChanged', 'User created, updated, or deleted.'), ('TenantChanged', 'Tenant created, updated, or deleted.'),
        ('SettingsChanged', 'Server settings changed.'), ('RestartRequested', 'Server restart requested.'),
        ('CredentialRevealed', 'A stored vault credential was revealed.'), ('VaultCredentialChanged', 'A vault credential was created, rotated, or deleted.')]),
    ('ResourceTypeEnum', 'Resource types used by permissions.', [
        ('All', 'Wildcard.'), ('Tenant', 'Tenant.'), ('Admin', 'Administrative control plane.'), ('User', 'User.'), ('Credential', 'Credential.'),
        ('Session', 'Session.'), ('Role', 'Role.'), ('Permission', 'Permission.'), ('Assignment', 'Role assignment.'), ('Audit', 'Audit record.'),
        ('RequestHistory', 'Request history.'), ('Settings', 'Server settings.'), ('Topic', 'Topic.'), ('Schedule', 'Schedule.'),
        ('RefinementSession', 'Refinement session.'), ('Run', 'Research run.'), ('Finding', 'Finding.'), ('Source', 'Source item.'),
        ('Evidence', 'Evidence.'), ('Prediction', 'Prediction.'), ('Suggestion', 'Suggestion.'), ('Feedback', 'Feedback.'),
        ('LearnedPreference', 'Learned preference.'), ('Briefing', 'Briefing.'), ('Edition', 'Edition.'), ('Template', 'Briefing template.'),
        ('Prompt', 'Prompt template.'), ('ModelEndpoint', 'Model endpoint.'), ('SearchProvider', 'Search provider.'), ('Voice', 'Voice profile.'),
        ('Media', 'Media asset.'), ('EmailMessage', 'Email message.'), ('Analytics', 'Analytics.')]),
    ('OperationTypeEnum', 'Operation types used by permissions.', [
        ('All', 'Wildcard.'), ('Create', 'Create.'), ('Read', 'Read.'), ('Write', 'Shorthand for Create, Update, and Delete.'),
        ('Update', 'Update.'), ('Delete', 'Delete.'), ('Execute', 'Run or trigger an operation.'), ('Admin', 'Privileged control-plane operation.')]),
    ('RequestTypeEnum', 'Classified request type; each maps to a required (resource, operation) pair in OperationScopeMap.', [
        ('Unknown', 'Unclassified request.'),
        ('HealthRead', 'Read health.'),
        ('TokenCreate', 'Create a session token.'),
        ('TokenRead', 'Validate or inspect the current token.'),
        ('TokenRefresh', 'Refresh the current token.'),
        ('TokenDelete', 'Revoke the current token.'),
        ('TenantRead', 'Read tenants.'),
        ('TenantWrite', 'Create or update tenants.'),
        ('TenantDelete', 'Delete tenants.'),
        ('UserRead', 'Read users.'),
        ('UserWrite', 'Create or update users.'),
        ('UserDelete', 'Delete users.'),
        ('UserSelfRead', 'Read own profile.'),
        ('UserSelfWrite', 'Update own profile or password.'),
        ('CredentialRead', 'Read credentials.'),
        ('CredentialWrite', 'Create or update credentials.'),
        ('CredentialDelete', 'Delete credentials.'),
        ('RoleRead', 'Read roles.'),
        ('RoleWrite', 'Create or update roles.'),
        ('RoleDelete', 'Delete roles.'),
        ('PermissionRead', 'Read permissions.'),
        ('PermissionWrite', 'Create or update permissions.'),
        ('PermissionDelete', 'Delete permissions.'),
        ('AssignmentRead', 'Read role assignments.'),
        ('AssignmentWrite', 'Create or update role assignments.'),
        ('AssignmentDelete', 'Delete role assignments.'),
        ('EffectivePermissionRead', 'Inspect effective permissions.'),
        ('AuditRead', 'Read audit records.'),
        ('AuditDelete', 'Delete audit records.'),
        ('RequestHistoryRead', 'Read request history.'),
        ('RequestHistoryDelete', 'Delete request history.'),
        ('SettingsRead', 'Read server settings.'),
        ('SettingsWrite', 'Update server settings.'),
        ('ServerRestart', 'Restart the server.'),
        ('ServerInfoRead', 'Read server information.'),
    ]),
]
