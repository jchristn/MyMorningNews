namespace Test.Shared.Suites
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using MyMorningNews.Core;
    using MyMorningNews.Core.Enums;
    using MyMorningNews.Core.Helpers;
    using MyMorningNews.Core.Security;
    using Test.Shared.Helpers;
    using Touchstone.Core;

    /// <summary>
    /// Identifiers, cryptography, secret handling, and permission evaluation (no database).
    /// </summary>
    public static class SecuritySuite
    {
        private const string S = "Security";

        /// <summary>
        /// Build the suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor Create()
        {
            return new TestSuiteDescriptor(S, "Identifiers, crypto, and permission evaluation", new[]
            {
                Cases.Case(S, "Ids.PrefixAndLength", "Identifiers carry the entity prefix and fixed length", IdsAsync, "fast"),
                Cases.Case(S, "Ids.KSortable", "Identifiers sort by creation time", SortableAsync, "fast"),
                Cases.Case(S, "Crypto.RoundTrip", "Protected values round-trip and differ per encryption (random nonce)", RoundTripAsync, "fast"),
                Cases.Case(S, "Crypto.TamperRejected", "Tampered or cross-purpose ciphertext is rejected", TamperAsync, "fast"),
                Cases.Case(S, "Secrets.Password", "Password hashing verifies plaintext and pre-hashed input", PasswordAsync, "fast"),
                Cases.Case(S, "Secrets.KeyFormats", "Access and secret keys use their prefixes and lengths", KeyFormatsAsync, "fast"),
                Cases.Case(S, "Rbac.DenyWins", "Explicit deny wins over permit", DenyWinsAsync, "fast"),
                Cases.Case(S, "Rbac.ImplicitDeny", "No matching grant denies implicitly", ImplicitDenyAsync, "fast"),
                Cases.Case(S, "Rbac.ResourceScope", "Resource-scoped grants apply only to their resource", ResourceScopeAsync, "fast"),
                Cases.Case(S, "Rbac.WriteExpands", "Write covers Create, Update, and Delete but not Read", WriteExpandsAsync, "fast"),
                Cases.Case(S, "Rbac.ScopeMapComplete", "Every request type has a permission mapping", ScopeMapAsync, "fast")
            });
        }

        private static Task IdsAsync(CancellationToken token)
        {
            string id = IdGenerator.GenerateUserId();
            Assert.True(id.StartsWith(IdPrefixes.User, StringComparison.Ordinal), "user id starts with usr_: " + id);
            Assert.Equal(IdGenerator.IdLength, id.Length, "id length");
            Assert.True(IdGenerator.GenerateTenantId().StartsWith("ten_", StringComparison.Ordinal), "tenant prefix");
            return Task.CompletedTask;
        }

        private static async Task SortableAsync(CancellationToken token)
        {
            string a = IdGenerator.GenerateAuditRecordId();
            await Task.Delay(5, token).ConfigureAwait(false);
            string b = IdGenerator.GenerateAuditRecordId();
            Assert.True(String.CompareOrdinal(a, b) < 0, "later id sorts after earlier id: " + a + " < " + b);
        }

        private static Task RoundTripAsync(CancellationToken token)
        {
            CryptoProtector p = new CryptoProtector("key-one", "session-token");
            string c1 = p.Protect("hello world");
            string c2 = p.Protect("hello world");
            Assert.Equal("hello world", p.Unprotect(c1), "round trip");
            Assert.True(c1 != c2, "two encryptions of the same plaintext differ");
            return Task.CompletedTask;
        }

        private static Task TamperAsync(CancellationToken token)
        {
            CryptoProtector p = new CryptoProtector("key-one", "session-token");
            string c = p.Protect("payload");
            char[] chars = c.ToCharArray();
            chars[chars.Length / 2] = chars[chars.Length / 2] == 'A' ? 'B' : 'A';
            Assert.True(p.Unprotect(new string(chars)) == null, "tampered ciphertext is rejected");
            Assert.True(new CryptoProtector("key-one", "credential-secret").Unprotect(c) == null, "another purpose cannot decrypt");
            Assert.True(new CryptoProtector("key-two", "session-token").Unprotect(c) == null, "another key cannot decrypt");
            Assert.True(p.Unprotect("not-base64!!") == null, "garbage is rejected");
            return Task.CompletedTask;
        }

        private static Task PasswordAsync(CancellationToken token)
        {
            string hash = SecretHasher.Sha256Hex("password");
            Assert.Equal(64, hash.Length, "hash length");
            Assert.True(SecretHasher.VerifyPassword("password", hash), "plaintext verifies");
            Assert.True(SecretHasher.VerifyPassword(hash.ToUpperInvariant(), hash), "pre-hashed input verifies");
            Assert.True(!SecretHasher.VerifyPassword("Password", hash), "wrong password fails");
            Assert.True(!SecretHasher.VerifyPassword(null, hash), "null fails");
            return Task.CompletedTask;
        }

        private static Task KeyFormatsAsync(CancellationToken token)
        {
            string ak = SecretGenerator.AccessKey();
            string sk = SecretGenerator.SecretKey();
            Assert.True(ak.StartsWith("access_", StringComparison.Ordinal) && ak.Length == 39, "access key format: " + ak);
            Assert.True(sk.StartsWith("secret_", StringComparison.Ordinal) && sk.Length == 47, "secret key format");
            Assert.True(SecretGenerator.AccessKey() != ak, "keys are random");
            return Task.CompletedTask;
        }

        private static Task DenyWinsAsync(CancellationToken token)
        {
            List<EffectiveGrant> grants = new List<EffectiveGrant>
            {
                new EffectiveGrant { ResourceType = ResourceTypeEnum.All, Operation = OperationTypeEnum.All },
                new EffectiveGrant { ResourceType = ResourceTypeEnum.Topic, Operation = OperationTypeEnum.Delete, PermissionType = PermissionTypeEnum.Deny }
            };
            Assert.Equal(AuthorizationOutcomeEnum.DeniedExplicit, PermissionEvaluator.Evaluate(grants, ResourceTypeEnum.Topic, OperationTypeEnum.Delete, null, out List<EffectiveGrant> _), "deny wins");
            Assert.Equal(AuthorizationOutcomeEnum.Permitted, PermissionEvaluator.Evaluate(grants, ResourceTypeEnum.Topic, OperationTypeEnum.Read, null, out List<EffectiveGrant> _), "other operations permitted");
            return Task.CompletedTask;
        }

        private static Task ImplicitDenyAsync(CancellationToken token)
        {
            List<EffectiveGrant> grants = new List<EffectiveGrant> { new EffectiveGrant { ResourceType = ResourceTypeEnum.Topic, Operation = OperationTypeEnum.Read } };
            Assert.Equal(AuthorizationOutcomeEnum.DeniedImplicit, PermissionEvaluator.Evaluate(grants, ResourceTypeEnum.User, OperationTypeEnum.Read, null, out List<EffectiveGrant> _), "no grant for User");
            Assert.Equal(AuthorizationOutcomeEnum.DeniedImplicit, PermissionEvaluator.Evaluate(new List<EffectiveGrant>(), ResourceTypeEnum.Topic, OperationTypeEnum.Read, null, out List<EffectiveGrant> _), "empty grants");
            return Task.CompletedTask;
        }

        private static Task ResourceScopeAsync(CancellationToken token)
        {
            List<EffectiveGrant> grants = new List<EffectiveGrant>
            {
                new EffectiveGrant { ResourceType = ResourceTypeEnum.Topic, Operation = OperationTypeEnum.All, ResourceScope = ResourceScopeEnum.Resource, ResourceId = "top_a" }
            };
            Assert.Equal(AuthorizationOutcomeEnum.Permitted, PermissionEvaluator.Evaluate(grants, ResourceTypeEnum.Topic, OperationTypeEnum.Update, "top_a", out List<EffectiveGrant> _), "matching resource");
            Assert.Equal(AuthorizationOutcomeEnum.DeniedImplicit, PermissionEvaluator.Evaluate(grants, ResourceTypeEnum.Topic, OperationTypeEnum.Update, "top_b", out List<EffectiveGrant> _), "other resource");
            Assert.Equal(AuthorizationOutcomeEnum.DeniedImplicit, PermissionEvaluator.Evaluate(grants, ResourceTypeEnum.Topic, OperationTypeEnum.Update, null, out List<EffectiveGrant> _), "tenant-level operation");
            List<EffectiveGrant> nonInheriting = new List<EffectiveGrant> { new EffectiveGrant { ResourceType = ResourceTypeEnum.Topic, Operation = OperationTypeEnum.Read, InheritsToChildren = false } };
            Assert.Equal(AuthorizationOutcomeEnum.DeniedImplicit, PermissionEvaluator.Evaluate(nonInheriting, ResourceTypeEnum.Topic, OperationTypeEnum.Read, "top_a", out List<EffectiveGrant> _), "non-inheriting tenant grant does not cover resources");
            return Task.CompletedTask;
        }

        private static Task WriteExpandsAsync(CancellationToken token)
        {
            List<EffectiveGrant> grants = new List<EffectiveGrant> { new EffectiveGrant { ResourceType = ResourceTypeEnum.Topic, Operation = OperationTypeEnum.Write } };
            foreach (OperationTypeEnum op in new[] { OperationTypeEnum.Create, OperationTypeEnum.Update, OperationTypeEnum.Delete })
                Assert.Equal(AuthorizationOutcomeEnum.Permitted, PermissionEvaluator.Evaluate(grants, ResourceTypeEnum.Topic, op, null, out List<EffectiveGrant> _), "write covers " + op);
            Assert.Equal(AuthorizationOutcomeEnum.DeniedImplicit, PermissionEvaluator.Evaluate(grants, ResourceTypeEnum.Topic, OperationTypeEnum.Read, null, out List<EffectiveGrant> _), "write does not cover read");
            return Task.CompletedTask;
        }

        private static Task ScopeMapAsync(CancellationToken token)
        {
            OperationScopeMap.Validate();
            return Task.CompletedTask;
        }
    }
}
