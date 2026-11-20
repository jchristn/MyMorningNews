#!/usr/bin/env python3
"""Shared pieces of the REST reference and Postman generators: getting the OpenAPI document, reading the supplemental
map (supplement.json), joining both with what routescan finds in the route handlers, and describing types.

The OpenAPI document comes from, in order of preference:
    --openapi FILE      a saved document
    --launch            build and run the server from this tree against a scratch database, then stop it
    --url URL           a running server (default http://127.0.0.1:12100/openapi.json, or MYMORNINGNEWS_OPENAPI_URL)

(c) 2026 Lattice AI LLC
"""
import contextlib
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[2]
SUPPLEMENT = HERE / "supplement.json"
DEFAULT_URL = "http://127.0.0.1:12100/openapi.json"
PARAM_NOTES = {
    "pageNumber": "One-based page. Default 1.",
    "pageSize": "Items per page. Default 25, maximum 1000.",
    "sortBy": "camelCase property to sort by. Unknown values use the default sort.",
    "sortDirection": "`Descending` (default) or `Ascending`.",
    "search": "Free text over the resource's searchable columns.",
    "fromUtc": "Created at or after this time (inclusive).",
    "toUtc": "Created before this time (exclusive).",
}
PAGING = ["pageNumber", "pageSize", "sortBy", "sortDirection", "search", "fromUtc", "toUtc"]

sys.path.insert(0, str(HERE))
sys.path.insert(0, str(ROOT / "sdk" / "csharp" / "tools"))
import routescan  # noqa: E402
import generate_models  # noqa: E402


def add_source_arguments(parser):
    """Add the options that choose where the OpenAPI document comes from."""
    parser.add_argument("--check", action="store_true", help="exit 1 when the output is out of date instead of writing it")
    parser.add_argument("--url", default=os.environ.get("MYMORNINGNEWS_OPENAPI_URL", DEFAULT_URL), help="OpenAPI document of a running server")
    parser.add_argument("--openapi", help="read the OpenAPI document from a file")
    parser.add_argument("--launch", action="store_true", help="run the server from source against a scratch database")
    parser.add_argument("--port", type=int, default=12199, help="port for --launch (default 12199)")
    parser.add_argument("--db-host", default="127.0.0.1", help="PostgreSQL host for --launch")
    parser.add_argument("--db-port", default="12102", help="PostgreSQL port for --launch")
    parser.add_argument("--db-user", default="postgres", help="PostgreSQL user for --launch")
    parser.add_argument("--db-password", default="password", help="PostgreSQL password for --launch")
    parser.add_argument("--db-name", default="mymorningnews_apidocs", help="scratch database for --launch (created when missing)")


def _fetch(url, attempts=1):
    last = None
    for i in range(attempts):
        try:
            with urllib.request.urlopen(url, timeout=10) as r:
                return json.loads(r.read().decode("utf-8"))
        except (urllib.error.URLError, ConnectionError, TimeoutError) as ex:
            last = ex
            time.sleep(1)
    raise SystemExit("could not read {0}: {1}".format(url, last))


@contextlib.contextmanager
def _launched(args):
    project = ROOT / "src" / "MyMorningNews.Server"
    subprocess.run(["dotnet", "build", str(project), "-f", "net10.0", "-v", "q", "-nologo"], check=True, stdout=subprocess.DEVNULL)
    work = tempfile.mkdtemp(prefix="mmn-apidocs-")
    env = dict(os.environ)
    env.update({
        "MYMORNINGNEWS_DB_SERVER": args.db_host, "MYMORNINGNEWS_DB_PORT": str(args.db_port), "MYMORNINGNEWS_DB_DATABASE": args.db_name,
        "MYMORNINGNEWS_DB_USERNAME": args.db_user, "MYMORNINGNEWS_DB_PASSWORD": args.db_password,
        "MYMORNINGNEWS_REST_HOSTNAME": "127.0.0.1", "MYMORNINGNEWS_REST_PORT": str(args.port),
        "MYMORNINGNEWS_SCHEDULER_ENABLED": "false", "MYMORNINGNEWS_LEARNING_ENABLED": "false", "MYMORNINGNEWS_TTS_ENABLED": "false",
        "MYMORNINGNEWS_EMAIL_PROVIDER": "File", "MYMORNINGNEWS_PUSH_ENABLED": "false",
    })
    log = open(os.path.join(work, "server.log"), "w")
    proc = subprocess.Popen(["dotnet", "run", "--project", str(project), "-f", "net10.0", "--no-build"], cwd=work, env=env, stdout=log, stderr=subprocess.STDOUT)
    try:
        base = "http://127.0.0.1:{0}".format(args.port)
        for i in range(90):
            if proc.poll() is not None:
                break
            try:
                with urllib.request.urlopen(base + "/v1.0/api/health", timeout=2):
                    break
            except Exception:
                time.sleep(1)
        if proc.poll() is not None:
            log.close()
            raise SystemExit("the server exited early; see " + os.path.join(work, "server.log"))
        yield base + "/openapi.json"
    finally:
        proc.terminate()
        try:
            proc.wait(timeout=20)
        except subprocess.TimeoutExpired:
            proc.kill()
        log.close()
        shutil.rmtree(work, ignore_errors=True)


def load_openapi(args):
    """The OpenAPI document named by the source options."""
    if args.openapi:
        return json.loads(pathlib.Path(args.openapi).read_text(encoding="utf-8"))
    if args.launch:
        with _launched(args) as url:
            return _fetch(url, 3)
    return _fetch(args.url, 3)


def load_supplement():
    """The checked-in supplemental map."""
    return json.loads(SUPPLEMENT.read_text(encoding="utf-8"))


def schema_name(schema):
    """Component name of a $ref schema, or None."""
    ref = (schema or {}).get("$ref", "")
    return ref.split("/")[-1] if ref else None


def describe_schema(schema):
    """A short type label for an OpenAPI schema (string, integer, date-time, enum values, arrays, references)."""
    if not schema:
        return "any"
    name = schema_name(schema)
    if name:
        return name
    t = schema.get("type")
    if schema.get("enum"):
        return "enum (" + ", ".join("`" + str(v) + "`" for v in schema["enum"]) + ")"
    if t == "array":
        return "array of " + describe_schema(schema.get("items"))
    if t == "string" and schema.get("format") == "date-time":
        return "date-time (ISO 8601 UTC)"
    if t == "integer":
        return "integer"
    if t == "number":
        return "number"
    if t == "object":
        if schema.get("additionalProperties"):
            return "object (map of " + describe_schema(schema["additionalProperties"]) + ")"
        return "object"
    return t or "any"


class Operation(object):
    """One route: what OpenAPI declares, what the supplement adds, and what the handler scan found."""

    def __init__(self, method, path, op, extra, scan, perms):
        self.method = method.upper()
        self.path = path
        self.key = self.method + " " + path
        self.tag = (op.get("tags") or ["OpenAPI"])[0]
        self.summary = op.get("summary") or self.key
        self.description = op.get("description") or ""
        params = op.get("parameters") or []
        self.path_params = [p for p in params if p.get("in") == "path"]
        self.query_params = [p for p in params if p.get("in") == "query"]
        body = ((op.get("requestBody") or {}).get("content") or {}).get("application/json", {}).get("schema")
        self.body_schema = body
        self.body_name = schema_name(body) or (schema_name(body.get("items")) if body and body.get("type") == "array" else None)
        self.body_is_array = bool(body and body.get("type") == "array")
        self.paged = "pageNumber" in [p["name"] for p in self.query_params]
        self.extra = extra or {}
        self.response = self.extra.get("response", "?")
        self.status = self.extra.get("status", {})
        self.notes = self.extra.get("notes", "")
        self.scan = scan or {}
        self.stage = self.scan.get("stage", "Pre" if self.tag == "OpenAPI" else "Post")
        self.request_types = self.scan.get("permissions", [])
        self.permissions = [(rt,) + perms[rt] for rt in self.request_types if rt in perms]
        declared = set(p["name"] for p in self.query_params)
        types = self.scan.get("queryTypes", {})
        paging_types = {"pageNumber": "integer", "pageSize": "integer", "sortDirection": "SortDirectionEnum", "fromUtc": "date-time", "toUtc": "date-time"}
        self.undeclared_query = [(q, types.get(q) or paging_types.get(q, "string")) for q in self.scan.get("query", []) if q not in declared]

    @property
    def anonymous(self):
        return self.stage == "Pre"

    def auth_text(self):
        """One line on who may call the route."""
        if self.anonymous:
            if "{token}" in self.path:
                return "None. The `{token}` path segment is the credential."
            if self.path == "/v1.0/token":
                return "None. The login headers are the credential."
            if self.path == "/v1.0/token/refresh-token":
                return "None. The refresh token in the body is the credential."
            return "None."
        if not self.permissions:
            return "Any authenticated principal."
        parts = []
        for rt, resource, operation, audience in self.permissions:
            if audience == "authenticated":
                parts.append("any authenticated principal (`{0}`)".format(rt))
            elif audience == "anonymous":
                parts.append("anyone (`{0}`)".format(rt))
            else:
                parts.append("`{0}:{1}` (`{2}`)".format(resource, operation, rt))
        text = "; ".join(dict.fromkeys(parts))
        return text[0].upper() + text[1:] + "."


def operations(spec, supplement):
    """Every operation in tag order (supplement "tags"), then in document order."""
    scan = routescan.scan()
    perms = routescan.permissions()
    routes = supplement.get("routes", {})
    ops = []
    for path, methods in spec.get("paths", {}).items():
        for method, op in methods.items():
            key = method.upper() + " " + path
            ops.append(Operation(method, path, op, routes.get(key), scan.get(key), perms))
    order = list(supplement.get("tags", {}).keys())
    ops.sort(key=lambda o: order.index(o.tag) if o.tag in order else len(order))
    return ops


def problems(ops, supplement):
    """Consistency problems: routes missing from the supplement or the tag list, stale supplement entries, and
    handlers that read query values or bodies the OpenAPI metadata does not declare."""
    out = []
    keys = set(o.key for o in ops)
    tags = supplement.get("tags", {})
    for o in ops:
        where = "{0}:{1}".format(o.scan.get("file", "?"), o.scan.get("line", "?"))
        if not o.extra:
            out.append("{0}: no entry in supplement.json routes ({1})".format(o.key, where))
        if o.tag not in tags:
            out.append("{0}: tag {1} has no description in supplement.json tags".format(o.key, o.tag))
        if not o.scan:
            continue
        declared = set(p["name"] for p in o.query_params)
        used = set(o.scan.get("query", []))
        missing = sorted(used - declared)
        if missing:
            out.append("{0}: handler reads query {1} but the OpenAPI metadata does not declare it ({2})".format(o.key, ", ".join(missing), where))
        if o.scan.get("body") and not o.body_schema:
            out.append("{0}: handler reads a {1} body but the OpenAPI metadata declares none ({2})".format(o.key, o.scan["body"], where))
    for key in supplement.get("routes", {}):
        if key not in keys:
            out.append("{0}: supplement.json entry has no matching route".format(key))
    return out


def models():
    """C# types under src/MyMorningNews.Core, keyed by name (generate_models.TypeInfo)."""
    found, dupes = generate_models.scan(str(ROOT / "src" / "MyMorningNews.Core"))
    return found


def camel(name):
    return name[0].lower() + name[1:] if name else name


GENERIC_RE = re.compile(r"^(\w+)<(.+)>$")


def type_refs(type_str):
    """Type names inside a C# type string (List<Topic> gives List and Topic)."""
    return re.findall(r"[A-Za-z_]\w*", type_str or "")


def csharp_label(type_str):
    """Readable label for a C# property type."""
    t = (type_str or "").replace("?", "").strip()
    t = re.sub(r"\b(?:[A-Za-z_]\w*\.)+([A-Za-z_]\w*)", r"\1", t)
    m = re.match(r"^(?:List|IList|IReadOnlyList|IEnumerable|ICollection|HashSet)<(.+)>$", t)
    if m:
        return "array of " + csharp_label(m.group(1))
    m = re.match(r"^(?:Dictionary|IDictionary|IReadOnlyDictionary|SortedDictionary)<\s*([^,]+)\s*,\s*(.+)>$", t)
    if m:
        return "object (map of " + csharp_label(m.group(2)) + ")"
    simple = {"string": "string", "String": "string", "int": "integer", "long": "integer", "short": "integer", "byte": "integer",
              "double": "number", "float": "number", "decimal": "number", "bool": "boolean", "DateTime": "date-time",
              "DateTimeOffset": "date-time", "TimeSpan": "string (duration)", "Guid": "string", "object": "any",
              "JsonElement": "any JSON", "JsonObject": "object", "JsonNode": "any JSON", "Int32": "integer", "Int64": "integer", "Boolean": "boolean"}
    return simple.get(t, t)


def unwrap_response(resp):
    """Model names a response type string mentions, in order (EnumerationResult<Topic> gives EnumerationResult, Topic)."""
    if isinstance(resp, dict):
        names = []
        for v in resp.values():
            names += unwrap_response(v)
        return names
    if not isinstance(resp, str) or "/" in resp or resp in ("none", "file", "audio", "?"):
        return []
    return [n for n in type_refs(resp) if n not in ("List", "Dictionary", "string", "int", "long", "bool", "double", "object")]


def response_label(resp):
    """Markdown label for a response (type link, content type, or none)."""
    if isinstance(resp, dict):
        return "object `{ " + ", ".join("{0}: {1}".format(k, v) for k, v in resp.items()) + " }`"
    if resp == "none":
        return "No body"
    if resp == "file":
        return "File bytes (the stored file's content type)"
    if resp == "audio":
        return "Audio bytes"
    if "/" in resp:
        return "`" + resp + "`"
    return "`" + resp + "`"


def path_variable(path, name, supplement):
    """Postman variable for a path parameter: {id} takes its name from the segment before it (topics gives topicId)."""
    overrides = supplement.get("pathVariables", {})
    for prefix, var in overrides.items():
        if prefix.endswith("{" + name + "}") and path.startswith(prefix):
            return var
    if name != "id":
        return name
    segments = path.split("/")
    idx = segments.index("{id}")
    seg = segments[idx - 1]
    words = seg.split("-")
    last = words[-1]
    if last.endswith("ies"):
        last = last[:-3] + "y"
    elif last.endswith("ses") or last.endswith("xes"):
        last = last[:-2]
    elif last.endswith("s") and not last.endswith("ss"):
        last = last[:-1]
    words[-1] = last
    return words[0] + "".join(w.capitalize() for w in words[1:]) + "Id"


def substitute(value, samples):
    """Replace {{var}} placeholders in a JSON-able value with sample values (for the Markdown examples)."""
    if isinstance(value, str):
        return re.sub(r"\{\{(\w+)\}\}", lambda m: str(samples.get(m.group(1), "<" + m.group(1) + ">")), value)
    if isinstance(value, list):
        return [substitute(v, samples) for v in value]
    if isinstance(value, dict):
        return {k: substitute(v, samples) for k, v in value.items()}
    return value


def write_or_check(path, text, check, label):
    """Write text to path, or with check compare and exit 1 when it differs."""
    current = path.read_text(encoding="utf-8") if path.exists() else None
    if check:
        if current != text:
            print("{0} is out of date; run {1}".format(path.relative_to(ROOT).as_posix(), label))
            sys.exit(1)
        print("{0} is current".format(path.relative_to(ROOT).as_posix()))
        return False
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return True
