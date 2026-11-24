"""
My Morning News Python SDK.

A client for the My Morning News REST API: topics refined through the wizard,
scheduled research runs, findings with evidence, predictions, suggestions,
feedback, learned preferences, editions and briefings, audio, email, and
analytics.

The SDK is a single module that depends only on ``requests``. Python names are
snake_case; the SDK converts them to the camelCase wire names for query
parameters and request bodies, and responses are returned as dataclasses whose
fields are snake_case. Every model keeps the full JSON payload in ``raw`` so
fields added by newer servers are never lost.

Example::

    from mymorningnews_sdk import MyMorningNewsClient

    with MyMorningNewsClient("http://127.0.0.1:12100") as client:
        client.login("admin@mymorningnews", "password")
        topic = client.topics.create(name="Battery chemistry", brief="Solid-state progress")
        print(topic.id, topic.status)

(c) 2026 Lattice AI LLC. Licensed under the MIT license.
"""

from __future__ import annotations

import hashlib
import hmac
import json
import re
import time
import urllib.parse
import uuid
from dataclasses import dataclass, field, fields as dataclass_fields
from datetime import date, datetime, timezone
from enum import Enum
from typing import Any, Callable, ClassVar, Dict, Generic, Iterable, Iterator, List, Mapping, Optional, Sequence, Tuple, Type, TypeVar, Union

import requests

__version__ = "0.1.0-alpha"
"""SDK version (PEP 440 form in packaging metadata: 0.1.0a0)."""

DEFAULT_BASE_URL = "http://127.0.0.1:12100"
"""Default server URL. Loopback is always 127.0.0.1."""

DEFAULT_TIMEOUT = 30.0
"""Default per-request timeout in seconds."""

USER_AGENT = "mymorningnews-sdk-python/" + __version__
"""User-Agent header sent with every request."""

T = TypeVar("T")

# Keys whose values are free-form maps. Their inner keys are sent exactly as given.
_OPAQUE_KEYS = frozenset({"variables", "features", "details", "metadata", "headers", "planNames", "separation"})


# ---------------------------------------------------------------------------
# Name conversion helpers
# ---------------------------------------------------------------------------


def to_camel(name: str) -> str:
    """Convert a snake_case name to camelCase.

    Names that are already camelCase are returned unchanged. A trailing
    underscore (used to avoid Python keywords, for example ``from_``) is dropped.

    Args:
        name: The name to convert.

    Returns:
        The camelCase form.
    """
    if not name:
        return name
    name = name.rstrip("_") or name
    if "_" not in name:
        return name
    parts = [p for p in name.split("_") if p != ""]
    if not parts:
        return name
    return parts[0] + "".join(p[:1].upper() + p[1:] for p in parts[1:])


def to_snake(name: str) -> str:
    """Convert a camelCase or PascalCase name to snake_case.

    Args:
        name: The name to convert.

    Returns:
        The snake_case form.
    """
    s = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", name)
    s = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", s)
    return s.lower()


def _format_scalar(value: Any) -> Any:
    """Convert enums, dates, and models into JSON-friendly values."""
    if isinstance(value, Enum):
        return value.value
    if isinstance(value, datetime):
        if value.tzinfo is None:
            value = value.replace(tzinfo=timezone.utc)
        return value.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")
    if isinstance(value, date):
        return value.isoformat()
    return value


def _wire_body(value: Any, convert_keys: bool = True) -> Any:
    """Convert a request body (dict, list, or Model) to its wire form.

    Dictionary keys may be snake_case or camelCase; both become camelCase.
    Values under free-form keys such as ``variables`` and ``features`` keep
    their keys exactly as given.
    """
    if value is None:
        return None
    if isinstance(value, Model):
        return value.to_wire()
    if isinstance(value, Mapping):
        out: Dict[str, Any] = {}
        for k, v in value.items():
            key = to_camel(str(k)) if convert_keys else str(k)
            out[key] = _wire_body(v, convert_keys=convert_keys and key not in _OPAQUE_KEYS)
        return out
    if isinstance(value, (list, tuple, set, frozenset)):
        return [_wire_body(v, convert_keys) for v in value]
    return _format_scalar(value)


def _wire_query(filters: Optional[Mapping[str, Any]]) -> Dict[str, str]:
    """Convert keyword filters to camelCase query parameters, dropping None values."""
    out: Dict[str, str] = {}
    if not filters:
        return out
    for k, v in filters.items():
        if v is None:
            continue
        key = to_camel(str(k))
        v = _format_scalar(v)
        if isinstance(v, bool):
            out[key] = "true" if v else "false"
        elif isinstance(v, (list, tuple, set)):
            out[key] = ",".join(str(_format_scalar(x)) for x in v)
        else:
            out[key] = str(v)
    return out


def _seg(value: str) -> str:
    """Percent-encode a single path segment."""
    if value is None or str(value) == "":
        raise ValueError("An identifier is required.")
    return urllib.parse.quote(str(value), safe="")


def _merge(body: Optional[Mapping[str, Any]], fields_: Mapping[str, Any]) -> Dict[str, Any]:
    """Merge an optional body mapping with keyword fields (keyword fields win)."""
    merged: Dict[str, Any] = {}
    if body is not None:
        if isinstance(body, Model):
            merged.update(body.to_wire())
        else:
            merged.update(dict(body))
    for k, v in fields_.items():
        if v is not None:
            merged[k] = v
    return merged


# ---------------------------------------------------------------------------
# Request signing (MMN1-HMAC-SHA256)
# ---------------------------------------------------------------------------

SIGNATURE_ALGORITHM = "MMN1-HMAC-SHA256"
"""The request signing algorithm name; the first line of every string to sign."""


def body_sha256(body: Optional[Union[bytes, str]]) -> str:
    """Lowercase hex SHA-256 of a request body (an empty body hashes the empty string).

    Args:
        body: The exact bytes sent (a str is encoded as UTF-8), or None for no body.

    Returns:
        64 lowercase hex characters.
    """
    if body is None:
        body = b""
    if isinstance(body, str):
        body = body.encode("utf-8")
    return hashlib.sha256(body).hexdigest()


def string_to_sign(method: str, path_and_query: str, tenant_id: str, date: str, nonce: str, body_hash: str) -> str:
    """Build the string a signed request signs.

    The lines are the algorithm, the upper-case method, the path and query exactly as sent, the tenant ID,
    the ``x-date`` value, the ``x-nonce`` value, and the lowercase hex SHA-256 of the body, joined by newlines.
    """
    return "\n".join([SIGNATURE_ALGORITHM, method.upper(), path_and_query, tenant_id, date, nonce, body_hash.lower()])


def sign_request(secret_key: str, method: str, path_and_query: str, tenant_id: str, date: str, nonce: str,
                 body: Optional[Union[bytes, str]] = None) -> str:
    """Compute the ``x-signature`` header for a request.

    Args:
        secret_key: The credential's secret key (the HMAC key, UTF-8).
        method: HTTP method.
        path_and_query: Path and query string exactly as they appear on the wire, for example ``/v1.0/api/topics?pageSize=1``.
        tenant_id: The tenant, also sent as ``x-tenant-guid``.
        date: The ``x-date`` value (ISO 8601 UTC).
        nonce: The ``x-nonce`` value (8 to 128 characters, never reused).
        body: The exact body bytes sent, or None.

    Returns:
        Lowercase hex HMAC-SHA256.
    """
    text = string_to_sign(method, path_and_query, tenant_id, date, nonce, body_sha256(body))
    return hmac.new(secret_key.encode("utf-8"), text.encode("utf-8"), hashlib.sha256).hexdigest()


# ---------------------------------------------------------------------------
# Errors
# ---------------------------------------------------------------------------


class MyMorningNewsError(Exception):
    """An error returned by the API or raised while talking to it.

    Attributes:
        status: HTTP status code (0 when the server could not be reached).
        code: The server's error code, for example ``NotFound`` or ``ValidationFailed``.
        message: Human-readable message.
        request_id: The server's request ID, useful when reporting problems.
        details: Optional string map with extra detail (may be None).
        body: The parsed response body (dict), raw text, or None.
    """

    def __init__(
        self,
        message: str,
        status: int = 0,
        code: Optional[str] = None,
        request_id: Optional[str] = None,
        details: Optional[Dict[str, str]] = None,
        body: Any = None,
    ) -> None:
        """Create an error.

        Args:
            message: Human-readable message.
            status: HTTP status code, or 0 for transport errors.
            code: Server error code.
            request_id: Server request ID.
            details: Extra detail map.
            body: Parsed body or raw text.
        """
        super().__init__(message)
        self.message = message
        self.status = status
        self.code = code
        self.request_id = request_id
        self.details = details
        self.body = body

    def __str__(self) -> str:
        """Return a one-line description with status, code, and request ID."""
        parts = []
        if self.status:
            parts.append(str(self.status))
        if self.code:
            parts.append(self.code)
        head = " ".join(parts)
        text = (head + ": " if head else "") + (self.message or "")
        if self.request_id:
            text += " (request " + self.request_id + ")"
        return text


class BadRequestError(MyMorningNewsError):
    """HTTP 400: the request was malformed or failed validation (``BadRequest``, ``ValidationFailed``)."""


class AuthenticationError(MyMorningNewsError):
    """HTTP 401: missing, invalid, or expired credentials."""


class PermissionDeniedError(MyMorningNewsError):
    """HTTP 403: authenticated but not permitted.

    Attributes:
        reason: Why the request was denied, when the server says.
        required_permission: The permission the operation needs, when the server says.
    """

    @property
    def reason(self) -> Optional[str]:
        """The server's denial reason, if present."""
        return self.body.get("reason") if isinstance(self.body, dict) else None

    @property
    def required_permission(self) -> Optional[str]:
        """The permission the operation requires, if present."""
        return self.body.get("requiredPermission") if isinstance(self.body, dict) else None


class EmailNotVerifiedError(PermissionDeniedError):
    """HTTP 403 ``EmailNotVerified`` at sign-in: the account signed up publicly and has not confirmed its email.

    Open the emailed link (:meth:`AccountResource.verify_email`) or ask for a new one
    (:meth:`AccountResource.resend_verification`), then sign in again.
    """


class PlanLimitError(MyMorningNewsError):
    """HTTP 402 that is not ``CapReached`` (payment required by the plan)."""


class CapReachedError(PlanLimitError):
    """HTTP 402 ``CapReached``: a plan limit was reached.

    Attributes:
        key: The limit, for example ``topics.max``, ``runs.daily.max``, ``usage.monthlyCostUsd.max``,
            ``audio.enabled``, or ``endpoints.custom`` (None when the server sent none).
        limit: The plan's value for the limit as text, for example ``"1"`` or ``"false"``.
        reset_utc: When the limit resets (timezone-aware UTC datetime), or None.
    """

    @property
    def key(self) -> Optional[str]:
        """The limit key from ``details.key``."""
        return (self.details or {}).get("key")

    @property
    def limit(self) -> Optional[str]:
        """The plan's value for the limit from ``details.limit``."""
        return (self.details or {}).get("limit")

    @property
    def reset_utc(self) -> Optional[datetime]:
        """When the limit resets, from ``details.resetUtc``, or None."""
        raw = (self.details or {}).get("resetUtc")
        if not raw:
            return None
        text = re.sub(r"(\.\d{6})\d+", r"\1", str(raw).strip()).replace("Z", "+00:00")
        try:
            parsed = datetime.fromisoformat(text)
        except ValueError:
            return None
        return parsed if parsed.tzinfo else parsed.replace(tzinfo=timezone.utc)


class NotFoundError(MyMorningNewsError):
    """HTTP 404: the resource does not exist or is not visible to the caller."""


class ConflictError(MyMorningNewsError):
    """HTTP 409: the request conflicts with the resource's current state."""


class SetupRequiredError(ConflictError):
    """HTTP 409 ``SetupRequired``: the workspace is missing something it needs, such as a working model key.

    Refinement sessions and :meth:`ResearchRequestsResource.create` raise it when the model endpoint for
    refinement has no key or failed its last check. Read :meth:`SetupResource.status`; an administrator
    can fix it with :meth:`SetupResource.set_model_key` or :meth:`SetupResource.verify`.

    Attributes:
        check: The setup check that is not ready, for example ``model:mdl_abc`` (matches
            :attr:`SetupCheck.key`; None when the server sent none).
    """

    @property
    def check(self) -> Optional[str]:
        """The setup check key from ``details.check``."""
        return (self.details or {}).get("check")


class GoneError(MyMorningNewsError):
    """HTTP 410: the resource expired (for example ``ArtifactExpired``; regenerate it)."""


class RangeNotSatisfiableError(MyMorningNewsError):
    """HTTP 416: the requested byte range is outside the file."""


class RateLimitError(MyMorningNewsError):
    """HTTP 429: too many requests; retry later."""


class ServerError(MyMorningNewsError):
    """HTTP 5xx: the server or an upstream provider failed."""


class TenantSelectionRequired(ConflictError):
    """HTTP 409 on login: the email belongs to several tenants.

    Retry :meth:`MyMorningNewsClient.login` with ``tenant_id`` set to one of
    :attr:`tenants`.

    Attributes:
        tenants: The tenants the user may sign in to.
    """

    def __init__(self, tenants: List["TenantChoice"], **kwargs: Any) -> None:
        """Create the error.

        Args:
            tenants: Tenant choices returned by the server.
            **kwargs: Passed to :class:`MyMorningNewsError`.
        """
        super().__init__("Several tenants match this sign-in; choose one with tenant_id.", **kwargs)
        self.tenants = tenants


class WaitTimeoutError(MyMorningNewsError, TimeoutError):
    """A polling helper such as :meth:`RunsResource.wait` ran out of time.

    Attributes:
        last: The last object observed before the timeout.
    """

    def __init__(self, message: str, last: Any = None) -> None:
        """Create the error.

        Args:
            message: Description of what timed out.
            last: The last object observed.
        """
        super().__init__(message, status=0, code="Timeout")
        self.last = last


_ERRORS_BY_STATUS: Dict[int, Type[MyMorningNewsError]] = {
    400: BadRequestError,
    401: AuthenticationError,
    403: PermissionDeniedError,
    404: NotFoundError,
    409: ConflictError,
    410: GoneError,
    416: RangeNotSatisfiableError,
    429: RateLimitError,
}


def _error_from_response(resp: requests.Response) -> MyMorningNewsError:
    """Build a typed error from a non-2xx response."""
    body: Any = None
    text = ""
    try:
        text = resp.text
    except Exception:  # pragma: no cover - defensive
        text = ""
    ctype = resp.headers.get("Content-Type", "")
    if "json" in ctype or text.strip().startswith("{"):
        try:
            body = resp.json()
        except ValueError:
            body = text
    else:
        body = text
    code = None
    message = resp.reason or ("HTTP " + str(resp.status_code))
    request_id = resp.headers.get("x-request-id")
    details = None
    if isinstance(body, dict):
        code = body.get("error") or code
        message = body.get("message") or message
        request_id = body.get("requestId") or request_id
        raw_details = body.get("details")
        details = dict(raw_details) if isinstance(raw_details, dict) else None
    cls = _ERRORS_BY_STATUS.get(resp.status_code)
    if resp.status_code == 402:
        cls = CapReachedError if code == "CapReached" else PlanLimitError
    elif resp.status_code == 403 and code == "EmailNotVerified":
        cls = EmailNotVerifiedError
    elif resp.status_code == 409 and code == "SetupRequired":
        cls = SetupRequiredError
    if cls is None:
        cls = ServerError if resp.status_code >= 500 else MyMorningNewsError
    return cls(message, status=resp.status_code, code=code, request_id=request_id, details=details, body=body)


# ---------------------------------------------------------------------------
# Models
# ---------------------------------------------------------------------------

_MODEL_REGISTRY: Dict[str, Type["Model"]] = {}


@dataclass
class Model:
    """Base class for response models.

    Fields are snake_case versions of the camelCase JSON names. Every field is
    optional (``None`` when the server omitted it). Dates are ISO 8601 UTC
    strings and enums are their string names. ``raw`` holds the full JSON
    object, so ``model["anyCamelCaseKey"]`` and ``model.get("any_key")`` reach
    fields this SDK version does not declare.
    """

    _wire: ClassVar[Dict[str, str]] = {}
    _nested: ClassVar[Dict[str, str]] = {}
    raw: Dict[str, Any] = field(default_factory=dict, repr=False, compare=False, kw_only=True)

    def __init_subclass__(cls, **kwargs: Any) -> None:
        """Register subclasses so nested fields can be resolved by name."""
        super().__init_subclass__(**kwargs)
        _MODEL_REGISTRY[cls.__name__] = cls

    @classmethod
    def _wire_name(cls, attr: str) -> str:
        """Return the JSON name for a dataclass attribute."""
        return cls._wire.get(attr) or to_camel(attr)

    @classmethod
    def from_dict(cls: Type[T], data: Any) -> T:
        """Build a model from a JSON object.

        Args:
            data: The decoded JSON object. ``None`` yields an empty model.

        Returns:
            The model instance.
        """
        if isinstance(data, cls):
            return data
        if not isinstance(data, Mapping):
            data = {}
        kwargs: Dict[str, Any] = {}
        for f in dataclass_fields(cls):  # type: ignore[arg-type]
            if f.name == "raw":
                continue
            wire = cls._wire_name(f.name)  # type: ignore[attr-defined]
            if wire not in data:
                continue
            value = data[wire]
            nested_name = cls._nested.get(f.name)  # type: ignore[attr-defined]
            if nested_name and value is not None:
                nested_cls = _MODEL_REGISTRY.get(nested_name)
                if nested_cls is not None:
                    if isinstance(value, list):
                        value = [nested_cls.from_dict(v) if isinstance(v, Mapping) else v for v in value]
                    elif isinstance(value, Mapping):
                        value = nested_cls.from_dict(value)
            kwargs[f.name] = value
        obj = cls(**kwargs)  # type: ignore[call-arg]
        obj.raw = dict(data)  # type: ignore[attr-defined]
        return obj

    def to_dict(self) -> Dict[str, Any]:
        """Return a copy of the raw JSON object (camelCase keys)."""
        return json.loads(json.dumps(self.raw))

    def to_wire(self) -> Dict[str, Any]:
        """Return a camelCase dict suitable for sending back as a request body.

        Starts from ``raw`` and overlays any fields changed on the instance.
        """
        out = dict(self.raw)
        for f in dataclass_fields(self):
            if f.name == "raw":
                continue
            value = getattr(self, f.name)
            if value is None:
                continue
            out[self._wire_name(f.name)] = _wire_body(value)
        return out

    def get(self, key: str, default: Any = None) -> Any:
        """Read a raw field by camelCase or snake_case name.

        Args:
            key: Field name in either form.
            default: Returned when the field is absent.

        Returns:
            The raw JSON value or ``default``.
        """
        if key in self.raw:
            return self.raw[key]
        camel = to_camel(key)
        return self.raw.get(camel, default)

    def __getitem__(self, key: str) -> Any:
        """Read a raw field by camelCase or snake_case name; raises KeyError when absent."""
        sentinel = object()
        value = self.get(key, sentinel)
        if value is sentinel:
            raise KeyError(key)
        return value

    def __contains__(self, key: object) -> bool:
        """Return True when the raw payload has the field (either name form)."""
        return isinstance(key, str) and (key in self.raw or to_camel(key) in self.raw)


@dataclass
class Page(Generic[T]):
    """One page of a list endpoint.

    Attributes:
        items: The items on this page.
        page_number: 1-based page number.
        page_size: Requested page size.
        total_count: Total matching records.
        total_pages: Total pages at this page size.
        duration_ms: Server-side query time in milliseconds.
        raw: The full JSON object.
    """

    items: List[T] = field(default_factory=list)
    page_number: int = 1
    page_size: int = 0
    total_count: int = 0
    total_pages: int = 0
    duration_ms: float = 0.0
    raw: Dict[str, Any] = field(default_factory=dict, repr=False, compare=False)

    @classmethod
    def from_dict(cls, data: Any, item_cls: Optional[Type[Any]] = None) -> "Page[Any]":
        """Build a page from the list envelope ``{ items, pageNumber, ... }``.

        Args:
            data: Decoded JSON object.
            item_cls: Model class for the items, or None to keep dicts.

        Returns:
            The page.
        """
        if not isinstance(data, Mapping):
            data = {}
        raw_items = data.get("items") or []
        items = [item_cls.from_dict(x) if item_cls is not None and isinstance(x, Mapping) else x for x in raw_items]
        return cls(
            items=items,
            page_number=int(data.get("pageNumber") or 1),
            page_size=int(data.get("pageSize") or 0),
            total_count=int(data.get("totalCount") or 0),
            total_pages=int(data.get("totalPages") or 0),
            duration_ms=float(data.get("durationMs") or 0.0),
            raw=dict(data),
        )

    @property
    def has_more(self) -> bool:
        """True when a later page exists."""
        return self.page_number < self.total_pages

    def __iter__(self) -> Iterator[T]:
        """Iterate the items on this page."""
        return iter(self.items)

    def __len__(self) -> int:
        """Number of items on this page."""
        return len(self.items)


@dataclass
class Download:
    """A binary or text download.

    Attributes:
        content: The bytes received (the requested range when ``partial``).
        content_type: The response Content-Type.
        file_name: File name from Content-Disposition, when present.
        status: HTTP status (200, or 206 for a partial range).
        content_range: The Content-Range header for partial responses.
        total_bytes: Full file size when known (from Content-Range).
        etag: The ETag header, when present.
    """

    content: bytes = b""
    content_type: Optional[str] = None
    file_name: Optional[str] = None
    status: int = 200
    content_range: Optional[str] = None
    total_bytes: Optional[int] = None
    etag: Optional[str] = None

    @property
    def partial(self) -> bool:
        """True for a 206 Partial Content response."""
        return self.status == 206

    @property
    def text(self) -> str:
        """The content decoded as UTF-8."""
        return self.content.decode("utf-8", errors="replace")

    def save(self, path: str) -> str:
        """Write the content to a file.

        Args:
            path: Destination path.

        Returns:
            The path written.
        """
        with open(path, "wb") as handle:
            handle.write(self.content)
        return path

    @classmethod
    def from_response(cls, resp: requests.Response) -> "Download":
        """Build a download from a ``requests`` response."""
        disposition = resp.headers.get("Content-Disposition", "")
        name = None
        match = re.search(r"filename\*=UTF-8''([^;]+)", disposition) or re.search(r'filename="?([^";]+)"?', disposition)
        if match:
            name = urllib.parse.unquote(match.group(1).strip())
        content_range = resp.headers.get("Content-Range")
        total: Optional[int] = None
        if content_range and "/" in content_range:
            tail = content_range.rsplit("/", 1)[1].strip()
            if tail.isdigit():
                total = int(tail)
        elif resp.status_code == 200:
            length = resp.headers.get("Content-Length")
            if length and length.isdigit():
                total = int(length)
        return cls(
            content=resp.content,
            content_type=resp.headers.get("Content-Type"),
            file_name=name,
            status=resp.status_code,
            content_range=content_range,
            total_bytes=total,
            etag=resp.headers.get("ETag"),
        )


ByteRange = Union[str, int, Tuple[Optional[int], Optional[int]], None]
"""A byte range: ``"bytes=0-99"``, ``(0, 99)``, ``(None, 500)`` for the last 500 bytes, or an int start offset."""


def _range_header(byte_range: ByteRange) -> Optional[str]:
    """Build a Range header value from a ByteRange."""
    if byte_range is None:
        return None
    if isinstance(byte_range, str):
        return byte_range if byte_range.startswith("bytes=") else "bytes=" + byte_range
    if isinstance(byte_range, int):
        return "bytes=" + str(byte_range) + "-"
    start, end = byte_range
    if start is None and end is None:
        return None
    if start is None:
        return "bytes=-" + str(end)
    return "bytes=" + str(start) + "-" + ("" if end is None else str(end))


# ---------------------------------------------------------------------------
# Enumerations (string values match the server's enum names)
# ---------------------------------------------------------------------------


class NotificationKind(str, Enum):
    """What a notification is about."""
    RUN_RESULTS = "RunResults"
    BRIEFING_READY = "BriefingReady"
    AUDIO_READY = "AudioReady"
    RUN_FAILED = "RunFailed"
    CAP_REACHED = "CapReached"
    PREDICTION_DUE = "PredictionDue"


class NotificationChannel(str, Enum):
    """Where a notification is delivered."""
    EMAIL = "Email"
    PUSH = "Push"
    IN_APP = "InApp"


class SortDirection(str, Enum):
    """Sort direction for enumeration."""

    DESCENDING = "Descending"
    ASCENDING = "Ascending"


class TopicStatus(str, Enum):
    """Topic lifecycle."""

    DRAFT = "Draft"
    ACTIVE = "Active"
    PAUSED = "Paused"
    ARCHIVED = "Archived"


class RunStatus(str, Enum):
    """Run status."""

    QUEUED = "Queued"
    RUNNING = "Running"
    COMPLETED = "Completed"
    PARTIALLY_SUCCEEDED = "PartiallySucceeded"
    FAILED = "Failed"
    CANCELLED = "Cancelled"
    SKIPPED = "Skipped"


class RunTrigger(str, Enum):
    """What started a run."""

    SCHEDULE = "Schedule"
    MANUAL = "Manual"
    REQUEST = "Request"
    EDITION = "Edition"
    PREVIEW = "Preview"


class FeedbackTargetType(str, Enum):
    """What feedback is about."""

    FINDING = "Finding"
    PREDICTION = "Prediction"
    PREDICTION_REVIEW = "PredictionReview"
    SUGGESTION = "Suggestion"
    SOURCE_ITEM = "SourceItem"
    DOMAIN = "Domain"
    BRIEFING = "Briefing"
    BRIEFING_SECTION = "BriefingSection"
    LEARNED_PREFERENCE = "LearnedPreference"


class RelevanceRating(str, Enum):
    """Relevance rating."""

    RELEVANT = "Relevant"
    SOMEWHAT_RELEVANT = "SomewhatRelevant"
    NOT_RELEVANT = "NotRelevant"


class AccuracyRating(str, Enum):
    """Accuracy rating."""

    ACCURATE = "Accurate"
    PARTIALLY_ACCURATE = "PartiallyAccurate"
    INACCURATE = "Inaccurate"
    TOO_EARLY = "TooEarly"


class UsefulnessRating(str, Enum):
    """Usefulness rating."""

    USEFUL = "Useful"
    NOT_USEFUL = "NotUseful"


class FeedbackChannel(str, Enum):
    """Where feedback came from."""

    DASHBOARD = "Dashboard"
    EMAIL = "Email"
    MOBILE = "Mobile"
    API = "Api"
    MCP = "Mcp"


class SuggestionStatus(str, Enum):
    """Suggestion status."""

    NEW = "New"
    ACCEPTED = "Accepted"
    SNOOZED = "Snoozed"
    DISMISSED = "Dismissed"
    DONE = "Done"


class PredictionStatus(str, Enum):
    """Prediction status."""

    OPEN = "Open"
    PROPOSED_RESOLVED = "ProposedResolved"
    ACCURATE = "Accurate"
    PARTIALLY_ACCURATE = "PartiallyAccurate"
    INACCURATE = "Inaccurate"
    UNRESOLVABLE = "Unresolvable"
    WITHDRAWN = "Withdrawn"


class IntervalType(str, Enum):
    """Schedule interval type."""

    EVERY_N_MINUTES = "EveryNMinutes"
    EVERY_N_HOURS = "EveryNHours"
    EVERY_N_DAYS = "EveryNDays"
    EVERY_N_WEEKS = "EveryNWeeks"
    DAYS_OF_WEEK = "DaysOfWeek"
    DAYS_OF_MONTH = "DaysOfMonth"
    ONCE = "Once"
    CRON = "Cron"
    MANUAL = "Manual"


class ScheduleOwnerType(str, Enum):
    """What a schedule belongs to."""

    TOPIC = "Topic"
    EDITION = "Edition"
    REQUEST = "Request"


class MissedRunPolicy(str, Enum):
    """Handling of runs missed while the server was down."""

    SKIP = "Skip"
    RUN_ONCE_ON_RECOVERY = "RunOnceOnRecovery"


class ConstraintKind(str, Enum):
    """Topic constraint kind."""

    INCLUDE_KEYWORD = "IncludeKeyword"
    EXCLUDE_KEYWORD = "ExcludeKeyword"
    INCLUDE_ENTITY = "IncludeEntity"
    EXCLUDE_ENTITY = "ExcludeEntity"
    PREFERRED_DOMAIN = "PreferredDomain"
    BLOCKED_DOMAIN = "BlockedDomain"
    TRACKED_QUESTION = "TrackedQuestion"
    FEED_URL = "FeedUrl"
    SEED_URL = "SeedUrl"


class DimensionWeight(str, Enum):
    """Dimension importance."""

    EXCLUDE = "Exclude"
    LOW = "Low"
    NORMAL = "Normal"
    HIGH = "High"


class AudienceLevel(str, Enum):
    """Reader expertise."""

    BEGINNER = "Beginner"
    PRACTITIONER = "Practitioner"
    EXPERT = "Expert"


class Horizon(str, Enum):
    """Time horizon of interest."""

    DAYS = "Days"
    WEEKS = "Weeks"
    MONTHS = "Months"
    YEARS = "Years"


class PreferenceKind(str, Enum):
    """Kind of learned preference."""

    PREFER = "Prefer"
    AVOID = "Avoid"
    PREFER_SOURCE = "PreferSource"
    AVOID_SOURCE = "AvoidSource"
    STYLE_NOTE = "StyleNote"


class PreferenceStatus(str, Enum):
    """Learned preference status."""

    PROPOSED = "Proposed"
    ACTIVE = "Active"
    REJECTED = "Rejected"
    DISABLED = "Disabled"


class ModelProvider(str, Enum):
    """Model provider: a hosted API or self-hosted server reached through PolyPrompt, or an agent CLI in the
    server image (``CLAUDE_CODE``, ``CODEX``, ``GEMINI_CLI``, ``MUX``)."""

    ANTHROPIC = "Anthropic"
    OPEN_AI = "OpenAi"
    OPEN_AI_COMPATIBLE = "OpenAiCompatible"
    OLLAMA = "Ollama"
    GEMINI = "Gemini"
    X_AI = "XAi"
    OLLAMA_CLOUD = "OllamaCloud"
    CLAUDE_CODE = "ClaudeCode"
    CODEX = "Codex"
    GEMINI_CLI = "GeminiCli"
    MUX = "Mux"


class ModelAuthMethod(str, Enum):
    """How a model endpoint signs in to its provider."""

    API_KEY = "ApiKey"
    ACCESS_TOKEN = "AccessToken"
    AUTH_FILE = "AuthFile"
    SERVER_SIGN_IN = "ServerSignIn"
    NONE = "None"


class ModelTask(str, Enum):
    """Pipeline task role used to pick a model."""

    REFINEMENT = "Refinement"
    QUERY_PLANNING = "QueryPlanning"
    EXTRACTION = "Extraction"
    SCORING = "Scoring"
    SYNTHESIS = "Synthesis"
    PREDICTION = "Prediction"
    SUGGESTION = "Suggestion"
    COMPOSITION = "Composition"
    PODCAST_SCRIPT = "PodcastScript"
    FEEDBACK_ANALYSIS = "FeedbackAnalysis"
    EMBEDDING = "Embedding"


class ReasoningEffort(str, Enum):
    """Reasoning effort level."""

    NONE = "None"
    MINIMAL = "Minimal"
    LOW = "Low"
    MEDIUM = "Medium"
    HIGH = "High"
    X_HIGH = "XHigh"
    MAX = "Max"


class SearchProviderKind(str, Enum):
    """Search provider kind."""

    SEARXNG = "Searxng"
    TAVILY = "Tavily"
    YOU_COM = "YouCom"
    RSS = "Rss"
    HACKER_NEWS = "HackerNews"
    ARXIV = "Arxiv"
    HOSTED_MODEL_WEB_SEARCH = "HostedModelWebSearch"


class VaultCredentialKind(str, Enum):
    """Kind of stored secret."""

    API_KEY = "ApiKey"
    BEARER_TOKEN = "BearerToken"
    BASIC_AUTH = "BasicAuth"
    CUSTOM_HEADER = "CustomHeader"
    CONNECTION_STRING = "ConnectionString"
    SIGN_IN_FILE = "SignInFile"


class VaultProvider(str, Enum):
    """Service a stored secret belongs to."""

    ANTHROPIC = "Anthropic"
    OPEN_AI = "OpenAi"
    GEMINI = "Gemini"
    X_AI = "XAi"
    OLLAMA_CLOUD = "OllamaCloud"
    OPEN_AI_COMPATIBLE = "OpenAiCompatible"
    TAVILY = "Tavily"
    YOU_COM = "YouCom"
    MAILGUN = "Mailgun"
    ELEVEN_LABS = "ElevenLabs"
    MCP = "Mcp"
    CUSTOM = "Custom"


class BriefingKind(str, Enum):
    """What a briefing covers."""

    EDITION = "Edition"
    RUN = "Run"
    ONE_TIME = "OneTime"


class BriefingStatus(str, Enum):
    """Briefing status."""

    QUEUED = "Queued"
    COMPOSING = "Composing"
    READY = "Ready"
    FAILED = "Failed"


class FindingTier(str, Enum):
    """Relevance tier."""

    MUST_READ = "MustRead"
    WORTH_A_LOOK = "WorthALook"
    FYI = "Fyi"
    BELOW_THRESHOLD = "BelowThreshold"


class FindingCategory(str, Enum):
    """Finding category."""

    NEWS = "News"
    RELEASE = "Release"
    RESEARCH = "Research"
    OPINION = "Opinion"
    TUTORIAL = "Tutorial"
    DATA = "Data"
    OTHER = "Other"


class ArtifactKind(str, Enum):
    """What a stored file is."""

    BRIEFING_MARKDOWN = "BriefingMarkdown"
    BRIEFING_HTML = "BriefingHtml"
    AUDIO = "Audio"
    CHAPTERS = "Chapters"
    EXPORT = "Export"


class MediaStatus(str, Enum):
    """Audio status."""

    QUEUED = "Queued"
    SCRIPTING = "Scripting"
    SYNTHESIZING = "Synthesizing"
    READY = "Ready"
    FAILED = "Failed"


class MediaKind(str, Enum):
    """Kind of audio."""

    BRIEFING_AUDIO = "BriefingAudio"
    PODCAST_EPISODE = "PodcastEpisode"


class EmailStatus(str, Enum):
    """Email status."""

    QUEUED = "Queued"
    SENDING = "Sending"
    SENT = "Sent"
    FAILED = "Failed"
    SUPPRESSED = "Suppressed"
    SKIPPED = "Skipped"


class LookbackMode(str, Enum):
    """Which findings an edition covers."""

    SINCE_LAST_EDITION = "SinceLastEdition"
    FIXED_HOURS = "FixedHours"


class DeliverableLength(str, Enum):
    """Length of a one-time deliverable."""

    SHORT = "Short"
    STANDARD = "Standard"
    LONG = "Long"


class UsageGroupBy(str, Enum):
    """Grouping for usage and cost reports."""

    USER = "User"
    MODEL = "Model"
    TASK = "Task"
    PROVIDER = "Provider"
    TOPIC = "Topic"
    PROMPT_KEY = "PromptKey"
    DAY = "Day"
    TENANT = "Tenant"


class SearchResultDisposition(str, Enum):
    """What happened to a search result."""

    PENDING = "Pending"
    FETCHED = "Fetched"
    SKIPPED_SEEN = "SkippedSeen"
    SKIPPED_BLOCKED = "SkippedBlocked"
    SKIPPED_ROBOTS = "SkippedRobots"
    SKIPPED_LIMIT = "SkippedLimit"
    SKIPPED_DUPLICATE = "SkippedDuplicate"
    FETCH_FAILED = "FetchFailed"


class ResearchRequestStatus(str, Enum):
    """One-time request status."""

    CLARIFYING = "Clarifying"
    SCHEDULED = "Scheduled"
    RUNNING = "Running"
    COMPLETED = "Completed"
    FAILED = "Failed"
    CANCELLED = "Cancelled"
    DECLINED = "Declined"


class RefinementStatus(str, Enum):
    """Refinement session status."""

    ACTIVE = "Active"
    COMPLETED = "Completed"
    ABANDONED = "Abandoned"


class McpTransport(str, Enum):
    """MCP server transport."""

    STREAMABLE_HTTP = "StreamableHttp"
    SSE = "Sse"


class SetupCheckKind(str, Enum):
    """What a setup check covers. Model and Search are required; Embeddings, Email, and Speech are optional."""

    MODEL = "Model"
    SEARCH = "Search"
    EMBEDDINGS = "Embeddings"
    EMAIL = "Email"
    SPEECH = "Speech"


class SetupCheckState(str, Enum):
    """State of a setup check."""

    READY = "Ready"
    MISSING = "Missing"
    UNVERIFIED = "Unverified"
    FAILED = "Failed"


class SetupProviderGroup(str, Enum):
    """How a model provider is reached, for grouping choices on the Setup page."""

    API = "Api"
    AGENT_CLI = "AgentCli"
    SELF_HOSTED = "SelfHosted"


# ---------------------------------------------------------------------------
# Response models (fields mirror the server DTOs in snake_case)
# ---------------------------------------------------------------------------


@dataclass
class TokenResponse(Model):
    """Issued session token."""
    _nested: ClassVar[Dict[str, str]] = {'user': 'User'}
    token: Optional[str] = None
    expires_utc: Optional[str] = None
    tenant_id: Optional[str] = None
    session_id: Optional[str] = None
    user_id: Optional[str] = None
    credential_id: Optional[str] = None
    principal_type: Optional[str] = None
    user: Optional[User] = None
    refresh_token: Optional[str] = None
    refresh_expires_utc: Optional[str] = None
    deletion_cancelled: Optional[bool] = None


@dataclass
class TenantSelection(Model):
    """Returned with status 409 when a sign-in without x-tenant-guid matches more than one tenant."""
    _nested: ClassVar[Dict[str, str]] = {'tenants': 'TenantChoice'}
    error: Optional[str] = None
    message: Optional[str] = None
    tenants: Optional[List[TenantChoice]] = None


@dataclass
class TenantChoice(Model):
    """A tenant the signing-in user belongs to."""
    id: Optional[str] = None
    name: Optional[str] = None


@dataclass
class TokenDetails(Model):
    """Details of the presented token and principal."""
    valid: Optional[bool] = None
    session_id: Optional[str] = None
    principal_type: Optional[str] = None
    scheme: Optional[str] = None
    tenant_id: Optional[str] = None
    tenant_name: Optional[str] = None
    user_id: Optional[str] = None
    credential_id: Optional[str] = None
    email: Optional[str] = None
    display_name: Optional[str] = None
    is_admin: Optional[bool] = None
    is_tenant_admin: Optional[bool] = None
    issued_utc: Optional[str] = None
    expires_utc: Optional[str] = None


@dataclass
class Health(Model):
    """Health check response."""
    status: Optional[str] = None
    version: Optional[str] = None
    started_utc: Optional[str] = None
    uptime_seconds: Optional[int] = None
    components: Optional[Dict[str, str]] = None


@dataclass
class User(Model):
    """User as returned by the API (no password material)."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    first_name: Optional[str] = None
    last_name: Optional[str] = None
    email: Optional[str] = None
    is_admin: Optional[bool] = None
    is_tenant_admin: Optional[bool] = None
    is_protected: Optional[bool] = None
    time_zone: Optional[str] = None
    locale: Optional[str] = None
    last_login_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class ServerInfo(Model):
    """Server version, configuration summary, and bundled tool links."""
    _nested: ClassVar[Dict[str, str]] = {'external_services': 'ExternalServiceInfo'}
    product: Optional[str] = None
    version: Optional[str] = None
    started_utc: Optional[str] = None
    uptime_seconds: Optional[int] = None
    database_type: Optional[str] = None
    schema_version: Optional[int] = None
    vector_available: Optional[bool] = None
    runtime_version: Optional[str] = None
    operating_system: Optional[str] = None
    repository_url: Optional[str] = None
    external_services: Optional[List[ExternalServiceInfo]] = None
    pending_restart: Optional[List[str]] = None


@dataclass
class ExternalServiceInfo(Model):
    """A bundled tool shown on the dashboard External Services card."""
    name: Optional[str] = None
    url: Optional[str] = None
    credentials: Optional[str] = None
    description: Optional[str] = None


@dataclass
class ClientConfig(Model):
    """Public configuration for web and mobile clients (no authentication required)."""
    product_name: Optional[str] = None
    server_version: Optional[str] = None
    signup_enabled: Optional[bool] = None
    push_enabled: Optional[bool] = None
    audio_enabled: Optional[bool] = None
    email_enabled: Optional[bool] = None
    minimum_app_version: Optional[str] = None
    app_url: Optional[str] = None
    deletion_grace_days: Optional[int] = None
    links: Optional[Dict[str, str]] = None
    copyright: Optional[str] = None
    sharing_enabled: Optional[bool] = None
    sharing_max_expiry_days: Optional[int] = None
    sharing_default_expiry_days: Optional[int] = None


@dataclass
class Topic(Model):
    """A research topic: what a reader wants to follow, with its brief, dimensions, constraints, and delivery options."""
    history_mode: Optional[str] = None
    history_count: Optional[int] = None
    history_since_utc: Optional[str] = None
    history_briefing_id: Optional[str] = None
    history_token_budget: Optional[int] = None

    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    name: Optional[str] = None
    slug: Optional[str] = None
    description: Optional[str] = None
    brief: Optional[str] = None
    audience_level: Optional[str] = None
    horizon: Optional[str] = None
    language: Optional[str] = None
    status: Optional[str] = None
    is_one_time: Optional[bool] = None
    novelty_preference: Optional[int] = None
    email_on_run: Optional[bool] = None
    email_when_empty: Optional[bool] = None
    max_email_findings: Optional[int] = None
    display_threshold: Optional[int] = None
    weight_llm: Optional[float] = None
    weight_feedback: Optional[float] = None
    weight_novelty: Optional[float] = None
    weight_source: Optional[float] = None
    weight_recency: Optional[float] = None
    seed_queries: Optional[List[str]] = None
    briefing_template_id: Optional[str] = None
    last_run_utc: Optional[str] = None
    last_refined_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None
    agentic_search: Optional[bool] = None
    """Agentic search: the research model searches and reads pages itself (web_search, fetch_url, allowed MCP tools)."""
    mcp_tool_ids: Optional[List[str]] = None
    """MCP tool IDs (``mct_...``) the topic may use in agentic search; empty allows every enabled tool."""
    voice_profile_id: Optional[str] = None
    """Voice profile that reads the topic's run briefings aloud; None uses the workspace default voice."""
    audio_enabled: Optional[bool] = None
    """Automatic audio: every briefing of the topic's research runs is also made into audio (default False)."""


@dataclass
class McpServer(Model):
    """A registered MCP server (Streamable HTTP or SSE) whose tools agentic research may call."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    name: Optional[str] = None
    url: Optional[str] = None
    transport: Optional[str] = None
    vault_credential_id: Optional[str] = None
    header_name: Optional[str] = None
    header_prefix: Optional[str] = None
    enabled: Optional[bool] = None
    tool_count: Optional[int] = None
    last_discovered_utc: Optional[str] = None
    last_error: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class McpServerTool(Model):
    """A tool discovered on an MCP server. New tools start disabled."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    mcp_server_id: Optional[str] = None
    name: Optional[str] = None
    description: Optional[str] = None
    input_schema: Optional[str] = None
    enabled: Optional[bool] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class McpServerDetail(Model):
    """An MCP server with its tools (sorted by name)."""
    _nested: ClassVar[Dict[str, str]] = {'server': 'McpServer', 'tools': 'McpServerTool'}
    server: Optional[McpServer] = None
    tools: Optional[List[McpServerTool]] = None


@dataclass
class McpServerTestResult(Model):
    """Result of testing an MCP server connection; nothing is saved. A failed connection has ``success`` False."""
    success: Optional[bool] = None
    tool_count: Optional[int] = None
    tools: Optional[List[str]] = None
    error: Optional[str] = None


@dataclass
class RunToolCall(Model):
    """One agentic tool call made during a run."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    run_id: Optional[str] = None
    topic_id: Optional[str] = None
    tool: Optional[str] = None
    arguments_summary: Optional[str] = None
    succeeded: Optional[bool] = None
    error_message: Optional[str] = None
    result_characters: Optional[int] = None
    duration_ms: Optional[float] = None
    sort_order: Optional[int] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class UsageMeter(Model):
    """My usage against my plan's limits. A None limit means unlimited."""
    audio_generated_seconds_today: Optional[float] = None
    audio_reserved_seconds_today: Optional[int] = None
    audio_daily_seconds_max: Optional[int] = None
    audio_reset_utc: Optional[str] = None
    system_subscriptions: Optional[int] = None
    system_subscriptions_max: Optional[int] = None

    plan_key: Optional[str] = None
    plan_name: Optional[str] = None
    month_to_date_cost_usd: Optional[float] = None
    monthly_cost_cap_usd: Optional[float] = None
    cost_used_fraction: Optional[float] = None
    reset_utc: Optional[str] = None
    topics: Optional[int] = None
    topics_max: Optional[int] = None
    runs_today: Optional[int] = None
    runs_daily_max: Optional[int] = None
    schedule_min_interval_minutes: Optional[int] = None
    audio_minutes: Optional[float] = None
    audio_minutes_max: Optional[int] = None
    audio_enabled: Optional[bool] = None
    custom_endpoints: Optional[bool] = None


@dataclass
class TopicDimension(Model):
    """A facet of a topic that research searches, reads, and ranks against."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    topic_id: Optional[str] = None
    name: Optional[str] = None
    description: Optional[str] = None
    rationale: Optional[str] = None
    weight: Optional[str] = None
    include_terms: Optional[List[str]] = None
    exclude_terms: Optional[List[str]] = None
    is_locked: Optional[bool] = None
    origin: Optional[str] = None
    sort_order: Optional[int] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class TopicConstraint(Model):
    """A rule or source on a topic: include or exclude terms and entities, domains, tracked questions, feeds, seed URLs."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    topic_id: Optional[str] = None
    dimension_id: Optional[str] = None
    kind: Optional[str] = None
    value: Optional[str] = None
    note: Optional[str] = None
    is_locked: Optional[bool] = None
    origin: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class TopicRecipient(Model):
    """An email recipient for a topic."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    topic_id: Optional[str] = None
    email: Optional[str] = None
    user_id: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class TopicDetail(Model):
    """A topic with its dimensions, constraints, and recipients."""
    _nested: ClassVar[Dict[str, str]] = {'topic': 'Topic', 'dimensions': 'TopicDimension', 'constraints': 'TopicConstraint', 'recipients': 'TopicRecipient'}
    topic: Optional[Topic] = None
    dimensions: Optional[List[TopicDimension]] = None
    constraints: Optional[List[TopicConstraint]] = None
    recipients: Optional[List[TopicRecipient]] = None


@dataclass
class RefinementSession(Model):
    """A topic refinement wizard session. The topic exists from the start in Draft status and holds the evolving dimensions, constraints, and brief."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    topic_id: Optional[str] = None
    user_id: Optional[str] = None
    status: Optional[str] = None
    step: Optional[str] = None
    round: Optional[int] = None
    seed_text: Optional[str] = None
    goal: Optional[str] = None
    depth: Optional[str] = None
    horizon_text: Optional[str] = None
    known: Optional[str] = None
    sufficient_context: Optional[bool] = None
    out_of_scope_reason: Optional[str] = None
    model_endpoint_id: Optional[str] = None
    last_error: Optional[str] = None
    completed_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class RefinementMessage(Model):
    """A message in a refinement session transcript."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    session_id: Optional[str] = None
    role: Optional[str] = None
    content: Optional[str] = None
    llm_call_id: Optional[str] = None
    sequence: Optional[int] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class RefinementQuestion(Model):
    """A clarifying question asked during refinement, with the reader answer."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    session_id: Optional[str] = None
    round: Optional[int] = None
    dimension_id: Optional[str] = None
    question_text: Optional[str] = None
    rationale: Optional[str] = None
    kind: Optional[str] = None
    answer_text: Optional[str] = None
    answered: Optional[bool] = None
    skipped: Optional[bool] = None
    sort_order: Optional[int] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class RefinementQuestionOption(Model):
    """An option for a choice question."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    question_id: Optional[str] = None
    label: Optional[str] = None
    value: Optional[str] = None
    selected: Optional[bool] = None
    sort_order: Optional[int] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class RefinementQuestionDetail(Model):
    """A refinement question with its options."""
    _nested: ClassVar[Dict[str, str]] = {'question': 'RefinementQuestion', 'options': 'RefinementQuestionOption'}
    question: Optional[RefinementQuestion] = None
    options: Optional[List[RefinementQuestionOption]] = None


@dataclass
class RefinementState(Model):
    """Everything the wizard needs to render a session."""
    _nested: ClassVar[Dict[str, str]] = {'session': 'RefinementSession', 'topic': 'Topic', 'dimensions': 'TopicDimension', 'constraints': 'TopicConstraint', 'questions': 'RefinementQuestionDetail', 'messages': 'RefinementMessage'}
    session: Optional[RefinementSession] = None
    topic: Optional[Topic] = None
    dimensions: Optional[List[TopicDimension]] = None
    constraints: Optional[List[TopicConstraint]] = None
    questions: Optional[List[RefinementQuestionDetail]] = None
    messages: Optional[List[RefinementMessage]] = None


@dataclass
class ResearchRequest(Model):
    """A one-time request ("build me a podcast on X"): researched once, now or at a set time, and delivered as a report, podcast, or email."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    topic_id: Optional[str] = None
    request_text: Optional[str] = None
    title: Optional[str] = None
    deliverables: Optional[List[str]] = None
    length: Optional[str] = None
    status: Optional[str] = None
    run_at_utc: Optional[str] = None
    email_recipients: Optional[List[str]] = None
    publish_to_feed: Optional[bool] = None
    out_of_scope_reason: Optional[str] = None
    estimated_cost_usd: Optional[float] = None
    run_id: Optional[str] = None
    briefing_id: Optional[str] = None
    converted_topic_id: Optional[str] = None
    error_message: Optional[str] = None
    confirmed_utc: Optional[str] = None
    completed_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class ResearchRequestDetail(Model):
    """A one-time request with its plan and clarifying questions."""
    _nested: ClassVar[Dict[str, str]] = {'request': 'ResearchRequest', 'topic': 'Topic', 'dimensions': 'TopicDimension', 'questions': 'RefinementQuestionDetail'}
    request: Optional[ResearchRequest] = None
    topic: Optional[Topic] = None
    dimensions: Optional[List[TopicDimension]] = None
    questions: Optional[List[RefinementQuestionDetail]] = None
    refinement_session_id: Optional[str] = None


@dataclass
class Schedule(Model):
    """When research runs for a topic, an edition composes, or a one-time request runs. Times are computed in the schedule time zone."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    owner_type: Optional[str] = None
    owner_id: Optional[str] = None
    user_id: Optional[str] = None
    name: Optional[str] = None
    interval_type: Optional[str] = None
    interval_value: Optional[int] = None
    minute_offset: Optional[int] = None
    days_of_week_mask: Optional[int] = None
    days_of_month: Optional[List[int]] = None
    times_of_day: Optional[List[str]] = None
    cron_expression: Optional[str] = None
    time_zone: Optional[str] = None
    start_utc: Optional[str] = None
    end_utc: Optional[str] = None
    run_once_utc: Optional[str] = None
    enabled: Optional[bool] = None
    missed_run_policy: Optional[str] = None
    next_run_utc: Optional[str] = None
    last_run_utc: Optional[str] = None
    locked_until_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class SchedulePreview(Model):
    """Validation result and upcoming runs for a schedule."""
    valid: Optional[bool] = None
    errors: Optional[List[str]] = None
    next_runs_utc: Optional[List[str]] = None
    next_runs_local: Optional[List[str]] = None


@dataclass
class Run(Model):
    """A research run for a topic or one-time request, from queued to finished."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    topic_id: Optional[str] = None
    user_id: Optional[str] = None
    schedule_id: Optional[str] = None
    research_request_id: Optional[str] = None
    trigger: Optional[str] = None
    status: Optional[str] = None
    is_dry_run: Optional[bool] = None
    cancel_requested: Optional[bool] = None
    queued_utc: Optional[str] = None
    started_utc: Optional[str] = None
    completed_utc: Optional[str] = None
    locked_until_utc: Optional[str] = None
    worker_id: Optional[str] = None
    skipped_reason: Optional[str] = None
    error_message: Optional[str] = None
    summary: Optional[str] = None
    duration_ms: Optional[float] = None
    new_finding_count: Optional[int] = None
    duplicate_count: Optional[int] = None
    source_count: Optional[int] = None
    input_tokens: Optional[int] = None
    output_tokens: Optional[int] = None
    estimated_cost_usd: Optional[float] = None
    context_snapshot_hash: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None
    resume_from_stage: Optional[str] = None
    llm_call_count: Optional[int] = None
    unpriced_call_count: Optional[int] = None
    cached_input_tokens: Optional[int] = None
    reasoning_tokens: Optional[int] = None


@dataclass
class RunStage(Model):
    """One stage of a research run (plan section 16)."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    run_id: Optional[str] = None
    stage: Optional[str] = None
    status: Optional[str] = None
    started_utc: Optional[str] = None
    completed_utc: Optional[str] = None
    duration_ms: Optional[float] = None
    items_in: Optional[int] = None
    items_out: Optional[int] = None
    error_message: Optional[str] = None
    sort_order: Optional[int] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None
    llm_call_count: Optional[int] = None
    unpriced_call_count: Optional[int] = None
    input_tokens: Optional[int] = None
    output_tokens: Optional[int] = None
    cached_input_tokens: Optional[int] = None
    reasoning_tokens: Optional[int] = None
    estimated_cost_usd: Optional[float] = None


@dataclass
class CostTotals(Model):
    """Cost and token totals for a run, a stage, a model, or a task.

    Amounts are US dollar estimates from the endpoints' prices, None when no call was priced.
    ``unpriced_call_count`` counts calls with no price (an endpoint without prices, or an agent CLI on a
    subscription); they are left out of the amounts.
    """
    estimated_cost_usd: Optional[float] = None
    input_cost_usd: Optional[float] = None
    output_cost_usd: Optional[float] = None
    llm_call_count: Optional[int] = None
    unpriced_call_count: Optional[int] = None
    failed_call_count: Optional[int] = None
    input_tokens: Optional[int] = None
    output_tokens: Optional[int] = None
    cached_input_tokens: Optional[int] = None
    cache_write_tokens: Optional[int] = None
    reasoning_tokens: Optional[int] = None
    model_duration_ms: Optional[float] = None


@dataclass
class StageCost(CostTotals):
    """Cost of one run stage. ``share_of_cost`` is the stage's share of the run's cost, 0 to 1, or None."""
    run_stage_id: Optional[str] = None
    stage: Optional[str] = None
    status: Optional[str] = None
    sort_order: Optional[int] = None
    duration_ms: Optional[float] = None
    items_in: Optional[int] = None
    items_out: Optional[int] = None
    share_of_cost: Optional[float] = None


@dataclass
class ModelCost(CostTotals):
    """Cost of one model endpoint and model in a run."""
    model_endpoint_id: Optional[str] = None
    endpoint_name: Optional[str] = None
    provider: Optional[str] = None
    model: Optional[str] = None


@dataclass
class TaskCost(CostTotals):
    """Cost of one model task in a run."""
    task: Optional[str] = None


@dataclass
class RunCostReport(Model):
    """What a run cost: totals, each stage in run order, each model, and each task.

    Computed from the run's model calls on request, so it is current while the run is going.
    ``unattributed`` holds calls with no stage, or None when every call has one. ``currency`` is always ``USD``.
    """
    _nested: ClassVar[Dict[str, str]] = {'totals': 'CostTotals', 'stages': 'StageCost', 'unattributed': 'CostTotals', 'by_model': 'ModelCost', 'by_task': 'TaskCost'}
    run_id: Optional[str] = None
    topic_id: Optional[str] = None
    status: Optional[str] = None
    currency: Optional[str] = None
    totals: Optional[CostTotals] = None
    cost_per_new_finding_usd: Optional[float] = None
    stages: Optional[List[StageCost]] = None
    unattributed: Optional[CostTotals] = None
    by_model: Optional[List[ModelCost]] = None
    by_task: Optional[List[TaskCost]] = None


@dataclass
class SearchQuery(Model):
    """A search query planned for a run and its outcome per provider."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    run_id: Optional[str] = None
    topic_id: Optional[str] = None
    dimension_id: Optional[str] = None
    query_text: Optional[str] = None
    rationale: Optional[str] = None
    search_provider_id: Optional[str] = None
    provider: Optional[str] = None
    result_count: Optional[int] = None
    duration_ms: Optional[float] = None
    succeeded: Optional[bool] = None
    error_message: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class SearchResult(Model):
    """A search result considered by a run, with what happened to it."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    search_query_id: Optional[str] = None
    run_id: Optional[str] = None
    topic_id: Optional[str] = None
    url: Optional[str] = None
    canonical_url_hash: Optional[str] = None
    title: Optional[str] = None
    snippet: Optional[str] = None
    published_utc: Optional[str] = None
    rank: Optional[int] = None
    provider: Optional[str] = None
    disposition: Optional[str] = None
    source_item_id: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class Finding(Model):
    """A ranked research finding backed by verified evidence."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    run_id: Optional[str] = None
    topic_id: Optional[str] = None
    user_id: Optional[str] = None
    dimension_id: Optional[str] = None
    source_item_id: Optional[str] = None
    title: Optional[str] = None
    summary: Optional[str] = None
    why_it_matters: Optional[str] = None
    category: Optional[str] = None
    relevance_score: Optional[float] = None
    llm_score: Optional[float] = None
    feedback_score: Optional[float] = None
    novelty_score: Optional[float] = None
    source_score: Optional[float] = None
    recency_score: Optional[float] = None
    tier: Optional[str] = None
    relevance_rationale: Optional[str] = None
    confidence: Optional[float] = None
    is_duplicate: Optional[bool] = None
    duplicate_of_finding_id: Optional[str] = None
    is_update_of_finding_id: Optional[str] = None
    rank: Optional[int] = None
    user_rating: Optional[str] = None
    published_utc: Optional[str] = None
    is_dry_run: Optional[bool] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None
    pinned: Optional[bool] = None
    hidden: Optional[bool] = None


@dataclass
class Evidence(Model):
    """A verbatim quote from a source, verified against the stored text."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    source_item_id: Optional[str] = None
    run_id: Optional[str] = None
    quote: Optional[str] = None
    start_offset: Optional[int] = None
    end_offset: Optional[int] = None
    context_text: Optional[str] = None
    quote_hash: Optional[str] = None
    verified: Optional[bool] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class EvidenceView(Model):
    """A piece of evidence with where it came from."""
    _nested: ClassVar[Dict[str, str]] = {'evidence': 'Evidence'}
    evidence: Optional[Evidence] = None
    source_url: Optional[str] = None
    source_title: Optional[str] = None
    source_domain: Optional[str] = None
    source_published_utc: Optional[str] = None
    role: Optional[str] = None


@dataclass
class FindingDetail(Model):
    """A finding with its evidence, source, and dimension."""
    _nested: ClassVar[Dict[str, str]] = {'finding': 'Finding', 'evidence': 'EvidenceView', 'dimension': 'TopicDimension'}
    finding: Optional[Finding] = None
    evidence: Optional[List[EvidenceView]] = None
    dimension: Optional[TopicDimension] = None
    topic_name: Optional[str] = None


@dataclass
class SourceItem(Model):
    """A fetched document, shared across runs in a tenant and reused while its content is unchanged."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    canonical_url: Optional[str] = None
    canonical_url_hash: Optional[str] = None
    domain: Optional[str] = None
    title: Optional[str] = None
    published_utc: Optional[str] = None
    fetched_utc: Optional[str] = None
    http_status: Optional[int] = None
    content_type: Optional[str] = None
    content_hash: Optional[str] = None
    content_text: Optional[str] = None
    content_bytes: Optional[int] = None
    first_seen_run_id: Optional[str] = None
    times_seen: Optional[int] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class Prediction(Model):
    """A falsifiable prediction with a confidence and a horizon, graded later."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    run_id: Optional[str] = None
    topic_id: Optional[str] = None
    user_id: Optional[str] = None
    dimension_id: Optional[str] = None
    statement: Optional[str] = None
    rationale: Optional[str] = None
    confidence: Optional[float] = None
    horizon_utc: Optional[str] = None
    status: Optional[str] = None
    resolved_utc: Optional[str] = None
    resolved_by: Optional[str] = None
    resolution_note: Optional[str] = None
    brier_score: Optional[float] = None
    is_dry_run: Optional[bool] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class PredictionReview(Model):
    """A proposed resolution of a prediction from new evidence; final only when accepted."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    prediction_id: Optional[str] = None
    run_id: Optional[str] = None
    proposed_status: Optional[str] = None
    rationale: Optional[str] = None
    confidence: Optional[float] = None
    accepted_by_user: Optional[bool] = None
    finding_ids: Optional[List[str]] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class PredictionDetail(Model):
    """A prediction with its proposed reviews."""
    _nested: ClassVar[Dict[str, str]] = {'prediction': 'Prediction', 'reviews': 'PredictionReview'}
    prediction: Optional[Prediction] = None
    reviews: Optional[List[PredictionReview]] = None


@dataclass
class Suggestion(Model):
    """Something worth digging into, linked to findings."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    run_id: Optional[str] = None
    topic_id: Optional[str] = None
    user_id: Optional[str] = None
    title: Optional[str] = None
    description: Optional[str] = None
    rationale: Optional[str] = None
    priority: Optional[str] = None
    effort: Optional[str] = None
    status: Optional[str] = None
    snoozed_until_utc: Optional[str] = None
    status_changed_utc: Optional[str] = None
    finding_ids: Optional[List[str]] = None
    is_dry_run: Optional[bool] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class Feedback(Model):
    """A reader rating of an item with an optional reason. The newest rating per user and item is current."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    target_type: Optional[str] = None
    target_id: Optional[str] = None
    topic_id: Optional[str] = None
    relevance: Optional[str] = None
    accuracy: Optional[str] = None
    usefulness: Optional[str] = None
    reason: Optional[str] = None
    channel: Optional[str] = None
    email_message_id: Optional[str] = None
    supersedes_feedback_id: Optional[str] = None
    is_current: Optional[bool] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class LearnedPreference(Model):
    """A preference the research agent follows: written by the reader or distilled from feedback and approved."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    topic_id: Optional[str] = None
    kind: Optional[str] = None
    statement: Optional[str] = None
    status: Optional[str] = None
    origin: Optional[str] = None
    support_count: Optional[int] = None
    confidence: Optional[float] = None
    last_evidence_utc: Optional[str] = None
    decided_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class PreferenceDetail(Model):
    """A learned preference with the feedback behind it."""
    _nested: ClassVar[Dict[str, str]] = {'preference': 'LearnedPreference', 'support': 'Feedback'}
    preference: Optional[LearnedPreference] = None
    support: Optional[List[Feedback]] = None


@dataclass
class Edition(Model):
    """A recurring briefing across topics with its own schedule, template, and delivery (plan section 21.3)."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    name: Optional[str] = None
    description: Optional[str] = None
    briefing_template_id: Optional[str] = None
    voice_profile_id: Optional[str] = None
    audio_enabled: Optional[bool] = None
    email_enabled: Optional[bool] = None
    push_enabled: Optional[bool] = None
    enabled: Optional[bool] = None
    fresh_research_before_compose: Optional[bool] = None
    fresh_research_lead_minutes: Optional[int] = None
    lookback_mode: Optional[str] = None
    lookback_hours: Optional[int] = None
    last_composed_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class EditionTopic(Model):
    """A topic included in an edition."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    edition_id: Optional[str] = None
    topic_id: Optional[str] = None
    sort_order: Optional[int] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class EditionDetail(Model):
    """An edition with its topics and schedules."""
    _nested: ClassVar[Dict[str, str]] = {'edition': 'Edition', 'topics': 'EditionTopic', 'schedules': 'Schedule'}
    edition: Optional[Edition] = None
    topics: Optional[List[EditionTopic]] = None
    schedules: Optional[List[Schedule]] = None
    covered_topic_ids: Optional[List[str]] = None


@dataclass
class Briefing(Model):
    """A composed briefing: an edition across topics, a single run, or a one-time report."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    kind: Optional[str] = None
    edition_id: Optional[str] = None
    topic_id: Optional[str] = None
    run_id: Optional[str] = None
    research_request_id: Optional[str] = None
    schedule_id: Optional[str] = None
    period_start_utc: Optional[str] = None
    period_end_utc: Optional[str] = None
    title: Optional[str] = None
    markdown: Optional[str] = None
    status: Optional[str] = None
    compose_after_utc: Optional[str] = None
    research_deadline_utc: Optional[str] = None
    locked_until_utc: Optional[str] = None
    briefing_template_version_id: Optional[str] = None
    media_asset_id: Optional[str] = None
    markdown_artifact_id: Optional[str] = None
    html_artifact_id: Optional[str] = None
    item_count: Optional[int] = None
    word_count: Optional[int] = None
    read_minutes: Optional[int] = None
    model_fallbacks: Optional[int] = None
    error_message: Optional[str] = None
    pinned: Optional[bool] = None
    is_dry_run: Optional[bool] = None
    composed_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class BriefingSection(Model):
    """One section of a composed briefing."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    briefing_id: Optional[str] = None
    section_key: Optional[str] = None
    heading: Optional[str] = None
    markdown: Optional[str] = None
    source: Optional[str] = None
    sort_order: Optional[int] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class BriefingItem(Model):
    """An item a briefing section covers, for feedback and analytics."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    briefing_id: Optional[str] = None
    section_id: Optional[str] = None
    item_type: Optional[str] = None
    item_id: Optional[str] = None
    citation_number: Optional[int] = None
    sort_order: Optional[int] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class BriefingDetail(Model):
    """A briefing with its sections, items, and stored files."""
    _nested: ClassVar[Dict[str, str]] = {'briefing': 'Briefing', 'sections': 'BriefingSection', 'items': 'BriefingItem', 'files': 'Artifact', 'audio': 'MediaAsset'}
    briefing: Optional[Briefing] = None
    sections: Optional[List[BriefingSection]] = None
    items: Optional[List[BriefingItem]] = None
    files: Optional[List[Artifact]] = None
    files_expired: Optional[bool] = None
    edition_name: Optional[str] = None
    topic_name: Optional[str] = None
    item_titles: Optional[Dict[str, str]] = None
    audio: Optional[MediaAsset] = None


@dataclass
class BriefingTemplate(Model):
    """A Markdown briefing template with front matter and section directives (plan section 21.1)."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    name: Optional[str] = None
    description: Optional[str] = None
    default_key: Optional[str] = None
    is_default: Optional[bool] = None
    current_version: Optional[int] = None
    customized: Optional[bool] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class BriefingTemplateVersion(Model):
    """One saved version of a briefing template."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    template_id: Optional[str] = None
    version: Optional[int] = None
    content: Optional[str] = None
    change_note: Optional[str] = None
    created_by_user_id: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class TemplateDetail(Model):
    """A briefing template with its current content and parsed outline."""
    _nested: ClassVar[Dict[str, str]] = {'template': 'BriefingTemplate'}
    template: Optional[BriefingTemplate] = None
    content: Optional[str] = None
    problems: Optional[List[Any]] = None
    sections: Optional[List[Any]] = None
    shared: Optional[bool] = None
    title: Optional[str] = None
    audio: Optional[Any] = None


@dataclass
class TemplateValidation(Model):
    """Result of validating template Markdown."""
    valid: Optional[bool] = None
    name: Optional[str] = None
    title: Optional[str] = None
    problems: Optional[List[Any]] = None
    sections: Optional[List[Any]] = None
    audio: Optional[Any] = None


@dataclass
class PromptTemplate(Model):
    """Editable, versioned prompt used by one pipeline task. Tenant-wide, or overridden for one topic."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    key: Optional[str] = None
    topic_id: Optional[str] = None
    name: Optional[str] = None
    description: Optional[str] = None
    role: Optional[str] = None
    output_type: Optional[str] = None
    current_version: Optional[int] = None
    is_customized: Optional[bool] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class PromptTemplateVersion(Model):
    """One saved version of a prompt template."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    prompt_template_id: Optional[str] = None
    version: Optional[int] = None
    content: Optional[str] = None
    change_note: Optional[str] = None
    created_by_user_id: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class PromptTemplateDetail(Model):
    """Prompt template with its current content."""
    _nested: ClassVar[Dict[str, str]] = {'template': 'PromptTemplate'}
    template: Optional[PromptTemplate] = None
    content: Optional[str] = None
    variables: Optional[List[str]] = None
    default_content: Optional[str] = None


@dataclass
class MediaAsset(Model):
    """An audio episode made from a briefing: script, synthesis status, stored audio, and chapters."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    briefing_id: Optional[str] = None
    edition_id: Optional[str] = None
    kind: Optional[str] = None
    status: Optional[str] = None
    format: Optional[str] = None
    voice_profile_id: Optional[str] = None
    title: Optional[str] = None
    script_markdown: Optional[str] = None
    script_from_model: Optional[bool] = None
    artifact_id: Optional[str] = None
    chapters_artifact_id: Optional[str] = None
    content_type: Optional[str] = None
    bytes: Optional[int] = None
    duration_seconds: Optional[float] = None
    segment_count: Optional[int] = None
    attempts: Optional[int] = None
    locked_until_utc: Optional[str] = None
    error_message: Optional[str] = None
    completed_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class MediaChapter(Model):
    """A chapter of an audio episode, aligned to a briefing section."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    media_asset_id: Optional[str] = None
    briefing_section_id: Optional[str] = None
    title: Optional[str] = None
    start_seconds: Optional[float] = None
    end_seconds: Optional[float] = None
    sort_order: Optional[int] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class MediaDetail(Model):
    """An audio episode with chapters and download paths."""
    _nested: ClassVar[Dict[str, str]] = {'media': 'MediaAsset', 'chapters': 'MediaChapter'}
    media: Optional[MediaAsset] = None
    chapters: Optional[List[MediaChapter]] = None
    audio_path: Optional[str] = None
    chapters_path: Optional[str] = None


@dataclass
class Artifact(Model):
    """A stored file: briefing Markdown or HTML, audio, chapters, or an export. Kept up to the retention window unless pinned."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    kind: Optional[str] = None
    topic_id: Optional[str] = None
    briefing_id: Optional[str] = None
    research_request_id: Optional[str] = None
    file_name: Optional[str] = None
    content_type: Optional[str] = None
    bytes: Optional[int] = None
    sha256: Optional[str] = None
    storage_provider: Optional[str] = None
    storage_key: Optional[str] = None
    expires_utc: Optional[str] = None
    pinned: Optional[bool] = None
    deleted_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class DownloadUrl(Model):
    """A time-limited download URL, when the store offers one."""
    url: Optional[str] = None
    expires_utc: Optional[str] = None
    content_path: Optional[str] = None


@dataclass
class FeedToken(Model):
    """A private podcast feed token. Only its SHA-256 hash is stored."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    edition_id: Optional[str] = None
    name: Optional[str] = None
    token_hash: Optional[str] = None
    token_prefix: Optional[str] = None
    last_used_utc: Optional[str] = None
    revoked_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class BriefingShare(Model):
    """A public share link for a briefing. Only the hash of the link secret is stored. Every share expires."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    briefing_id: Optional[str] = None
    token_hash: Optional[str] = None
    token_prefix: Optional[str] = None
    show_owner_name: Optional[bool] = None
    expires_utc: Optional[str] = None
    revoked_utc: Optional[str] = None
    view_count: Optional[int] = None
    last_viewed_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class BriefingShareCreated(Model):
    """A new public share; the secret and the two links are shown once."""
    _nested: ClassVar[Dict[str, str]] = {'share': 'BriefingShare'}
    share: Optional[BriefingShare] = None
    secret: Optional[str] = None
    url: Optional[str] = None
    read_url: Optional[str] = None
    listen_url: Optional[str] = None
    audio_ready: Optional[bool] = None


@dataclass
class FeedTokenCreated(Model):
    """A new feed token; the secret and URL are shown once."""
    _nested: ClassVar[Dict[str, str]] = {'token': 'FeedToken'}
    token: Optional[FeedToken] = None
    secret: Optional[str] = None
    feed_url: Optional[str] = None


@dataclass
class EmailMessage(Model):
    """An outbound email in the outbox: rendered once, delivered by the worker with retries, idempotent per key."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    template: Optional[str] = None
    run_id: Optional[str] = None
    briefing_id: Optional[str] = None
    topic_id: Optional[str] = None
    recipient: Optional[str] = None
    subject: Optional[str] = None
    status: Optional[str] = None
    provider: Optional[str] = None
    provider_message_id: Optional[str] = None
    attempts: Optional[int] = None
    next_attempt_utc: Optional[str] = None
    locked_until_utc: Optional[str] = None
    last_error: Optional[str] = None
    idempotency_key: Optional[str] = None
    rendered_html: Optional[str] = None
    rendered_text: Optional[str] = None
    sent_utc: Optional[str] = None
    failed_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class EmailPreferences(Model):
    """My email preferences."""
    address: Optional[str] = None
    run_emails: Optional[bool] = None
    briefing_emails: Optional[bool] = None
    failure_emails: Optional[bool] = None
    unsubscribed_topic_ids: Optional[List[str]] = None


@dataclass
class UsageReport(Model):
    """Model usage and cost report."""
    _nested: ClassVar[Dict[str, str]] = {'totals': 'UsageRow', 'rows': 'UsageRow'}
    from_utc: Optional[str] = None
    to_utc: Optional[str] = None
    group_by: Optional[str] = None
    totals: Optional[UsageRow] = None
    rows: Optional[List[UsageRow]] = None


@dataclass
class UsageRow(Model):
    """Aggregated model usage for one group.

    With Model grouping, ``key`` is the endpoint ID and the model name joined by ``|`` and ``model_endpoint_id`` and
    ``model`` carry the two parts. In a grouped time series the key ``(other)`` combines the groups beyond ``top``.
    """
    key: Optional[str] = None
    label: Optional[str] = None
    model_endpoint_id: Optional[str] = None
    model: Optional[str] = None
    calls: Optional[int] = None
    failures: Optional[int] = None
    input_tokens: Optional[int] = None
    cached_input_tokens: Optional[int] = None
    cache_write_tokens: Optional[int] = None
    output_tokens: Optional[int] = None
    reasoning_tokens: Optional[int] = None
    total_tokens: Optional[int] = None
    tool_calls: Optional[int] = None
    cost_usd: Optional[float] = None
    average_duration_ms: Optional[float] = None
    average_time_to_first_token_ms: Optional[float] = None
    p95_time_to_first_token_ms: Optional[float] = None
    cost_share: Optional[float] = None


@dataclass
class UsageTimeseries(Model):
    """Gap-filled model usage time series.

    ``group_by`` is None and ``groups`` is empty unless the series was requested with ``group_by``; then ``groups`` holds
    the range totals per group (highest cost first, ``(other)`` last) and each bucket's ``rows`` the groups with calls in it.
    """
    _nested: ClassVar[Dict[str, str]] = {'buckets': 'UsageBucket', 'groups': 'UsageRow'}
    from_utc: Optional[str] = None
    to_utc: Optional[str] = None
    bucket_minutes: Optional[int] = None
    group_by: Optional[str] = None
    groups: Optional[List[UsageRow]] = None
    buckets: Optional[List[UsageBucket]] = None


@dataclass
class UsageBucket(Model):
    """Model usage in one time bucket. The bucket's own fields cover every call in it; ``rows`` splits them by group."""
    _nested: ClassVar[Dict[str, str]] = {'rows': 'UsageRow'}
    bucket_start_utc: Optional[str] = None
    bucket_end_utc: Optional[str] = None
    calls: Optional[int] = None
    failures: Optional[int] = None
    input_tokens: Optional[int] = None
    output_tokens: Optional[int] = None
    cached_input_tokens: Optional[int] = None
    cache_write_tokens: Optional[int] = None
    reasoning_tokens: Optional[int] = None
    tool_calls: Optional[int] = None
    cost_usd: Optional[float] = None
    average_time_to_first_token_ms: Optional[float] = None
    p95_time_to_first_token_ms: Optional[float] = None
    rows: Optional[List[UsageRow]] = None


@dataclass
class LlmCall(Model):
    """One model call made through the model gateway, with tokens, latency, cost, and optional diagnostic payloads."""
    provider_connection_id: Optional[str] = None

    id: Optional[str] = None
    tenant_id: Optional[str] = None
    task: Optional[str] = None
    run_id: Optional[str] = None
    run_stage_id: Optional[str] = None
    refinement_session_id: Optional[str] = None
    briefing_id: Optional[str] = None
    model_endpoint_id: Optional[str] = None
    provider: Optional[str] = None
    model: Optional[str] = None
    prompt_key: Optional[str] = None
    prompt_template_version_id: Optional[str] = None
    user_id: Optional[str] = None
    topic_id: Optional[str] = None
    streamed: Optional[bool] = None
    attempts: Optional[int] = None
    input_tokens: Optional[int] = None
    cached_input_tokens: Optional[int] = None
    cache_write_tokens: Optional[int] = None
    output_tokens: Optional[int] = None
    reasoning_tokens: Optional[int] = None
    total_tokens: Optional[int] = None
    duration_ms: Optional[float] = None
    time_to_first_token_ms: Optional[float] = None
    streaming_duration_ms: Optional[float] = None
    chunk_count: Optional[int] = None
    output_tokens_per_second: Optional[float] = None
    tool_call_count: Optional[int] = None
    tool_names: Optional[List[str]] = None
    success: Optional[bool] = None
    status_code: Optional[int] = None
    finish_reason: Optional[str] = None
    error_message: Optional[str] = None
    structured_output_repairs: Optional[int] = None
    input_cost_usd: Optional[float] = None
    output_cost_usd: Optional[float] = None
    estimated_cost_usd: Optional[float] = None
    is_dry_run: Optional[bool] = None
    prompt_text: Optional[str] = None
    response_text: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class ModelEndpoint(Model):
    """A model provider endpoint reached through PolyPrompt.

    ``last_validation_succeeded`` is True after a successful validation or model call, False after a failed
    validation, and None when never checked or after its connection settings changed. ``auth_method`` is a
    :class:`ModelAuthMethod` name; None means the provider's usual method (an API key for hosted APIs, none for
    Ollama and OpenAI-compatible servers without a key, and the server's own sign-in for agent CLIs).
    """
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    name: Optional[str] = None
    provider: Optional[str] = None
    endpoint: Optional[str] = None
    credential_id: Optional[str] = None
    model: Optional[str] = None
    embedding_model: Optional[str] = None
    embedding_dimensions: Optional[int] = None
    supports_chat: Optional[bool] = None
    supports_tools: Optional[bool] = None
    supports_embeddings: Optional[bool] = None
    temperature: Optional[float] = None
    max_tokens: Optional[int] = None
    reasoning_effort: Optional[str] = None
    context_length: Optional[int] = None
    timeout_ms: Optional[int] = None
    input_cost_per_m_tok: Optional[float] = None
    output_cost_per_m_tok: Optional[float] = None
    cached_input_cost_per_m_tok: Optional[float] = None
    cache_write_cost_per_m_tok: Optional[float] = None
    last_validated_utc: Optional[str] = None
    last_validation_result: Optional[str] = None
    last_validation_succeeded: Optional[bool] = None
    auth_method: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class ModelAssignment(Model):
    """Assignment of a pipeline task role to a model endpoint, tenant-wide or for one topic."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    task: Optional[str] = None
    topic_id: Optional[str] = None
    model_endpoint_id: Optional[str] = None
    model_override: Optional[str] = None
    reasoning_effort: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class ModelValidation(Model):
    """Result of validating a model endpoint. ``succeeded`` is True when every check that applies to the
    endpoint passed (chat when it has a chat model, embeddings when it has an embedding model)."""
    reachable: Optional[bool] = None
    succeeded: Optional[bool] = None
    model_found: Optional[bool] = None
    supports_tools: Optional[bool] = None
    embedding_dimensions: Optional[int] = None
    messages: Optional[List[str]] = None
    validated_utc: Optional[str] = None


@dataclass
class SetupCheck(Model):
    """One thing the workspace needs before people can use it, and whether it is ready.

    ``key`` is stable: ``model:{endpointId}`` (or ``model:none``), ``search``, ``embeddings``, ``email``, or
    ``speech``. The speech check (the text to speech endpoint) carries ``model``, ``voice`` (the default voice),
    and ``base_url``.
    ``kind`` is a :class:`SetupCheckKind` name and ``state`` a :class:`SetupCheckState` name. For model checks,
    ``auth_method`` is a :class:`ModelAuthMethod` name and ``needs_api_key`` tells whether that method needs a
    stored secret (an API key, a token, or a sign-in file). ``base_url`` is the server address for Ollama and
    OpenAI-compatible model checks shown to administrators, and None otherwise.
    """
    key: Optional[str] = None
    kind: Optional[str] = None
    required: Optional[bool] = None
    state: Optional[str] = None
    message: Optional[str] = None
    resource_id: Optional[str] = None
    resource_name: Optional[str] = None
    provider: Optional[str] = None
    model: Optional[str] = None
    voice: Optional[str] = None
    base_url: Optional[str] = None
    tasks: Optional[List[str]] = None
    auth_method: Optional[str] = None
    needs_api_key: Optional[bool] = None
    has_api_key: Optional[bool] = None
    uses_platform_key: Optional[bool] = None
    verified_utc: Optional[str] = None


@dataclass
class SetupConnectionResult(Model):
    """Outcome of a Setup page connection attempt.

    ``succeeded`` tells whether the live test prompt worked. ``kept_previous`` is True when a working model was
    kept because the new one failed its test; the workspace keeps using it. ``message`` is the test's result in
    plain language (the provider's error text on failure).
    """
    model_endpoint_id: Optional[str] = None
    succeeded: Optional[bool] = None
    kept_previous: Optional[bool] = None
    message: Optional[str] = None


@dataclass
class SetupStatus(Model):
    """Whether the workspace has everything it needs, checked with live calls.

    ``ready`` is True when every required check is ready; ``can_configure`` tells whether the caller (a
    global or tenant administrator) can change the configuration. Checks come required ones first.
    ``connection`` is the result of the attempt on responses to :meth:`SetupResource.connect`, and None
    otherwise.
    """
    _nested: ClassVar[Dict[str, str]] = {'checks': 'SetupCheck', 'connection': 'SetupConnectionResult'}
    ready: Optional[bool] = None
    can_configure: Optional[bool] = None
    checks: Optional[List[SetupCheck]] = None
    checked_utc: Optional[str] = None
    connection: Optional[SetupConnectionResult] = None


@dataclass
class SetupAuthOption(Model):
    """One way a provider can sign in, as offered on the Setup page.

    ``method`` is a :class:`ModelAuthMethod` name. ``input`` is what the user supplies: ``Text`` (a key or
    token), ``File`` (a sign-in file's contents, named by ``file_name``: ``auth.json``, ``oauth_creds.json``,
    or ``endpoints.json``), or ``None``.
    """
    method: Optional[str] = None
    input: Optional[str] = None
    file_name: Optional[str] = None
    recommended: Optional[bool] = None


@dataclass
class SetupProviderOption(Model):
    """A model provider the Setup page can connect, with the ways it can sign in (recommended first).

    ``group`` is a :class:`SetupProviderGroup` name. ``available`` tells whether the provider can be used on
    this server now (for agent CLIs: allowed and installed), with ``unavailable_reason`` when it cannot;
    ``turned_off`` is True for agent CLIs when the operator turned them off (``Models.AllowAgentClis``).
    ``default_model`` is the model filled in by default (``default`` lets an agent CLI choose; None means the
    user must name one). ``needs_base_url`` is True for Ollama and OpenAI-compatible servers.
    """
    _nested: ClassVar[Dict[str, str]] = {'methods': 'SetupAuthOption'}
    provider: Optional[str] = None
    group: Optional[str] = None
    available: Optional[bool] = None
    installed: Optional[bool] = None
    turned_off: Optional[bool] = None
    unavailable_reason: Optional[str] = None
    recommended: Optional[bool] = None
    default_model: Optional[str] = None
    needs_base_url: Optional[bool] = None
    default_base_url: Optional[str] = None
    methods: Optional[List[SetupAuthOption]] = None


@dataclass
class SetupOptions(Model):
    """Model providers the Setup page can connect on this server, recommended first within each group.

    ``server_container`` names the server container, for sign-in commands such as
    ``docker exec -it {container} claude``.
    """
    _nested: ClassVar[Dict[str, str]] = {'providers': 'SetupProviderOption'}
    providers: Optional[List[SetupProviderOption]] = None
    server_container: Optional[str] = None


@dataclass
class ModelTestResult(Model):
    """Result of a model test call."""
    success: Optional[bool] = None
    text: Optional[str] = None
    model: Optional[str] = None
    input_tokens: Optional[int] = None
    output_tokens: Optional[int] = None
    duration_ms: Optional[float] = None
    error: Optional[str] = None
    supports_tools: Optional[bool] = None


@dataclass
class ModelListItem(Model):
    """A model reported by an endpoint."""
    name: Optional[str] = None
    display_name: Optional[str] = None
    input_token_limit: Optional[int] = None
    output_token_limit: Optional[int] = None
    supports_tools: Optional[bool] = None


@dataclass
class SearchProvider(Model):
    """A web search source: SearXNG, Tavily, You.com, RSS, Hacker News, or arXiv."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    name: Optional[str] = None
    kind: Optional[str] = None
    endpoint: Optional[str] = None
    credential_id: Optional[str] = None
    is_default: Optional[bool] = None
    max_results: Optional[int] = None
    timeout_ms: Optional[int] = None
    last_tested_utc: Optional[str] = None
    last_test_ok: Optional[bool] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class VoiceProfile(Model):
    """A text-to-speech voice: provider, endpoint, voices for narrator and two-host formats, and speed."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    name: Optional[str] = None
    provider: Optional[str] = None
    endpoint: Optional[str] = None
    model: Optional[str] = None
    voice: Optional[str] = None
    secondary_voice: Optional[str] = None
    speed: Optional[float] = None
    credential_id: Optional[str] = None
    is_default: Optional[bool] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class AvailableVoice(Model):
    """A voice someone can choose for a topic: a workspace voice profile, or a voice the speech server offers.

    ``voice`` is the speech server's voice ID (such as ``af_heart``) and ``label`` a readable label (such as
    "Heart (American English, female)"; the voice ID when it follows no known naming). ``voice_profile_id`` and
    ``name`` are None for a speech server voice that has no profile yet; choosing it for a topic creates one.
    """
    voice: Optional[str] = None
    label: Optional[str] = None
    voice_profile_id: Optional[str] = None
    name: Optional[str] = None
    is_default: Optional[bool] = None


@dataclass
class AvailableVoices(Model):
    """The voices someone can choose for a topic: voice profiles first, then the speech server's other voices.

    ``enabled`` tells whether speech is turned on for the server; ``listed`` is True when the speech server
    listed its voices and False when the list is only the configured default voices.
    """
    _nested: ClassVar[Dict[str, str]] = {'voices': 'AvailableVoice'}
    enabled: Optional[bool] = None
    listed: Optional[bool] = None
    default_voice_profile_id: Optional[str] = None
    voices: Optional[List["AvailableVoice"]] = None


@dataclass
class VaultCredential(Model):
    """Vault credential as returned by the API (never the secret)."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    name: Optional[str] = None
    kind: Optional[str] = None
    provider: Optional[str] = None
    header_name: Optional[str] = None
    username: Optional[str] = None
    secret_preview: Optional[str] = None
    description: Optional[str] = None
    created_by_user_id: Optional[str] = None
    last_used_utc: Optional[str] = None
    last_revealed_utc: Optional[str] = None
    expires_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class VaultUsageItem(Model):
    """A record that references a vault credential."""
    resource_type: Optional[str] = None
    resource_id: Optional[str] = None
    name: Optional[str] = None


@dataclass
class BreakdownRow(Model):
    """A rate for one group."""
    key: Optional[str] = None
    label: Optional[str] = None
    count: Optional[int] = None
    positive: Optional[int] = None
    rate: Optional[float] = None
    value: Optional[float] = None


@dataclass
class OverviewReport(Model):
    """Headline numbers for a window: activity counts plus the key figures from the relevance, accuracy, feedback, suggestion, quality, and usage reports. Built for dashboards, the mobile home screen, and the MCP summary tool."""
    from_utc: Optional[str] = None
    to_utc: Optional[str] = None
    active_topics: Optional[int] = None
    runs: Optional[int] = None
    runs_completed: Optional[int] = None
    runs_partially_succeeded: Optional[int] = None
    runs_failed: Optional[int] = None
    findings_shown: Optional[int] = None
    findings_rated: Optional[int] = None
    relevance_precision: Optional[float] = None
    briefings_ready: Optional[int] = None
    emails_sent: Optional[int] = None
    emails_failed: Optional[int] = None
    predictions_open: Optional[int] = None
    predictions_resolved: Optional[int] = None
    prediction_accuracy: Optional[float] = None
    mean_brier: Optional[float] = None
    feedback_total: Optional[int] = None
    feedback_with_reason: Optional[int] = None
    suggestions_new: Optional[int] = None
    suggestions_accepted: Optional[int] = None
    quote_failure_rate: Optional[float] = None
    model_calls: Optional[int] = None
    model_cost_usd: Optional[float] = None


@dataclass
class EmailReport(Model):
    """Email delivery for a window: totals, delivery time, a breakdown by template and day, feedback link use, and unsubscribes."""
    _nested: ClassVar[Dict[str, str]] = {'by_template': 'BreakdownRow', 'by_day': 'BreakdownRow', 'by_status': 'BreakdownRow'}
    total: Optional[int] = None
    sent: Optional[int] = None
    failed: Optional[int] = None
    pending: Optional[int] = None
    skipped: Optional[int] = None
    delivery_rate: Optional[float] = None
    mean_seconds_to_send: Optional[float] = None
    by_template: Optional[List[BreakdownRow]] = None
    by_day: Optional[List[BreakdownRow]] = None
    by_status: Optional[List[BreakdownRow]] = None
    feedback_link_opens: Optional[int] = None
    feedback_link_submits: Optional[int] = None
    unsubscribes: Optional[int] = None


@dataclass
class FeedbackReport(Model):
    """Feedback volume and coverage (plan section 20)."""
    _nested: ClassVar[Dict[str, str]] = {'by_channel': 'BreakdownRow', 'by_target_type': 'BreakdownRow', 'by_day': 'BreakdownRow', 'by_channel_day': 'BreakdownRow'}
    total: Optional[int] = None
    with_reason: Optional[int] = None
    by_channel: Optional[List[BreakdownRow]] = None
    by_target_type: Optional[List[BreakdownRow]] = None
    by_day: Optional[List[BreakdownRow]] = None
    by_channel_day: Optional[List[BreakdownRow]] = None
    email_link_opens: Optional[int] = None
    email_link_submits: Optional[int] = None


@dataclass
class Device(Model):
    """A mobile device registered for push notifications."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    expo_push_token: Optional[str] = None
    platform: Optional[str] = None
    name: Optional[str] = None
    locale: Optional[str] = None
    last_seen_utc: Optional[str] = None
    last_error: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class Notification(Model):
    """An in-app notification for the signed-in user, with the item it points to."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    kind: Optional[str] = None
    title: Optional[str] = None
    body: Optional[str] = None
    topic_id: Optional[str] = None
    run_id: Optional[str] = None
    briefing_id: Optional[str] = None
    catalog_publication_id: Optional[str] = None
    media_asset_id: Optional[str] = None
    prediction_id: Optional[str] = None
    read_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class NotificationPreferenceCell(Model):
    """One cell of the notification preference matrix: a kind of notification on one channel."""
    kind: Optional[str] = None
    channel: Optional[str] = None
    enabled: Optional[bool] = None


@dataclass
class NotificationChannelStatus(Model):
    """Whether a notification channel can deliver right now, and why not."""
    channel: Optional[str] = None
    available: Optional[bool] = None
    reason: Optional[str] = None


@dataclass
class NotificationPreferences(Model):
    """My notification preferences: every kind on every channel, and whether each channel can deliver."""
    _nested: ClassVar[Dict[str, str]] = {'preferences': 'NotificationPreferenceCell', 'channels': 'NotificationChannelStatus'}
    preferences: Optional[List[NotificationPreferenceCell]] = None
    channels: Optional[List[NotificationChannelStatus]] = None
    address: Optional[str] = None
    unsubscribed_topic_ids: Optional[List[str]] = None

    def enabled(self, kind: Any, channel: Any) -> bool:
        """Whether a kind is on for a channel (``False`` when the cell is missing)."""
        k = kind.value if isinstance(kind, Enum) else kind
        c = channel.value if isinstance(channel, Enum) else channel
        for cell in self.preferences or []:
            if cell.kind == k and cell.channel == c:
                return bool(cell.enabled)
        return False


@dataclass
class NotificationCount(Model):
    """A number of notifications: how many are unread, or how many were marked read."""
    count: Optional[int] = None


@dataclass
class BriefingProgress(Model):
    """Private listening position for an owned or subscribed briefing issue."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    briefing_id: Optional[str] = None
    position_seconds: Optional[float] = None
    duration_seconds: Optional[float] = None
    completed: Optional[bool] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


# TYPES BEGIN PLATFORM
@dataclass
class CatalogEntryResponse(Model):
    """Public catalog projection. Contains no canonical workspace, prompts, credentials, or research trace."""
    id: Optional[str] = None
    title: Optional[str] = None
    description: Optional[str] = None
    category: Optional[str] = None
    language: Optional[str] = None
    status: Optional[str] = None
    free_eligible: Optional[bool] = None
    eligible: Optional[bool] = None
    subscribed: Optional[bool] = None
    suspended: Optional[bool] = None
    email_enabled: Optional[bool] = None
    push_enabled: Optional[bool] = None
    sort_order: Optional[int] = None
    published_utc: Optional[str] = None


@dataclass
class CatalogIssueResponse(Model):
    """Shared issue content projection. Always authorized through the caller's subscription."""
    id: Optional[str] = None
    publication_id: Optional[str] = None
    title: Optional[str] = None
    markdown: Optional[str] = None
    created_utc: Optional[str] = None
    audio_path: Optional[str] = None
    audio_seconds: Optional[float] = None


@dataclass
class CatalogResearchResponse(Model):
    """System-administrator-only canonical research settings."""
    topic: Optional[Any] = None
    schedule: Optional[Any] = None
    _nested: ClassVar[Dict[str, str]] = {'topic': 'Topic', 'schedule': 'Schedule'}


@dataclass
class CatalogSubscription(Model):
    """A user subscribes to a canonical publication; delivery and interaction state stay private."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    publication_id: Optional[str] = None
    email_enabled: Optional[bool] = None
    push_enabled: Optional[bool] = None
    suspended: Optional[bool] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class CatalogPublication(Model):
    """Explicit administrator publication of a canonical system topic. Reads use an allowlisted projection."""
    id: Optional[str] = None
    topic_id: Optional[str] = None
    owner_tenant_id: Optional[str] = None
    title: Optional[str] = None
    description: Optional[str] = None
    category: Optional[str] = None
    language: Optional[str] = None
    status: Optional[str] = None
    free_eligible: Optional[bool] = None
    sort_order: Optional[int] = None
    revision: Optional[int] = None
    published_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class BillingStatusResponse(Model):
    """Account-bound purchase offers, captured signup benefit and verified access."""
    enabled: Optional[bool] = None
    purchase_account_token: Optional[str] = None
    apple_available: Optional[bool] = None
    google_available: Optional[bool] = None
    offers: Optional[List[Any]] = None
    signup_benefit: Optional[Any] = None
    subscriptions: Optional[List[Any]] = None
    plan: Optional[Any] = None
    _nested: ClassVar[Dict[str, str]] = {'offers': 'BillingOfferResponse', 'signup_benefit': 'SignupBenefit', 'subscriptions': 'BillingSubscriptionResponse', 'plan': 'EffectivePlan'}


@dataclass
class BillingOfferResponse(Model):
    """Public purchase offer and package allowances. Provider secrets are never included."""
    key: Optional[str] = None
    plan_key: Optional[str] = None
    interval: Optional[str] = None
    usd_cents: Optional[int] = None
    apple_product_id: Optional[str] = None
    google_product_id: Optional[str] = None
    google_base_plan_id: Optional[str] = None
    plan_name: Optional[str] = None
    custom_topics: Optional[str] = None
    system_subscriptions: Optional[str] = None
    daily_audio_seconds: Optional[str] = None
    web_available: Optional[bool] = None


@dataclass
class BillingSubscriptionResponse(Model):
    """Verified subscription status without payment identifiers or verification credentials."""
    id: Optional[str] = None
    provider: Optional[str] = None
    environment: Optional[str] = None
    plan_id: Optional[str] = None
    status: Optional[str] = None
    ends_utc: Optional[str] = None
    cancel_at_period_end: Optional[bool] = None
    last_verified_utc: Optional[str] = None


@dataclass
class BillingRedirectResponse(Model):
    """A server-selected billing redirect. The URL alone never grants paid access."""
    id: Optional[str] = None
    url: Optional[str] = None
    expires_utc: Optional[str] = None


@dataclass
class ProviderConnectionsResponse(Model):
    """Private connection metadata and current funding selection."""
    connections: Optional[List[Any]] = None
    local_speech_available: Optional[bool] = None
    plan: Optional[Any] = None
    _nested: ClassVar[Dict[str, str]] = {'connections': 'ProviderConnection', 'plan': 'EffectivePlan'}


@dataclass
class ProviderConnection(Model):
    """Private per-account model or speech connection. Secrets are encrypted and excluded from all API projections."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    kind: Optional[str] = None
    provider: Optional[str] = None
    endpoint: Optional[str] = None
    model: Optional[str] = None
    voice: Optional[str] = None
    secret_last4: Optional[str] = None
    validated_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None


@dataclass
class SignupBenefit(Model):
    """Immutable signup offer captured before checkout; never grants paid access before verification."""
    id: Optional[str] = None
    tenant_id: Optional[str] = None
    user_id: Optional[str] = None
    promo_code_id: Optional[str] = None
    plan_id: Optional[str] = None
    discount_percent: Optional[float] = None
    discount_months: Optional[int] = None
    redeemed_utc: Optional[str] = None
    revoked_utc: Optional[str] = None
    active: Optional[bool] = None
    created_utc: Optional[str] = None
    last_update_utc: Optional[str] = None
    billing_plan_key: Optional[str] = None
    duration_days: Optional[int] = None
    redeemed_subscription_id: Optional[str] = None
    apple_offer_id: Optional[str] = None
    google_offer_id: Optional[str] = None


@dataclass
class CatalogPublicationRequest(Model):
    """Administrator catalog metadata. A new publication creates an isolated canonical topic."""
    title: Optional[str] = None
    description: Optional[str] = None
    category: Optional[str] = None
    language: Optional[str] = None
    free_eligible: Optional[bool] = None
    status: Optional[str] = None
    sort_order: Optional[int] = None
    revision: Optional[int] = None


@dataclass
class CatalogResearchRequest(Model):
    """Canonical research configuration. The publication determines the protected owner; caller-supplied owner IDs are ignored."""
    topic: Optional[Any] = None
    schedule: Optional[Any] = None


@dataclass
class CatalogSubscriptionRequest(Model):
    """Personal delivery preferences for a shared briefing subscription."""
    email_enabled: Optional[bool] = None
    push_enabled: Optional[bool] = None


@dataclass
class EntitlementOverrideRequest(Model):
    """Set one user's entitlement override. Global administrator only. Delete the override to restore plan inheritance."""
    feature_key: Optional[str] = None
    value: Optional[str] = None
    reason: Optional[str] = None
    expires_utc: Optional[str] = None


@dataclass
class BillingCheckoutRequest(Model):
    """Start web checkout using an operator-configured public offer key."""
    offer_key: Optional[str] = None


@dataclass
class BillingPurchaseRequest(Model):
    """Native purchase evidence. Server verification and immutable account binding are always required."""
    provider: Optional[str] = None
    signed_transaction: Optional[str] = None
    purchase_token: Optional[str] = None


@dataclass
class ProviderConnectionRequest(Model):
    """Save a private model or speech connection. Secrets are write-only; an empty key retains an unchanged provider's key."""
    kind: Optional[str] = None
    provider: Optional[str] = None
    model: Optional[str] = None
    endpoint: Optional[str] = None
    api_key: Optional[str] = None
    voice: Optional[str] = None


@dataclass
class FundingModeRequest(Model):
    """Choose Personal for BYOK or Platform to restore the account's assigned package. Existing subscriptions must be managed separately."""
    mode: Optional[str] = None


@dataclass
class BriefingProgressRequest(Model):
    """Save where the caller stopped listening to a briefing."""
    position_seconds: Optional[float] = None
    duration_seconds: Optional[float] = None
    completed: Optional[bool] = None


# TYPES END PLATFORM

@dataclass
class EffectivePlan(Model):
    """The plan a user has right now and its entitlement values."""
    subscription_required: Optional[bool] = None
    funding_mode: Optional[str] = None
    feature_sources: Optional[Dict[str, str]] = None
    billing_subscription_id: Optional[str] = None
    billing_provider: Optional[str] = None

    plan: Optional[Any] = None
    features: Optional[Dict[str, str]] = None
    assignment: Optional[Any] = None
    ends_utc: Optional[str] = None
    upcoming: Optional[List[Any]] = None
    plan_names: Optional[Dict[str, str]] = None


@dataclass
class PromoCheck(Model):
    """Whether a code can be redeemed."""
    benefit_kind: Optional[str] = None
    discount_percent: Optional[float] = None
    discount_months: Optional[int] = None
    billing_plan_key: Optional[str] = None

    valid: Optional[bool] = None
    reason: Optional[str] = None
    plan_name: Optional[str] = None
    duration_days: Optional[int] = None
    message: Optional[str] = None


TERMINAL_RUN_STATUSES = frozenset({"Completed", "PartiallySucceeded", "Failed", "Cancelled", "Skipped"})
"""Run statuses after which a run never changes again."""

RUN_STAGES: Tuple[str, ...] = (
    "AssembleContext", "PlanQueries", "Search", "Filter", "Fetch", "Extract", "Embed", "Dedupe", "Score",
    "Synthesize", "ReviewPredictions", "Predict", "Suggest", "ComposeBriefing", "Notify",
)
"""The research pipeline's stages, in order."""

RESUMABLE_RUN_STAGES: Tuple[str, ...] = RUN_STAGES[RUN_STAGES.index("Synthesize"):]
"""Stages :meth:`RunsResource.retry` can resume the same run from. Earlier stages start a new run."""


# ---------------------------------------------------------------------------
# Server-sent events
# ---------------------------------------------------------------------------


@dataclass
class RunEvent:
    """One server-sent event from :meth:`RunsResource.events`.

    Attributes:
        event: Event name: ``run`` (the run changed), ``stage`` (one stage changed), ``ping`` (keep-alive,
            about every 15 seconds), ``done`` (the final run; the stream then closes), or ``timeout`` (the stream
            reached its maximum length; reconnect to continue). An event without a name is ``message``.
        id: The event's ``id`` field, or None when the event had none (``ping`` events have none).
        data: The ``data`` field parsed as JSON, or the raw text when it is not JSON (None when empty).
        raw_data: The ``data`` field as sent (several ``data`` lines are joined with a newline).
        retry: The ``retry`` field in milliseconds, when sent.
    """

    event: str = "message"
    id: Optional[str] = None
    data: Any = None
    raw_data: str = ""
    retry: Optional[int] = None

    @property
    def is_final(self) -> bool:
        """True for ``done`` and ``timeout``, after which the server closes the stream."""
        return self.event in ("done", "timeout")

    @property
    def run(self) -> Optional[Run]:
        """The :class:`Run` carried by a ``run`` or ``done`` event, or None."""
        if self.event in ("run", "done") and isinstance(self.data, Mapping) and "id" in self.data:
            return Run.from_dict(self.data)
        return None

    @property
    def stage(self) -> Optional[RunStage]:
        """The :class:`RunStage` carried by a ``stage`` event, or None."""
        if self.event == "stage" and isinstance(self.data, Mapping):
            return RunStage.from_dict(self.data)
        return None


def parse_sse(lines: Iterable[Union[str, bytes]]) -> Iterator[RunEvent]:
    """Parse server-sent event lines into :class:`RunEvent` objects.

    Follows the event-stream format: a blank line ends an event; lines starting with ``:`` are comments;
    ``field: value`` drops one space after the colon; a field with no colon has an empty value; several
    ``data`` lines join with a newline; an event without ``event`` is named ``message``; an event with no
    ``data`` lines is not dispatched; a trailing ``\r`` is removed; and an unfinished event at the end of
    the input is discarded.

    Args:
        lines: Lines without their line terminators (str or UTF-8 bytes).

    Yields:
        Each complete event.
    """
    name = ""
    event_id: Optional[str] = None
    data: List[str] = []
    retry: Optional[int] = None
    for raw in lines:
        line = raw.decode("utf-8", errors="replace") if isinstance(raw, (bytes, bytearray)) else str(raw)
        if line.endswith("\r"):
            line = line[:-1]
        if line == "":
            if data:
                text = "\n".join(data)
                try:
                    parsed: Any = json.loads(text) if text != "" else None
                except ValueError:
                    parsed = text
                yield RunEvent(event=name or "message", id=event_id, data=parsed, raw_data=text, retry=retry)
            name, event_id, data, retry = "", None, [], None
            continue
        if line.startswith(":"):
            continue
        if ":" in line:
            fname, value = line.split(":", 1)
            if value.startswith(" "):
                value = value[1:]
        else:
            fname, value = line, ""
        if fname == "event":
            name = value
        elif fname == "data":
            data.append(value)
        elif fname == "id":
            if "\0" not in value:
                event_id = value
        elif fname == "retry":
            if value.isdigit():
                retry = int(value)


# ---------------------------------------------------------------------------
# Resource groups
# ---------------------------------------------------------------------------

API = "/v1.0/api"


class _Resource:
    """Base class for resource groups. Instances are created by the client."""

    def __init__(self, client: "MyMorningNewsClient") -> None:
        """Bind the group to a client.

        Args:
            client: The owning client.
        """
        self._client = client

    def _json(self, method: str, path: str, query: Optional[Mapping[str, Any]] = None, body: Any = None, auth: bool = True) -> Any:
        """Send a JSON request and return the decoded body (None for 204)."""
        return self._client.request(method, path, query=query, body=body, auth=auth)

    def _one(self, model: Optional[Type[T]], method: str, path: str, query: Optional[Mapping[str, Any]] = None, body: Any = None, auth: bool = True) -> Any:
        """Send a request and convert the JSON object to ``model`` (or return it as-is when model is None)."""
        data = self._json(method, path, query=query, body=body, auth=auth)
        if model is None or data is None:
            return data
        return model.from_dict(data)  # type: ignore[attr-defined]

    def _many(self, model: Optional[Type[T]], method: str, path: str, query: Optional[Mapping[str, Any]] = None, body: Any = None) -> List[Any]:
        """Send a request that returns a JSON array and convert each element."""
        data = self._json(method, path, query=query, body=body) or []
        if model is None:
            return list(data)
        return [model.from_dict(x) for x in data]  # type: ignore[attr-defined]

    def _page(self, model: Optional[Type[T]], path: str, filters: Mapping[str, Any]) -> "Page[Any]":
        """GET a paged list and convert it to a :class:`Page`."""
        data = self._json("GET", path, query=filters)
        return Page.from_dict(data, model)

    def _download(self, path: str, query: Optional[Mapping[str, Any]] = None, byte_range: ByteRange = None, auth: bool = True) -> Download:
        """GET a binary route and return a :class:`Download`."""
        headers: Dict[str, str] = {}
        rh = _range_header(byte_range)
        if rh:
            headers["Range"] = rh
        resp = self._client.request("GET", path, query=query, headers=headers, raw=True, auth=auth)
        return Download.from_response(resp)


class TopicsResource(_Resource):
    """Topics: CRUD, lifecycle, dimensions, constraints, recipients, search providers, runs, and learning.

    Paths live under ``/v1.0/api/topics``.
    """

    def list(self, **filters: Any) -> Page[Topic]:
        """List topics.

        Args:
            **filters: ``status`` (TopicStatus), ``is_one_time``, ``user_id`` (admins), and the paging
                filters ``page_number``, ``page_size``, ``sort_by``, ``sort_direction``, ``search``,
                ``from_utc``, ``to_utc``.

        Returns:
            A page of :class:`Topic`.
        """
        return self._page(Topic, API + "/topics", filters)

    def get(self, topic_id: str) -> TopicDetail:
        """Read a topic with its dimensions, constraints, and recipients.

        Raises:
            NotFoundError: The topic does not exist or belongs to another user.
        """
        return self._one(TopicDetail, "GET", API + "/topics/" + _seg(topic_id))

    def create(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> Topic:
        """Create a topic without the wizard (it starts as a Draft).

        Args:
            body: A ``TopicUpsertRequest`` dict (snake_case or camelCase keys).
            **fields: Fields merged over ``body``, for example ``name``, ``description``, ``brief``,
                ``audience_level``, ``horizon``, ``status``, ``email_on_run``, ``seed_queries``.

        Returns:
            The new :class:`Topic` (HTTP 201).
        """
        return self._one(Topic, "POST", API + "/topics", body=_merge(body, fields))

    def update(self, topic_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> Topic:
        """Update a topic. Only the fields you send change."""
        return self._one(Topic, "PUT", API + "/topics/" + _seg(topic_id), body=_merge(body, fields))

    def delete(self, topic_id: str) -> None:
        """Delete a topic and everything under it (HTTP 204)."""
        self._json("DELETE", API + "/topics/" + _seg(topic_id))

    def pause(self, topic_id: str) -> Topic:
        """Pause a topic so schedules skip it."""
        return self._one(Topic, "POST", API + "/topics/" + _seg(topic_id) + "/pause")

    def resume(self, topic_id: str) -> Topic:
        """Resume a paused or archived topic (status Active).

        Raises:
            ConflictError: The topic is a Draft without a brief.
        """
        return self._one(Topic, "POST", API + "/topics/" + _seg(topic_id) + "/resume")

    def archive(self, topic_id: str) -> Topic:
        """Archive a topic."""
        return self._one(Topic, "POST", API + "/topics/" + _seg(topic_id) + "/archive")

    def duplicate(self, topic_id: str) -> TopicDetail:
        """Duplicate a topic as a new Draft, copying dimensions and constraints."""
        return self._one(TopicDetail, "POST", API + "/topics/" + _seg(topic_id) + "/duplicate")

    def refine(self, topic_id: str) -> RefinementState:
        """Start a wizard session to refine an existing topic (HTTP 201)."""
        return self._one(RefinementState, "POST", API + "/topics/" + _seg(topic_id) + "/refine")

    def run(self, topic_id: str, dry_run: bool = False) -> Run:
        """Queue a run of the topic now (HTTP 202). This spends model tokens.

        Args:
            topic_id: The topic.
            dry_run: True for a dry run that sends no email and is excluded from history.

        Returns:
            The queued :class:`Run`. Use :meth:`RunsResource.wait` to follow it.
        """
        return self._one(Run, "POST", API + "/topics/" + _seg(topic_id) + "/run", query={"dryRun": True} if dry_run else None)

    def dry_run(self, topic_id: str) -> Run:
        """Queue a dry run (no email, not counted in history). Same as ``run(topic_id, dry_run=True)``."""
        return self.run(topic_id, dry_run=True)

    def learn(self, topic_id: str) -> Dict[str, Any]:
        """Run a learning pass now. Returns ``{"proposed": int, "preferences": [...]}`` as a dict."""
        return self._json("POST", API + "/topics/" + _seg(topic_id) + "/learn")

    def ranking_recommendation(self, topic_id: str) -> Dict[str, Any]:
        """Suggested ranking weights from your ratings.

        Returns:
            A dict with ``ratedFindings``, ``enough``, ``current``, ``recommended``, ``separation``, and ``note``.
        """
        return self._json("GET", API + "/topics/" + _seg(topic_id) + "/ranking-recommendation")

    # Dimensions

    def dimensions(self, topic_id: str) -> List[TopicDimension]:
        """List a topic's dimensions."""
        return self._many(TopicDimension, "GET", API + "/topics/" + _seg(topic_id) + "/dimensions")

    def add_dimension(self, topic_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> TopicDimension:
        """Add a dimension (``name`` required; ``weight``, ``include_terms``, ``exclude_terms``, ``is_locked``, ...)."""
        return self._one(TopicDimension, "POST", API + "/topics/" + _seg(topic_id) + "/dimensions", body=_merge(body, fields))

    def update_dimension(self, topic_id: str, dimension_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> TopicDimension:
        """Update a dimension. It becomes user-owned and is kept on regeneration."""
        return self._one(TopicDimension, "PUT", API + "/topics/" + _seg(topic_id) + "/dimensions/" + _seg(dimension_id), body=_merge(body, fields))

    def delete_dimension(self, topic_id: str, dimension_id: str) -> None:
        """Delete a dimension."""
        self._json("DELETE", API + "/topics/" + _seg(topic_id) + "/dimensions/" + _seg(dimension_id))

    # Constraints

    def constraints(self, topic_id: str, kind: Optional[Union[str, ConstraintKind]] = None) -> List[TopicConstraint]:
        """List a topic's constraints, optionally of one ``kind``."""
        return self._many(TopicConstraint, "GET", API + "/topics/" + _seg(topic_id) + "/constraints", query={"kind": kind})

    def add_constraint(self, topic_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> TopicConstraint:
        """Add a constraint (``kind`` and ``value`` required)."""
        return self._one(TopicConstraint, "POST", API + "/topics/" + _seg(topic_id) + "/constraints", body=_merge(body, fields))

    def update_constraint(self, topic_id: str, constraint_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> TopicConstraint:
        """Update a constraint."""
        return self._one(TopicConstraint, "PUT", API + "/topics/" + _seg(topic_id) + "/constraints/" + _seg(constraint_id), body=_merge(body, fields))

    def delete_constraint(self, topic_id: str, constraint_id: str) -> None:
        """Delete a constraint."""
        self._json("DELETE", API + "/topics/" + _seg(topic_id) + "/constraints/" + _seg(constraint_id))

    # Recipients

    def recipients(self, topic_id: str) -> List[TopicRecipient]:
        """List a topic's email recipients."""
        return self._many(TopicRecipient, "GET", API + "/topics/" + _seg(topic_id) + "/recipients")

    def add_recipient(self, topic_id: str, email: str, active: bool = True) -> TopicRecipient:
        """Add an email recipient."""
        return self._one(TopicRecipient, "POST", API + "/topics/" + _seg(topic_id) + "/recipients", body={"email": email, "active": active})

    def delete_recipient(self, topic_id: str, recipient_id: str) -> None:
        """Remove an email recipient."""
        self._json("DELETE", API + "/topics/" + _seg(topic_id) + "/recipients/" + _seg(recipient_id))

    # Search providers

    def search_providers(self, topic_id: str) -> List[SearchProvider]:
        """Search providers the topic uses (the tenant defaults when none are chosen)."""
        return self._many(SearchProvider, "GET", API + "/topics/" + _seg(topic_id) + "/search-providers")

    def set_search_providers(self, topic_id: str, providers: Sequence[Union[str, Mapping[str, Any]]]) -> List[SearchProvider]:
        """Choose a topic's search providers. An empty list restores the tenant defaults.

        Args:
            topic_id: The topic.
            providers: Provider IDs, or dicts with ``search_provider_id``, ``enabled``, and ``max_results``.

        Returns:
            The providers the topic now uses.
        """
        items = [{"searchProviderId": p} if isinstance(p, str) else p for p in providers]
        return self._many(SearchProvider, "PUT", API + "/topics/" + _seg(topic_id) + "/search-providers", body=items)


class RefinementResource(_Resource):
    """The topic wizard: refinement sessions under ``/v1.0/api/refinement-sessions``.

    ``start``, ``get``, ``list``, ``answer``, ``accept_sources``, ``complete``, and ``abandon`` do not
    call a model. The ``generate_*`` and ``suggest_sources`` verbs do (they spend tokens).
    """

    def start(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> RefinementState:
        """Start the wizard for a new topic (``seed_text`` required; ``goal``, ``depth``, ``horizon``, ``known``, ``model_endpoint_id``)."""
        return self._one(RefinementState, "POST", API + "/refinement-sessions", body=_merge(body, fields))

    def create(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> RefinementState:
        """Alias of :meth:`start`."""
        return self.start(body, **fields)

    def list(self, **filters: Any) -> Page[RefinementSession]:
        """List my refinement sessions (filters: ``status``, ``topic_id``, plus paging)."""
        return self._page(RefinementSession, API + "/refinement-sessions", filters)

    def get(self, session_id: str) -> RefinementState:
        """Read the full session state (session, topic, dimensions, constraints, questions, messages)."""
        return self._one(RefinementState, "GET", API + "/refinement-sessions/" + _seg(session_id))

    def generate_dimensions(self, session_id: str, guidance: Optional[str] = None) -> RefinementState:
        """Propose dimensions (keeps locked and user-owned ones). Calls a model."""
        return self._one(RefinementState, "POST", API + "/refinement-sessions/" + _seg(session_id) + "/dimensions", body={"guidance": guidance})

    def generate_questions(self, session_id: str, guidance: Optional[str] = None) -> RefinementState:
        """Ask the next round of clarifying questions. Calls a model."""
        return self._one(RefinementState, "POST", API + "/refinement-sessions/" + _seg(session_id) + "/questions", body={"guidance": guidance})

    def answer(self, session_id: str, question_id: str, answer_text: Optional[str] = None, selected_option_ids: Optional[Sequence[str]] = None, skipped: bool = False) -> RefinementQuestionDetail:
        """Answer or skip a question."""
        body = {"answerText": answer_text, "selectedOptionIds": list(selected_option_ids or []), "skipped": skipped}
        return self._one(RefinementQuestionDetail, "PUT", API + "/refinement-sessions/" + _seg(session_id) + "/questions/" + _seg(question_id), body=body)

    def generate_context(self, session_id: str) -> RefinementState:
        """Draft the brief and constraints. Calls a model."""
        return self._one(RefinementState, "POST", API + "/refinement-sessions/" + _seg(session_id) + "/context")

    def suggest_sources(self, session_id: str) -> List[Dict[str, Any]]:
        """Suggest feeds and seed pages, each checked with a live fetch. Calls a model. Returns ``SourceCheck`` dicts."""
        return self._json("POST", API + "/refinement-sessions/" + _seg(session_id) + "/sources") or []

    def accept_sources(self, session_id: str, feed_urls: Optional[Sequence[str]] = None, seed_urls: Optional[Sequence[str]] = None) -> RefinementState:
        """Add accepted feeds and seed pages to the topic as constraints."""
        return self._one(RefinementState, "POST", API + "/refinement-sessions/" + _seg(session_id) + "/sources/accept",
                         body={"feedUrls": list(feed_urls or []), "seedUrls": list(seed_urls or [])})

    def complete(self, session_id: str) -> RefinementState:
        """Apply the topic (a Draft becomes Active)."""
        return self._one(RefinementState, "POST", API + "/refinement-sessions/" + _seg(session_id) + "/complete")

    def abandon(self, session_id: str) -> None:
        """Abandon the session. A draft topic the session created is deleted (HTTP 204)."""
        self._json("POST", API + "/refinement-sessions/" + _seg(session_id) + "/abandon")


class ResearchRequestsResource(_Resource):
    """One-time research requests under ``/v1.0/api/requests``.

    ``create`` and ``replan`` call a model to plan the request.
    """

    def create(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> ResearchRequestDetail:
        """Create a one-time request (``request_text`` required; ``deliverables``, ``length``, ``run_at_utc``,
        ``email_recipients``, ``publish_to_feed``, ``skip_questions``, ``model_endpoint_id``). Calls a model."""
        return self._one(ResearchRequestDetail, "POST", API + "/requests", body=_merge(body, fields))

    def list(self, **filters: Any) -> Page[ResearchRequest]:
        """List my one-time requests (filters: ``status``, plus paging)."""
        return self._page(ResearchRequest, API + "/requests", filters)

    def get(self, request_id: str) -> ResearchRequestDetail:
        """Read a request with its plan and questions."""
        return self._one(ResearchRequestDetail, "GET", API + "/requests/" + _seg(request_id))

    def delete(self, request_id: str) -> None:
        """Delete a request (HTTP 204)."""
        self._json("DELETE", API + "/requests/" + _seg(request_id))

    def answer(self, request_id: str, question_id: str, answer_text: Optional[str] = None, selected_option_ids: Optional[Sequence[str]] = None, skipped: bool = False) -> RefinementQuestionDetail:
        """Answer or skip a clarifying question."""
        body = {"answerText": answer_text, "selectedOptionIds": list(selected_option_ids or []), "skipped": skipped}
        return self._one(RefinementQuestionDetail, "PUT", API + "/requests/" + _seg(request_id) + "/questions/" + _seg(question_id), body=body)

    def replan(self, request_id: str) -> ResearchRequestDetail:
        """Plan again with the answers given. Calls a model."""
        return self._one(ResearchRequestDetail, "POST", API + "/requests/" + _seg(request_id) + "/replan")

    def confirm(self, request_id: str, run_at_utc: Optional[Union[str, datetime]] = None) -> ResearchRequestDetail:
        """Confirm the request: run once now, or at ``run_at_utc``. The run spends tokens."""
        return self._one(ResearchRequestDetail, "POST", API + "/requests/" + _seg(request_id) + "/confirm", body={"runAtUtc": run_at_utc})

    def cancel(self, request_id: str) -> ResearchRequestDetail:
        """Cancel a request that has not started."""
        return self._one(ResearchRequestDetail, "POST", API + "/requests/" + _seg(request_id) + "/cancel")

    def convert(self, request_id: str) -> Topic:
        """Make the request a recurring topic."""
        return self._one(Topic, "POST", API + "/requests/" + _seg(request_id) + "/convert")


class SchedulesResource(_Resource):
    """Schedules for topics and editions under ``/v1.0/api/schedules``."""

    def list(self, **filters: Any) -> Page[Schedule]:
        """List schedules (filters: ``owner_type``, ``owner_id``, plus paging)."""
        return self._page(Schedule, API + "/schedules", filters)

    def get(self, schedule_id: str) -> Schedule:
        """Read a schedule."""
        return self._one(Schedule, "GET", API + "/schedules/" + _seg(schedule_id))

    def create(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> Schedule:
        """Create a schedule (``owner_type``, ``owner_id``, ``interval_type`` required; see ``ScheduleUpsertRequest``)."""
        return self._one(Schedule, "POST", API + "/schedules", body=_merge(body, fields))

    def update(self, schedule_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> Schedule:
        """Update a schedule."""
        return self._one(Schedule, "PUT", API + "/schedules/" + _seg(schedule_id), body=_merge(body, fields))

    def delete(self, schedule_id: str) -> None:
        """Delete a schedule (HTTP 204)."""
        self._json("DELETE", API + "/schedules/" + _seg(schedule_id))

    def preview(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> SchedulePreview:
        """Validate a schedule and preview its next runs without saving (``preview_count`` sets how many)."""
        return self._one(SchedulePreview, "POST", API + "/schedules/preview", body=_merge(body, fields))


class RunsResource(_Resource):
    """Research runs under ``/v1.0/api/runs``.

    :meth:`events` streams progress as server-sent events; :meth:`wait` polls ``GET /runs/{id}`` instead.
    """

    def list(self, **filters: Any) -> Page[Run]:
        """List runs (filters: ``topic_id``, ``status``, ``trigger``, plus paging).

        ``sort_by="estimatedCostUsd"`` (runs without a priced cost sort last) or ``sort_by="durationMs"`` ranks
        runs by cost or duration.
        """
        return self._page(Run, API + "/runs", filters)

    def get(self, run_id: str) -> Run:
        """Read a run."""
        return self._one(Run, "GET", API + "/runs/" + _seg(run_id))

    def costs(self, run_id: str) -> RunCostReport:
        """What a run cost: totals, each stage, each model, and each task.

        Computed on request, so it is current while the run is going. Amounts are US dollar estimates from the
        endpoints' prices; unpriced calls are counted in ``unpriced_call_count`` and left out of the amounts.

        Raises:
            NotFoundError: The run does not exist or belongs to another user.
        """
        return self._one(RunCostReport, "GET", API + "/runs/" + _seg(run_id) + "/costs")

    def stages(self, run_id: str) -> List[RunStage]:
        """A run's pipeline stages in order."""
        return self._many(RunStage, "GET", API + "/runs/" + _seg(run_id) + "/stages")

    def queries(self, run_id: str) -> List[SearchQuery]:
        """A run's search queries."""
        return self._many(SearchQuery, "GET", API + "/runs/" + _seg(run_id) + "/queries")

    def results(self, run_id: str, disposition: Optional[Union[str, SearchResultDisposition]] = None) -> List[SearchResult]:
        """A run's search results with their dispositions, optionally filtered by ``disposition``."""
        return self._many(SearchResult, "GET", API + "/runs/" + _seg(run_id) + "/results", query={"disposition": disposition})

    def llm_calls(self, run_id: str, **filters: Any) -> Page[LlmCall]:
        """A run's model calls, oldest first (paging filters)."""
        return self._page(LlmCall, API + "/runs/" + _seg(run_id) + "/llm-calls", filters)

    def tool_calls(self, run_id: str) -> List[RunToolCall]:
        """A run's agentic tool calls (web_search, fetch_url, and MCP tools), in call order.

        Raises:
            NotFoundError: The run does not exist or belongs to another user.
        """
        return self._many(RunToolCall, "GET", API + "/runs/" + _seg(run_id) + "/tool-calls")

    def email_preview(self, run_id: str) -> Dict[str, Any]:
        """Render the run digest email without sending it. May call a model for the lead paragraph.

        Returns:
            A dict with the rendered subject, HTML, and text.
        """
        return self._json("GET", API + "/runs/" + _seg(run_id) + "/email-preview")

    def cancel(self, run_id: str) -> Run:
        """Cancel a queued or running run."""
        return self._one(Run, "POST", API + "/runs/" + _seg(run_id) + "/cancel")

    def retry(self, run_id: str, from_stage: Optional[str] = None) -> Run:
        """Retry a finished run (HTTP 202). This spends model tokens.

        Args:
            run_id: A finished run.
            from_stage: ``Synthesize`` or a later stage (see :data:`RESUMABLE_RUN_STAGES`) reruns those stages on
                the same run with its saved findings, and the returned run has ``resume_from_stage`` set. No stage,
                or an earlier one, queues a new run of the topic.

        Returns:
            The queued :class:`Run` (the same run, or a new one).

        Raises:
            ConflictError: The run is not finished, or its topic no longer exists.
            BadRequestError: ``from_stage`` is not a pipeline stage.
            NotFoundError: The run does not exist or is not visible.
        """
        return self._one(Run, "POST", API + "/runs/" + _seg(run_id) + "/retry", query={"fromStage": from_stage})

    def events(self, run_id: str, timeout: Optional[float] = None) -> Iterator[RunEvent]:
        """Stream a run's progress as server-sent events.

        The connection opens when this method is called, so a missing run raises :class:`NotFoundError` here,
        not on the first ``next()``. The first events are every stage and the current run; afterwards ``stage``
        and ``run`` events arrive on changes, ``ping`` keeps the connection alive, and the stream ends after a
        ``done`` (finished) or ``timeout`` (server limit; call again to reconnect) event. Signed requests are
        signed like any other request.

        Args:
            run_id: The run.
            timeout: Optional maximum seconds to stream; the generator then stops quietly. None (default)
                streams until the server ends the stream.

        Returns:
            A generator of :class:`RunEvent`. Close it (or leave the loop) to drop the connection.
        """
        read_timeout = max(self._client.timeout, 45.0)
        resp = self._client.request("GET", API + "/runs/" + _seg(run_id) + "/events", headers={"Accept": "text/event-stream"},
                                    raw=True, stream=True, timeout=(self._client.timeout, read_timeout))
        deadline = None if timeout is None else time.monotonic() + max(0.0, float(timeout))
        return self._event_stream(resp, deadline)

    @staticmethod
    def _event_stream(resp: requests.Response, deadline: Optional[float]) -> Iterator[RunEvent]:
        """Yield parsed events from an open streaming response, closing it when done."""
        try:
            for ev in parse_sse(resp.iter_lines(delimiter=b"\n")):
                yield ev
                if ev.is_final:
                    return
                if deadline is not None and time.monotonic() >= deadline:
                    return
        except requests.RequestException as ex:
            raise MyMorningNewsError("The event stream failed: " + str(ex), status=0, code="ConnectionError") from ex
        finally:
            resp.close()

    def wait(self, run_id: str, timeout: float = 900.0, interval: float = 3.0, on_poll: Optional[Callable[[Run], None]] = None) -> Run:
        """Poll a run until it reaches a terminal status.

        Args:
            run_id: The run.
            timeout: Maximum seconds to wait (default 900).
            interval: Seconds between polls (default 3, minimum 0.2).
            on_poll: Optional callback invoked with each polled :class:`Run`.

        Returns:
            The finished :class:`Run` (status in :data:`TERMINAL_RUN_STATUSES`).

        Raises:
            WaitTimeoutError: The run did not finish in time (``last`` holds the last poll).
        """
        interval = max(0.2, float(interval))
        deadline = time.monotonic() + max(0.0, float(timeout))
        while True:
            run = self.get(run_id)
            if on_poll is not None:
                on_poll(run)
            if run.status in TERMINAL_RUN_STATUSES:
                return run
            if time.monotonic() >= deadline:
                raise WaitTimeoutError("Run " + run_id + " did not finish within " + str(timeout) + " seconds.", last=run)
            time.sleep(interval)


class FindingsResource(_Resource):
    """Findings under ``/v1.0/api/findings``."""

    def list(self, **filters: Any) -> Page[Finding]:
        """List findings.

        Hidden findings, duplicates, dry runs, and below-threshold findings are left out by default.

        Args:
            **filters: ``topic_id``, ``run_id``, ``tier``, ``category``, ``include_duplicates``, ``include_dry_run``,
                ``min_score``, ``dimension_id``, ``include_below_threshold``, ``include_hidden``, ``pinned``, plus paging.
        """
        return self._page(Finding, API + "/findings", filters)

    def get(self, finding_id: str) -> FindingDetail:
        """Read a finding with its evidence and dimension."""
        return self._one(FindingDetail, "GET", API + "/findings/" + _seg(finding_id))

    def update(self, finding_id: str, pinned: Optional[bool] = None, hidden: Optional[bool] = None) -> Finding:
        """Pin or hide a finding. Only the flags you pass change.

        Hidden findings are left out of lists (unless ``include_hidden=True``), briefings, and emails.

        Args:
            finding_id: The finding.
            pinned: True to keep it at the top of lists, False to unpin, None to leave as is.
            hidden: True to hide it, False to show it again, None to leave as is.

        Returns:
            The updated :class:`Finding`.
        """
        return self._one(Finding, "PUT", API + "/findings/" + _seg(finding_id), body={"pinned": pinned, "hidden": hidden})

    def pin(self, finding_id: str, pinned: bool = True) -> Finding:
        """Pin (or unpin) a finding."""
        return self.update(finding_id, pinned=pinned)

    def unpin(self, finding_id: str) -> Finding:
        """Unpin a finding."""
        return self.update(finding_id, pinned=False)

    def hide(self, finding_id: str, hidden: bool = True) -> Finding:
        """Hide (or show) a finding."""
        return self.update(finding_id, hidden=hidden)

    def unhide(self, finding_id: str) -> Finding:
        """Show a hidden finding again."""
        return self.update(finding_id, hidden=False)

    def delete(self, finding_id: str) -> None:
        """Delete a finding and its evidence links (HTTP 204). Shared evidence and sources stay; ratings stay in history."""
        self._json("DELETE", API + "/findings/" + _seg(finding_id))

    def evidence(self, evidence_id: str) -> EvidenceView:
        """Read one piece of evidence with its source. Same as :meth:`EvidenceResource.get`."""
        return self._one(EvidenceView, "GET", API + "/evidence/" + _seg(evidence_id))


class EvidenceResource(_Resource):
    """Evidence under ``/v1.0/api/evidence`` (visible to its owner only)."""

    def get(self, evidence_id: str) -> EvidenceView:
        """Read one piece of evidence with its source URL, title, domain, and role.

        Raises:
            NotFoundError: The evidence does not exist or belongs to another user.
        """
        return self._one(EvidenceView, "GET", API + "/evidence/" + _seg(evidence_id))


class SourcesResource(_Resource):
    """Fetched sources under ``/v1.0/api/sources``."""

    def list(self, **filters: Any) -> Page[SourceItem]:
        """List fetched sources for my topics (filters: ``topic_id``, ``run_id``, plus paging)."""
        return self._page(SourceItem, API + "/sources", filters)

    def get(self, source_id: str) -> SourceItem:
        """Read a fetched source."""
        return self._one(SourceItem, "GET", API + "/sources/" + _seg(source_id))


class PredictionsResource(_Resource):
    """Predictions and their reviews."""

    def list(self, **filters: Any) -> Page[Prediction]:
        """List predictions (filters: ``topic_id``, ``status``, ``include_dry_run``, plus paging)."""
        return self._page(Prediction, API + "/predictions", filters)

    def get(self, prediction_id: str) -> PredictionDetail:
        """Read a prediction with its reviews."""
        return self._one(PredictionDetail, "GET", API + "/predictions/" + _seg(prediction_id))

    def resolve(self, prediction_id: str, status: Union[str, PredictionStatus], note: Optional[str] = None) -> Prediction:
        """Resolve a prediction (Accurate, PartiallyAccurate, Inaccurate, Unresolvable, or Withdrawn)."""
        return self._one(Prediction, "POST", API + "/predictions/" + _seg(prediction_id) + "/resolve", body={"status": status, "note": note})

    def accept_review(self, review_id: str) -> Prediction:
        """Accept a proposed resolution."""
        return self._one(Prediction, "POST", API + "/prediction-reviews/" + _seg(review_id) + "/accept")

    def reject_review(self, review_id: str) -> Prediction:
        """Reject a proposed resolution."""
        return self._one(Prediction, "POST", API + "/prediction-reviews/" + _seg(review_id) + "/reject")


class SuggestionsResource(_Resource):
    """Suggestions under ``/v1.0/api/suggestions``."""

    def list(self, **filters: Any) -> Page[Suggestion]:
        """List suggestions (filters: ``topic_id``, ``status``, ``priority``, ``include_dry_run``, plus paging)."""
        return self._page(Suggestion, API + "/suggestions", filters)

    def get(self, suggestion_id: str) -> Suggestion:
        """Read a suggestion."""
        return self._one(Suggestion, "GET", API + "/suggestions/" + _seg(suggestion_id))

    def set_status(self, suggestion_id: str, status: Union[str, SuggestionStatus], snoozed_until_utc: Optional[Union[str, datetime]] = None) -> Suggestion:
        """Accept, snooze (with a future ``snoozed_until_utc``), dismiss, or complete a suggestion."""
        return self._one(Suggestion, "PUT", API + "/suggestions/" + _seg(suggestion_id) + "/status", body={"status": status, "snoozedUntilUtc": snoozed_until_utc})


class FeedbackResource(_Resource):
    """Feedback on findings, predictions, suggestions, briefings, sources, domains, and preferences."""

    def create(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> Feedback:
        """Give feedback (``target_type``, ``target_id``, and a rating, an ``action``, or a ``reason``).

        Returns:
            The saved :class:`Feedback` (HTTP 201). Earlier feedback on the same target is superseded.
        """
        return self._one(Feedback, "POST", API + "/feedback", body=_merge(body, fields))

    def submit(self, target_type: Union[str, FeedbackTargetType], target_id: str, relevance: Optional[Union[str, RelevanceRating]] = None,
               accuracy: Optional[Union[str, AccuracyRating]] = None, usefulness: Optional[Union[str, UsefulnessRating]] = None,
               reason: Optional[str] = None, action: Optional[str] = None, channel: Union[str, FeedbackChannel] = "Api") -> Feedback:
        """Convenience form of :meth:`create` with explicit arguments."""
        return self.create(target_type=target_type, target_id=target_id, relevance=relevance, accuracy=accuracy,
                           usefulness=usefulness, reason=reason, action=action, channel=channel)

    def list(self, **filters: Any) -> Page[Feedback]:
        """List feedback (filters: ``target_type``, ``target_id``, ``topic_id``, ``channel``, ``include_history``, plus paging)."""
        return self._page(Feedback, API + "/feedback", filters)

    def update(self, feedback_id: str, reason: Optional[str] = None, body: Optional[Mapping[str, Any]] = None) -> Feedback:
        """Edit the reason on my feedback."""
        return self._one(Feedback, "PUT", API + "/feedback/" + _seg(feedback_id), body=_merge(body, {"reason": reason}))

    def delete(self, feedback_id: str) -> None:
        """Retract my feedback (the earlier rating, if any, becomes current)."""
        self._json("DELETE", API + "/feedback/" + _seg(feedback_id))


class PreferencesResource(_Resource):
    """Learned preferences under ``/v1.0/api/learned-preferences``."""

    def list(self, **filters: Any) -> Page[LearnedPreference]:
        """List learned preferences (filters: ``topic_id``, ``status``, ``origin``, plus paging)."""
        return self._page(LearnedPreference, API + "/learned-preferences", filters)

    def get(self, preference_id: str) -> PreferenceDetail:
        """Read a preference with its supporting feedback."""
        return self._one(PreferenceDetail, "GET", API + "/learned-preferences/" + _seg(preference_id))

    def create(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> LearnedPreference:
        """Write a preference, active at once (``statement`` required, 1 to 500 characters; ``kind``, ``topic_id``)."""
        return self._one(LearnedPreference, "POST", API + "/learned-preferences", body=_merge(body, fields))

    def update(self, preference_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> LearnedPreference:
        """Edit a preference."""
        return self._one(LearnedPreference, "PUT", API + "/learned-preferences/" + _seg(preference_id), body=_merge(body, fields))

    def delete(self, preference_id: str) -> None:
        """Delete a preference."""
        self._json("DELETE", API + "/learned-preferences/" + _seg(preference_id))

    def accept(self, preference_id: str) -> LearnedPreference:
        """Accept a proposed preference. Raises :class:`ConflictError` unless it is Proposed."""
        return self._one(LearnedPreference, "POST", API + "/learned-preferences/" + _seg(preference_id) + "/accept")

    def reject(self, preference_id: str) -> LearnedPreference:
        """Reject a proposed preference. Raises :class:`ConflictError` unless it is Proposed."""
        return self._one(LearnedPreference, "POST", API + "/learned-preferences/" + _seg(preference_id) + "/reject")

    def enable(self, preference_id: str) -> LearnedPreference:
        """Enable a preference."""
        return self._one(LearnedPreference, "POST", API + "/learned-preferences/" + _seg(preference_id) + "/enable")

    def disable(self, preference_id: str) -> LearnedPreference:
        """Disable a preference."""
        return self._one(LearnedPreference, "POST", API + "/learned-preferences/" + _seg(preference_id) + "/disable")


class EditionsResource(_Resource):
    """Editions (multi-topic briefings) under ``/v1.0/api/editions``."""

    def list(self, **filters: Any) -> Page[Edition]:
        """List my editions (paging filters)."""
        return self._page(Edition, API + "/editions", filters)

    def get(self, edition_id: str) -> EditionDetail:
        """Read an edition with its topics and schedules."""
        return self._one(EditionDetail, "GET", API + "/editions/" + _seg(edition_id))

    def create(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> EditionDetail:
        """Create an edition (``name`` required; ``briefing_template_id``, ``audio_enabled``, ``email_enabled``, ...)."""
        return self._one(EditionDetail, "POST", API + "/editions", body=_merge(body, fields))

    def update(self, edition_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> EditionDetail:
        """Update an edition."""
        return self._one(EditionDetail, "PUT", API + "/editions/" + _seg(edition_id), body=_merge(body, fields))

    def delete(self, edition_id: str) -> None:
        """Delete an edition (its briefings stay)."""
        self._json("DELETE", API + "/editions/" + _seg(edition_id))

    def set_topics(self, edition_id: str, topic_ids: Sequence[str]) -> EditionDetail:
        """Set an edition's topics in order."""
        return self._one(EditionDetail, "PUT", API + "/editions/" + _seg(edition_id) + "/topics", body={"topicIds": list(topic_ids)})

    def compose(self, edition_id: str) -> Briefing:
        """Compose the edition now (HTTP 202). Calls a model.

        Raises:
            ConflictError: A briefing for this edition is already on its way (``details`` has its ID).
        """
        return self._one(Briefing, "POST", API + "/editions/" + _seg(edition_id) + "/compose")

    def preview(self, edition_id: str, template_content: Optional[str] = None) -> Briefing:
        """Preview the edition from the latest data without saving. Calls a model.

        Args:
            edition_id: The edition.
            template_content: Optional unsaved template Markdown to preview with.
        """
        return self._one(Briefing, "POST", API + "/editions/" + _seg(edition_id) + "/preview", body={"templateContent": template_content})


class BriefingsResource(_Resource):
    """Briefings under ``/v1.0/api/briefings``: detail, Markdown and HTML downloads, pinning, and audio."""

    def list(self, **filters: Any) -> Page[Briefing]:
        """List briefings, newest first (filters: ``kind``, ``edition_id``, ``topic_id``, ``run_id``, ``status``,
        ``pinned``, ``include_dry_run``, plus paging). List items omit ``markdown``."""
        return self._page(Briefing, API + "/briefings", filters)

    def get(self, briefing_id: str) -> BriefingDetail:
        """Read a briefing with its sections, items, files, and latest audio."""
        return self._one(BriefingDetail, "GET", API + "/briefings/" + _seg(briefing_id))

    def delete(self, briefing_id: str) -> None:
        """Delete a briefing and its files."""
        self._json("DELETE", API + "/briefings/" + _seg(briefing_id))

    def pin(self, briefing_id: str, pinned: bool = True) -> Briefing:
        """Pin (or unpin) a briefing so its files outlive the retention window."""
        return self._one(Briefing, "PUT", API + "/briefings/" + _seg(briefing_id) + "/pin", body={"pinned": pinned})

    def unpin(self, briefing_id: str) -> Briefing:
        """Unpin a briefing."""
        return self.pin(briefing_id, False)

    def get_progress(self, briefing_id: str) -> Dict[str, Any]:
        """Where the caller stopped listening: ``positionSeconds``, ``durationSeconds``, ``completed``."""
        return self._json("GET", API + "/briefings/" + _seg(briefing_id) + "/progress")

    def save_progress(self, briefing_id: str, position_seconds: float, completed: bool = False, duration_seconds: Optional[float] = None) -> Dict[str, Any]:
        """Save where the caller stopped listening."""
        body: Dict[str, Any] = {"positionSeconds": position_seconds, "completed": completed}
        if duration_seconds is not None:
            body["durationSeconds"] = duration_seconds
        return self._json("PUT", API + "/briefings/" + _seg(briefing_id) + "/progress", body=body)

    def regenerate(self, briefing_id: str) -> Briefing:
        """Store fresh Markdown and HTML files from the saved briefing (after ``GoneError``)."""
        return self._one(Briefing, "POST", API + "/briefings/" + _seg(briefing_id) + "/regenerate")

    def generate_audio(self, briefing_id: str, voice_profile_id: Optional[str] = None) -> MediaAsset:
        """Make (or remake) audio for a ready briefing (HTTP 202). May call a model for the script.

        Raises:
            ConflictError: Audio is disabled (``AudioDisabled``), the briefing is not ready, or audio is in progress.
        """
        return self._one(MediaAsset, "POST", API + "/briefings/" + _seg(briefing_id) + "/audio", body={"voiceProfileId": voice_profile_id})

    def list_shares(self, briefing_id: str, **filters: Any) -> Page[BriefingShare]:
        """List the public share links of a briefing, newest first (hashes redacted; links are shown only at creation)."""
        return self._page(BriefingShare, API + "/briefings/" + _seg(briefing_id) + "/shares", filters)

    def share(self, briefing_id: str, expires_in_days: Optional[int] = None, show_owner_name: bool = False) -> BriefingShareCreated:
        """Share a ready briefing through public links that anyone can open with no sign-in.

        Returns one public link (``url``) for listening and reading, once; only a hash is stored.
        Every share expires: ``expires_in_days`` is 1 to 14 (the server maximum), and 7 when omitted.

        Raises:
            BadRequestError: ``expires_in_days`` is outside 1 to 14 (``ValidationFailed``).
            ConflictError: Sharing is turned off (``SharingDisabled``) or the briefing is not ready.
        """
        return self._one(BriefingShareCreated, "POST", API + "/briefings/" + _seg(briefing_id) + "/shares",
                         body={"expiresInDays": expires_in_days, "showOwnerName": show_owner_name})

    def revoke_share(self, briefing_id: str, share_id: str, purge: bool = False) -> None:
        """Revoke a share so both links stop working; with ``purge``, delete one that is already revoked or expired."""
        self._json("DELETE", API + "/briefings/" + _seg(briefing_id) + "/shares/" + _seg(share_id), query={"purge": True} if purge else None)

    def download(self, briefing_id: str, format: str = "md", byte_range: ByteRange = None) -> Download:
        """Download a briefing file.

        Args:
            briefing_id: The briefing.
            format: ``md`` (default) or ``html``.
            byte_range: Optional byte range.

        Raises:
            GoneError: The file passed retention (``ArtifactExpired``); call :meth:`regenerate`.
            ConflictError: The briefing is not ready yet.
        """
        return self._download(API + "/briefings/" + _seg(briefing_id) + "/download", query={"format": format}, byte_range=byte_range)

    def markdown(self, briefing_id: str) -> str:
        """Return the briefing Markdown as text."""
        return self.download(briefing_id, "md").text

    def html(self, briefing_id: str) -> str:
        """Return the briefing HTML as text."""
        return self.download(briefing_id, "html").text

    def download_audio(self, briefing_id: str, byte_range: ByteRange = None) -> Download:
        """Download the briefing's latest audio episode.

        Raises:
            NotFoundError: The briefing has no finished audio.
        """
        detail = self.get(briefing_id)
        audio = detail.audio
        if audio is None or not audio.artifact_id:
            raise NotFoundError("The briefing has no finished audio.", status=404, code="NotFound")
        return self._client.artifacts.content(audio.artifact_id, byte_range=byte_range)


class TemplatesResource(_Resource):
    """Briefing templates under ``/v1.0/api/briefing-templates``."""

    def list(self, **filters: Any) -> Page[BriefingTemplate]:
        """List templates I can use (filters: ``scope`` = all, shared, or mine; ``search``; paging)."""
        return self._page(BriefingTemplate, API + "/briefing-templates", filters)

    def get(self, template_id: str) -> TemplateDetail:
        """Read a template with its current content, problems, and sections."""
        return self._one(TemplateDetail, "GET", API + "/briefing-templates/" + _seg(template_id))

    def create(self, content: Optional[str] = None, name: Optional[str] = None, shared: Optional[bool] = None, body: Optional[Mapping[str, Any]] = None) -> TemplateDetail:
        """Create (import) a template from Markdown. Shared templates need a tenant administrator."""
        return self._one(TemplateDetail, "POST", API + "/briefing-templates", body=_merge(body, {"content": content, "name": name, "shared": shared}))

    def update(self, template_id: str, content: Optional[str] = None, change_note: Optional[str] = None, body: Optional[Mapping[str, Any]] = None) -> TemplateDetail:
        """Save a new template version."""
        return self._one(TemplateDetail, "PUT", API + "/briefing-templates/" + _seg(template_id), body=_merge(body, {"content": content, "changeNote": change_note}))

    def delete(self, template_id: str) -> None:
        """Delete a template."""
        self._json("DELETE", API + "/briefing-templates/" + _seg(template_id))

    def versions(self, template_id: str) -> List[BriefingTemplateVersion]:
        """List template versions."""
        return self._many(BriefingTemplateVersion, "GET", API + "/briefing-templates/" + _seg(template_id) + "/versions")

    def validate(self, content: str) -> TemplateValidation:
        """Validate template Markdown without saving."""
        return self._one(TemplateValidation, "POST", API + "/briefing-templates/validate", body={"content": content})

    def export(self, template_id: str) -> Download:
        """Export the current version as a Markdown file."""
        return self._download(API + "/briefing-templates/" + _seg(template_id) + "/export")

    def rollback(self, template_id: str, version: int) -> TemplateDetail:
        """Restore an earlier version (saved as a new version)."""
        return self._one(TemplateDetail, "POST", API + "/briefing-templates/" + _seg(template_id) + "/rollback", body={"version": version})

    def reset(self, template_id: str) -> TemplateDetail:
        """Reset a seeded template to the shipped default."""
        return self._one(TemplateDetail, "POST", API + "/briefing-templates/" + _seg(template_id) + "/reset")


class PromptsResource(_Resource):
    """Prompt templates under ``/v1.0/api/prompts``: versions, topic overrides, and the prompt lab."""

    def list(self, **filters: Any) -> Page[PromptTemplate]:
        """List prompt templates (filters: ``key``, ``topic_id``, ``is_customized``, ``tenant_only``, ``role``, paging)."""
        return self._page(PromptTemplate, API + "/prompts", filters)

    def get(self, prompt_id: str) -> PromptTemplateDetail:
        """Read a prompt template with its content, variables, and default content."""
        return self._one(PromptTemplateDetail, "GET", API + "/prompts/" + _seg(prompt_id))

    def update(self, prompt_id: str, content: Optional[str] = None, change_note: Optional[str] = None, body: Optional[Mapping[str, Any]] = None) -> PromptTemplateDetail:
        """Save a new prompt version.

        Raises:
            BadRequestError: The prompt is not valid (``details`` lists the problems).
        """
        return self._one(PromptTemplateDetail, "PUT", API + "/prompts/" + _seg(prompt_id), body=_merge(body, {"content": content, "changeNote": change_note}))

    def delete(self, prompt_id: str) -> None:
        """Delete a topic override. Tenant prompts cannot be deleted (:class:`ConflictError`); reset them."""
        self._json("DELETE", API + "/prompts/" + _seg(prompt_id))

    def versions(self, prompt_id: str) -> List[PromptTemplateVersion]:
        """Version history, newest first."""
        return self._many(PromptTemplateVersion, "GET", API + "/prompts/" + _seg(prompt_id) + "/versions")

    def rollback(self, prompt_id: str, version: int) -> PromptTemplateDetail:
        """Restore an earlier version."""
        return self._one(PromptTemplateDetail, "POST", API + "/prompts/" + _seg(prompt_id) + "/rollback", query={"version": version})

    def reset(self, prompt_id: str) -> PromptTemplateDetail:
        """Restore the shipped default."""
        return self._one(PromptTemplateDetail, "POST", API + "/prompts/" + _seg(prompt_id) + "/reset")

    def create_override(self, key: str, topic_id: str, content: Optional[str] = None) -> PromptTemplateDetail:
        """Create a topic override of a prompt (HTTP 201)."""
        return self._one(PromptTemplateDetail, "POST", API + "/prompts/overrides", body={"key": key, "topicId": topic_id, "content": content})

    def lab(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> Dict[str, Any]:
        """Run a prompt in the prompt lab (``key``, optional draft ``content``, ``variables``, ``model_endpoint_id``,
        ``model``, ``topic_id``). Calls a model. Returns a ``PromptLabResponse`` dict."""
        return self._json("POST", API + "/prompts/lab", body=_merge(body, fields))


class MediaResource(_Resource):
    """Audio episodes under ``/v1.0/api/media``. Audio bytes stream from :meth:`ArtifactsResource.content`."""

    def list(self, **filters: Any) -> Page[MediaAsset]:
        """List audio episodes (filters: ``briefing_id``, ``edition_id``, ``status``, ``kind``, paging)."""
        return self._page(MediaAsset, API + "/media", filters)

    def get(self, media_id: str) -> MediaDetail:
        """Read an episode with its chapters."""
        return self._one(MediaDetail, "GET", API + "/media/" + _seg(media_id))

    def delete(self, media_id: str) -> None:
        """Delete an episode and its files."""
        self._json("DELETE", API + "/media/" + _seg(media_id))

    def download(self, media_id: str, byte_range: ByteRange = None) -> Download:
        """Download an episode's audio (supports byte ranges for seeking).

        Raises:
            NotFoundError: The episode has no stored audio yet.
        """
        detail = self.get(media_id)
        if detail.media is None or not detail.media.artifact_id:
            raise NotFoundError("The episode has no stored audio yet.", status=404, code="NotFound")
        return self._client.artifacts.content(detail.media.artifact_id, byte_range=byte_range)


class ArtifactsResource(_Resource):
    """Stored files under ``/v1.0/api/artifacts``."""

    def list(self, **filters: Any) -> Page[Artifact]:
        """List stored files (filters: ``briefing_id``, ``kind``, ``pinned``, paging)."""
        return self._page(Artifact, API + "/artifacts", filters)

    def get(self, artifact_id: str) -> Artifact:
        """Read stored file metadata."""
        return self._one(Artifact, "GET", API + "/artifacts/" + _seg(artifact_id))

    def content(self, artifact_id: str, byte_range: ByteRange = None, attachment: bool = False) -> Download:
        """Stream a stored file. A byte range returns HTTP 206 with ``content_range`` set.

        Raises:
            RangeNotSatisfiableError: The range is outside the file.
        """
        return self._download(API + "/artifacts/" + _seg(artifact_id) + "/content", query={"download": True} if attachment else None, byte_range=byte_range)

    def download(self, artifact_id: str, byte_range: ByteRange = None) -> Download:
        """Download a stored file as an attachment (same bytes as :meth:`content`)."""
        return self.content(artifact_id, byte_range=byte_range, attachment=True)

    def download_url(self, artifact_id: str) -> DownloadUrl:
        """A presigned URL when the store offers one (``url`` is None for local storage; use ``content_path``)."""
        return self._one(DownloadUrl, "GET", API + "/artifacts/" + _seg(artifact_id) + "/download-url")


class FeedTokensResource(_Resource):
    """Private podcast feed tokens under ``/v1.0/api/feed-tokens``."""

    def list(self) -> List[FeedToken]:
        """List my feed tokens (not paged; ``token_hash`` is redacted)."""
        return self._many(FeedToken, "GET", API + "/feed-tokens")

    def create(self, name: Optional[str] = None, edition_id: Optional[str] = None) -> FeedTokenCreated:
        """Create a private podcast feed. The ``secret`` and ``feed_url`` are returned only once."""
        return self._one(FeedTokenCreated, "POST", API + "/feed-tokens", body={"name": name, "editionId": edition_id})

    def delete(self, token_id: str, purge: bool = False) -> None:
        """Revoke a token. With ``purge=True`` a revoked token is deleted outright."""
        self._json("DELETE", API + "/feed-tokens/" + _seg(token_id), query={"purge": True} if purge else None)

    def revoke(self, token_id: str) -> None:
        """Revoke a token (it stays listed with ``revoked_utc`` set)."""
        self.delete(token_id)

    def purge(self, token_id: str) -> None:
        """Revoke (if needed) and then delete a token."""
        self.delete(token_id)
        self.delete(token_id, purge=True)


class EmailResource(_Resource):
    """The email outbox, test email, and email preferences."""

    def list(self, **filters: Any) -> Page[EmailMessage]:
        """List the outbox (filters: ``status``, ``template``, ``run_id``, ``briefing_id``, ``topic_id``, ``recipient``, paging)."""
        return self._page(EmailMessage, API + "/email-messages", filters)

    def get(self, message_id: str) -> EmailMessage:
        """Read an email with its rendered HTML and text."""
        return self._one(EmailMessage, "GET", API + "/email-messages/" + _seg(message_id))

    def delete(self, message_id: str) -> None:
        """Delete an email from the outbox."""
        self._json("DELETE", API + "/email-messages/" + _seg(message_id))

    def resend(self, message_id: str) -> EmailMessage:
        """Send an email again (HTTP 202).

        Raises:
            ConflictError: The email is already on its way or has no rendered body.
        """
        return self._one(EmailMessage, "POST", API + "/email-messages/" + _seg(message_id) + "/resend")

    def test(self) -> EmailMessage:
        """Send a test email to the signed-in user (HTTP 202)."""
        return self._one(EmailMessage, "POST", API + "/email/test")

    def preferences(self) -> EmailPreferences:
        """My email preferences."""
        return self._one(EmailPreferences, "GET", API + "/email-preferences")

    def set_preferences(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> EmailPreferences:
        """Change my email preferences (``run_emails``, ``briefing_emails``, ``failure_emails``,
        ``resubscribe_topic_ids``, ``unsubscribe_topic_ids``)."""
        return self._one(EmailPreferences, "PUT", API + "/email-preferences", body=_merge(body, fields))


class AnalyticsResource(_Resource):
    """Analytics reports under ``/v1.0/api/analytics``.

    Every method accepts ``topic_id``, ``from_`` (sent as ``from``), ``to``, ``user_id``, and ``all_``
    (sent as ``all``). The default scope is the caller's data for the last 90 days; administrators can pass
    ``user_id``, or ``all_=True`` for the tenant. ``overview``, ``feedback``, and ``email`` return typed reports;
    the others return dicts. Time series are gap-filled by the server.
    """

    def overview(self, **filters: Any) -> OverviewReport:
        """Headline counts: topics, runs, findings, briefings, emails, predictions, feedback, suggestions, and model cost."""
        return OverviewReport.from_dict(self._report("overview", filters))

    def email(self, **filters: Any) -> EmailReport:
        """Email delivery analytics: totals, delivery rate, breakdowns by template, day, and status, and link activity."""
        return EmailReport.from_dict(self._report("email", filters))

    def _report(self, name: str, filters: Mapping[str, Any]) -> Dict[str, Any]:
        """GET one analytics report."""
        return self._json("GET", API + "/analytics/" + name, query=filters)

    def relevance(self, **filters: Any) -> Dict[str, Any]:
        """Relevance analytics."""
        return self._report("relevance", filters)

    def accuracy(self, **filters: Any) -> Dict[str, Any]:
        """Prediction accuracy analytics."""
        return self._report("accuracy", filters)

    def feedback(self, **filters: Any) -> FeedbackReport:
        """Feedback analytics, with breakdowns by channel, target type, day, and channel per day."""
        return FeedbackReport.from_dict(self._report("feedback", filters))

    def reasons(self, **filters: Any) -> Dict[str, Any]:
        """Feedback reason analytics."""
        return self._report("reasons", filters)

    def suggestions(self, **filters: Any) -> Dict[str, Any]:
        """Suggestion analytics."""
        return self._report("suggestions", filters)

    def quality(self, **filters: Any) -> Dict[str, Any]:
        """Quality analytics."""
        return self._report("quality", filters)


class UsageResource(_Resource):
    """Model usage and cost under ``/v1.0/api/usage``.

    Filters: ``from_`` (sent as ``from``), ``to``, ``user_id``, ``topic_id``, ``model_endpoint_id``, ``include_dry_run``,
    ``top``, ``bucket_minutes``, ``all_tenants`` (global admins), ``group_by`` (UsageGroupBy), ``task``.
    Grouping by Model keys each row by endpoint and model name (``key`` is ``<endpoint id>|<model>``).
    """

    def me(self, **filters: Any) -> UsageReport:
        """The caller's own model usage and cost."""
        return self._one(UsageReport, "GET", API + "/usage/me", query=filters)

    def report(self, **filters: Any) -> UsageReport:
        """Model usage and cost report."""
        return self._one(UsageReport, "GET", API + "/usage/report", query=filters)

    def timeseries(self, **filters: Any) -> UsageTimeseries:
        """Model usage and cost time series.

        Every bucket carries calls, failures, cost, token types (input, output, cached, cache-write, reasoning), tool
        calls, and the average and 95th percentile time to first token. Pass ``group_by`` (any grouping except Day;
        Tenant needs ``all_tenants``) to also get ``groups`` and per-bucket ``rows``; ``top`` (default 10) limits the
        groups and the rest are combined under the key ``(other)``.
        """
        return self._one(UsageTimeseries, "GET", API + "/usage/timeseries", query=filters)


class LlmCallsResource(_Resource):
    """Model call records under ``/v1.0/api/llm-calls``."""

    def list(self, **filters: Any) -> Page[LlmCall]:
        """List model calls (filters: ``run_id``, ``run_stage_id``, ``refinement_session_id``, ``briefing_id``, ``model_endpoint_id``, ``model``,
        ``prompt_key``, ``user_id``, ``topic_id``, ``success``, ``is_dry_run``, ``task``, paging)."""
        return self._page(LlmCall, API + "/llm-calls", filters)

    def get(self, call_id: str) -> LlmCall:
        """Read a model call with its payloads."""
        return self._one(LlmCall, "GET", API + "/llm-calls/" + _seg(call_id))


class ModelEndpointsResource(_Resource):
    """Model endpoints under ``/v1.0/api/model-endpoints``."""

    def list(self, **filters: Any) -> Page[ModelEndpoint]:
        """List model endpoints (filters: ``active``, ``supports_chat``, ``supports_embeddings``, ``provider``, paging)."""
        return self._page(ModelEndpoint, API + "/model-endpoints", filters)

    def get(self, endpoint_id: str) -> ModelEndpoint:
        """Read a model endpoint (API keys are never returned)."""
        return self._one(ModelEndpoint, "GET", API + "/model-endpoints/" + _seg(endpoint_id))

    def create(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> ModelEndpoint:
        """Create a model endpoint (``name`` and ``provider`` required; see ``ModelEndpointUpsertRequest``)."""
        return self._one(ModelEndpoint, "POST", API + "/model-endpoints", body=_merge(body, fields))

    def update(self, endpoint_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> ModelEndpoint:
        """Update a model endpoint. Omitted fields are unchanged.

        To set an optional value back to null, name it in ``clear_fields`` (JSON names: ``endpoint``, ``credentialId``,
        ``authMethod``, ``model``, ``embeddingModel``, ``temperature``, ``reasoningEffort``, ``contextLength``,
        ``inputCostPerMTok``, ``outputCostPerMTok``, ``cachedInputCostPerMTok``, ``cacheWriteCostPerMTok``), for example
        ``update(endpoint_id, clear_fields=["temperature", "contextLength"])``.

        Raises:
            BadRequestError: A name cannot be cleared, or the same request also gives it a value.
        """
        return self._one(ModelEndpoint, "PUT", API + "/model-endpoints/" + _seg(endpoint_id), body=_merge(body, fields))

    def delete(self, endpoint_id: str) -> None:
        """Delete a model endpoint."""
        self._json("DELETE", API + "/model-endpoints/" + _seg(endpoint_id))

    def validate(self, endpoint_id: str) -> ModelValidation:
        """Validate connectivity, tool support, and embeddings. Contacts the provider."""
        return self._one(ModelValidation, "POST", API + "/model-endpoints/" + _seg(endpoint_id) + "/validate")

    def models(self, endpoint_id: str) -> List[ModelListItem]:
        """List models the endpoint offers. Contacts the provider."""
        return self._many(ModelListItem, "GET", API + "/model-endpoints/" + _seg(endpoint_id) + "/models")

    def test(self, endpoint_id: str, prompt: Optional[str] = None, model: Optional[str] = None) -> ModelTestResult:
        """Send a sample prompt (recorded as a dry run). Contacts the provider and may spend tokens.

        Args:
            endpoint_id: The endpoint.
            prompt: Optional prompt text (the server uses a default when omitted).
            model: Optional model override.

        Returns:
            A :class:`ModelTestResult`; ``success`` is False and ``error`` is set when the call failed.
        """
        body = {"prompt": prompt, "model": model} if (prompt or model) else None
        return self._one(ModelTestResult, "POST", API + "/model-endpoints/" + _seg(endpoint_id) + "/test", body=body)


class ModelAssignmentsResource(_Resource):
    """Task role assignments under ``/v1.0/api/model-assignments``."""

    def list(self, **filters: Any) -> Page[ModelAssignment]:
        """List assignments (filters: ``topic_id``, ``task``, paging)."""
        return self._page(ModelAssignment, API + "/model-assignments", filters)

    def set(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> ModelAssignment:
        """Assign a task role to an endpoint (``task`` and ``model_endpoint_id``; ``topic_id`` for a topic override)."""
        return self._one(ModelAssignment, "PUT", API + "/model-assignments", body=_merge(body, fields))

    def update(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> ModelAssignment:
        """Alias of :meth:`set`."""
        return self.set(body, **fields)

    def delete(self, assignment_id: str) -> None:
        """Remove a topic assignment override (tenant defaults cannot be deleted)."""
        self._json("DELETE", API + "/model-assignments/" + _seg(assignment_id))


class SearchProvidersResource(_Resource):
    """Search providers under ``/v1.0/api/search-providers``."""

    def list(self, **filters: Any) -> Page[SearchProvider]:
        """List search providers (filters: ``kind``, paging)."""
        return self._page(SearchProvider, API + "/search-providers", filters)

    def get(self, provider_id: str) -> SearchProvider:
        """Read a search provider."""
        return self._one(SearchProvider, "GET", API + "/search-providers/" + _seg(provider_id))

    def create(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> SearchProvider:
        """Add a search provider (``name`` and ``kind`` required)."""
        return self._one(SearchProvider, "POST", API + "/search-providers", body=_merge(body, fields))

    def update(self, provider_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> SearchProvider:
        """Update a search provider."""
        return self._one(SearchProvider, "PUT", API + "/search-providers/" + _seg(provider_id), body=_merge(body, fields))

    def delete(self, provider_id: str) -> None:
        """Delete a search provider."""
        self._json("DELETE", API + "/search-providers/" + _seg(provider_id))

    def test(self, provider_id: str, query: Optional[str] = None, feed_urls: Optional[Sequence[str]] = None, max_results: Optional[int] = None) -> Dict[str, Any]:
        """Run a test query through the provider. Returns the outcome as a dict."""
        return self._json("POST", API + "/search-providers/" + _seg(provider_id) + "/test",
                          body={"query": query, "feedUrls": list(feed_urls) if feed_urls else None, "maxResults": max_results})

    def fetch_test(self, url: str) -> Dict[str, Any]:
        """Fetch a page as the research pipeline would. Returns the fetch result as a dict."""
        return self._json("POST", API + "/fetch/test", body={"url": url})


class VoicesResource(_Resource):
    """Voice profiles under ``/v1.0/api/voices``."""

    def list(self, **filters: Any) -> Page[VoiceProfile]:
        """List voice profiles (paging filters)."""
        return self._page(VoiceProfile, API + "/voices", filters)

    def get(self, voice_id: str) -> VoiceProfile:
        """Read a voice profile."""
        return self._one(VoiceProfile, "GET", API + "/voices/" + _seg(voice_id))

    def create(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> VoiceProfile:
        """Create a voice profile (``name`` and ``voice`` required)."""
        return self._one(VoiceProfile, "POST", API + "/voices", body=_merge(body, fields))

    def update(self, voice_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> VoiceProfile:
        """Update a voice profile."""
        return self._one(VoiceProfile, "PUT", API + "/voices/" + _seg(voice_id), body=_merge(body, fields))

    def delete(self, voice_id: str) -> None:
        """Delete a voice profile."""
        self._json("DELETE", API + "/voices/" + _seg(voice_id))

    def test(self, voice_id: str, text: Optional[str] = None) -> Download:
        """Speak a sample with the voice. Returns the audio bytes.

        Raises:
            ServerError: The speech service failed (``TtsError``, HTTP 502).
        """
        resp = self._client.request("POST", API + "/voices/" + _seg(voice_id) + "/test", body={"text": text}, raw=True)
        return Download.from_response(resp)

    def available(self) -> AvailableVoices:
        """The voices a topic can use: the workspace's voice profiles, then the speech server's other voices.

        Each has a readable label. Anyone who can read topics may call it. To use one for a topic, send its
        ``voice_profile_id``, or its ``voice`` when it has no profile yet, to :meth:`TopicsResource.update`.
        """
        return self._one(AvailableVoices, "GET", API + "/voices/available")

    def preview(self, voice: Optional[str] = None, voice_profile_id: Optional[str] = None, text: Optional[str] = None) -> Download:
        """Speak a short sample (up to 300 characters) with a speech server voice or a voice profile.

        Args:
            voice: A speech server voice ID from :meth:`available`, such as ``af_heart``.
            voice_profile_id: A workspace voice profile to speak with instead.
            text: Text to speak; None speaks a default sentence.

        Raises:
            ValueError: Neither ``voice`` nor ``voice_profile_id`` was given.
            ConflictError: Speech is turned off on the server (HTTP 409).
            ServerError: The speech service failed (``TtsError``, HTTP 502).
        """
        if not voice and not voice_profile_id:
            raise ValueError("voice or voice_profile_id is required.")
        body: Dict[str, Any] = {}
        if voice_profile_id:
            body["voiceProfileId"] = voice_profile_id
        else:
            body["voice"] = voice
        if text:
            body["text"] = text
        resp = self._client.request("POST", API + "/voices/preview", body=body, raw=True)
        return Download.from_response(resp)


class VaultResource(_Resource):
    """Vault credentials under ``/v1.0/api/vault/credentials``. Secrets are never returned (reveal is C# only)."""

    def list(self, **filters: Any) -> Page[VaultCredential]:
        """List credentials (filters: ``active``, ``kind``, ``provider``, paging)."""
        return self._page(VaultCredential, API + "/vault/credentials", filters)

    def get(self, credential_id: str) -> VaultCredential:
        """Read a credential (no secret)."""
        return self._one(VaultCredential, "GET", API + "/vault/credentials/" + _seg(credential_id))

    def create(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> VaultCredential:
        """Store a credential (``name``, ``kind``, ``provider``, ``secret``)."""
        return self._one(VaultCredential, "POST", API + "/vault/credentials", body=_merge(body, fields))

    def update(self, credential_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> VaultCredential:
        """Update or rotate a credential (send ``secret`` to rotate). Omitted fields are unchanged.

        To remove an optional value, name it in ``clear_fields`` (JSON names: ``headerName``, ``username``,
        ``description``, ``expiresUtc``), for example ``update(credential_id, clear_fields=["expiresUtc"])``.

        Raises:
            BadRequestError: A name cannot be cleared, or the same request also gives it a value.
        """
        return self._one(VaultCredential, "PUT", API + "/vault/credentials/" + _seg(credential_id), body=_merge(body, fields))

    def delete(self, credential_id: str) -> None:
        """Delete a credential.

        Raises:
            ConflictError: The credential is still in use.
        """
        self._json("DELETE", API + "/vault/credentials/" + _seg(credential_id))

    def usage(self, credential_id: str) -> List[VaultUsageItem]:
        """Records that use the credential."""
        return self._many(VaultUsageItem, "GET", API + "/vault/credentials/" + _seg(credential_id) + "/usage")


class NotificationsResource(_Resource):
    """In-app notifications and notification preferences under ``/v1.0/api/notifications``."""

    def list(self, **filters: Any) -> Page[Notification]:
        """List my in-app notifications, newest first (filters: ``unread``, ``kind``, paging)."""
        return self._page(Notification, API + "/notifications", filters)

    def unread_count(self) -> int:
        """How many of my in-app notifications are unread."""
        return int(self._one(NotificationCount, "GET", API + "/notifications/unread-count").count or 0)

    def mark_read(self, notification_id: str) -> Notification:
        """Mark a notification as read.

        Raises:
            NotFoundError: The notification does not exist or is not mine.
        """
        return self._one(Notification, "POST", API + "/notifications/" + _seg(notification_id) + "/read")

    def mark_all_read(self) -> int:
        """Mark all my notifications as read and return how many were marked."""
        return int(self._one(NotificationCount, "POST", API + "/notifications/read-all").count or 0)

    def delete(self, notification_id: str) -> None:
        """Delete a notification.

        Raises:
            NotFoundError: The notification does not exist or is not mine.
        """
        self._json("DELETE", API + "/notifications/" + _seg(notification_id))

    def preferences(self) -> NotificationPreferences:
        """My notification preferences: every kind on every channel, and whether each channel can deliver."""
        return self._one(NotificationPreferences, "GET", API + "/notifications/preferences")

    def set_preferences(self, preferences: Optional[Sequence[Mapping[str, Any]]] = None, unsubscribe_topic_ids: Optional[Sequence[str]] = None,
                        resubscribe_topic_ids: Optional[Sequence[str]] = None) -> NotificationPreferences:
        """Change my notification preferences. Only the cells that are sent change.

        Args:
            preferences: Cells such as ``{"kind": "AudioReady", "channel": "Push", "enabled": True}``.
            unsubscribe_topic_ids: Topics to stop run emails for.
            resubscribe_topic_ids: Topics to get run emails for again.
        """
        cells = []
        for cell in preferences or []:
            c = cell.to_wire() if isinstance(cell, Model) else dict(cell)
            kind, channel = c.get("kind"), c.get("channel")
            cells.append({"kind": kind.value if isinstance(kind, Enum) else kind, "channel": channel.value if isinstance(channel, Enum) else channel, "enabled": bool(c.get("enabled"))})
        body: Dict[str, Any] = {}
        if preferences is not None:
            body["preferences"] = cells
        if unsubscribe_topic_ids is not None:
            body["unsubscribeTopicIds"] = list(unsubscribe_topic_ids)
        if resubscribe_topic_ids is not None:
            body["resubscribeTopicIds"] = list(resubscribe_topic_ids)
        return self._one(NotificationPreferences, "PUT", API + "/notifications/preferences", body=body)

    def set_preference(self, kind: Any, channel: Any, enabled: bool) -> NotificationPreferences:
        """Turn one kind on or off for one channel."""
        return self.set_preferences([{"kind": kind, "channel": channel, "enabled": enabled}])


class DevicesResource(_Resource):
    """Push notification devices under ``/v1.0/api/devices``."""

    def list(self) -> List[Device]:
        """List my push devices (not paged)."""
        return self._many(Device, "GET", API + "/devices")

    def create(self, expo_push_token: str, platform: str, name: Optional[str] = None, locale: Optional[str] = None) -> Device:
        """Register a device (``platform`` is ``ios`` or ``android``; the token must be an Expo push token)."""
        return self._one(Device, "POST", API + "/devices", body={"expoPushToken": expo_push_token, "platform": platform, "name": name, "locale": locale})

    def register(self, expo_push_token: str, platform: str, name: Optional[str] = None, locale: Optional[str] = None) -> Device:
        """Alias of :meth:`create`."""
        return self.create(expo_push_token, platform, name, locale)

    def delete(self, device_id: str) -> None:
        """Remove a device."""
        self._json("DELETE", API + "/devices/" + _seg(device_id))

    def test(self, device_id: str) -> Dict[str, Any]:
        """Send a test notification to a device (contacts the Expo push service). Returns the push result as a dict."""
        return self._json("POST", API + "/devices/" + _seg(device_id) + "/test")


# BEGIN PLATFORM
class CatalogResource(_Resource):
    """Platform account/catalog API with server-enforced ownership and permissions."""

    def list(self, **filters: Any) -> Page[CatalogEntryResponse]:
        """Browse published system briefings and account eligibility."""
        return self._page(CatalogEntryResponse, "/v1.0/api/catalog", filters)

    def subscriptions(self, **filters: Any) -> Page[CatalogSubscription]:
        """List private subscriptions, including suspended subscriptions."""
        return self._page(CatalogSubscription, "/v1.0/api/catalog/subscriptions", filters)

    def subscribe(self, id: str, body: Mapping[str, Any]) -> CatalogSubscription:
        """Subscribe or update delivery preferences. Separate system subscription limits apply."""
        return self._one(CatalogSubscription, "PUT", "/v1.0/api/catalog/" + _seg(id) + "/subscription", body=body)

    def unsubscribe(self, id: str) -> None:
        """Unsubscribe without deleting canonical content."""
        self._json("DELETE", "/v1.0/api/catalog/" + _seg(id) + "/subscription")

    def issues(self, id: str, **filters: Any) -> Page[CatalogIssueResponse]:
        """List issues readable through this subscription."""
        return self._page(CatalogIssueResponse, "/v1.0/api/catalog/" + _seg(id) + "/issues", filters)

    def all_issues(self, **filters: Any) -> Page[CatalogIssueResponse]:
        """List shared issues across all current subscriptions."""
        return self._page(CatalogIssueResponse, "/v1.0/api/catalog/issues", filters)

    def issue(self, id: str, issue_id: str) -> CatalogIssueResponse:
        """Read shared content without exposing canonical workspace internals."""
        return self._one(CatalogIssueResponse, "GET", "/v1.0/api/catalog/" + _seg(id) + "/issues/" + _seg(issue_id))

    def audio(self, id: str, issue_id: str, byte_range: ByteRange = None) -> Download:
        """Download authenticated shared audio. Playback does not consume generation allowance."""
        return self._download("/v1.0/api/catalog/" + _seg(id) + "/issues/" + _seg(issue_id) + "/audio", byte_range=byte_range)

    def progress(self, id: str, issue_id: str) -> BriefingProgress:
        """Read private listening progress."""
        return self._one(BriefingProgress, "GET", "/v1.0/api/catalog/" + _seg(id) + "/issues/" + _seg(issue_id) + "/progress")

    def save_progress(self, id: str, issue_id: str, body: Mapping[str, Any]) -> BriefingProgress:
        """Save private listening progress for a shared issue."""
        return self._one(BriefingProgress, "PUT", "/v1.0/api/catalog/" + _seg(id) + "/issues/" + _seg(issue_id) + "/progress", body=body)


class BillingResource(_Resource):
    """Platform account/catalog API with server-enforced ownership and permissions."""

    def status(self) -> BillingStatusResponse:
        """Read offers, native account binding, verified subscriptions and effective access."""
        return self._one(BillingStatusResponse, "GET", "/v1.0/api/account/billing")

    def checkout(self, body: Mapping[str, Any]) -> BillingRedirectResponse:
        """Create or resume one account-bound Stripe checkout. A redirect never grants access."""
        return self._one(BillingRedirectResponse, "POST", "/v1.0/api/account/billing/checkout", body=body)

    def portal(self) -> BillingRedirectResponse:
        """Open management for the verified Stripe customer."""
        return self._one(BillingRedirectResponse, "POST", "/v1.0/api/account/billing/portal")

    def verify_purchase(self, body: Mapping[str, Any]) -> EffectivePlan:
        """Verify or restore an account-bound Apple or Google purchase."""
        return self._one(EffectivePlan, "POST", "/v1.0/api/account/billing/purchases", body=body)


class ProvidersResource(_Resource):
    """Platform account/catalog API with server-enforced ownership and permissions."""

    def list(self) -> ProviderConnectionsResponse:
        """List only this account's masked provider connections."""
        return self._one(ProviderConnectionsResponse, "GET", "/v1.0/api/account/providers")

    def save(self, body: Mapping[str, Any]) -> ProviderConnection:
        """Connect or rotate a write-only model or speech API key. Clears validation."""
        return self._one(ProviderConnection, "PUT", "/v1.0/api/account/providers", body=body)

    def test(self, id: str) -> Dict[str, Any]:
        """Run a fixed small probe with this account's credential. Six probes per minute."""
        return self._json("POST", "/v1.0/api/account/providers/" + _seg(id) + "/test")

    def remove(self, id: str) -> Dict[str, Any]:
        """Remove a private key. BYOK calls fail closed, without platform fallback."""
        return self._json("DELETE", "/v1.0/api/account/providers/" + _seg(id))

    def set_funding(self, body: Mapping[str, Any]) -> EffectivePlan:
        """Choose Personal or Platform funding. A verified chat connection is required for BYOK."""
        return self._one(EffectivePlan, "PUT", "/v1.0/api/account/funding", body=body)


class AdminCatalogResource(_Resource):
    """Platform account/catalog API with server-enforced ownership and permissions."""

    def list(self, **filters: Any) -> Page[CatalogPublication]:
        """System administrator: list every publication lifecycle state."""
        return self._page(CatalogPublication, "/v1.0/api/admin/catalog", filters)

    def get(self, id: str) -> CatalogPublication:
        """System administrator: read a canonical publication."""
        return self._one(CatalogPublication, "GET", "/v1.0/api/admin/catalog/" + _seg(id))

    def create(self, body: Mapping[str, Any]) -> CatalogPublication:
        """System administrator: create a canonical system topic and publication."""
        return self._one(CatalogPublication, "POST", "/v1.0/api/admin/catalog", body=body)

    def update(self, id: str, body: Mapping[str, Any]) -> CatalogPublication:
        """System administrator: update publication with current revision for conflict protection."""
        return self._one(CatalogPublication, "PUT", "/v1.0/api/admin/catalog/" + _seg(id), body=body)

    def issues(self, id: str, **filters: Any) -> Page[CatalogIssueResponse]:
        """System administrator: inspect canonical issue projections."""
        return self._page(CatalogIssueResponse, "/v1.0/api/admin/catalog/" + _seg(id) + "/issues", filters)

    def research(self, id: str) -> CatalogResearchResponse:
        """System administrator: read canonical research and schedule configuration."""
        return self._one(CatalogResearchResponse, "GET", "/v1.0/api/admin/catalog/" + _seg(id) + "/research")

    def save_research(self, id: str, body: Mapping[str, Any]) -> CatalogResearchResponse:
        """System administrator: configure canonical research and schedule."""
        return self._one(CatalogResearchResponse, "PUT", "/v1.0/api/admin/catalog/" + _seg(id) + "/research", body=body)

    def run(self, id: str) -> Run:
        """System administrator: start a canonical research run."""
        return self._one(Run, "POST", "/v1.0/api/admin/catalog/" + _seg(id) + "/run")


class AdminEntitlementsResource(_Resource):
    """Platform account/catalog API with server-enforced ownership and permissions."""

    def get(self, tenant_id: str, user_id: str) -> EffectivePlan:
        """System administrator: inspect effective user allowances and provenance."""
        return self._one(EffectivePlan, "GET", "/v1.0/tenants/" + _seg(tenant_id) + "/users/" + _seg(user_id) + "/entitlements")

    def set(self, tenant_id: str, user_id: str, body: Mapping[str, Any]) -> EffectivePlan:
        """System administrator: override one limit with an audit reason and optional expiry."""
        return self._one(EffectivePlan, "PUT", "/v1.0/tenants/" + _seg(tenant_id) + "/users/" + _seg(user_id) + "/entitlements", body=body)

    def clear(self, tenant_id: str, user_id: str, feature_key: str) -> None:
        """System administrator: remove an override and restore plan inheritance."""
        self._json("DELETE", "/v1.0/tenants/" + _seg(tenant_id) + "/users/" + _seg(user_id) + "/entitlements/" + _seg(feature_key))


# END PLATFORM

class AccountResource(_Resource):
    """The signed-in user's account (plan, promotional codes, usage, export, deletion) and the public
    sign-up, email verification, and password reset routes (which never send the client's credentials)."""

    def plan(self) -> EffectivePlan:
        """My plan and its limits."""
        return self._one(EffectivePlan, "GET", API + "/account/plan")

    def redeem(self, code: str) -> EffectivePlan:
        """Redeem a promotional code. Returns the plan now in effect (HTTP 201)."""
        return self._one(EffectivePlan, "POST", API + "/promo-codes/redeem", body={"code": code})

    def usage(self) -> UsageMeter:
        """My usage against my plan's limits. Needs a signed-in user, not an access key (BadRequestError otherwise)."""
        return self._one(UsageMeter, "GET", API + "/account/usage")

    def export(self) -> Download:
        """Download my data as a ZIP of JSON and Markdown. Needs a signed-in user."""
        return self._download(API + "/account/export")

    def delete(self) -> Dict[str, Any]:
        """Delete my account (HTTP 202). Signs out every session and schedules the purge after the grace
        period (default 7 days); signing in again before ``purgeAfterUtc`` cancels it.

        Returns:
            ``{"purgeAfterUtc": ..., "message": ...}``.

        Raises:
            ConflictError: Administrators and protected accounts cannot be deleted.
        """
        return self._json("POST", API + "/account/delete")

    def signup(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> Dict[str, Any]:
        """Sign up for the hosted service (public). Always HTTP 202 with the same body, whether or not the
        email already has an account.

        Args:
            body: A ``SignupRequest`` dict.
            **fields: ``email``, ``password``, ``accept_terms``, ``confirm_age`` (required), and
                ``display_name``, ``time_zone``, ``locale``, ``promo_code``, ``turnstile_token``.

        Returns:
            ``{"message": ..., "verificationRequired": bool}``.

        Raises:
            NotFoundError: Sign-up is closed (code ``SignupClosed``).
            BadRequestError: Validation failed (weak password, terms or age not confirmed, blocked domain).
            RateLimitError: Too many sign-ups from this address.
        """
        return self._json("POST", "/v1.0/public/signup", body=_merge(body, fields), auth=False)

    def verify_email(self, token: str) -> Dict[str, Any]:
        """Confirm an email address with the token from the emailed link (public).

        Returns:
            ``{"verified": True, "email": ...}``.

        Raises:
            BadRequestError: The link is not valid, was already used, or expired.
        """
        return self._json("POST", "/v1.0/public/verify-email", body={"token": token}, auth=False)

    def resend_verification(self, email: str) -> Dict[str, Any]:
        """Send a new verification link (public). Always HTTP 202 with ``{"message": ...}``."""
        return self._json("POST", "/v1.0/public/verify-email/resend", body={"email": email}, auth=False)

    def forgot_password(self, email: str) -> Dict[str, Any]:
        """Email a password reset link (public). Always HTTP 202 with ``{"message": ...}``."""
        return self._json("POST", "/v1.0/public/password/forgot", body={"email": email}, auth=False)

    def reset_password(self, token: str, password: str) -> Dict[str, Any]:
        """Set a new password with the token from a reset link (public). Signs out every session.

        Returns:
            ``{"reset": True, "email": ...}``.

        Raises:
            BadRequestError: The link is not valid or the password is too weak (minimum 8 characters).
        """
        return self._json("POST", "/v1.0/public/password/reset", body={"token": token, "password": password}, auth=False)


class SetupResource(_Resource):
    """Workspace setup under ``/v1.0/api/setup``: whether the workspace has a chat model with a working key
    and web search, live checks that prove it, the model providers and sign-in methods the server offers,
    connecting one, and storing a model API key. Until ``ready`` is True, refinement sessions and research
    requests raise :class:`SetupRequiredError`."""

    def status(self) -> SetupStatus:
        """The workspace setup status (``ready``, ``can_configure``, and the checks)."""
        return self._one(SetupStatus, "GET", API + "/setup")

    def verify(self, check: Optional[Union[str, "SetupCheckKind"]] = None) -> SetupStatus:
        """Re-check setup with live calls. Administrators only (PermissionDeniedError otherwise).

        Sends a short probe prompt to each chat endpoint, a test query to each default web search provider,
        one embedding request, and one short speech request, so it contacts the providers.

        Args:
            check: Re-check only this kind (``Model``, ``Search``, ``Embeddings``, or ``Speech``); the other
                checks are reported from stored results. None re-checks everything.
        """
        query = None if check is None else {"check": check.value if isinstance(check, Enum) else str(check)}
        return self._one(SetupStatus, "POST", API + "/setup/verify", query=query)

    def options(self) -> SetupOptions:
        """Model providers and sign-in methods the Setup page can connect on this server. Administrators only
        (PermissionDeniedError otherwise).

        Lists hosted APIs (API key), agent CLIs in the server image (Claude Code, Codex, Gemini CLI, Mux), and
        self-hosted servers, each with whether it is installed and allowed and its sign-in methods,
        recommended first.
        """
        return self._one(SetupOptions, "GET", API + "/setup/options")

    def connect(self, provider: Union[str, ModelProvider], auth_method: Optional[Union[str, ModelAuthMethod]] = None,
                secret: Optional[str] = None, model: Optional[str] = None, base_url: Optional[str] = None,
                model_endpoint_id: Optional[str] = None) -> SetupStatus:
        """Connect the workspace's chat model and verify it with a live call. Administrators only.

        Stores the secret for the chosen sign-in method encrypted in the vault, tests it with a live call, and
        points every chat task at the endpoint. When the workspace already has a working model, the new sign-in
        is tested first and the working one is kept if the test fails. ``connection`` on the result says what
        happened (``succeeded``, ``kept_previous``, ``message``). The secret is never returned.

        Args:
            provider: A :class:`ModelProvider` (name or member).
            auth_method: A :class:`ModelAuthMethod`; None uses the provider's recommended method.
            secret: The API key, the access token (``claude setup-token``), or the sign-in file's contents
                (``auth.json``, ``oauth_creds.json``, ``endpoints.json``). Required unless the method is
                ``ServerSignIn`` or ``None``, or the endpoint already has one stored for the same method.
            model: The model; None uses the provider's default (an agent CLI's own default when it has one).
            base_url: Base URL for Ollama and OpenAI-compatible servers.
            model_endpoint_id: Endpoint to change; None reuses the workspace's endpoint for the same provider,
                or creates one.

        Returns:
            The :class:`SetupStatus` after the endpoint was checked.

        Raises:
            ValueError: ``provider`` is empty.
            BadRequestError: The server rejected a value (400 ``ValidationFailed``, with ``details["field"]`` set
                to ``provider``, ``authMethod``, ``secret``, ``model``, or ``baseUrl``), for example a missing
                secret, a method the provider does not accept, or an agent CLI that is not allowed or not installed.
            PermissionDeniedError: The caller is not an administrator.
            NotFoundError: The named endpoint does not exist.
        """
        if not provider:
            raise ValueError("provider is required.")
        body: Dict[str, Any] = {"provider": provider}
        if auth_method is not None:
            body["authMethod"] = auth_method
        if secret is not None:
            body["secret"] = secret
        if model is not None:
            body["model"] = model
        if base_url is not None:
            body["baseUrl"] = base_url
        if model_endpoint_id is not None:
            body["modelEndpointId"] = model_endpoint_id
        return self._one(SetupStatus, "POST", API + "/setup/connect", body=body)

    def set_model_key(self, model_endpoint_id: str, api_key: str) -> SetupStatus:
        """Store an API key for a model endpoint and verify it with a live call. Administrators only.

        The key is stored encrypted in the vault, attached to the endpoint, and never returned.

        Args:
            model_endpoint_id: The model endpoint.
            api_key: The API key.

        Returns:
            The :class:`SetupStatus` after the key was checked.

        Raises:
            ValueError: ``model_endpoint_id`` or ``api_key`` is empty.
            BadRequestError: The server rejected a value (400 ``ValidationFailed``).
            PermissionDeniedError: The caller is not an administrator.
            NotFoundError: The endpoint does not exist.
        """
        if not model_endpoint_id:
            raise ValueError("model_endpoint_id is required.")
        if not api_key:
            raise ValueError("api_key is required.")
        return self._one(SetupStatus, "POST", API + "/setup/model-key", body={"modelEndpointId": model_endpoint_id, "apiKey": api_key})


class McpServersResource(_Resource):
    """MCP server registry for agentic research (``/v1.0/api/mcp-servers``): register remote MCP servers
    (Streamable HTTP or SSE URLs; stdio is not supported), test them, discover tools, and turn tools on
    or off. Tenant administrators manage it; members read it."""

    def list(self, **filters: Any) -> Page[McpServer]:
        """List registered servers.

        Args:
            **filters: ``enabled`` and the paging filters.
        """
        return self._page(McpServer, API + "/mcp-servers", filters)

    def get(self, server_id: str) -> McpServerDetail:
        """Read a server with its tools."""
        return self._one(McpServerDetail, "GET", API + "/mcp-servers/" + _seg(server_id))

    def create(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> McpServer:
        """Register a server (HTTP 201).

        Args:
            body: A ``McpServerUpsertRequest`` dict.
            **fields: ``name`` and ``url`` (required), ``transport`` (McpTransport), ``vault_credential_id``,
                ``header_name``, ``header_prefix``, ``enabled``.

        Raises:
            BadRequestError: ``name`` or ``url`` is missing, or the URL is not absolute http or https.
        """
        return self._one(McpServer, "POST", API + "/mcp-servers", body=_merge(body, fields))

    def update(self, server_id: str, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> McpServer:
        """Change a server; only the fields you send change. Send ``vault_credential_id=""`` to remove the credential."""
        return self._one(McpServer, "PUT", API + "/mcp-servers/" + _seg(server_id), body=_merge(body, fields))

    def delete(self, server_id: str) -> None:
        """Remove a server and its tools (HTTP 204)."""
        self._json("DELETE", API + "/mcp-servers/" + _seg(server_id))

    def test(self, server_id: str) -> McpServerTestResult:
        """Connect and list tools without saving them. A failed connection is HTTP 200 with ``success`` False."""
        return self._one(McpServerTestResult, "POST", API + "/mcp-servers/" + _seg(server_id) + "/test")

    def discover(self, server_id: str) -> McpServerDetail:
        """Discover the server's tools and save them. New tools start disabled, existing tools keep their
        setting, and tools the server no longer lists are removed.

        Raises:
            ServerError: HTTP 502 ``McpServerUnavailable`` when the server cannot be reached.
        """
        return self._one(McpServerDetail, "POST", API + "/mcp-servers/" + _seg(server_id) + "/discover")

    def set_tool_enabled(self, server_id: str, tool_id: str, enabled: bool) -> McpServerTool:
        """Turn a tool on or off for agentic research.

        Raises:
            NotFoundError: The tool is not on this server.
        """
        return self._one(McpServerTool, "PUT", API + "/mcp-servers/" + _seg(server_id) + "/tools/" + _seg(tool_id), body={"enabled": bool(enabled)})


class SettingsResource(_Resource):
    """Server settings (administrators). Secrets are masked."""

    def get(self) -> Dict[str, Any]:
        """Read server settings. Returns ``{"settings": ..., "fields": [...], "pendingRestart": [...]}``."""
        return self._json("GET", API + "/settings")

    def update(self, settings: Dict[str, Any]) -> Dict[str, Any]:
        """Save the complete settings object from get(); masked secrets remain unchanged."""
        if not isinstance(settings, dict):
            raise TypeError("settings must be a dictionary")
        return self._json("PUT", API + "/settings", body=settings)


class PublicResource(_Resource):
    """Anonymous routes under ``/v1.0/public``: promo code checks, podcast feeds, artwork, and email pages."""

    def promo_code(self, code: str) -> PromoCheck:
        """Check whether a promotional code can be redeemed (rate limited per IP)."""
        return self._one(PromoCheck, "GET", "/v1.0/public/promo-codes/" + _seg(code), auth=False)

    def podcast_feed(self, feed_secret: str) -> str:
        """Fetch a private podcast feed (RSS 2.0) by its secret token."""
        return self._download("/v1.0/public/feeds/" + _seg(feed_secret) + "/podcast.xml", auth=False).text

    def episode(self, feed_secret: str, file: str, byte_range: ByteRange = None) -> Download:
        """Download a podcast episode's audio (Range supported)."""
        return self._download("/v1.0/public/feeds/" + _seg(feed_secret) + "/episodes/" + _seg(file), byte_range=byte_range, auth=False)

    def chapters(self, feed_secret: str, file: str) -> Dict[str, Any]:
        """Podcast episode chapters (Podcasting 2.0 JSON)."""
        return json.loads(self._download("/v1.0/public/feeds/" + _seg(feed_secret) + "/chapters/" + _seg(file), auth=False).text)

    def asset(self, name: str) -> Download:
        """Public artwork, for example ``glyph.png`` or ``logo.png``."""
        return self._download("/v1.0/public/assets/" + _seg(name), auth=False)

    def feedback_page(self, action_token: str, action: Optional[str] = None) -> str:
        """The HTML feedback page an email link opens."""
        return self._download("/v1.0/public/feedback/" + _seg(action_token), query={"a": action}, auth=False).text

    def submit_feedback(self, action_token: str, action: Optional[str] = None, reason: Optional[str] = None) -> str:
        """Submit the email feedback form. Returns the resulting HTML page."""
        resp = self._client.request("POST", "/v1.0/public/feedback/" + _seg(action_token), form={"a": action, "reason": reason}, raw=True, auth=False)
        return resp.text

    def unsubscribe_page(self, action_token: str) -> str:
        """The HTML unsubscribe page an email link opens."""
        return self._download("/v1.0/public/unsubscribe/" + _seg(action_token), auth=False).text

    def unsubscribe(self, action_token: str, undo: bool = False) -> str:
        """Unsubscribe (or undo with ``undo=True``). Returns the resulting HTML page."""
        resp = self._client.request("POST", "/v1.0/public/unsubscribe/" + _seg(action_token), form={"op": "undo" if undo else "unsubscribe"}, raw=True, auth=False)
        return resp.text


# ---------------------------------------------------------------------------
# Client
# ---------------------------------------------------------------------------


class MyMorningNewsClient:
    """Client for the My Morning News REST API.

    Authenticate with :meth:`login` (email and password), :meth:`login_with_access_key`,
    a ``token`` passed to the constructor, or an access key and secret passed to the
    constructor (sent on every request). Resource groups are attributes, for example
    ``client.topics.list()``.

    The client wraps one ``requests.Session``. It is not safe to share across threads
    while one thread logs in or out; otherwise concurrent calls are fine.

    Example::

        with MyMorningNewsClient() as client:
            client.login("admin@mymorningnews", "password")
            for topic in client.iterate(client.topics.list, status="Active"):
                print(topic.name)
    """

    def __init__(
        self,
        base_url: str = DEFAULT_BASE_URL,
        token: Optional[str] = None,
        access_key: Optional[str] = None,
        secret_key: Optional[str] = None,
        tenant_id: Optional[str] = None,
        timeout: float = DEFAULT_TIMEOUT,
        session: Optional[requests.Session] = None,
        signed_requests: bool = False,
    ) -> None:
        """Create a client.

        Args:
            base_url: Server URL (default ``http://127.0.0.1:12100``). A trailing slash is ignored.
            token: An existing bearer session token.
            access_key: Access key, sent as ``x-access-key`` when no token is set.
            secret_key: Secret key, sent as ``x-secret-key`` with the access key.
            tenant_id: Tenant ID, sent as ``x-tenant-guid`` with access keys and used by :meth:`login`.
            timeout: Per-request timeout in seconds (default 30; minimum 1).
            session: Optional ``requests.Session`` to reuse. The client closes only sessions it created.
            signed_requests: Sign each request with the access key's secret (MMN1-HMAC-SHA256) instead of
                sending ``x-secret-key``. Needs ``access_key``, ``secret_key``, and ``tenant_id``; ignored while a
                session token is set. Default False.

        Raises:
            ValueError: ``base_url`` is empty, or ``signed_requests`` is set without an access key, secret, and tenant.
        """
        if not base_url:
            raise ValueError("base_url is required.")
        self.base_url = base_url.rstrip("/")
        self.token = token
        self.access_key = access_key
        self.secret_key = secret_key
        self.tenant_id = tenant_id
        self.timeout = max(1.0, float(timeout))
        self._owns_session = session is None
        self._session = session or requests.Session()
        self.signed_requests = bool(signed_requests)
        if self.signed_requests and not (access_key and secret_key and tenant_id):
            raise ValueError("signed_requests needs access_key, secret_key, and tenant_id.")
        self.last_login: Optional[TokenResponse] = None

        self.topics = TopicsResource(self)
        self.refinement = RefinementResource(self)
        self.requests = ResearchRequestsResource(self)
        self.schedules = SchedulesResource(self)
        self.runs = RunsResource(self)
        self.findings = FindingsResource(self)
        self.evidence = EvidenceResource(self)
        self.sources = SourcesResource(self)
        self.predictions = PredictionsResource(self)
        self.suggestions = SuggestionsResource(self)
        self.feedback = FeedbackResource(self)
        self.preferences = PreferencesResource(self)
        self.editions = EditionsResource(self)
        self.briefings = BriefingsResource(self)
        self.templates = TemplatesResource(self)
        self.prompts = PromptsResource(self)
        self.media = MediaResource(self)
        self.artifacts = ArtifactsResource(self)
        self.feed_tokens = FeedTokensResource(self)
        self.email = EmailResource(self)
        self.analytics = AnalyticsResource(self)
        self.usage = UsageResource(self)
        self.llm_calls = LlmCallsResource(self)
        self.model_endpoints = ModelEndpointsResource(self)
        self.model_assignments = ModelAssignmentsResource(self)
        self.search_providers = SearchProvidersResource(self)
        self.voices = VoicesResource(self)
        self.vault = VaultResource(self)
        self.devices = DevicesResource(self)
        self.notifications = NotificationsResource(self)
        self.catalog = CatalogResource(self)
        self.billing = BillingResource(self)
        self.providers = ProvidersResource(self)
        self.admin_catalog = AdminCatalogResource(self)
        self.admin_entitlements = AdminEntitlementsResource(self)
        self.account = AccountResource(self)
        self.mcp_servers = McpServersResource(self)
        self.setup = SetupResource(self)
        self.settings = SettingsResource(self)
        self.public = PublicResource(self)

    # Context manager and lifecycle

    def __enter__(self) -> "MyMorningNewsClient":
        """Enter a ``with`` block."""
        return self

    def __exit__(self, exc_type: Any, exc: Any, tb: Any) -> None:
        """Leave a ``with`` block, closing the HTTP session if the client created it."""
        self.close()

    def close(self) -> None:
        """Close the HTTP session if the client created it. Does not log out."""
        if self._owns_session:
            self._session.close()

    def __repr__(self) -> str:
        """Describe the client without revealing credentials."""
        mode = "token" if self.token else (("signed-access-key" if self.signed_requests else "access-key") if self.access_key else "anonymous")
        return "MyMorningNewsClient(base_url=%r, auth=%s, tenant_id=%r)" % (self.base_url, mode, self.tenant_id)

    # Transport

    def _auth_headers(self) -> Dict[str, str]:
        """Headers that authenticate the next request."""
        headers: Dict[str, str] = {}
        if self.token:
            headers["Authorization"] = "Bearer " + self.token
        elif self.access_key and self.secret_key:
            headers["x-access-key"] = self.access_key
            if not self.signed_requests:
                headers["x-secret-key"] = self.secret_key
            if self.tenant_id:
                headers["x-tenant-guid"] = self.tenant_id
        return headers

    def _signing(self, auth: bool) -> bool:
        """True when the next request should carry a signature."""
        return bool(auth and self.signed_requests and not self.token and self.access_key and self.secret_key and self.tenant_id)

    def _sign(self, prepared: requests.PreparedRequest) -> None:
        """Add x-date, x-nonce, and x-signature to a prepared request.

        Signs ``prepared.path_url`` (the path and query exactly as requests sends them) and the exact body bytes.
        """
        date = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
        nonce = uuid.uuid4().hex
        body = prepared.body
        if body is not None and not isinstance(body, (bytes, str)):
            raise ValueError("Signed requests need an in-memory body.")
        prepared.headers["x-date"] = date
        prepared.headers["x-nonce"] = nonce
        prepared.headers["x-signature"] = sign_request(self.secret_key or "", prepared.method or "GET", prepared.path_url,
                                                       self.tenant_id or "", date, nonce, body)

    def request(
        self,
        method: str,
        path: str,
        query: Optional[Mapping[str, Any]] = None,
        body: Any = None,
        headers: Optional[Mapping[str, str]] = None,
        raw: bool = False,
        auth: bool = True,
        form: Optional[Mapping[str, Any]] = None,
        stream: bool = False,
        timeout: Optional[Union[float, Tuple[float, float]]] = None,
    ) -> Any:
        """Send a request to any route. Resource methods are built on this.

        Args:
            method: HTTP method.
            path: Path beginning with ``/`` (for example ``/v1.0/api/topics``).
            query: Query filters; snake_case names become camelCase and None values are dropped.
            body: JSON body (dict, list, or Model); keys become camelCase.
            headers: Extra headers.
            raw: Return the ``requests.Response`` instead of decoded JSON.
            auth: Send the client's credentials (False for anonymous public routes).
            form: Form fields to send as ``application/x-www-form-urlencoded`` instead of JSON.
            stream: Stream the response body (use with ``raw=True``; the caller closes the response).
            timeout: Overrides the client timeout: seconds, or a ``(connect, read)`` tuple.

        Returns:
            Decoded JSON (None for an empty body or HTTP 204), or the response when ``raw`` is True.

        Raises:
            MyMorningNewsError: A typed subclass for any non-2xx status, or status 0 when the server is unreachable.
        """
        url = self.base_url + (path if path.startswith("/") else "/" + path)
        send_headers: Dict[str, str] = {"Accept": "application/json", "User-Agent": USER_AGENT}
        if auth:
            send_headers.update(self._auth_headers())
        if headers:
            send_headers.update(headers)
        data: Any = None
        if form is not None:
            data = {k: str(_format_scalar(v)) for k, v in form.items() if v is not None}
            send_headers["Content-Type"] = "application/x-www-form-urlencoded"
        elif body is not None:
            data = json.dumps(_wire_body(body)).encode("utf-8")
            send_headers["Content-Type"] = "application/json"
        try:
            prepared = self._session.prepare_request(requests.Request(method.upper(), url, params=_wire_query(query), data=data, headers=send_headers))
            if self._signing(auth):
                self._sign(prepared)
            env = self._session.merge_environment_settings(prepared.url, {}, stream, None, None)
            resp = self._session.send(prepared, timeout=timeout if timeout is not None else self.timeout, **env)
        except requests.RequestException as ex:
            raise MyMorningNewsError("Could not reach " + self.base_url + ": " + str(ex), status=0, code="ConnectionError") from ex
        if resp.status_code >= 400:
            try:
                raise _error_from_response(resp)
            finally:
                if stream:
                    resp.close()
        if raw:
            return resp
        if resp.status_code == 204 or not resp.content:
            return None
        ctype = resp.headers.get("Content-Type", "")
        if "json" in ctype:
            return resp.json()
        try:
            return resp.json()
        except ValueError:
            return resp.text

    # Authentication

    def login(self, email: str, password: str, tenant_id: Optional[str] = None) -> TokenResponse:
        """Sign in with email and password and keep the session token.

        Args:
            email: The user's email.
            password: The user's password.
            tenant_id: Tenant to sign in to. Without it a single matching tenant signs in.

        Returns:
            The :class:`TokenResponse`; the client now sends its token.

        Raises:
            TenantSelectionRequired: The email belongs to several tenants (see ``tenants``).
            AuthenticationError: The credentials are wrong.
            EmailNotVerifiedError: The account signed up publicly and has not confirmed its email (403).
            RateLimitError: Too many sign-in attempts.
        """
        headers = {"x-email": email, "x-password": password}
        tenant = tenant_id or self.tenant_id
        if tenant:
            headers["x-tenant-guid"] = tenant
        return self._issue(headers)

    def login_with_access_key(self, access_key: str, secret_key: str, tenant_id: Optional[str] = None) -> TokenResponse:
        """Exchange an access key and secret for a session token and keep it.

        Args:
            access_key: Access key.
            secret_key: Secret key.
            tenant_id: The credential's tenant (defaults to the client's ``tenant_id``).

        Returns:
            The :class:`TokenResponse`.
        """
        headers = {"x-access-key": access_key, "x-secret-key": secret_key}
        tenant = tenant_id or self.tenant_id
        if tenant:
            headers["x-tenant-guid"] = tenant
        return self._issue(headers)

    def _issue(self, headers: Dict[str, str]) -> TokenResponse:
        """POST /v1.0/token with credential headers and store the token."""
        try:
            data = self.request("POST", "/v1.0/token", headers=headers, auth=False)
        except ConflictError as ex:
            if isinstance(ex.body, dict) and isinstance(ex.body.get("tenants"), list):
                choices = [TenantChoice.from_dict(t) for t in ex.body["tenants"]]
                raise TenantSelectionRequired(choices, status=409, code=ex.code or "TenantSelectionRequired", request_id=ex.request_id, body=ex.body) from None
            raise
        result = TokenResponse.from_dict(data)
        self.token = result.token
        self.tenant_id = result.tenant_id or self.tenant_id
        self.last_login = result
        return result

    def logout(self) -> None:
        """Revoke the current session token and forget it. Does nothing when no token is set."""
        if not self.token:
            return
        try:
            self.request("DELETE", "/v1.0/token")
        finally:
            self.token = None

    def refresh(self) -> TokenResponse:
        """Exchange the current token for a fresh one and keep it."""
        result = TokenResponse.from_dict(self.request("POST", "/v1.0/token/refresh"))
        if result.token:
            self.token = result.token
        return result

    def refresh_with_token(self, refresh_token: str) -> TokenResponse:
        """Exchange a mobile refresh token for a new session token (and rotated refresh token)."""
        result = TokenResponse.from_dict(self.request("POST", "/v1.0/token/refresh-token", body={"refreshToken": refresh_token}, auth=False))
        if result.token:
            self.token = result.token
        return result

    def validate_token(self) -> TokenDetails:
        """Validate the current credentials (``GET /v1.0/token``)."""
        return TokenDetails.from_dict(self.request("GET", "/v1.0/token"))

    def token_details(self) -> TokenDetails:
        """Token and principal details, including issue and expiry times."""
        return TokenDetails.from_dict(self.request("GET", "/v1.0/token/details"))

    # Server and profile

    def health(self) -> Health:
        """Server health (anonymous)."""
        return Health.from_dict(self.request("GET", API + "/health", auth=False))

    def server_info(self) -> ServerInfo:
        """Server information and bundled tool links."""
        return ServerInfo.from_dict(self.request("GET", API + "/server-info"))

    def client_config(self) -> ClientConfig:
        """Public client configuration (anonymous)."""
        return ClientConfig.from_dict(self.request("GET", API + "/client-config", auth=False))

    def me(self) -> User:
        """Read my profile."""
        return User.from_dict(self.request("GET", API + "/me"))

    def update_me(self, body: Optional[Mapping[str, Any]] = None, **fields: Any) -> User:
        """Update my profile (``first_name``, ``last_name``, ``time_zone``, ``locale``)."""
        return User.from_dict(self.request("PUT", API + "/me", body=_merge(body, fields)))

    def change_password(self, current_password: str, new_password: str) -> Any:
        """Change my password."""
        return self.request("PUT", API + "/me/password", body={"currentPassword": current_password, "newPassword": new_password})

    # Pagination

    def iterate(self, list_fn: Callable[..., Any], **filters: Any) -> Iterator[Any]:
        """Iterate every item across all pages of a list method. See :func:`iterate`."""
        return iterate(list_fn, **filters)


def iterate(list_fn: Callable[..., Any], page_size: int = 100, max_pages: Optional[int] = None, **filters: Any) -> Iterator[Any]:
    """Yield every item from a paged list method, fetching pages as needed.

    Args:
        list_fn: A list method such as ``client.topics.list``. Methods that return a plain list are yielded once.
        page_size: Page size to request (default 100, clamped to 1..1000).
        max_pages: Optional cap on pages fetched.
        **filters: Filters passed to every call (``page_number`` is managed here).

    Yields:
        Each item in order.
    """
    filters.pop("page_number", None)
    filters.pop("pageNumber", None)
    size = max(1, min(1000, int(page_size)))
    number = 1
    fetched = 0
    while True:
        result = list_fn(page_number=number, page_size=size, **filters)
        if isinstance(result, list):
            yield from result
            return
        items = list(result.items)
        yield from items
        fetched += 1
        if not items or number >= result.total_pages or (max_pages is not None and fetched >= max_pages):
            return
        number += 1


__all__ = [
    "MyMorningNewsClient", "MyMorningNewsError", "BadRequestError", "AuthenticationError", "PermissionDeniedError",
    "NotFoundError", "ConflictError", "GoneError", "RangeNotSatisfiableError", "RateLimitError", "ServerError",
    "TenantSelectionRequired", "WaitTimeoutError", "EmailNotVerifiedError", "PlanLimitError", "CapReachedError", "SetupRequiredError", "Model", "Page", "Download", "iterate", "to_camel", "to_snake",
    "sign_request", "string_to_sign", "body_sha256", "SIGNATURE_ALGORITHM", "RunEvent", "parse_sse", "RUN_STAGES",
    "RESUMABLE_RUN_STAGES",
    "DEFAULT_BASE_URL", "TERMINAL_RUN_STATUSES", "__version__",
]

__all__ += sorted(_MODEL_REGISTRY)
__all__ += sorted(n for n, o in list(globals().items()) if isinstance(o, type) and issubclass(o, Enum) and o is not Enum and o.__module__ == __name__)
