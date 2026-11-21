"""
My Morning News Python SDK test harness.

Runs the SDK against a live server, creates its own data (names start with
``SDK-python-``), checks every resource group, and deletes what it created.
Cases that start research runs or call a paid model are skipped unless
``--with-run`` is given.

Usage::

    python test_harness.py [--base-url URL] [--email EMAIL] [--password PASSWORD]
                           [--tenant TENANT] [--demo-email EMAIL] [--demo-password PASSWORD]
                           [--with-run] [--verbose]

Environment variables MYMORNINGNEWS_BASE_URL, MYMORNINGNEWS_EMAIL, and
MYMORNINGNEWS_PASSWORD override the defaults. Exit code 0 means every case
passed or was skipped.

(c) 2026 Lattice AI LLC. Licensed under the MIT license.
"""

from __future__ import annotations

import argparse
import copy
import json
import os
import re
import sys
import threading
import time
import traceback
import urllib.parse
import uuid
from dataclasses import dataclass, field
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from typing import Any, Callable, Dict, List, Optional, Tuple, Type

import requests

import mymorningnews_sdk as mmn
from mymorningnews_sdk import (
    AuthenticationError,
    BadRequestError,
    ConflictError,
    MyMorningNewsClient,
    MyMorningNewsError,
    NotFoundError,
)


# ---------------------------------------------------------------------------
# Environment and configuration
# ---------------------------------------------------------------------------


@dataclass
class HarnessConfig:
    """Settings for one harness run.

    Attributes:
        base_url: Server URL.
        email: Admin email.
        password: Admin password.
        tenant: Tenant ID (optional).
        demo_email: Second, non-admin user for the cross-user check.
        demo_password: Second user's password.
        with_run: Also run cases that start research runs or call paid models.
        verbose: Print tracebacks and extra detail (never secrets).
    """

    base_url: str = mmn.DEFAULT_BASE_URL
    email: str = "admin@mymorningnews"
    password: str = "password"
    tenant: Optional[str] = None
    demo_email: str = "demo@mymorningnews"
    demo_password: str = "password"
    with_run: bool = False
    verbose: bool = False

    @classmethod
    def from_args(cls, argv: Optional[List[str]] = None) -> "HarnessConfig":
        """Build the configuration from defaults, environment variables, and arguments (in that order)."""
        parser = argparse.ArgumentParser(description="My Morning News Python SDK test harness")
        parser.add_argument("--base-url", default=os.environ.get("MYMORNINGNEWS_BASE_URL", mmn.DEFAULT_BASE_URL))
        parser.add_argument("--email", default=os.environ.get("MYMORNINGNEWS_EMAIL", "admin@mymorningnews"))
        parser.add_argument("--password", default=os.environ.get("MYMORNINGNEWS_PASSWORD", "password"))
        parser.add_argument("--tenant", default=os.environ.get("MYMORNINGNEWS_TENANT"))
        parser.add_argument("--demo-email", default=os.environ.get("MYMORNINGNEWS_DEMO_EMAIL", "demo@mymorningnews"))
        parser.add_argument("--demo-password", default=os.environ.get("MYMORNINGNEWS_DEMO_PASSWORD", "password"))
        parser.add_argument("--with-run", action="store_true", help="run cases that start research runs or call paid models")
        parser.add_argument("--verbose", action="store_true", help="print tracebacks and details")
        a = parser.parse_args(argv)
        return cls(base_url=a.base_url, email=a.email, password=a.password, tenant=a.tenant, demo_email=a.demo_email,
                   demo_password=a.demo_password, with_run=a.with_run, verbose=a.verbose)


class Skip(Exception):
    """Raised by a case to report SKIP with a reason."""


@dataclass
class CaseResult:
    """Outcome of one case."""

    name: str
    status: str
    ms: float
    note: str = ""


@dataclass
class Case:
    """A registered harness case."""

    name: str
    fn: Callable[["Harness"], Optional[str]]
    needs_run: bool = False


CASES: List[Case] = []


def case(name: str, needs_run: bool = False) -> Callable[[Callable[["Harness"], Optional[str]]], Callable[["Harness"], Optional[str]]]:
    """Register a case. Cases run in registration order and share state through the harness."""
    def wrap(fn: Callable[["Harness"], Optional[str]]) -> Callable[["Harness"], Optional[str]]:
        CASES.append(Case(name, fn, needs_run))
        return fn
    return wrap


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------


def check(condition: Any, message: str) -> None:
    """Assert a condition with a message."""
    if not condition:
        raise AssertionError(message)


def eq(actual: Any, expected: Any, what: str) -> None:
    """Assert equality with a readable message."""
    if actual != expected:
        raise AssertionError(what + ": expected " + repr(expected) + ", got " + repr(actual))


def expect_error(error_type: Type[MyMorningNewsError], fn: Callable[[], Any], code: Optional[str] = None) -> MyMorningNewsError:
    """Assert that ``fn`` raises ``error_type`` (and optionally the server error ``code``)."""
    try:
        fn()
    except error_type as ex:
        if code is not None and ex.code != code:
            raise AssertionError("expected error code " + code + ", got " + str(ex.code) + " (" + ex.message + ")")
        check(isinstance(ex.status, int), "error carries a status")
        return ex
    except MyMorningNewsError as ex:
        raise AssertionError("expected " + error_type.__name__ + ", got " + type(ex).__name__ + ": " + str(ex))
    raise AssertionError("expected " + error_type.__name__ + " but the call succeeded")


def expect_value_error(fn: Callable[[], Any]) -> None:
    """Assert that ``fn`` raises ValueError."""
    try:
        fn()
    except ValueError:
        return
    raise AssertionError("expected ValueError")


def ids(items: Any) -> List[str]:
    """IDs of a page or list of models."""
    return [x.id for x in items]


def _in_days(days: int) -> str:
    """An ISO 8601 UTC time the given number of days from now (requests can be scheduled up to a year ahead)."""
    import datetime
    when = datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=days)
    return when.replace(microsecond=0).strftime("%Y-%m-%dT%H:%M:%SZ")


class Harness:
    """Holds clients, shared state, and the cleanup stack, and runs the cases."""

    def __init__(self, config: HarnessConfig) -> None:
        """Create the harness."""
        self.config = config
        self.client = MyMorningNewsClient(config.base_url, tenant_id=config.tenant, timeout=120.0)
        self.demo: Optional[MyMorningNewsClient] = None
        self.state: Dict[str, Any] = {}
        self.prefix = "SDK-python-" + uuid.uuid4().hex[:8]
        self._cleanup: List[Tuple[str, Callable[[], Any]]] = []
        self.results: List[CaseResult] = []

    def name(self, label: str) -> str:
        """A unique, prefixed name for created data."""
        return self.prefix + "-" + label

    def need(self, *keys: str) -> Any:
        """Return state values, or skip when a prerequisite case failed."""
        values = []
        for k in keys:
            if self.state.get(k) is None:
                raise Skip("prerequisite '" + k + "' is missing")
            values.append(self.state[k])
        return values[0] if len(values) == 1 else tuple(values)

    def on_cleanup(self, label: str, fn: Callable[[], Any]) -> None:
        """Register an undo step; steps run in reverse order at the end."""
        self._cleanup.append((label, fn))

    def log(self, text: str) -> None:
        """Print a detail line when verbose."""
        if self.config.verbose:
            print("      " + text)

    def run(self) -> int:
        """Run every case, then clean up. Returns the process exit code."""
        print("My Morning News Python SDK harness " + mmn.__version__)
        print("Server: " + self.config.base_url + "   prefix: " + self.prefix + "   with-run: " + str(self.config.with_run))
        print("")
        for c in CASES:
            started = time.perf_counter()
            status = "PASS"
            note = ""
            if c.needs_run and not self.config.with_run:
                status, note = "SKIP", "needs --with-run (starts a run or calls a paid model)"
            else:
                try:
                    note = c.fn(self) or ""
                except Skip as ex:
                    status, note = "SKIP", str(ex)
                except Exception as ex:  # noqa: BLE001 - any failure is reported
                    status, note = "FAIL", type(ex).__name__ + ": " + str(ex)
                    if self.config.verbose:
                        traceback.print_exc()
            ms = (time.perf_counter() - started) * 1000.0
            self.results.append(CaseResult(c.name, status, ms, note))
            line = "%-4s  %-58s %8.1f ms" % (status, c.name, ms)
            if note and status != "PASS":
                line += "  (" + note + ")"
            elif note and self.config.verbose:
                line += "  [" + note + "]"
            print(line)

        print("")
        print("Cleanup")
        failures = 0
        for label, fn in reversed(self._cleanup):
            try:
                fn()
                self.log("cleaned " + label)
            except NotFoundError:
                self.log("already gone " + label)
            except Exception as ex:  # noqa: BLE001
                failures += 1
                print("  cleanup failed: " + label + ": " + type(ex).__name__ + ": " + str(ex))
        print("  " + str(len(self._cleanup)) + " steps, " + str(failures) + " failed")
        for c in (self.demo, self.client):
            if c is not None:
                try:
                    c.logout()
                except MyMorningNewsError:
                    pass
                c.close()

        passed = sum(1 for r in self.results if r.status == "PASS")
        failed = sum(1 for r in self.results if r.status == "FAIL")
        skipped = sum(1 for r in self.results if r.status == "SKIP")
        total_ms = sum(r.ms for r in self.results)
        print("")
        print("Summary: %d cases, %d passed, %d failed, %d skipped, %.1f ms" % (len(self.results), passed, failed, skipped, total_ms))
        if failed:
            print("Failed cases:")
            for r in self.results:
                if r.status == "FAIL":
                    print("  " + r.name + ": " + r.note)
        return 1 if failed or failures else 0


# ---------------------------------------------------------------------------
# Health, configuration, and authentication
# ---------------------------------------------------------------------------


@case("Health: anonymous health check")
def t_health(h: Harness) -> str:
    health = h.client.health()
    check(health.status in ("Healthy", "Degraded"), "status is Healthy or Degraded, got " + str(health.status))
    check(health.version, "version is set")
    return "version " + str(health.version)


@case("Client config: anonymous")
def t_client_config(h: Harness) -> None:
    cfg = h.client.client_config()
    check(cfg.product_name, "productName is set")
    check("Lattice AI LLC" in (cfg.copyright or ""), "copyright names Lattice AI LLC")


@case("Auth: bad password raises AuthenticationError")
def t_bad_login(h: Harness) -> None:
    with MyMorningNewsClient(h.config.base_url) as c:
        ex = expect_error(AuthenticationError, lambda: c.login(h.config.email, "wrong-" + uuid.uuid4().hex, h.config.tenant))
        eq(ex.status, 401, "status")
        check(c.token is None, "no token is kept after a failed login")


@case("Auth: login with email and password")
def t_login(h: Harness) -> None:
    result = h.client.login(h.config.email, h.config.password, h.config.tenant)
    check(result.token and h.client.token == result.token, "the token is stored on the client")
    check(result.tenant_id, "tenantId is returned")
    check(result.user is not None and result.user.email == h.config.email, "the user is returned")
    h.state["tenant"] = result.tenant_id
    h.state["user_id"] = result.user_id
    h.state["is_admin"] = bool(result.user.is_admin) if result.user else False


@case("Auth: token details and validation")
def t_token_details(h: Harness) -> None:
    h.need("tenant")
    d = h.client.token_details()
    check(d.valid is True, "token is valid")
    eq(d.tenant_id, h.state["tenant"], "tenantId")
    eq(d.email, h.config.email, "email")
    check(d.expires_utc, "expiresUtc is set")
    v = h.client.validate_token()
    check(v.valid is True, "GET /v1.0/token reports valid")


@case("Auth: me and update_me round trip")
def t_me(h: Harness) -> None:
    h.need("tenant")
    me = h.client.me()
    eq(me.email, h.config.email, "email")
    again = h.client.update_me(first_name=me.first_name, last_name=me.last_name)
    eq(again.first_name, me.first_name, "firstName unchanged")


@case("Server info and settings (admin)")
def t_server_info(h: Harness) -> str:
    h.need("tenant")
    info = h.client.server_info()
    check(info.version, "server version")
    if not h.state.get("is_admin"):
        raise Skip("settings need an administrator")
    settings = h.client.settings.get()
    check(isinstance(settings.get("settings"), dict), "settings object returned")
    return str(info.database_type)


@case("Auth: refresh issues a new working token")
def t_refresh(h: Harness) -> None:
    with MyMorningNewsClient(h.config.base_url) as c:
        first = c.login(h.config.email, h.config.password, h.config.tenant)
        fresh = c.refresh()
        check(fresh.token and fresh.token != first.token, "a new token is issued")
        check(c.me().email == h.config.email, "the new token works")
        c.logout()


@case("Auth: logout revokes the token")
def t_logout(h: Harness) -> None:
    c = MyMorningNewsClient(h.config.base_url)
    c.login(h.config.email, h.config.password, h.config.tenant)
    old = c.token
    c.logout()
    check(c.token is None, "token is cleared")
    c.token = old
    expect_error(AuthenticationError, c.me)
    c.token = None
    c.close()


@case("Auth: access key login and direct headers")
def t_access_key(h: Harness) -> str:
    tenant, user_id = h.need("tenant", "user_id")
    created = h.client.request("POST", "/v1.0/tenants/" + tenant + "/credentials",
                               body={"user_id": user_id, "name": h.name("key"), "auth_mode": "Hybrid"})
    cred = created.get("credential") or {}
    cred_id = cred.get("id")
    check(cred_id and cred.get("accessKey") and created.get("secretKey"), "credential, access key, and secret are returned")
    h.on_cleanup("credential " + cred_id, lambda: h.client.request("DELETE", "/v1.0/tenants/" + tenant + "/credentials/" + cred_id))
    with MyMorningNewsClient(h.config.base_url, access_key=cred["accessKey"], secret_key=created["secretKey"], tenant_id=tenant) as direct:
        eq(direct.token_details().scheme is not None, True, "direct headers authenticate")
        eq(direct.me().id, user_id, "direct access key resolves the user")
    with MyMorningNewsClient(h.config.base_url) as exchanged:
        r = exchanged.login_with_access_key(cred["accessKey"], created["secretKey"], tenant)
        check(r.token and r.credential_id == cred_id, "session issued for the credential")
        check(exchanged.token_details().valid, "exchanged token works")
        exchanged.logout()
    return "credential exercised and scheduled for deletion"


@case("Signing: known test vector (offline)")
def t_sign_vector(h: Harness) -> None:
    sig = mmn.sign_request("secret_test", "GET", "/v1.0/api/topics?pageSize=1", "default", "2026-01-01T00:00:00Z", "nonce-12345678", b"")
    eq(sig, "230d17d0e29816b3bec0fd251a730c14a192f0994792062dbebb7512589aae24", "signature")
    eq(mmn.body_sha256(None), mmn.body_sha256(b""), "no body hashes like an empty body")
    text = mmn.string_to_sign("get", "/x", "t", "d", "n", "ABC")
    eq(text, "MMN1-HMAC-SHA256\nGET\n/x\nt\nd\nn\nabc", "string to sign uppercases the method and lowercases the hash")
    expect_value_error(lambda: MyMorningNewsClient(h.config.base_url, access_key="ak", signed_requests=True))


@case("Signing: signed requests against the live server")
def t_signed_live(h: Harness) -> str:
    tenant, user_id = h.need("tenant", "user_id")
    created = h.client.request("POST", "/v1.0/tenants/" + tenant + "/credentials",
                               body={"user_id": user_id, "name": h.name("signed-key"), "auth_mode": "SignedRequest"})
    cred = created.get("credential") or {}
    cred_id = cred.get("id")
    access, secret = cred.get("accessKey"), created.get("secretKey")
    check(cred_id and access and secret, "credential, access key, and secret are returned")
    h.on_cleanup("credential " + cred_id, lambda: h.client.request("DELETE", "/v1.0/tenants/" + tenant + "/credentials/" + cred_id))
    with MyMorningNewsClient(h.config.base_url, access_key=access, secret_key=secret, tenant_id=tenant, signed_requests=True) as signed:
        eq(signed.token_details().scheme, "AccessKeySignature", "server saw a signature, not a secret")
        eq(signed.me().id, user_id, "GET with no query")
        page = signed.topics.list(search=h.prefix + " a&b=c/d", page_size=1, sort_direction="Descending")
        check(isinstance(page.items, list), "GET with an encoded query string")
        t = signed.topics.create(name=h.name("signed-topic"), description="Signed POST with a JSON body: caf\u00e9")
        h.on_cleanup("topic " + t.id, lambda: h.client.topics.delete(t.id))
        eq(signed.topics.update(t.id, description="signed PUT").description, "signed PUT", "PUT with a body")
        signed.topics.delete(t.id)
        expect_error(NotFoundError, lambda: signed.topics.get(t.id))
    with MyMorningNewsClient(h.config.base_url, access_key=access, secret_key="wrong-" + uuid.uuid4().hex, tenant_id=tenant, signed_requests=True) as bad:
        expect_error(AuthenticationError, bad.me)
    # Replaying the exact same signed headers must fail the second time (nonce reuse).
    path = "/v1.0/api/me"
    date = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
    nonce = "harness-" + uuid.uuid4().hex
    headers = {"x-access-key": access, "x-tenant-guid": tenant, "x-date": date, "x-nonce": nonce,
               "x-signature": mmn.sign_request(secret, "GET", path, tenant, date, nonce, b"")}
    first = requests.get(h.config.base_url + path, headers=headers, timeout=30)
    eq(first.status_code, 200, "first use of a nonce")
    second = requests.get(h.config.base_url + path, headers=headers, timeout=30)
    eq(second.status_code, 401, "replayed nonce is refused")
    stale = dict(headers, **{"x-nonce": "harness-" + uuid.uuid4().hex, "x-date": "2020-01-01T00:00:00Z"})
    stale["x-signature"] = mmn.sign_request(secret, "GET", path, tenant, stale["x-date"], stale["x-nonce"], b"")
    eq(requests.get(h.config.base_url + path, headers=stale, timeout=30).status_code, 401, "stale x-date is refused")
    return "GET, POST, PUT, DELETE signed; bad secret, replay, and stale date refused"


@case("Auth: cross-user client (demo user) signs in")
def t_demo_login(h: Harness) -> None:
    demo = MyMorningNewsClient(h.config.base_url, tenant_id=h.config.tenant)
    try:
        demo.login(h.config.demo_email, h.config.demo_password, h.config.tenant)
    except AuthenticationError:
        demo.close()
        raise Skip("demo user is not available")
    h.demo = demo
    h.state["demo"] = True


# ---------------------------------------------------------------------------
# Topics
# ---------------------------------------------------------------------------


@case("Topics: create")
def t_topic_create(h: Harness) -> None:
    h.need("tenant")
    t = h.client.topics.create(name=h.name("topic"), description="Created by the Python SDK harness.",
                               brief="Track solid-state battery research for the SDK harness.", audience_level="Expert",
                               email_on_run=False, seed_queries=["solid-state battery"])
    check(t.id and t.id.startswith("top_"), "topic ID has the top_ prefix")
    eq(t.status, "Draft", "new topics start as Draft")
    eq(t.audience_level, "Expert", "audienceLevel (snake_case input became camelCase)")
    eq(t.seed_queries, ["solid-state battery"], "seedQueries")
    h.state["topic"] = t.id
    h.on_cleanup("topic " + t.id, lambda: h.client.topics.delete(t.id))


@case("Topics: get detail")
def t_topic_get(h: Harness) -> None:
    tid = h.need("topic")
    d = h.client.topics.get(tid)
    eq(d.topic.id, tid, "topic.id")
    check(isinstance(d.dimensions, list) and isinstance(d.recipients, list), "dimensions and recipients are lists")


@case("Topics: update")
def t_topic_update(h: Harness) -> None:
    tid = h.need("topic")
    t = h.client.topics.update(tid, {"description": "Updated by the harness.", "noveltyPreference": 70})
    eq(t.description, "Updated by the harness.", "description")
    eq(t.novelty_preference, 70, "noveltyPreference (camelCase input)")
    eq(h.client.topics.get(tid).topic.novelty_preference, 70, "read back")


@case("Topics: list contains it (search filter)")
def t_topic_list(h: Harness) -> None:
    tid = h.need("topic")
    page = h.client.topics.list(search=h.prefix, page_size=10, sort_by="createdUtc", sort_direction=mmn.SortDirection.DESCENDING)
    check(tid in ids(page), "created topic is listed")
    check(page.total_count >= 1 and page.page_number == 1, "paging metadata")


@case("Pagination: iterate walks every page")
def t_iterate(h: Harness) -> None:
    tid = h.need("topic")
    seen = [t.id for t in mmn.iterate(h.client.topics.list, page_size=1, search=h.prefix)]
    check(tid in seen, "iterate yields the topic")
    eq(len(seen), len(set(seen)), "no duplicates across pages")


@case("Topics: dimensions add, list, update, delete")
def t_dimensions(h: Harness) -> None:
    tid = h.need("topic")
    d = h.client.topics.add_dimension(tid, name="Electrolytes", weight="High", include_terms=["sulfide"])
    eq(d.weight, "High", "weight")
    check(d.id in ids(h.client.topics.dimensions(tid)), "dimension listed")
    u = h.client.topics.update_dimension(tid, d.id, name="Solid electrolytes", weight="Normal")
    eq(u.name, "Solid electrolytes", "renamed")
    h.client.topics.delete_dimension(tid, d.id)
    check(d.id not in ids(h.client.topics.dimensions(tid)), "dimension removed")


@case("Topics: constraints add, filter by kind, update, delete")
def t_constraints(h: Harness) -> None:
    tid = h.need("topic")
    c = h.client.topics.add_constraint(tid, kind=mmn.ConstraintKind.EXCLUDE_KEYWORD, value="rumor")
    eq(c.kind, "ExcludeKeyword", "kind")
    check(c.id in ids(h.client.topics.constraints(tid, kind="ExcludeKeyword")), "listed under its kind")
    check(c.id not in ids(h.client.topics.constraints(tid, kind="IncludeKeyword")), "not listed under another kind")
    u = h.client.topics.update_constraint(tid, c.id, kind="ExcludeKeyword", value="speculation", note="harness")
    eq(u.value, "speculation", "value")
    h.client.topics.delete_constraint(tid, c.id)
    check(c.id not in ids(h.client.topics.constraints(tid)), "constraint removed")


@case("Topics: recipients add, list, delete")
def t_recipients(h: Harness) -> None:
    tid = h.need("topic")
    r = h.client.topics.add_recipient(tid, "sdk-python@example.com")
    eq(r.email, "sdk-python@example.com", "email")
    check(r.id in ids(h.client.topics.recipients(tid)), "recipient listed")
    h.client.topics.delete_recipient(tid, r.id)
    check(r.id not in ids(h.client.topics.recipients(tid)), "recipient removed")
    expect_error(BadRequestError, lambda: h.client.topics.add_recipient(tid, "not-an-email"), code="ValidationFailed")


@case("Topics: search providers choose and reset")
def t_topic_providers(h: Harness) -> None:
    tid = h.need("topic")
    defaults = h.client.topics.search_providers(tid)
    check(len(defaults) >= 1, "tenant defaults apply")
    chosen = h.client.topics.set_search_providers(tid, [defaults[0].id])
    eq(ids(chosen), [defaults[0].id], "only the chosen provider")
    h.client.topics.set_search_providers(tid, [])
    check(len(h.client.topics.search_providers(tid)) >= 1, "reset to defaults")


@case("Topics: pause, resume, archive")
def t_topic_lifecycle(h: Harness) -> None:
    tid = h.need("topic")
    eq(h.client.topics.pause(tid).status, "Paused", "paused")
    eq(h.client.topics.resume(tid).status, "Active", "resumed")
    eq(h.client.topics.archive(tid).status, "Archived", "archived")
    eq(h.client.topics.pause(tid).status, "Paused", "paused again (no schedule, so nothing runs)")


@case("Topics: resume a Draft without a brief raises ConflictError")
def t_topic_conflict(h: Harness) -> None:
    h.need("tenant")
    t = h.client.topics.create(name=h.name("draft"))
    h.on_cleanup("topic " + t.id, lambda: h.client.topics.delete(t.id))
    expect_error(ConflictError, lambda: h.client.topics.resume(t.id), code="Conflict")
    h.client.topics.delete(t.id)
    expect_error(NotFoundError, lambda: h.client.topics.get(t.id), code="NotFound")


@case("Topics: duplicate")
def t_topic_duplicate(h: Harness) -> None:
    tid = h.need("topic")
    copy = h.client.topics.duplicate(tid)
    h.on_cleanup("topic " + str(copy.topic.id), lambda: h.client.topics.delete(copy.topic.id))
    check(copy.topic.id != tid, "a new topic")
    eq(copy.topic.status, "Draft", "copies are drafts")
    check(copy.topic.name.endswith("(copy)"), "named as a copy")
    h.client.topics.delete(copy.topic.id)


@case("Topics: ranking recommendation")
def t_ranking(h: Harness) -> None:
    tid = h.need("topic")
    r = h.client.topics.ranking_recommendation(tid)
    check("current" in r and "recommended" in r, "current and recommended weights")


@case("Topics: create without a name raises BadRequestError")
def t_topic_bad(h: Harness) -> None:
    h.need("tenant")
    ex = expect_error(BadRequestError, lambda: h.client.topics.create(description="no name"), code="ValidationFailed")
    check(ex.request_id, "error carries a request ID")
    check(ex.message, "error carries a message")


@case("Topics: cross-user isolation (demo cannot see admin topic)")
def t_cross_user(h: Harness) -> None:
    tid = h.need("topic")
    if h.demo is None:
        raise Skip("demo user is not available")
    expect_error(NotFoundError, lambda: h.demo.topics.get(tid))
    check(tid not in ids(h.demo.topics.list(search=h.prefix)), "not in the demo user's list")
    expect_error(NotFoundError, lambda: h.demo.topics.update(tid, description="hijack"))
    expect_error(NotFoundError, lambda: h.demo.topics.delete(tid))
    eq(h.client.topics.get(tid).topic.id, tid, "still readable by its owner")


# ---------------------------------------------------------------------------
# Refinement and one-time requests
# ---------------------------------------------------------------------------


@case("Refinement: start, get, list, abandon (no model calls)")
def t_refinement(h: Harness) -> None:
    h.need("tenant")
    state = h.client.refinement.start(seed_text=h.name("wizard") + " perovskite solar cells", goal="Track progress")
    sid = state.session.id
    draft = state.topic.id
    h.on_cleanup("refinement " + sid, lambda: h.client.refinement.abandon(sid))
    h.on_cleanup("topic " + draft, lambda: h.client.topics.delete(draft))
    check(sid and draft, "session and draft topic")
    eq(h.client.refinement.get(sid).session.id, sid, "get")
    check(sid in ids(h.client.refinement.list(topic_id=draft)), "listed by topic")
    h.client.refinement.abandon(sid)
    expect_error(NotFoundError, lambda: h.client.topics.get(draft))


@case("Refinement: refine an existing topic, then abandon")
def t_refine_existing(h: Harness) -> None:
    tid = h.need("topic")
    state = h.client.topics.refine(tid)
    eq(state.topic.id, tid, "session is for the topic")
    h.client.refinement.abandon(state.session.id)
    eq(h.client.topics.get(tid).topic.id, tid, "the existing topic survives abandon")


@case("Refinement: generate dimensions and questions", needs_run=True)
def t_refinement_llm(h: Harness) -> None:
    state = h.client.refinement.start(seed_text=h.name("wizard-llm") + " grid-scale storage")
    h.on_cleanup("topic " + state.topic.id, lambda: h.client.topics.delete(state.topic.id))
    dims = h.client.refinement.generate_dimensions(state.session.id)
    check(len(dims.dimensions) >= 1, "dimensions proposed")
    qs = h.client.refinement.generate_questions(state.session.id)
    check(isinstance(qs.questions, list), "questions returned")
    if qs.questions:
        q = qs.questions[0]
        h.client.refinement.answer(state.session.id, q.question.id, skipped=True)
    h.client.refinement.abandon(state.session.id)


@case("Requests: list and 404 on a missing request")
def t_requests(h: Harness) -> None:
    h.need("tenant")
    page = h.client.requests.list(page_size=5)
    check(isinstance(page.items, list), "page of requests")
    expect_error(NotFoundError, lambda: h.client.requests.get("rrq_missing"))


@case("Requests: create, get, cancel, delete", needs_run=True)
def t_requests_llm(h: Harness) -> None:
    rr = h.client.requests.create(request_text=h.name("request") + " summarize sodium-ion battery news this week",
                                  skip_questions=True, run_at_utc=_in_days(30))
    rid = rr.request.id
    h.on_cleanup("request " + rid, lambda: h.client.requests.delete(rid))
    eq(h.client.requests.get(rid).request.id, rid, "get")
    h.client.requests.cancel(rid)
    h.client.requests.delete(rid)
    expect_error(NotFoundError, lambda: h.client.requests.get(rid))


# ---------------------------------------------------------------------------
# Schedules
# ---------------------------------------------------------------------------


@case("Schedules: preview valid and invalid")
def t_schedule_preview(h: Harness) -> None:
    tid = h.need("topic")
    p = h.client.schedules.preview(owner_type="Topic", owner_id=tid, interval_type="EveryNDays", interval_value=1,
                                   times_of_day=["07:00"], time_zone="UTC", preview_count=3)
    check(p.valid is True, "valid schedule")
    eq(len(p.next_runs_utc or []), 3, "three upcoming runs")
    bad = h.client.schedules.preview(owner_type="Topic", owner_id=tid, interval_type="Cron", cron_expression="not cron")
    check(bad.valid is False and bad.errors, "invalid cron reported")


@case("Schedules: create, get, update, list, delete")
def t_schedules(h: Harness) -> None:
    tid = h.need("topic")
    s = h.client.schedules.create(owner_type=mmn.ScheduleOwnerType.TOPIC, owner_id=tid, name=h.name("schedule"),
                                  interval_type=mmn.IntervalType.EVERY_N_DAYS, interval_value=1, times_of_day=["06:30"],
                                  time_zone="UTC", enabled=False)
    h.on_cleanup("schedule " + s.id, lambda: h.client.schedules.delete(s.id))
    eq(s.enabled, False, "created disabled so it never fires")
    eq(h.client.schedules.get(s.id).owner_id, tid, "ownerId")
    u = h.client.schedules.update(s.id, owner_type="Topic", owner_id=tid, interval_type="EveryNDays", interval_value=2,
                                  times_of_day=["06:30"], time_zone="UTC", enabled=False)
    eq(u.interval_value, 2, "intervalValue")
    check(s.id in ids(h.client.schedules.list(owner_type="Topic", owner_id=tid)), "listed by owner")
    h.client.schedules.delete(s.id)
    expect_error(NotFoundError, lambda: h.client.schedules.get(s.id))
    expect_error(BadRequestError, lambda: h.client.schedules.create(owner_type="Topic", name="missing owner"))


# ---------------------------------------------------------------------------
# Runs and research output
# ---------------------------------------------------------------------------


@case("Runs: list, get, stages, queries, results, wait")
def t_runs(h: Harness) -> str:
    h.need("tenant")
    page = h.client.runs.list(page_size=5, sort_by="createdUtc", sort_direction="Descending")
    check(isinstance(page.items, list), "page of runs")
    done = [r for r in page.items if r.status in mmn.TERMINAL_RUN_STATUSES]
    if not done:
        raise Skip("no finished run is visible")
    r = h.client.runs.get(done[0].id)
    eq(r.id, done[0].id, "get")
    check(isinstance(h.client.runs.stages(r.id), list), "stages list")
    check(isinstance(h.client.runs.queries(r.id), list), "queries list")
    check(isinstance(h.client.runs.results(r.id), list), "results list")
    calls = h.client.runs.llm_calls(r.id, page_size=50)
    check(isinstance(calls, mmn.Page), "run model calls are paged")
    eq(all(c.run_id == r.id for c in calls.items), True, "every call belongs to the run")
    stamps = [c.created_utc or "" for c in calls.items]
    eq(stamps, sorted(stamps), "oldest first")
    waited = h.client.runs.wait(r.id, timeout=10, interval=0.5)
    check(waited.status in mmn.TERMINAL_RUN_STATUSES, "wait returns a finished run at once")
    return r.id


class _FakeStream:
    """A stand-in for a streaming response, used by the offline SSE case."""

    def __init__(self, lines: List[bytes]) -> None:
        self.lines = lines
        self.closed = False

    def iter_lines(self, delimiter: Optional[bytes] = None) -> Any:
        return iter(self.lines)

    def close(self) -> None:
        self.closed = True


@case("SSE: parser handles comments, multi-line data, defaults (offline)")
def t_sse_parser(h: Harness) -> None:
    lines: List[Any] = [
        ": connected",
        "retry: 3000",
        "id: 1",
        "event: stage",
        'data: {"stage":"Search",',
        'data: "status":"Running"}',
        "",
        ": a comment between events",
        "data: plain text",
        "",
        "event: ping",
        "data:{}",
        "",
        "event: no-data",
        "",
        "",
        b"id: 7\r",
        b"event: done\r",
        b'data: {"id":"run_x","status":"Completed"}\r',
        b"\r",
        "event: unfinished",
        "data: {}",
    ]
    events = list(mmn.parse_sse(lines))
    eq([e.event for e in events], ["stage", "message", "ping", "done"], "event names (no-data and unfinished events dropped)")
    stage = events[0]
    eq(stage.id, "1", "id")
    eq(stage.retry, 3000, "retry")
    eq(stage.raw_data, '{"stage":"Search",\n"status":"Running"}', "data lines joined with a newline")
    eq(stage.data, {"stage": "Search", "status": "Running"}, "multi-line JSON parsed")
    eq(stage.stage.stage if stage.stage else None, "Search", "typed RunStage")
    eq(events[1].data, "plain text", "non-JSON data stays text")
    eq(events[1].id, None, "no id on an event without one")
    eq(events[2].data, {}, "no space after the colon")
    done = events[3]
    check(done.is_final and done.run is not None and done.run.status == "Completed", "done carries the final Run (CRLF lines)")
    eq(done.id, "7", "id from CRLF line")
    fake = _FakeStream([b"event: run", b'data: {"id":"r","status":"Running"}', b"", b"event: done", b'data: {"id":"r","status":"Failed"}', b"",
                        b"event: run", b'data: {"id":"r"}', b""])
    seen = [e.event for e in mmn.RunsResource._event_stream(fake, None)]  # type: ignore[arg-type]
    eq(seen, ["run", "done"], "the stream stops after done")
    check(fake.closed, "the response is closed")


def _events_route_or_skip(h: Harness) -> None:
    """Skip when the server predates the run events route."""
    try:
        h.client.runs.events("run_missing")
    except NotFoundError as ex:
        if ex.code == "RouteNotFound":
            raise Skip("this server has no /runs/{id}/events route yet")
        eq(ex.code, "NotFound", "missing run is a typed 404")
        check(isinstance(ex.body, dict) and ex.body.get("requestId"), "JSON error body")
        return
    raise AssertionError("events on a missing run should raise NotFoundError")


@case("Runs: events and retry return 404 JSON for a missing run")
def t_run_events_404(h: Harness) -> None:
    h.need("tenant")
    _events_route_or_skip(h)
    expect_error(NotFoundError, lambda: h.client.runs.retry("run_missing"), code="NotFound")
    expect_error(NotFoundError, lambda: h.client.runs.retry("run_missing", from_stage="Notify"), code="NotFound")
    if h.demo is not None:
        expect_error(NotFoundError, lambda: h.demo.runs.events("run_missing"), code="NotFound")


@case("Runs: retry an unfinished run raises ConflictError")
def t_run_retry_409(h: Harness) -> str:
    h.need("tenant")
    _events_route_or_skip(h)
    active = [r for r in h.client.runs.list(page_size=50).items if r.status not in mmn.TERMINAL_RUN_STATUSES]
    if not active:
        raise Skip("no queued or running run is visible (making one would start a paid run)")
    ex = expect_error(ConflictError, lambda: h.client.runs.retry(active[0].id, from_stage="Notify"), code="Conflict")
    check(active[0].status or "", "status known")
    return ex.message


@case("Runs: replay a finished run's events, retry with a bad stage")
def t_run_events_replay(h: Harness) -> str:
    h.need("tenant")
    _events_route_or_skip(h)
    done_runs = [r for r in h.client.runs.list(page_size=20).items if r.status in mmn.TERMINAL_RUN_STATUSES]
    if not done_runs:
        raise Skip("no finished run is visible")
    r = done_runs[0]
    events = list(h.client.runs.events(r.id, timeout=60))
    check(events, "events received")
    eq(events[-1].event, "done", "a finished run ends with done")
    eq(events[-1].run.id if events[-1].run else None, r.id, "done carries the run")
    eq(len([e for e in events if e.event == "stage"]), len(h.client.runs.stages(r.id)), "every stage is replayed")
    # An unknown stage is rejected before anything is queued, so this spends nothing.
    expect_error(BadRequestError, lambda: h.client.runs.retry(r.id, from_stage="NotAStage"), code="ValidationFailed")
    return str(len(events)) + " events for " + r.id


@case("Runs: 404 on a missing run")
def t_run_missing(h: Harness) -> None:
    h.need("tenant")
    expect_error(NotFoundError, lambda: h.client.runs.get("run_missing"), code="NotFound")
    expect_error(NotFoundError, lambda: h.client.runs.llm_calls("run_missing"))
    expect_error(NotFoundError, lambda: h.client.topics.run("top_missing"))


@case("Runs: dry run a topic, wait, stages, findings, email preview", needs_run=True)
def t_run_live(h: Harness) -> str:
    tid = h.need("topic")
    h.client.topics.resume(tid)
    run = h.client.topics.dry_run(tid)
    eq(run.is_dry_run, True, "dry run")
    final = h.client.runs.wait(run.id, timeout=1800, interval=5)
    check(final.status in mmn.TERMINAL_RUN_STATUSES, "finished")
    check(len(h.client.runs.stages(run.id)) >= 1, "stages recorded")
    check(h.client.runs.llm_calls(run.id).total_count >= 1, "model calls recorded for the run")
    replay = list(h.client.runs.events(run.id, timeout=120))
    eq(replay[-1].event, "done", "finished run stream ends with done")
    resumed = h.client.runs.retry(run.id, from_stage="Notify")
    eq(resumed.id, run.id, "resuming keeps the same run")
    eq(resumed.resume_from_stage, "Notify", "resumeFromStage")
    streamed = [e.event for e in h.client.runs.events(run.id, timeout=600)]
    eq(streamed[-1], "done", "live stream ends with done")
    h.client.runs.wait(run.id, timeout=600, interval=5)
    h.client.findings.list(run_id=run.id, include_dry_run=True)
    h.client.runs.email_preview(run.id)
    h.client.topics.pause(tid)
    return str(final.status)


@case("Findings: list, get, 404")
def t_findings(h: Harness) -> str:
    h.need("tenant")
    page = h.client.findings.list(page_size=3, include_dry_run=True)
    expect_error(NotFoundError, lambda: h.client.findings.get("fnd_missing"))
    if not page.items:
        return "no findings to read"
    d = h.client.findings.get(page.items[0].id)
    eq(d.finding.id, page.items[0].id, "finding detail")
    check(isinstance(d.evidence, list), "evidence list")
    return "read " + page.items[0].id


@case("Findings: pin, hide, include_hidden, delete")
def t_findings_edit(h: Harness) -> str:
    h.need("tenant")
    expect_error(NotFoundError, lambda: h.client.findings.update("fnd_missing", pinned=True))
    expect_error(NotFoundError, lambda: h.client.findings.delete("fnd_missing"))
    check(isinstance(h.client.findings.list(include_hidden=True, pinned=True, page_size=5).items, list), "include_hidden and pinned filters")
    mine = [f for f in h.client.findings.list(page_size=50, include_dry_run=True, include_hidden=True).items if f.user_id == h.state.get("user_id")]
    if not mine:
        raise Skip("no findings of my own to edit")
    f = mine[0]
    was_pinned, was_hidden = bool(f.pinned), bool(f.hidden)
    try:
        eq(h.client.findings.pin(f.id).pinned, True, "pinned")
        check(f.id in ids(h.client.findings.list(pinned=True, include_dry_run=True, include_hidden=True, page_size=100)), "listed as pinned")
        hidden = h.client.findings.hide(f.id)
        eq(hidden.hidden, True, "hidden")
        check(f.id not in ids(h.client.findings.list(include_dry_run=True, page_size=1000, run_id=f.run_id)), "hidden findings are left out by default")
        check(f.id in ids(h.client.findings.list(include_dry_run=True, include_hidden=True, page_size=1000, run_id=f.run_id)), "include_hidden lists it")
    finally:
        h.client.findings.update(f.id, pinned=was_pinned, hidden=was_hidden)
    return "edited and restored " + f.id + " (delete is not exercised on real data)"


@case("Evidence: get and 404")
def t_evidence(h: Harness) -> str:
    h.need("tenant")
    expect_error(NotFoundError, lambda: h.client.evidence.get("evd_missing"))
    for f in h.client.findings.list(page_size=10, include_dry_run=True).items:
        d = h.client.findings.get(f.id)
        for view in d.evidence or []:
            if view.evidence is not None and view.evidence.id:
                e = h.client.evidence.get(view.evidence.id)
                eq(e.evidence.id, view.evidence.id, "evidence")
                eq(h.client.findings.evidence(view.evidence.id).evidence.id, view.evidence.id, "findings.evidence alias")
                return "read " + view.evidence.id
    return "no evidence to read"


@case("Sources: list, get, 404")
def t_sources(h: Harness) -> str:
    h.need("tenant")
    page = h.client.sources.list(page_size=3)
    expect_error(NotFoundError, lambda: h.client.sources.get("src_missing"))
    if not page.items:
        return "no sources to read"
    eq(h.client.sources.get(page.items[0].id).id, page.items[0].id, "source")
    return "read " + page.items[0].id


@case("Predictions: list, get, resolve and review 404s")
def t_predictions(h: Harness) -> str:
    h.need("tenant")
    page = h.client.predictions.list(page_size=3, include_dry_run=True)
    expect_error(NotFoundError, lambda: h.client.predictions.get("prd_missing"))
    expect_error(NotFoundError, lambda: h.client.predictions.resolve("prd_missing", mmn.PredictionStatus.ACCURATE, note="x"))
    expect_error(NotFoundError, lambda: h.client.predictions.accept_review("prv_missing"))
    expect_error(NotFoundError, lambda: h.client.predictions.reject_review("prv_missing"))
    if not page.items:
        return "no predictions to read"
    d = h.client.predictions.get(page.items[0].id)
    eq(d.prediction.id, page.items[0].id, "prediction detail")
    return "read " + page.items[0].id


@case("Suggestions: list, get, set_status 404")
def t_suggestions(h: Harness) -> str:
    h.need("tenant")
    page = h.client.suggestions.list(page_size=3, include_dry_run=True)
    expect_error(NotFoundError, lambda: h.client.suggestions.get("sug_missing"))
    expect_error(NotFoundError, lambda: h.client.suggestions.set_status("sug_missing", mmn.SuggestionStatus.DISMISSED))
    if not page.items:
        return "no suggestions to read"
    eq(h.client.suggestions.get(page.items[0].id).id, page.items[0].id, "suggestion")
    return "read " + page.items[0].id


# ---------------------------------------------------------------------------
# Learned preferences and feedback
# ---------------------------------------------------------------------------


@case("Preferences: create, get, update, disable, enable, list")
def t_preferences(h: Harness) -> None:
    tid = h.need("topic")
    p = h.client.preferences.create(statement=h.name("pref") + " prefer peer-reviewed sources", kind=mmn.PreferenceKind.PREFER, topic_id=tid)
    h.on_cleanup("preference " + p.id, lambda: h.client.preferences.delete(p.id))
    eq(p.status, "Active", "written preferences are active at once")
    eq(h.client.preferences.get(p.id).preference.id, p.id, "detail")
    u = h.client.preferences.update(p.id, statement=h.name("pref") + " prefer primary sources")
    check(u.statement.endswith("primary sources"), "statement updated")
    eq(h.client.preferences.disable(p.id).status, "Disabled", "disabled")
    eq(h.client.preferences.enable(p.id).status, "Active", "enabled")
    check(p.id in ids(h.client.preferences.list(topic_id=tid)), "listed by topic")
    expect_error(ConflictError, lambda: h.client.preferences.accept(p.id))
    expect_error(ConflictError, lambda: h.client.preferences.reject(p.id))
    h.state["preference"] = p.id


@case("Feedback: submit with reason, list, update, delete")
def t_feedback(h: Harness) -> None:
    pid = h.need("preference")
    f = h.client.feedback.submit(mmn.FeedbackTargetType.LEARNED_PREFERENCE, pid, relevance=mmn.RelevanceRating.RELEVANT,
                                 reason="SDK harness: matches what I want.")
    h.on_cleanup("feedback " + f.id, lambda: h.client.feedback.delete(f.id))
    eq(f.target_id, pid, "targetId")
    eq(f.relevance, "Relevant", "relevance")
    eq(f.channel, "Api", "channel")
    check(f.id in ids(h.client.feedback.list(target_type="LearnedPreference", target_id=pid)), "listed by target")
    u = h.client.feedback.update(f.id, reason="SDK harness: edited reason.")
    eq(u.reason, "SDK harness: edited reason.", "reason edited")
    h.client.feedback.delete(f.id)
    check(f.id not in ids(h.client.feedback.list(target_type="LearnedPreference", target_id=pid)), "retracted")


@case("Feedback: Mcp channel")
def t_feedback_mcp(h: Harness) -> None:
    pid = h.need("preference")
    f = h.client.feedback.submit("LearnedPreference", pid, relevance="SomewhatRelevant", channel=mmn.FeedbackChannel.MCP)
    h.on_cleanup("feedback " + f.id, lambda: h.client.feedback.delete(f.id))
    eq(f.channel, "Mcp", "channel")
    check(f.id in ids(h.client.feedback.list(channel="Mcp", target_id=pid)), "listed by channel")
    h.client.feedback.delete(f.id)


@case("Feedback: no rating and no reason raises BadRequestError")
def t_feedback_bad(h: Harness) -> None:
    pid = h.need("preference")
    expect_error(BadRequestError, lambda: h.client.feedback.create(target_type="LearnedPreference", target_id=pid))
    expect_error(NotFoundError, lambda: h.client.feedback.submit("Finding", "fnd_missing", relevance="Relevant"))


@case("Preferences: delete, then 404")
def t_preference_delete(h: Harness) -> None:
    pid = h.need("preference")
    h.client.preferences.delete(pid)
    expect_error(NotFoundError, lambda: h.client.preferences.get(pid))


# ---------------------------------------------------------------------------
# Editions, briefings, templates, prompts
# ---------------------------------------------------------------------------


@case("Editions: create, get, update, set topics, list")
def t_editions(h: Harness) -> None:
    tid = h.need("topic")
    e = h.client.editions.create(name=h.name("edition"), description="Harness edition", audio_enabled=False, email_enabled=False, enabled=False)
    eid = e.edition.id
    h.on_cleanup("edition " + eid, lambda: h.client.editions.delete(eid))
    eq(e.edition.name, h.name("edition"), "name")
    eq(h.client.editions.get(eid).edition.id, eid, "get")
    u = h.client.editions.update(eid, description="Harness edition, updated")
    eq(u.edition.description, "Harness edition, updated", "description")
    withtopics = h.client.editions.set_topics(eid, [tid])
    eq([t.topic_id for t in withtopics.topics], [tid], "topics in order")
    check(eid in ids(h.client.editions.list(page_size=100)), "listed")
    expect_error(ConflictError, lambda: h.client.editions.create(name=h.name("edition")))
    h.state["edition"] = eid


@case("Editions: compose and preview", needs_run=True)
def t_edition_compose(h: Harness) -> None:
    eid = h.need("edition")
    preview = h.client.editions.preview(eid)
    check(preview.id or preview.raw, "preview returned")
    queued = h.client.editions.compose(eid)
    h.on_cleanup("briefing " + str(queued.id), lambda: h.client.briefings.delete(queued.id))


@case("Feed tokens: create, list, public feed, revoke, purge")
def t_feed_tokens(h: Harness) -> None:
    eid = h.need("edition")
    created = h.client.feed_tokens.create(name=h.name("feed"), edition_id=eid)
    tok = created.token
    h.on_cleanup("feed token " + tok.id, lambda: h.client.feed_tokens.purge(tok.id))
    check(created.secret and created.feed_url, "secret and feed URL returned once")
    eq(tok.token_hash, "redacted", "hash is redacted")
    check(tok.id in ids(h.client.feed_tokens.list()), "listed")
    xml = h.client.public.podcast_feed(created.secret)
    check("<rss" in xml, "public feed is RSS")
    h.client.feed_tokens.revoke(tok.id)
    revoked = [t for t in h.client.feed_tokens.list() if t.id == tok.id]
    check(revoked and revoked[0].revoked_utc, "revokedUtc set")
    expect_error(NotFoundError, lambda: h.client.public.podcast_feed(created.secret))
    h.client.feed_tokens.delete(tok.id, purge=True)
    check(tok.id not in ids(h.client.feed_tokens.list()), "purged")


@case("Editions: delete, then 404")
def t_edition_delete(h: Harness) -> None:
    eid = h.need("edition")
    h.client.editions.delete(eid)
    expect_error(NotFoundError, lambda: h.client.editions.get(eid))


@case("Briefings: list, get, download Markdown and HTML, byte range")
def t_briefings(h: Harness) -> str:
    h.need("tenant")
    expect_error(NotFoundError, lambda: h.client.briefings.get("brf_missing"))
    page = h.client.briefings.list(page_size=10, status="Ready")
    if not page.items:
        raise Skip("no ready briefing is visible")
    b = page.items[0]
    d = h.client.briefings.get(b.id)
    eq(d.briefing.id, b.id, "detail")
    if d.files_expired:
        raise Skip("briefing files passed retention")
    md = h.client.briefings.download(b.id, "md")
    check(md.content and "markdown" in (md.content_type or ""), "Markdown download")
    check(md.file_name, "file name from Content-Disposition")
    html = h.client.briefings.html(b.id)
    check("<" in html, "HTML download")
    part = h.client.briefings.download(b.id, "md", byte_range=(0, 9))
    eq(part.status, 206, "partial content")
    eq(part.content, md.content[:10], "range bytes match")
    expect_error(BadRequestError, lambda: h.client.briefings.download(b.id, "pdf"))
    return b.id


@case("Artifacts: list, get, content, range, download URL")
def t_artifacts(h: Harness) -> str:
    h.need("tenant")
    expect_error(NotFoundError, lambda: h.client.artifacts.get("art_missing"))
    page = h.client.artifacts.list(page_size=10)
    live = [a for a in page.items if not a.deleted_utc and (a.bytes or 0) > 16]
    if not live:
        raise Skip("no stored files are visible")
    a = live[0]
    eq(h.client.artifacts.get(a.id).id, a.id, "metadata")
    full = h.client.artifacts.content(a.id)
    eq(len(full.content), a.bytes, "full content length")
    tail = h.client.artifacts.content(a.id, byte_range=(None, 8))
    eq(tail.content, full.content[-8:], "suffix range")
    eq(tail.total_bytes, a.bytes, "total from Content-Range")
    url = h.client.artifacts.download_url(a.id)
    check(url.content_path and url.content_path.endswith("/content"), "content path")
    expect_error(mmn.RangeNotSatisfiableError, lambda: h.client.artifacts.content(a.id, byte_range=(a.bytes + 10, None)))
    return a.id


@case("Media: list and get")
def t_media(h: Harness) -> str:
    h.need("tenant")
    expect_error(NotFoundError, lambda: h.client.media.get("med_missing"))
    page = h.client.media.list(page_size=5)
    if not page.items:
        return "no audio episodes to read"
    d = h.client.media.get(page.items[0].id)
    eq(d.media.id, page.items[0].id, "episode")
    return "read " + page.items[0].id


@case("Templates: list, get, export, validate")
def t_templates_read(h: Harness) -> None:
    h.need("tenant")
    page = h.client.templates.list(scope="shared")
    check(page.items, "shared templates exist")
    seeded = page.items[0]
    d = h.client.templates.get(seeded.id)
    check(d.content, "content")
    ex = h.client.templates.export(seeded.id)
    check("markdown" in (ex.content_type or "") and ex.file_name and ex.file_name.endswith(".md"), "Markdown export")
    eq(ex.text, d.content, "export matches content")
    v = h.client.templates.validate(d.content)
    check(v.valid is True, "seeded template validates")
    h.state["template_content"] = d.content


@case("Templates: create, update, versions, rollback, delete")
def t_templates_write(h: Harness) -> None:
    content = h.need("template_content")
    t = h.client.templates.create(content=content, name=h.name("template"))
    tid = t.template.id
    h.on_cleanup("template " + tid, lambda: h.client.templates.delete(tid))
    eq(t.template.name, h.name("template"), "name")
    eq(t.shared, False, "personal template")
    u = h.client.templates.update(tid, content=content + "\n", change_note="harness edit")
    eq(u.template.current_version, 2, "new version")
    versions = h.client.templates.versions(tid)
    check(len(versions) >= 2, "two versions")
    r = h.client.templates.rollback(tid, 1)
    check((r.template.current_version or 0) >= 2, "rollback saved as a version")
    check(tid in ids(h.client.templates.list(scope="mine")), "listed as mine")
    expect_error(ConflictError, lambda: h.client.templates.reset(tid))
    h.client.templates.delete(tid)
    expect_error(NotFoundError, lambda: h.client.templates.get(tid))


@case("Prompts: list, get, versions")
def t_prompts_read(h: Harness) -> None:
    h.need("tenant")
    page = h.client.prompts.list(tenant_only=True, page_size=50)
    check(page.items, "prompts exist")
    p = page.items[0]
    d = h.client.prompts.get(p.id)
    eq(d.template.key, p.key, "key")
    check(d.content, "content")
    check(len(h.client.prompts.versions(p.id)) >= 1, "versions")
    expect_error(ConflictError, lambda: h.client.prompts.delete(p.id))
    h.state["prompt_key"] = p.key
    h.state["prompt_content"] = d.content


@case("Prompts: topic override create, update, versions, rollback, reset, delete")
def t_prompt_override(h: Harness) -> None:
    tid, key, content = h.need("topic", "prompt_key", "prompt_content")
    o = h.client.prompts.create_override(key, tid, content)
    oid = o.template.id
    h.on_cleanup("prompt override " + oid, lambda: h.client.prompts.delete(oid))
    eq(o.template.topic_id, tid, "override is for the topic")
    expect_error(ConflictError, lambda: h.client.prompts.create_override(key, tid, content))
    u = h.client.prompts.update(oid, content=content + "\n", change_note="harness edit")
    check((u.template.current_version or 0) >= 2, "new version")
    check(len(h.client.prompts.versions(oid)) >= 2, "versions")
    h.client.prompts.rollback(oid, 1)
    h.client.prompts.reset(oid)
    check(oid in ids(h.client.prompts.list(topic_id=tid)), "listed for the topic")
    h.client.prompts.delete(oid)
    expect_error(NotFoundError, lambda: h.client.prompts.get(oid))


@case("Prompts: prompt lab", needs_run=True)
def t_prompt_lab(h: Harness) -> None:
    key = h.need("prompt_key")
    r = h.client.prompts.lab(key=key, variables={"topic.name": "SDK harness"})
    check(isinstance(r, dict), "lab result")


# ---------------------------------------------------------------------------
# Email, analytics, usage, model calls
# ---------------------------------------------------------------------------


@case("Email: preferences get and put")
def t_email_prefs(h: Harness) -> None:
    h.need("tenant")
    p = h.client.email.preferences()
    check(p.address, "address")
    again = h.client.email.set_preferences(run_emails=p.run_emails, briefing_emails=p.briefing_emails, failure_emails=p.failure_emails)
    eq(again.run_emails, p.run_emails, "unchanged runEmails")


@case("Email: test email, outbox list, get, resend, delete")
def t_email(h: Harness) -> str:
    h.need("tenant")
    m = h.client.email.test()
    h.on_cleanup("email " + m.id, lambda: h.client.email.delete(m.id))
    eq(m.recipient, h.config.email, "sent to me")
    check(m.id in ids(h.client.email.list(recipient=h.config.email, page_size=50, sort_by="createdUtc", sort_direction="Descending")), "in the outbox")
    got = h.client.email.get(m.id)
    eq(got.id, m.id, "get")
    deadline = time.monotonic() + 45
    while got.status in ("Queued", "Sending") and time.monotonic() < deadline:
        time.sleep(2)
        got = h.client.email.get(m.id)
    note = "final status " + str(got.status)
    if got.status in ("Queued", "Sending"):
        expect_error(ConflictError, lambda: h.client.email.resend(m.id))
    else:
        again = h.client.email.resend(m.id)
        eq(again.id, m.id, "resend keeps the message")
    h.client.email.delete(m.id)
    expect_error(NotFoundError, lambda: h.client.email.get(m.id))
    return note


@case("Analytics: relevance, accuracy, feedback, reasons, suggestions, quality")
def t_analytics(h: Harness) -> None:
    h.need("tenant")
    a = h.client.analytics
    for name, fn in (("relevance", a.relevance), ("accuracy", a.accuracy), ("feedback", a.feedback),
                     ("reasons", a.reasons), ("suggestions", a.suggestions), ("quality", a.quality)):
        r = fn(from_="2026-01-01T00:00:00Z")
        check(isinstance(r, (dict, mmn.Model)), name + " returns an object")
    fb = a.feedback(from_="2026-01-01T00:00:00Z")
    check(isinstance(fb, mmn.FeedbackReport) and isinstance(fb.by_channel_day, list), "feedback report with byChannelDay")


@case("Analytics: overview and email reports")
def t_analytics_new(h: Harness) -> None:
    h.need("tenant")
    o = h.client.analytics.overview()
    check(isinstance(o, mmn.OverviewReport), "overview report")
    check(isinstance(o.runs, int) and isinstance(o.active_topics, int) and o.from_utc and o.to_utc, "overview counts and window")
    check((o.runs_completed or 0) <= (o.runs or 0), "completed runs do not exceed runs")
    e = h.client.analytics.email(from_="2026-01-01T00:00:00Z")
    check(isinstance(e, mmn.EmailReport), "email report")
    check(isinstance(e.by_template, list) and isinstance(e.by_status, list) and isinstance(e.by_day, list), "breakdowns")
    check((e.sent or 0) + (e.failed or 0) <= (e.total or 0), "sent plus failed do not exceed total")
    if e.by_status:
        check(isinstance(e.by_status[0], mmn.BreakdownRow), "breakdown rows are typed")
    if h.state.get("is_admin"):
        check(isinstance(h.client.analytics.overview(all_=True), mmn.OverviewReport), "tenant-wide overview for admins")


@case("Usage: me, report, timeseries")
def t_usage(h: Harness) -> None:
    h.need("tenant")
    me = h.client.usage.me()
    check(me.totals is not None and isinstance(me.rows, list), "usage report")
    rep = h.client.usage.report(group_by=mmn.UsageGroupBy.MODEL, include_dry_run=True)
    check(rep.totals is not None, "report totals")
    ts = h.client.usage.timeseries(bucket_minutes=1440)
    eq(ts.bucket_minutes, 1440, "bucketMinutes")
    check(len(ts.buckets or []) >= 1, "gap-filled buckets")


@case("LLM calls: list and get")
def t_llm_calls(h: Harness) -> str:
    h.need("tenant")
    page = h.client.llm_calls.list(page_size=2)
    expect_error(NotFoundError, lambda: h.client.llm_calls.get("llm_missing"))
    if not page.items:
        return "no model calls recorded"
    c = h.client.llm_calls.get(page.items[0].id)
    eq(c.id, page.items[0].id, "call detail")
    return "read " + c.id


# ---------------------------------------------------------------------------
# Model endpoints, search providers, voices, vault, devices
# ---------------------------------------------------------------------------


@case("Model endpoints: create, get, update, list")
def t_endpoints(h: Harness) -> None:
    h.need("tenant")
    e = h.client.model_endpoints.create(name=h.name("ollama"), provider=mmn.ModelProvider.OLLAMA, endpoint="http://host.docker.internal:11434",
                                        embedding_model="all-minilm", supports_chat=False, supports_embeddings=True, timeout_ms=5000, active=True)
    h.on_cleanup("model endpoint " + e.id, lambda: h.client.model_endpoints.delete(e.id))
    eq(e.provider, "Ollama", "provider")
    eq(h.client.model_endpoints.get(e.id).id, e.id, "get")
    u = h.client.model_endpoints.update(e.id, name=h.name("ollama-2"), provider="Ollama", endpoint="http://host.docker.internal:11434",
                                        embedding_model="all-minilm", supports_chat=False, supports_embeddings=True, timeout_ms=6000)
    eq(u.timeout_ms, 6000, "timeoutMs")
    check(e.id in ids(h.client.model_endpoints.list(provider="Ollama", page_size=100)), "listed by provider")
    expect_error(BadRequestError, lambda: h.client.model_endpoints.create(provider="Ollama"))
    h.state["endpoint"] = e.id


@case("Model endpoints: validate and list models (local Ollama)")
def t_endpoint_validate(h: Harness) -> str:
    eid = h.need("endpoint")
    v = h.client.model_endpoints.validate(eid)
    check(isinstance(v, mmn.ModelValidation) and v.raw, "validation result")
    try:
        models = h.client.model_endpoints.models(eid)
        check(isinstance(models, list), "model list")
        return "reachable=" + str(v.reachable) + ", " + str(len(models)) + " models"
    except mmn.ServerError as ex:
        eq(ex.code, "UpstreamError", "unreachable provider is a typed 502")
        return "Ollama not reachable from the server (typed UpstreamError)"


@case("Model endpoints: test sends a sample prompt", needs_run=True)
def t_endpoint_test(h: Harness) -> None:
    page = h.client.model_endpoints.list(supports_chat=True)
    check(page.items, "a chat endpoint exists")
    r = h.client.model_endpoints.test(page.items[0].id, prompt="Reply with the single word ok.")
    check(r.success is True, "test call succeeded: " + str(r.error))


@case("Model assignments: topic override set, list, delete")
def t_assignments(h: Harness) -> None:
    tid, eid = h.need("topic", "endpoint")
    a = h.client.model_assignments.set(task=mmn.ModelTask.EMBEDDING, topic_id=tid, model_endpoint_id=eid)
    h.on_cleanup("assignment " + a.id, lambda: h.client.model_assignments.delete(a.id))
    eq(a.topic_id, tid, "topic override")
    check(a.id in ids(h.client.model_assignments.list(topic_id=tid)), "listed for the topic")
    h.client.model_assignments.delete(a.id)
    check(a.id not in ids(h.client.model_assignments.list(topic_id=tid)), "removed")


@case("Model endpoints: delete, then 404")
def t_endpoint_delete(h: Harness) -> None:
    eid = h.need("endpoint")
    h.client.model_endpoints.delete(eid)
    expect_error(NotFoundError, lambda: h.client.model_endpoints.get(eid))


@case("Search providers: create, get, update, list, test, delete")
def t_search_providers(h: Harness) -> str:
    h.need("tenant")
    p = h.client.search_providers.create(name=h.name("hn"), kind=mmn.SearchProviderKind.HACKER_NEWS, is_default=False, max_results=5, active=True)
    h.on_cleanup("search provider " + p.id, lambda: h.client.search_providers.delete(p.id))
    eq(p.kind, "HackerNews", "kind")
    eq(h.client.search_providers.get(p.id).id, p.id, "get")
    u = h.client.search_providers.update(p.id, name=h.name("hn-2"), kind="HackerNews", max_results=7, is_default=False)
    eq(u.max_results, 7, "maxResults")
    check(p.id in ids(h.client.search_providers.list(kind="HackerNews")), "listed by kind")
    outcome = h.client.search_providers.test(p.id, query="battery", max_results=3)
    check(isinstance(outcome, dict) and "success" in outcome, "test outcome")
    h.client.search_providers.delete(p.id)
    expect_error(NotFoundError, lambda: h.client.search_providers.get(p.id))
    expect_error(BadRequestError, lambda: h.client.search_providers.create(name=h.name("nokind")))
    return "test success=" + str(outcome.get("success"))


@case("Voices: create, get, update, list, test, delete")
def t_voices(h: Harness) -> str:
    h.need("tenant")
    v = h.client.voices.create(name=h.name("voice"), provider="OpenAiCompatibleSpeech", model="kokoro", voice="af_heart", speed=1.0, is_default=False)
    h.on_cleanup("voice " + v.id, lambda: h.client.voices.delete(v.id))
    eq(v.voice, "af_heart", "voice")
    eq(h.client.voices.get(v.id).id, v.id, "get")
    u = h.client.voices.update(v.id, name=h.name("voice"), provider="OpenAiCompatibleSpeech", model="kokoro", voice="af_heart", speed=1.2)
    eq(u.speed, 1.2, "speed")
    check(v.id in ids(h.client.voices.list(page_size=100)), "listed")
    try:
        audio = h.client.voices.test(v.id, "Testing the Python SDK.")
        check(audio.content and (audio.content_type or "").startswith("audio"), "audio bytes")
        note = str(len(audio.content)) + " audio bytes"
    except mmn.ServerError as ex:
        eq(ex.code, "TtsError", "speech failure is a typed 502")
        note = "speech service unavailable (typed TtsError)"
    h.client.voices.delete(v.id)
    expect_error(NotFoundError, lambda: h.client.voices.get(v.id))
    expect_error(BadRequestError, lambda: h.client.voices.create(name=h.name("novoice")))
    return note


@case("Vault: create, get, update, usage, list, delete")
def t_vault(h: Harness) -> None:
    h.need("tenant")
    c = h.client.vault.create(name=h.name("secret"), kind=mmn.VaultCredentialKind.API_KEY, provider=mmn.VaultProvider.CUSTOM,
                              secret="sdk-python-not-a-real-secret", description="harness")
    h.on_cleanup("vault credential " + c.id, lambda: h.client.vault.delete(c.id))
    check("secret" not in c.raw, "no plaintext secret in the response")
    eq(h.client.vault.get(c.id).id, c.id, "get")
    u = h.client.vault.update(c.id, name=h.name("secret"), kind="ApiKey", provider="Custom", description="harness, updated")
    eq(u.description, "harness, updated", "description")
    eq(h.client.vault.usage(c.id), [], "unused")
    check(c.id in ids(h.client.vault.list(provider="Custom")), "listed by provider")
    h.client.vault.delete(c.id)
    expect_error(NotFoundError, lambda: h.client.vault.get(c.id))


@case("Devices: register, list, delete")
def t_devices(h: Harness) -> None:
    h.need("tenant")
    token = "ExponentPushToken[" + h.prefix + "]"
    d = h.client.devices.register(token, "ios", name=h.name("device"), locale="en-US")
    h.on_cleanup("device " + d.id, lambda: h.client.devices.delete(d.id))
    eq(d.platform, "ios", "platform")
    check(d.id in ids(h.client.devices.list()), "listed")
    h.client.devices.delete(d.id)
    check(d.id not in ids(h.client.devices.list()), "removed")
    expect_error(BadRequestError, lambda: h.client.devices.register(token, "windows"))


# ---------------------------------------------------------------------------
# Account, promo codes, public routes
# ---------------------------------------------------------------------------


@case("Account: plan, and redeem an unknown code")
def t_account(h: Harness) -> None:
    h.need("tenant")
    plan = h.client.account.plan()
    check(plan.plan and isinstance(plan.features, dict), "plan with features")
    ex = expect_error(ConflictError, lambda: h.client.account.redeem("SDKPY-NOT-A-CODE"))
    eq(ex.code, "PromoCodeInvalid", "error code")
    check(ex.details and ex.details.get("reason"), "details carry the reason")


class _FakeMcpHandler(BaseHTTPRequestHandler):
    """Minimal Streamable HTTP MCP endpoint: initialize, ping, and tools/list answered with JSON."""

    tool_names = ("harness_echo", "harness_time")
    requests_seen = 0

    def log_message(self, format: str, *args: Any) -> None:  # noqa: A002 - signature from the base class
        """Keep the harness output quiet."""

    def do_DELETE(self) -> None:  # noqa: N802 - http.server naming
        """End a session."""
        type(self).requests_seen += 1
        self.send_response(200)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def do_GET(self) -> None:  # noqa: N802
        """No event stream."""
        type(self).requests_seen += 1
        self.send_response(405)
        self.send_header("Content-Length", "0")
        self.end_headers()

    def do_POST(self) -> None:  # noqa: N802
        """Answer one JSON-RPC message."""
        type(self).requests_seen += 1
        length = int(self.headers.get("Content-Length") or 0)
        try:
            msg = json.loads(self.rfile.read(length) or b"{}")
        except ValueError:
            self.send_response(400)
            self.send_header("Content-Length", "0")
            self.end_headers()
            return
        if msg.get("id") is None:
            self.send_response(202)
            self.send_header("Content-Length", "0")
            self.end_headers()
            return
        reply: Dict[str, Any] = {"jsonrpc": "2.0", "id": msg["id"]}
        method = msg.get("method")
        extra: Dict[str, str] = {}
        if method == "initialize":
            reply["result"] = {
                "protocolVersion": (msg.get("params") or {}).get("protocolVersion") or "2025-11-25",
                "capabilities": {"tools": {}},
                "serverInfo": {"name": "sdk-harness-fake", "version": "1.0.0"},
            }
            extra["Mcp-Session-Id"] = uuid.uuid4().hex
        elif method == "tools/list":
            reply["result"] = {"tools": [{"name": n, "description": "Harness tool " + n + "; never called.",
                                          "inputSchema": {"type": "object", "properties": {}}} for n in self.tool_names]}
        elif method == "ping":
            reply["result"] = {}
        else:
            reply["error"] = {"code": -32601, "message": "Method not found: " + str(method)}
        payload = json.dumps(reply).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        for k, v in extra.items():
            self.send_header(k, v)
        self.end_headers()
        self.wfile.write(payload)


def _start_fake_mcp() -> Tuple[ThreadingHTTPServer, str]:
    """Start the fake MCP server on a free loopback port. Returns the server and its MCP URL."""
    server = ThreadingHTTPServer(("127.0.0.1", 0), _FakeMcpHandler)
    server.daemon_threads = True
    threading.Thread(target=server.serve_forever, daemon=True).start()
    return server, "http://127.0.0.1:" + str(server.server_address[1]) + "/mcp"


def _link_token(outbox: MyMorningNewsClient, template: str) -> str:
    """The newest link token in an outbox for a template."""
    page = outbox.email.list(template=template, page_size=20)
    items = sorted(page.items, key=lambda m: str(m.created_utc or ""), reverse=True)
    check(items, "a " + template + " email is in the outbox")
    full = outbox.email.get(items[0].id)
    m = re.search(r"token=([A-Za-z0-9%_\-.~]+)", full.get("renderedText") or "")
    check(m, "the " + template + " email carries a link token")
    return urllib.parse.unquote(m.group(1))


@case("Account: usage, export, delete, and 402 CapReached (Free plan)")
def t_account_limits(h: Harness) -> None:
    tenant = h.need("tenant")
    if not h.state.get("is_admin"):
        raise Skip("needs an administrator to create a user and grant a plan")
    mine = h.client.account.usage()
    check(mine.plan_key, "admin plan key")
    expect_error(ConflictError, h.client.account.delete, code="Conflict")
    plans = h.client.request("GET", "/v1.0/api/admin/plans")
    free = next((p for p in plans if p["plan"]["key"] == "free"), None)
    if free is None:
        raise Skip("the server has no Free plan")
    email = "sdk-python-cap-" + uuid.uuid4().hex[:8] + "@example.com"
    password = "Harness-" + uuid.uuid4().hex[:8]
    user = h.client.request("POST", "/v1.0/tenants/" + tenant + "/users",
                            body={"email": email, "password": password, "first_name": "SDK", "last_name": "Harness", "active": True})

    def remove_user() -> None:
        for e in h.client.editions.list(user_id=user["id"], page_size=100).items:
            if e.user_id == user["id"]:
                h.client.editions.delete(e.id)
        h.client.request("DELETE", "/v1.0/tenants/" + tenant + "/users/" + user["id"])

    h.on_cleanup("cap user", remove_user)
    grant = h.client.request("POST", "/v1.0/api/admin/plan-assignments",
                             body={"tenant_id": tenant, "user_id": user["id"], "plan_id": free["plan"]["id"], "duration_days": 1, "note": "SDK Python harness"})
    h.on_cleanup("cap plan assignment", lambda: h.client.request("DELETE", "/v1.0/api/admin/plan-assignments/" + grant["id"], query={"tenant_id": tenant}))
    with MyMorningNewsClient(h.config.base_url) as u:
        u.login(email, password, tenant)
        meter = u.account.usage()
        eq(meter.plan_key, "free", "plan key")
        check(isinstance(meter.topics_max, int), "the Free plan limits topics")
        zip_file = u.account.export()
        eq(zip_file.content_type, "application/zip", "export content type")
        check(zip_file.content[:2] == b"PK", "ZIP signature")
        created: List[str] = []
        try:
            for _ in range(int(meter.topics or 0), int(meter.topics_max)):
                created.append(u.topics.create(name=h.name("cap"), brief="Harness data; plan limit check.").id)
            cap = expect_error(mmn.CapReachedError, lambda: u.topics.create(name=h.name("cap"), brief="Over the limit."), code="CapReached")
            eq(cap.status, 402, "status")
            eq(cap.key, "topics.max", "cap key")
            eq(cap.limit, str(meter.topics_max), "cap limit")
            check(isinstance(cap, mmn.PlanLimitError), "CapReachedError is a PlanLimitError")
        finally:
            for tid in created:
                u.topics.delete(tid)
        deletion = u.account.delete()
        check(deletion.get("purgeAfterUtc"), "purgeAfterUtc")
        expect_error(AuthenticationError, u.me)
        u.login(email, password, tenant)
        eq(u.me().id, user["id"], "signing in again keeps the account")
        u.logout()
    h.client.request("DELETE", "/v1.0/api/admin/plan-assignments/" + grant["id"], query={"tenant_id": tenant})
    remove_user()


@case("Account: public sign-up, EmailNotVerifiedError, verify, resend, forgot, reset")
def t_signup(h: Harness) -> None:
    h.need("tenant")
    if not h.state.get("is_admin"):
        raise Skip("needs an administrator to open sign-up through the settings API")
    current = h.client.settings.get()
    original = copy.deepcopy(current["settings"])
    hosting = current["settings"].get("hosting")
    if not isinstance(hosting, dict):
        raise Skip("the server has no hosting settings")
    if any(f.get("path") == "hosting.publicSignupEnabled" and f.get("overriddenByEnvironment") for f in current.get("fields") or []):
        raise Skip("Hosting.PublicSignupEnabled is set by the environment")
    unique = uuid.uuid4().hex[:8]
    email = "sdk-python-signup-" + unique + "@example.com"
    display = h.prefix + "-signup-" + unique
    password = "Harness signup " + unique
    body = {"email": email, "password": password, "display_name": display, "time_zone": "UTC", "accept_terms": True, "confirm_age": True}
    was_open = bool(hosting.get("publicSignupEnabled"))
    with MyMorningNewsClient(h.config.base_url) as anon:
        if not was_open:
            expect_error(NotFoundError, lambda: anon.account.signup(body), code="SignupClosed")

        def restore() -> None:
            h.client.request("PUT", "/v1.0/api/settings", body=original)

        h.on_cleanup("settings (sign-up)", restore)
        opened = copy.deepcopy(current["settings"])
        opened["hosting"].update({"publicSignupEnabled": True, "requireEmailVerification": True, "signupsPerIpPerHour": 1000})
        check(h.client.request("PUT", "/v1.0/api/settings", body=opened).get("success"), "settings accepted")
        try:
            first = anon.account.signup(body)
            check(first.get("verificationRequired") is True, "verification required")
            eq(anon.account.signup(body).get("message"), first.get("message"), "same answer for an existing account")
            expect_error(BadRequestError, lambda: anon.account.signup(dict(body, email="x" + email, accept_terms=False)), code="ValidationFailed")
            blocked = expect_error(mmn.EmailNotVerifiedError, lambda: anon.login(email, password), code="EmailNotVerified")
            eq(blocked.status, 403, "status")
            check(isinstance(blocked, mmn.PermissionDeniedError), "EmailNotVerifiedError is a PermissionDeniedError")
            check(anon.token is None, "no session after a refused sign-in")
            check(anon.account.resend_verification(email).get("message"), "resend message")
            anon.account.resend_verification("nobody-" + unique + "@example.com")
            anon.account.forgot_password("nobody-" + unique + "@example.com")
            expect_error(BadRequestError, lambda: anon.account.verify_email("not-a-token"), code="ValidationFailed")
            expect_error(BadRequestError, lambda: anon.account.reset_password("not-a-token", "a long new password"), code="ValidationFailed")

            tenants = h.client.request("GET", "/v1.0/tenants", query={"page_size": 1000})
            personal = next((t for t in tenants["items"] if t["name"] == display + " (personal)"), None)
            check(personal is not None, "the personal tenant exists")
            pid = personal["id"]
            h.on_cleanup("personal tenant", lambda: h.client.request("DELETE", "/v1.0/tenants/" + pid))
            check(personal.get("isPersonal") is True, "personal tenant")
            users = h.client.request("GET", "/v1.0/tenants/" + pid + "/users")
            signed_up = next(x for x in users["items"] if x["email"] == email)
            key = h.client.request("POST", "/v1.0/tenants/" + pid + "/credentials", body={"user_id": signed_up["id"], "name": h.name("outbox"), "auth_mode": "Hybrid"})
            with MyMorningNewsClient(h.config.base_url, access_key=key["credential"]["accessKey"], secret_key=key["secretKey"], tenant_id=pid) as outbox:
                verify_token = _link_token(outbox, "VerifyEmail")
                verified = anon.account.verify_email(verify_token)
                check(verified.get("verified") is True and verified.get("email") == email, "verified")
                expect_error(BadRequestError, lambda: anon.account.verify_email(verify_token))
                session = anon.login(email, password)
                eq(session.tenant_id, pid, "signed in to the personal tenant")
                eq(anon.account.usage().topics, 0, "no topics yet")
                anon.logout()
                anon.account.forgot_password(email)
                reset_token = _link_token(outbox, "PasswordReset")
                expect_error(BadRequestError, lambda: anon.account.reset_password(reset_token, "short"))
                renewed = password + " renewed"
                check(anon.account.reset_password(reset_token, renewed).get("reset") is True, "reset")
                expect_error(AuthenticationError, lambda: anon.login(email, password))
                anon.login(email, renewed)
                anon.logout()
            h.client.request("DELETE", "/v1.0/tenants/" + pid)
        finally:
            restore()
        if not was_open:
            expect_error(NotFoundError, lambda: anon.account.signup(body), code="SignupClosed")


@case("MCP servers: CRUD, test, discover, tools, agentic topic, run tool calls")
def t_mcp_servers(h: Harness) -> str:
    h.need("tenant")
    c = h.client
    expect_error(BadRequestError, lambda: c.mcp_servers.create(name=h.name("mcp")), code="ValidationFailed")
    expect_error(BadRequestError, lambda: c.mcp_servers.create(name=h.name("mcp"), url="ftp://127.0.0.1/mcp"), code="ValidationFailed")
    expect_error(NotFoundError, lambda: c.runs.tool_calls("run_sdkpythondoesnotexist"))
    runs = c.runs.list(page_size=1)
    if runs.items:
        calls = c.runs.tool_calls(runs.items[0].id)
        check(all(x.run_id == runs.items[0].id for x in calls), "tool calls belong to the run")

    dead = c.mcp_servers.create(name=h.name("mcp-dead"), url="http://127.0.0.1:9/mcp", enabled=False)
    h.on_cleanup("mcp server (dead)", lambda: c.mcp_servers.delete(dead.id))
    check(dead.id.startswith("mcs_") and dead.enabled is False and dead.header_name == "Authorization", "created disabled with defaults")
    failed = c.mcp_servers.test(dead.id)
    check(failed.success is False and failed.error, "a failed test is success False with an error")
    bad = expect_error(mmn.ServerError, lambda: c.mcp_servers.discover(dead.id), code="McpServerUnavailable")
    eq(bad.status, 502, "status")
    check(c.mcp_servers.get(dead.id).server.last_error, "lastError is saved")
    expect_error(NotFoundError, lambda: c.mcp_servers.set_tool_enabled(dead.id, "mct_sdkpythondoesnotexist", True))
    check(dead.id in ids(c.mcp_servers.list(enabled=False, page_size=100).items), "the disabled server is listed")
    c.mcp_servers.delete(dead.id)
    expect_error(NotFoundError, lambda: c.mcp_servers.get(dead.id))

    fake, url = _start_fake_mcp()
    try:
        server = c.mcp_servers.create(name=h.name("mcp"), url=url, transport=mmn.McpTransport.STREAMABLE_HTTP)
        h.on_cleanup("mcp server", lambda: c.mcp_servers.delete(server.id))
        renamed = c.mcp_servers.update(server.id, name=server.name + "-renamed", header_prefix="Token ")
        check(renamed.name == server.name + "-renamed" and renamed.header_prefix == "Token ", "updated")
        expect_error(BadRequestError, lambda: c.mcp_servers.update(server.id, name=" "), code="ValidationFailed")
        ok = c.mcp_servers.test(server.id)
        if not ok.success:
            raise Skip("the server cannot reach the fake MCP server at " + url + " (server in a container?): " + str(ok.error))
        eq(ok.tool_count, len(_FakeMcpHandler.tool_names), "tool count")
        eq(len(c.mcp_servers.get(server.id).tools or []), 0, "test saves nothing")
        found = c.mcp_servers.discover(server.id)
        eq(len(found.tools), len(_FakeMcpHandler.tool_names), "discovered tools")
        check(found.server.last_discovered_utc, "lastDiscoveredUtc")
        check(all(t.enabled is False and t.id.startswith("mct_") for t in found.tools), "new tools start disabled")
        echo = next(t for t in found.tools if t.name == "harness_echo")
        check(c.mcp_servers.set_tool_enabled(server.id, echo.id, True).enabled is True, "tool enabled")
        again = c.mcp_servers.discover(server.id)
        check(next(t for t in again.tools if t.name == "harness_echo").enabled is True, "rediscovery keeps the setting")

        topic = c.topics.create(name=h.name("agentic"), brief="Harness data for agentic fields; never run.", agentic_search=True, mcp_tool_ids=[echo.id, "not-a-tool-id"])
        h.on_cleanup("agentic topic", lambda: c.topics.delete(topic.id))
        check(topic.agentic_search is True, "agenticSearch saved")
        eq(topic.mcp_tool_ids, [echo.id], "mcpToolIds keeps only mct_ IDs")
        read = c.topics.get(topic.id).topic
        check(read.agentic_search is True and echo.id in read.mcp_tool_ids, "agentic fields read back")
        cleared = c.topics.update(topic.id, agentic_search=False, mcp_tool_ids=[])
        check(cleared.agentic_search is False and cleared.mcp_tool_ids == [], "agentic fields cleared")
        c.topics.delete(topic.id)
        c.mcp_servers.delete(server.id)
        check(_FakeMcpHandler.requests_seen > 0, "the server talked to the fake MCP server")
    finally:
        fake.shutdown()
        fake.server_close()
    with MyMorningNewsClient(h.config.base_url) as anon:
        expect_error(AuthenticationError, anon.mcp_servers.list)
    return str(len(found.tools)) + " tools discovered from the fake MCP server"


@case("Public: promo code check (unknown and real)")
def t_public_promo(h: Harness) -> str:
    h.need("tenant")
    unknown = h.client.public.promo_code("SDKPY-NOT-A-CODE")
    eq(unknown.valid, False, "unknown code is invalid")
    if not h.state.get("is_admin"):
        return "admin promo setup skipped"
    code = "SDKPY-" + uuid.uuid4().hex[:8].upper()
    stats = h.client.request("POST", "/v1.0/api/admin/promo-codes", body={"code": code, "description": h.name("promo"), "duration_days": 7, "max_redemptions": 1})
    pc = stats.get("code") or {}
    pid = pc.get("id")
    check(pid, "promo code created")
    h.on_cleanup("promo code " + pid, lambda: h.client.request("DELETE", "/v1.0/api/admin/promo-codes/" + pid, query={"force": True}))
    known = h.client.public.promo_code(code)
    eq(known.valid, True, "new code is valid")
    h.client.request("DELETE", "/v1.0/api/admin/promo-codes/" + pid)
    eq(h.client.public.promo_code(code).valid, False, "deleted code is invalid")
    return code


@case("Public: artwork and invalid email links")
def t_public_misc(h: Harness) -> None:
    logo = h.client.public.asset("logo.png")
    eq(logo.content_type, "image/png", "logo is PNG")
    check(logo.content[:4] == b"\x89PNG", "PNG signature")
    expect_error(NotFoundError, lambda: h.client.public.feedback_page("not-a-token"))
    expect_error(NotFoundError, lambda: h.client.public.unsubscribe_page("not-a-token"))


@case("Errors: unreachable server raises MyMorningNewsError with status 0")
def t_unreachable(h: Harness) -> None:
    with MyMorningNewsClient("http://127.0.0.1:9", timeout=2) as c:
        ex = expect_error(MyMorningNewsError, c.health)
        eq(ex.status, 0, "status")
        eq(ex.code, "ConnectionError", "code")


@case("Topics: delete, then 404")
def t_topic_delete(h: Harness) -> None:
    tid = h.need("topic")
    h.client.topics.delete(tid)
    expect_error(NotFoundError, lambda: h.client.topics.get(tid))


def main(argv: Optional[List[str]] = None) -> int:
    """Entry point: parse arguments and run the harness."""
    return Harness(HarnessConfig.from_args(argv)).run()


if __name__ == "__main__":
    sys.exit(main())
