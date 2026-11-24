#!/usr/bin/env python3
"""Read the route registrars in src/MyMorningNews.Server/Routes and report, for every registered route, what the
OpenAPI document cannot say on its own: whether the route runs before or after authentication, the permission
(RequestTypeEnum) its handler checks, the query values and body type the handler reads, and the status codes the
handler can send.

The generators use it for the auth column and for consistency warnings. Run it directly to print what it found:
    python3 src/Tools/apidocs/routescan.py [--json]

(c) 2026 Lattice AI LLC
"""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
ROUTES = ROOT / "src" / "MyMorningNews.Server" / "Routes"

REG_RE = re.compile(r"webserver\.Routes\.(Pre|Post)Authentication\.(Static|Parameter)\.Add\(")
VAR_RE = re.compile(r"(?:const\s+)?string\s+(\w+)\s*=\s*(\"[^\"]*\"|\w+)\s*;")
FOREACH_RE = re.compile(r"foreach\s*\(\s*string\s+(\w+)\s+in\s+new\[\]\s*\{([^}]*)\}\s*\)")
METHOD_RE = re.compile(r"(?:private|protected|public|internal)\s+(?:static\s+)?(?:async\s+)?(?:Task(?:<[^>]+>)?|void|bool|string\??|[A-Z]\w*(?:<[^>]+>)?\??)\s+(\w+)\s*\(")


def _match_close(text, start, open_ch, close_ch):
    """Index of the bracket that closes the one at start, skipping string and char literals."""
    depth = 0
    i = start
    n = len(text)
    while i < n:
        c = text[i]
        if c == '"':
            verbatim = i > 0 and text[i - 1] == "@"
            i += 1
            while i < n:
                if verbatim and text[i] == '"' and i + 1 < n and text[i + 1] == '"':
                    i += 2
                    continue
                if not verbatim and text[i] == "\\":
                    i += 2
                    continue
                if text[i] == '"':
                    break
                i += 1
        elif c == "'":
            i += 1
            while i < n and text[i] != "'":
                i += 2 if text[i] == "\\" else 1
        elif c == open_ch:
            depth += 1
        elif c == close_ch:
            depth -= 1
            if depth == 0:
                return i
        i += 1
    return -1


def _split_args(text):
    """Split a call's argument text on top-level commas."""
    args = []
    depth = 0
    cur = []
    i = 0
    while i < len(text):
        c = text[i]
        if c == '"':
            j = i + 1
            while j < len(text) and text[j] != '"':
                j += 2 if text[j] == "\\" else 1
            cur.append(text[i:j + 1])
            i = j + 1
            continue
        if c in "([{":
            depth += 1
        elif c in ")]}":
            depth -= 1
        if c == "," and depth == 0:
            args.append("".join(cur).strip())
            cur = []
        else:
            cur.append(c)
        i += 1
    if cur:
        args.append("".join(cur).strip())
    return args


def _eval_path(expr, env):
    """Evaluate a concatenation of string literals and known variables into one or more paths."""
    results = [""]
    for part in [p.strip() for p in expr.split("+")]:
        if part.startswith('"') and part.endswith('"'):
            values = [part[1:-1]]
        elif part in env:
            v = env[part]
            values = v if isinstance(v, list) else [v]
        else:
            return None
        results = [r + v for r in results for v in values]
    return results


def _methods(text):
    """Map of method name to body text for every method in a file (first definition wins)."""
    out = {}
    for m in METHOD_RE.finditer(text):
        name = m.group(1)
        paren = text.index("(", m.end() - 1)
        close = _match_close(text, paren, "(", ")")
        if close < 0:
            continue
        rest = text[close + 1:close + 200].lstrip()
        if rest.startswith("=>"):
            end = text.find(";", close)
            body = text[close + 1:end + 1]
        else:
            brace = text.find("{", close)
            semi = text.find(";", close)
            if brace < 0 or (0 <= semi < brace):
                continue
            end = _match_close(text, brace, "{", "}")
            body = text[brace:end + 1]
        out.setdefault(name, body)
    return out


def _constants():
    """Public string constants from MyMorningNews.Core/Constants.cs, as Constants.Name."""
    text = (ROOT / "src" / "MyMorningNews.Core" / "Constants.cs").read_text(encoding="utf-8")
    return {"Constants." + m.group(1): m.group(2) for m in re.finditer(r"public\s+const\s+string\s+(\w+)\s*=\s*\"([^\"]*)\"\s*;", text)}


def _collect(body, methods, seen, depth):
    """Body text of a handler plus the same-class helpers it calls with ctx, to a small depth."""
    parts = [body]
    if depth <= 0:
        return parts
    for name in re.findall(r"\b([A-Z]\w*)\(\s*ctx\b", body):
        if name in seen or name not in methods:
            continue
        seen.add(name)
        parts += _collect(methods[name], methods, seen, depth - 1)
    return parts


def _analyze(text):
    """Facts a handler's code reveals: permissions, query names, body type, and status codes."""
    facts = {"permissions": [], "query": [], "queryTypes": {}, "body": None, "optionalBody": False, "statuses": {}, "sendsJson": [], "artifact": False, "html": False, "sse": False}

    def status(code, label):
        facts["statuses"].setdefault(str(code), [])
        if label and label not in facts["statuses"][str(code)]:
            facts["statuses"][str(code)].append(label)

    for p in re.findall(r"\b\w+Async\(\s*ctx\s*,\s*RequestTypeEnum\.(\w+)", text):
        if p not in facts["permissions"]:
            facts["permissions"].append(p)
    if facts["permissions"]:
        status(403, "Forbidden")
    kinds = {"": "string", "Int": "integer", "Bool": "boolean", "Date": "date-time", "Long": "integer", "Double": "number"}
    for kind, q in re.findall(r"\bQuery(Int|Bool|Date|Enum<\w+>|Long|Double)?\(\s*ctx\s*,\s*\"(\w+)\"", text):
        if q not in facts["query"]:
            facts["query"].append(q)
            facts["queryTypes"][q] = kinds.get(kind, kind[5:-1] if kind.startswith("Enum<") else "string")
    if "ListScope(ctx)" in text:
        for q, t in (("userId", "string"), ("mine", "boolean")):
            if q not in facts["query"]:
                facts["query"].append(q)
                facts["queryTypes"][q] = t
    if "ApplyPaging(ctx" in text:
        for q in ("pageNumber", "pageSize", "sortBy", "sortDirection", "search", "fromUtc", "toUtc"):
            if q not in facts["query"]:
                facts["query"].append(q)
    m = re.search(r"ReadBodyAsync<(.+?)>\(\s*ctx", text)
    if m:
        facts["body"] = m.group(1)
        status(400, "BadRequest")
    m = re.search(r"ReadOptionalBodyAsync<(.+?)>\(\s*ctx", text)
    if m and not facts["body"]:
        facts["body"] = m.group(1)
        facts["optionalBody"] = True
    for code in re.findall(r"SendJsonAsync\(\s*ctx\s*,\s*(\d{3})", text):
        status(code, None)
    for code, label in re.findall(r"SendErrorAsync\(\s*ctx\s*,\s*(\d{3})\s*,\s*\"(\w+)\"", text):
        status(code, label)
    for code in re.findall(r"Send(?:Html|Text|Xml|Bytes)\w*Async\(\s*ctx\s*,\s*(\d{3})", text):
        status(code, None)
        facts["html"] = True
    for code in re.findall(r"StatusCode\s*=\s*(\d{3})", text):
        status(code, None)
    if "SendNotFoundAsync(" in text or "ResolveTenantAsync(" in text:
        status(404, "NotFound")
    if "RequireTenantAsync(" in text:
        status(400, "TenantRequired")
    if "SendArtifactAsync(" in text:
        facts["artifact"] = True
        for code, label in (("200", None), ("206", None), ("410", "ArtifactExpired"), ("416", "RangeNotSatisfiable")):
            status(code, label)
    if "text/event-stream" in text:
        facts["sse"] = True
    for expr_start in [mm.end() for mm in re.finditer(r"SendJsonAsync\(\s*ctx\s*,\s*2\d\d\s*,", text)]:
        close = _match_close("(" + text[expr_start:], 0, "(", ")")
        if close > 0:
            facts["sendsJson"].append(re.sub(r"\s+", " ", text[expr_start:expr_start + close - 1]).strip()[:160])
    return facts


def scan():
    """Every registered route as a dict keyed by "METHOD /path"."""
    routes = {}
    for path in sorted(ROUTES.glob("*.cs")):
        text = path.read_text(encoding="utf-8")
        methods = _methods(text)
        env = dict(_constants())
        for m in re.finditer(r"private\s+const\s+string\s+(\w+)\s*=\s*\"([^\"]*)\"\s*;", text):
            env[m.group(1)] = m.group(2)
        events = []
        for m in VAR_RE.finditer(text):
            events.append((m.start(), "var", m))
        for m in FOREACH_RE.finditer(text):
            events.append((m.start(), "foreach", m))
        for m in REG_RE.finditer(text):
            events.append((m.start(), "reg", m))
        events.sort(key=lambda e: e[0])
        for pos, kind, m in events:
            if kind == "var":
                value = m.group(2)
                if value.startswith('"'):
                    env[m.group(1)] = value[1:-1]
                elif value in env:
                    env[m.group(1)] = env[value]
                continue
            if kind == "foreach":
                env[m.group(1)] = [v.strip().strip('"') for v in m.group(2).split(",") if v.strip()]
                continue
            open_paren = m.end() - 1
            close = _match_close(text, open_paren, "(", ")")
            args = _split_args(text[open_paren + 1:close])
            verb = args[0].replace("HttpMethod.", "")
            paths = _eval_path(args[1], env)
            handler_expr = args[2]
            hm = re.search(r"=>\s*(\w+)\(", handler_expr)
            handler = hm.group(1) if hm else handler_expr.strip()
            twin = re.search(r"EnumerateAsync\(ctx,\s*(\w+)\)", handler_expr)
            if twin:
                handler = twin.group(1)
            line = text.count("\n", 0, pos) + 1
            if paths is None:
                routes["?" + verb + " " + args[1]] = {"error": "unresolved path expression", "file": path.name, "line": line}
                continue
            body = methods.get(handler, "")
            facts = _analyze("\n".join(_collect(body, methods, {handler}, 3)))
            meta = text[open_paren:close]
            for p in paths:
                key = verb + " " + p
                routes[key] = {
                    "method": verb,
                    "path": p,
                    "stage": m.group(1),
                    "file": path.relative_to(ROOT).as_posix(),
                    "line": line,
                    "handler": handler,
                    "declaredBody": (re.search(r"Body<(.+?)>\(\)", meta).group(1) if re.search(r"Body<(.+?)>\(\)", meta) else None),
                    "paged": "Paged(" in meta,
                    "declaredQuery": re.findall(r"QueryParam\(\s*\"(\w+)\"", meta) + (["pageNumber", "pageSize", "sortBy", "sortDirection", "search", "fromUtc", "toUtc", "order", "createdAfter", "createdBefore"] if "Paged(" in meta else []),
                    **facts,
                }
    return routes


def permissions():
    """RequestTypeEnum name to (resource, operation, audience) from OperationScopeMap.cs, where audience is
    "anonymous", "authenticated" (any signed-in principal), or "granted" (needs the permission)."""
    text = (ROOT / "src" / "MyMorningNews.Core" / "Security" / "OperationScopeMap.cs").read_text(encoding="utf-8")
    out = {}
    for m in re.finditer(r"RequestTypeEnum\.(\w+)\s*,\s*new\s+RequiredPermission\(\s*ResourceTypeEnum\.(\w+)\s*,\s*OperationTypeEnum\.(\w+)([^)]*)\)", text):
        extra = m.group(4)
        audience = "anonymous" if "anonymous: true" in extra else "authenticated" if "anyAuthenticated: true" in extra else "granted"
        out[m.group(1)] = (m.group(2), m.group(3), audience)
    return out


def main():
    routes = scan()
    if "--json" in sys.argv:
        print(json.dumps(routes, indent=2))
        return
    for key, r in routes.items():
        if "error" in r:
            print("{0}: {1} ({2}:{3})".format(key, r["error"], r["file"], r["line"]))
            continue
        print("{0:<70} {1:<4} {2:<28} {3}".format(key, r["stage"], ",".join(r["permissions"]) or "-", ",".join(sorted(r["statuses"].keys()))))
    print(len(routes), "routes")


if __name__ == "__main__":
    main()
