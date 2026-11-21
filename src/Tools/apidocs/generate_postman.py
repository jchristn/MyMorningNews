#!/usr/bin/env python3
"""Regenerate assets/postman/MyMorningNews.postman_collection.json (Postman collection v2.1) from the server's OpenAPI
document, the route handlers (routescan.py), and the supplemental map (supplement.json).

Usage (from the repository root):
    python3 src/Tools/apidocs/generate_postman.py              # read http://127.0.0.1:12100/openapi.json
    python3 src/Tools/apidocs/generate_postman.py --launch     # run the server from this tree first (needs PostgreSQL)
    python3 src/Tools/apidocs/generate_postman.py --check      # exit 1 when the collection is out of date

The collection has a folder per OpenAPI tag, a request per route with its description, auth, path variables, query
parameters, and an example body, plus an Authentication examples folder (access key, signed request, administration key).
(c) 2026 Lattice AI LLC
"""
import argparse
import json
import re
import sys

import common

OUT = common.ROOT / "assets" / "postman" / "MyMorningNews.postman_collection.json"
SCHEMA = "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
COLLECTION_ID = "6d1f3b0e-6a8f-4f5e-9c3a-2f1d7a4b9e10"
DOCS = "https://github.com/jchristn/MyMorningNews/blob/main/REST_API.md"
NOAUTH = {"type": "noauth"}

VARIABLES = [
    ("baseUrl", "http://127.0.0.1:12100", "Server address. The Docker stack publishes the REST server on host port 12100."),
    ("tenantId", "default", "Tenant ID, sent as x-tenant-guid and used in control-plane paths. The seeded tenant is default."),
    ("email", "admin@mymorningnews", "Email for Token > Issue a session token. The default is the local development administrator."),
    ("password", "password", "Password for Token > Issue a session token. Change it on any server other people can reach."),
    ("token", "", "Session token. Issue a session token sets it; every request without its own auth sends it as a bearer token."),
    ("refreshToken", "", "Mobile refresh token, set when you sign in with x-client: mobile."),
    ("adminApiKey", "mymorningnewsadmin", "Administration key (Auth.AdminApiKey) for requests that use x-api-key."),
    ("accessKey", "", "Access key of a credential. Creating a credential sets it."),
    ("secretKey", "", "Secret key of a credential, returned once at creation. Creating a credential sets it."),
    ("userId", "", "Your user ID. Issue a session token sets it."),
]

PARAM_EXAMPLES = {"date-time": "2026-01-01T00:00:00Z", "integer": "1", "boolean": "true", "number": "1"}


def js(lines):
    return {"type": "text/javascript", "exec": lines}


def capture_script(captures):
    """Test script that stores values from a successful response into collection variables."""
    lines = ["if (pm.response.code >= 200 && pm.response.code < 300) {", "    const body = pm.response.json();"]
    for var, path in captures.items():
        # "json:a.b" stores the value as JSON text, for a later request body that sends it back unchanged.
        as_json = path.startswith("json:")
        if as_json:
            path = path[5:]
        steps = path.split(".")
        access = lambda parts: "body" + "".join("[" + p + "]" if p.isdigit() else "." + p for p in parts)
        guard = " && ".join(access(steps[:i + 1]) for i in range(len(steps)))
        expr = ("JSON.stringify(" + access(steps) + ", null, 2)") if as_json else access(steps)
        lines.append("    if (body && {0}) pm.collectionVariables.set(\"{1}\", {2});".format(guard, var, expr))
    lines.append("}")
    return lines


class Builder(object):
    def __init__(self, spec, ops, supplement):
        self.spec = spec
        self.ops = ops
        self.sup = supplement
        self.models = common.models()
        self.keys = set(o.key for o in ops)
        self.path_vars = set()

    def id_path(self, type_name):
        """Dotted path to the new record's id in a response of this type (id, or wrapper.id)."""
        t = self.models.get(type_name)
        if t is None:
            return None
        names = [p[1] for p in t.props]
        if "Id" in names:
            return "id"
        for p in t.props:
            inner = self.models.get(re.sub(r"^.*\.", "", p[0].replace("?", "")))
            if inner is not None and "Id" in [q[1] for q in inner.props]:
                return common.camel(p[1]) + ".id"
        return None

    def captures(self, o):
        extra = dict(self.sup.get("captures", {}).get(o.key, {}))
        if o.method == "POST" and isinstance(o.response, str):
            for k in sorted(self.keys):
                m = re.match(r"^GET " + re.escape(o.path) + r"/\{(\w+)\}$", k)
                if m:
                    idp = self.id_path(o.response)
                    if idp:
                        extra.setdefault(common.path_variable(k[4:], m.group(1), self.sup), idp)
                    break
        return extra

    def url(self, o):
        path = o.path
        variables = []
        for p in o.path_params:
            var = common.path_variable(o.path, p["name"], self.sup)
            self.path_vars.add(var)
            path = path.replace("{" + p["name"] + "}", ":" + p["name"])
            variables.append({"key": p["name"], "value": "{{" + var + "}}", "description": "{0} (set {{{{{1}}}}} in the collection variables).".format(common.describe_schema(p.get("schema")), var)})
        defaults = self.sup.get("postmanQuery", {}).get(o.key, {})
        notes = self.sup.get("parameters", {})
        query = []
        for p in o.query_params:
            name = p["name"]
            schema = p.get("schema") or {}
            value = defaults.get(name)
            if value is None:
                if schema.get("enum"):
                    value = str(schema["enum"][0])
                elif name.endswith("Id") and name not in ("tenantId",):
                    value = "{{" + name + "}}"
                elif name == "tenantId":
                    value = "{{tenantId}}"
                elif name == "pageSize":
                    value = "25"
                else:
                    value = PARAM_EXAMPLES.get(schema.get("format") if schema.get("format") == "date-time" else schema.get("type"), "")
            desc = notes.get(o.key + ":" + name) or notes.get(name) or common.PARAM_NOTES.get(name, "")
            item = {"key": name, "value": value, "description": (common.describe_schema(schema) + ". " + desc).strip()}
            if name not in defaults:
                item["disabled"] = True
            query.append(item)
        for name, kind in o.undeclared_query:
            value = defaults.get(name, "")
            item = {"key": name, "value": value, "description": "{0}. {1} Read by the handler; not declared in the OpenAPI document.".format(kind, notes.get(o.key + ":" + name) or notes.get(name, "")).replace(".  ", ". ")}
            if name not in defaults:
                item["disabled"] = True
            query.append(item)
        raw = "{{baseUrl}}" + path
        enabled = [q for q in query if not q.get("disabled")]
        if enabled:
            raw += "?" + "&".join(q["key"] + "=" + q["value"] for q in enabled)
        url = {"raw": raw, "host": ["{{baseUrl}}"], "path": [s for s in path.split("/") if s]}
        if query:
            url["query"] = query
        if variables:
            url["variable"] = variables
        return url

    def description(self, o):
        lines = ["**" + o.summary.rstrip(".") + ".**"]
        if o.description:
            lines[0] += " " + o.description
        lines += ["", "- Auth: " + o.auth_text(), "- Response: " + plain_response(o.response)]
        if o.notes:
            lines.append("- Notes: " + o.notes)
        codes = dict(o.status)
        if not o.anonymous:
            codes.setdefault("401", "Unauthorized")
        lines.append("- Status: " + "; ".join("{0} {1}".format(c, codes[c]) for c in sorted(codes)))
        lines += ["", "Reference: [REST_API.md]({0}#{1})".format(DOCS, slug(o.method + " " + o.path))]
        return "\n".join(lines)

    def body(self, o):
        pb = self.sup.get("postmanBodies", {}).get(o.key)
        if isinstance(pb, str):
            return {"mode": "raw", "raw": pb, "options": {"raw": {"language": "json"}}}
        rb = pb if pb is not None else self.sup.get("routeBodies", {}).get(o.key)
        if rb is None:
            rb = self.sup.get("bodies", {}).get(o.body_name or "")
            if rb is not None and o.body_is_array:
                rb = [rb]
        if rb is None:
            rb = {}
        return {"mode": "raw", "raw": json.dumps(rb, indent=2, ensure_ascii=False), "options": {"raw": {"language": "json"}}}

    def request(self, o):
        req = {"method": o.method, "header": [], "url": self.url(o), "description": self.description(o)}
        item = {"name": o.summary.rstrip("."), "request": req}
        events = []
        if o.anonymous:
            req["auth"] = NOAUTH
        if self.sup.get("postmanAuth", {}).get(o.key) == "adminApiKey":
            req["auth"] = {"type": "apikey", "apikey": [{"key": "key", "value": "x-api-key", "type": "string"}, {"key": "value", "value": "{{adminApiKey}}", "type": "string"}, {"key": "in", "value": "header", "type": "string"}]}
        if o.key == "POST /v1.0/token":
            req["header"] = [
                {"key": "x-email", "value": "{{email}}", "type": "text"},
                {"key": "x-password", "value": "{{password}}", "type": "text"},
                {"key": "x-tenant-guid", "value": "{{tenantId}}", "type": "text", "description": "Optional for email sign-in."},
                {"key": "x-client", "value": "mobile", "type": "text", "disabled": True, "description": "Enable to also receive a refresh token."},
            ]
            events.append({"listen": "test", "script": js([
                "pm.test(\"Signed in\", function () { pm.response.to.have.status(200); });",
                "if (pm.response.code === 200) {",
                "    const body = pm.response.json();",
                "    pm.collectionVariables.set(\"token\", body.token);",
                "    pm.collectionVariables.set(\"userId\", body.userId || \"\");",
                "    pm.collectionVariables.set(\"tenantId\", body.tenantId);",
                "    if (body.refreshToken) pm.collectionVariables.set(\"refreshToken\", body.refreshToken);",
                "}",
            ])})
        elif o.key == "DELETE /v1.0/token":
            events.append({"listen": "test", "script": js(["if (pm.response.code === 204) pm.collectionVariables.set(\"token\", \"\");"])})
        else:
            caps = self.captures(o)
            if caps:
                events.append({"listen": "test", "script": js(capture_script(caps))})
        if o.body_schema is not None:
            req["header"].append({"key": "Content-Type", "value": "application/json", "type": "text"})
            req["body"] = self.body(o)
        if o.response == "text/event-stream":
            req["header"].append({"key": "Accept", "value": "text/event-stream", "type": "text"})
        if events:
            item["event"] = events
        example = self.example(o)
        if example is not None:
            code = sorted(c for c in o.status if c.startswith("2"))[0]
            item["response"] = [{"name": "Example", "originalRequest": {"method": o.method, "header": [], "url": req["url"]},
                                 "status": common_status(code), "code": int(code), "_postman_previewlanguage": "json",
                                 "header": [{"key": "Content-Type", "value": "application/json"}], "body": json.dumps(example, indent=2, ensure_ascii=False)}]
        return item

    def example(self, o):
        route = self.sup.get("routeExamples", {}).get(o.key)
        if route is not None:
            return route
        if not isinstance(o.response, str):
            return None
        return self.sup.get("examples", {}).get(o.response)

    def auth_examples(self):
        signer = [
            "// Signs this request with MMN1-HMAC-SHA256 using the accessKey and secretKey collection variables.",
            "// Copy this script to any request (and set its auth to No Auth) to sign it. The secret never leaves Postman.",
            "const accessKey = pm.variables.get(\"accessKey\");",
            "const secretKey = pm.variables.get(\"secretKey\");",
            "const tenantId = pm.variables.get(\"tenantId\");",
            "const method = pm.request.method.toUpperCase();",
            "const pathAndQuery = pm.variables.replaceIn(pm.request.url.getPathWithQuery());",
            "const body = pm.request.body && pm.request.body.mode === \"raw\" ? pm.variables.replaceIn(pm.request.body.raw || \"\") : \"\";",
            "const date = new Date().toISOString().replace(/\\.\\d{3}Z$/, \"Z\");",
            "const nonce = CryptoJS.lib.WordArray.random(16).toString(CryptoJS.enc.Hex);",
            "const bodyHash = CryptoJS.SHA256(body).toString(CryptoJS.enc.Hex);",
            "const toSign = [\"MMN1-HMAC-SHA256\", method, pathAndQuery, tenantId, date, nonce, bodyHash].join(\"\\n\");",
            "const signature = CryptoJS.HmacSHA256(toSign, secretKey).toString(CryptoJS.enc.Hex);",
            "pm.request.headers.upsert({ key: \"x-access-key\", value: accessKey });",
            "pm.request.headers.upsert({ key: \"x-tenant-guid\", value: tenantId });",
            "pm.request.headers.upsert({ key: \"x-date\", value: date });",
            "pm.request.headers.upsert({ key: \"x-nonce\", value: nonce });",
            "pm.request.headers.upsert({ key: \"x-signature\", value: signature });",
        ]
        topics = {"raw": "{{baseUrl}}/v1.0/api/topics?pageSize=1", "host": ["{{baseUrl}}"], "path": ["v1.0", "api", "topics"], "query": [{"key": "pageSize", "value": "1"}]}
        tenants = {"raw": "{{baseUrl}}/v1.0/tenants?pageSize=5", "host": ["{{baseUrl}}"], "path": ["v1.0", "tenants"], "query": [{"key": "pageSize", "value": "5"}]}
        items = [
            {"name": "Signed request (MMN1-HMAC-SHA256)", "event": [{"listen": "prerequest", "script": js(signer)}],
             "request": {"method": "GET", "auth": NOAUTH, "header": [], "url": topics,
                         "description": "Lists one topic using a signed request. The pre-request script builds the string to sign (algorithm, method, path and query, tenant, x-date, x-nonce, and the SHA-256 of the body, joined by line feeds), signs it with HMAC-SHA256 keyed by secretKey, and adds the x-access-key, x-tenant-guid, x-date, x-nonce, and x-signature headers. Any auth mode except SessionExchange can sign. See [Signed requests](" + DOCS + "#signed-requests)."}},
            {"name": "Access key and secret (direct headers)", "request": {"method": "GET", "auth": NOAUTH, "url": topics,
             "header": [{"key": "x-access-key", "value": "{{accessKey}}", "type": "text"}, {"key": "x-secret-key", "value": "{{secretKey}}", "type": "text"}, {"key": "x-tenant-guid", "value": "{{tenantId}}", "type": "text"}],
             "description": "Lists one topic with a credential's access key and secret in headers. The credential's auth mode must be DirectHeader or Hybrid. See [Access keys](" + DOCS + "#access-keys)."}},
            {"name": "Administration key", "request": {"method": "GET", "auth": NOAUTH, "url": tenants,
             "header": [{"key": "x-api-key", "value": "{{adminApiKey}}", "type": "text"}],
             "description": "Lists tenants with the server administration key. Add x-tenant-guid to call tenant-scoped /v1.0/api routes with it. See [Administration key](" + DOCS + "#administration-key)."}},
        ]
        return {"name": "Authentication examples", "description": "The other ways to authenticate besides a bearer session token: a credential's access key and secret, a signed request, and the administration key. Each request sets its own auth to No Auth and sends the headers itself.", "item": items}

    def order(self, o):
        """Position inside its folder: firstInFolder keys in their listed order, then the rest, then lastInFolder keys."""
        first = self.sup.get("firstInFolder", [])
        last = self.sup.get("lastInFolder", [])
        if o.key in first:
            return first.index(o.key) - len(first)
        if o.key in last:
            return 1 + last.index(o.key)
        return 0

    def build(self):
        """Folders in tag order, then Cleanup (every delete, latest folder first, so the collection runs from top to
        bottom: each record is created, used, and only then deleted), then the run-last folder (logout, restart)."""
        tags = self.sup.get("tags", {})
        folders = {}
        cleanup = self.sup.get("cleanupFolder", {})
        final = self.sup.get("finalFolder", {})
        final_keys = final.get("keys", [])
        deletes = []
        finals = {}
        for o in self.ops:
            folders.setdefault(o.tag, [])
        for o in sorted(self.ops, key=self.order):
            folders.setdefault(o.tag, [])
            if o.key in final_keys:
                finals[o.key] = self.request(o)
            elif cleanup and o.method == "DELETE":
                deletes.append((o.tag, self.request(o)))
            else:
                folders[o.tag].append(self.request(o))
        items = [{"name": tag, "description": tags.get(tag, ""), "item": reqs} for tag, reqs in folders.items() if reqs]
        names = [i["name"] for i in items]
        # The examples use the access key and secret that Credentials > Create a credential stores, so they follow it.
        anchor = "Credentials" if "Credentials" in names else "Token"
        items.insert(names.index(anchor) + 1 if anchor in names else 0, self.auth_examples())
        if deletes:
            tag_order = list(folders.keys())
            # Latest folder first and, inside a folder, the last delete first (child records before their parent).
            ordered = [d for i, d in sorted(enumerate(deletes), key=lambda e: (-tag_order.index(e[1][0]), -e[0]))]
            for tag, req in ordered:
                req["name"] = tag + ": " + req["name"]
            items.append({"name": cleanup.get("name", "Cleanup"), "description": cleanup.get("description", ""), "item": [r for t, r in ordered]})
        if finals:
            items.append({"name": final.get("name", "Run last"), "description": final.get("description", ""), "item": [finals[k] for k in final_keys if k in finals]})
        variables = [{"key": k, "value": v, "type": "string", "description": d} for k, v, d in VARIABLES]
        known = set(k for k, v, d in VARIABLES)
        for var in sorted(self.path_vars | set(v for caps in self.sup.get("captures", {}).values() for v in caps)):
            if var not in known:
                variables.append({"key": var, "value": self.sup.get("variableDefaults", {}).get(var, ""), "type": "string", "description": "ID used in request paths. Create requests set it from the new record."})
                known.add(var)
        info = self.spec.get("info", {})
        return {
            "info": {"_postman_id": COLLECTION_ID, "name": "My Morning News", "schema": SCHEMA, "description": collection_description(info, len(self.ops))},
            "auth": {"type": "bearer", "bearer": [{"key": "token", "value": "{{token}}", "type": "string"}]},
            "item": items,
            "variable": variables,
        }


def common_status(code):
    return {"200": "OK", "201": "Created", "202": "Accepted", "204": "No Content", "206": "Partial Content"}.get(code, "OK")


def plain_response(resp):
    if isinstance(resp, dict):
        return "object with " + ", ".join("{0} ({1})".format(k, v) for k, v in resp.items())
    return common.response_label(resp).replace("`", "")


def slug(text):
    s = text.strip().lower()
    s = re.sub(r"[^\w\- ]", "", s)
    return s.replace(" ", "-")


def collection_description(info, count):
    return "\n".join([
        "Every route of the My Morning News REST API ({0} requests), one folder per resource group. Generated by src/Tools/apidocs/generate_postman.py from the server's OpenAPI document; the full reference is [REST_API.md]({1}).".format(count, DOCS),
        "",
        "**Getting started.** Set baseUrl (default http://127.0.0.1:12100), tenantId, email, and password, then send **Token > Issue a session token**. Its test script stores token, userId, and tenantId. Every request then authenticates with the collection's bearer auth ({{token}}) unless it sets its own. Create requests store the new record's ID (topicId, runId, and so on) so the read, update, and delete requests that follow use it.",
        "",
        "**Other credentials.** The Authentication examples folder shows an access key and secret, a signed request (MMN1-HMAC-SHA256, with a pre-request script you can copy), and the administration key (x-api-key).",
        "",
        "**Conventions.** Lists take pageNumber, pageSize, sortBy, sortDirection, search, fromUtc, and toUtc, and return { items, pageNumber, pageSize, totalCount, totalPages, durationMs }. Errors are { error, message, requestId, details }. Optional query parameters are included but disabled; enable the ones you need.",
        "",
        "**Running the whole collection.** The folders run top to bottom on a fresh stack: each folder creates its records and uses them, the Cleanup folder then deletes them (newest first), and the Run last folder signs out and restarts the server. Change my password sends the current password as the new one, and Update server settings sends back the settings that Read server settings returned, so a run leaves the administrator's password and the server's settings as they were. Requests for records a fresh stack does not have yet (runs, findings, briefings, audio) answer 404 until a run has produced some.",
        "",
        "**Careful.** Requests that start runs, refinement steps, prompt lab runs, and model tests call the configured model and cost money. Run a topic uses dryRun=true by default. Delete requests delete, and the bulk deletes in Cleanup remove every matching record.",
        "",
        "(c) 2026 Lattice AI LLC",
    ])


def main():
    parser = argparse.ArgumentParser(description="Regenerate the Postman collection.")
    common.add_source_arguments(parser)
    args = parser.parse_args()
    spec = common.load_openapi(args)
    supplement = common.load_supplement()
    ops = common.operations(spec, supplement)
    builder = Builder(spec, ops, supplement)
    collection = builder.build()
    text = json.dumps(collection, indent=2, ensure_ascii=False) + "\n"
    if "\u2014" in text:
        raise SystemExit("the collection would contain an em-dash; fix the source text")
    if common.write_or_check(OUT, text, args.check, "src/Tools/apidocs/generate_postman.py"):
        print("{0}: {1} requests in {2} folders".format(OUT.relative_to(common.ROOT).as_posix(), len(ops), len(collection["item"])))
    blocking = [p for p in common.problems(ops, supplement) if "no entry in supplement.json" in p or "has no description" in p]
    if blocking:
        for line in blocking:
            print("error: " + line, file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
