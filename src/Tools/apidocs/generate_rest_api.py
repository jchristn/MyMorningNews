#!/usr/bin/env python3
"""Regenerate the route and data type reference in REST_API.md from the server's OpenAPI document, the route
handlers (routescan.py), the C# models in src/MyMorningNews.Core, and the supplemental map (supplement.json).

Usage (from the repository root):
    python3 src/Tools/apidocs/generate_rest_api.py              # read http://127.0.0.1:12100/openapi.json, rewrite REST_API.md
    python3 src/Tools/apidocs/generate_rest_api.py --launch     # run the server from this tree first (needs PostgreSQL)
    python3 src/Tools/apidocs/generate_rest_api.py --check      # exit 1 when REST_API.md is out of date

The section between <!-- generated:start --> and <!-- generated:end --> is replaced; everything else is hand-written.
The run also fails when a route has no supplement entry or a handler reads values its OpenAPI metadata leaves out.
(c) 2026 Lattice AI LLC
"""
import argparse
import json
import re
import sys

import common

DOC = common.ROOT / "REST_API.md"
START = "<!-- generated:start -->"
END = "<!-- generated:end -->"
BASE = "http://127.0.0.1:12100"


STATUS_TEXT = {
    "200": "OK", "201": "Created", "202": "Accepted", "204": "No Content", "206": "Partial Content",
    "400": "Bad Request", "401": "Unauthorized", "403": "Forbidden", "404": "Not Found", "409": "Conflict", "410": "Gone",
    "416": "Range Not Satisfiable", "422": "Unprocessable Entity", "429": "Too Many Requests", "499": "Client Closed Request",
    "500": "Internal Server Error", "502": "Bad Gateway", "503": "Service Unavailable",
}


class Slugs(object):
    """GitHub-style heading anchors, with -1, -2 suffixes for repeats."""

    def __init__(self):
        self.counts = {}

    @staticmethod
    def base(text):
        s = text.strip().lower()
        s = re.sub(r"[^\w\- ]", "", s)
        return s.replace(" ", "-")

    def add(self, text):
        b = self.base(text)
        n = self.counts.get(b, 0)
        self.counts[b] = n + 1
        return b if n == 0 else "{0}-{1}".format(b, n)


def headings(markdown):
    """Heading texts in a block of Markdown, skipping fenced code."""
    out = []
    fenced = False
    for line in markdown.splitlines():
        if line.startswith("```"):
            fenced = not fenced
            continue
        if not fenced and re.match(r"^#{1,6} ", line):
            out.append(re.sub(r"^#{1,6} ", "", line).strip())
    return out


def esc(text):
    return (text or "").replace("|", "\\|").replace("\n", " ")


class TypeCatalog(object):
    """Request schemas from OpenAPI plus every Core type the responses reach, with field tables."""

    def __init__(self, spec, ops, supplement):
        self.schemas = spec.get("components", {}).get("schemas", {})
        self.found = common.models()
        db = common.ROOT / "src" / "MyMorningNews.Core" / "Database" / "DatabaseSettings.cs"
        for t in common.generate_models.parse_file(str(db), "src/MyMorningNews.Core/Database/DatabaseSettings.cs"):
            self.found.setdefault(t.name, t)
        self.examples = supplement.get("examples", {})
        roots = []
        for o in ops:
            roots += common.unwrap_response(o.response)
            if o.body_name:
                roots.append(o.body_name)
        roots += list(self.schemas.keys())
        self.order = []
        queue = list(dict.fromkeys(roots))
        while queue:
            n = queue.pop(0)
            if n in self.order or (n not in self.found and n not in self.schemas):
                continue
            self.order.append(n)
            t = self.found.get(n)
            if t is None:
                continue
            if t.base and t.base in self.found:
                queue.append(t.base)
            for p in t.props:
                queue += common.type_refs(common.generate_models.map_type(p[0]))
        self.anchors = {}
        self.route_anchor = {}
        self.sent_by = {}
        self.returned_by = {}
        for o in ops:
            if o.body_name:
                self.sent_by.setdefault(o.body_name, []).append(o.key)
            for n in dict.fromkeys(common.unwrap_response(o.response)):
                if n != "EnumerationResult":
                    self.returned_by.setdefault(n, []).append(o.key)

    def documented(self, name):
        return name in self.order

    def link(self, name):
        return "[`{0}`](#{1})".format(name, self.anchors.get(name, Slugs.base(name))) if name in self.anchors else "`" + name + "`"

    def linked_label(self, label):
        """Turn type names inside a label into links, leaving code spans alone."""
        def repl(m):
            word = m.group(0)
            return self.link(word) if word in self.anchors else word
        pieces = label.split("`")
        for i in range(0, len(pieces), 2):
            pieces[i] = re.sub(r"\b[A-Z]\w*\b", repl, pieces[i])
        return "`".join(pieces)

    def props(self, name):
        """(json name, type label, description) for a class, base class first."""
        t = self.found.get(name)
        rows = []
        if t is not None:
            chain = []
            cur = t
            while cur is not None:
                chain.insert(0, cur)
                cur = self.found.get(cur.base) if cur.base else None
            for c in chain:
                for p in c.props:
                    rows.append((common.camel(p[1]), common.csharp_label(common.generate_models.map_type(p[0])), p[3]))
        schema = self.schemas.get(name)
        if schema is not None and schema.get("properties"):
            known = {r[0]: r for r in rows}
            rows = [known.get(k) or (k, common.describe_schema(v), "") for k, v in schema["properties"].items()]
        return rows

    def render(self, slugs):
        parts = []
        classes = [n for n in self.order if self.found.get(n) is None or self.found[n].kind == "class"]
        enums = [n for n in self.order if self.found.get(n) is not None and self.found[n].kind == "enum"]
        classes.sort(key=lambda s: s.lower())
        enums.sort(key=lambda s: s.lower())
        return classes, enums

    def body(self, slugs, classes, enums):
        parts = ["## Data types", "", "Field names are camelCase on the wire. Enums travel as their names (`\"Active\"`, not a number). Null fields are left out of responses.", ""]
        parts.append("### Objects")
        slugs.add("Objects")
        parts.append("")
        for n in classes:
            t = self.found.get(n)
            parts.append("#### " + n)
            parts.append("")
            summary = t.summary if t is not None else ""
            if summary:
                parts += [summary, ""]
            for label, keys in (("Request body of", self.sent_by.get(n, [])), ("Returned by", self.returned_by.get(n, []))):
                if keys:
                    shown = ", ".join("[{0}](#{1})".format(k, self.route_anchor[k]) for k in keys[:8])
                    more = " and {0} more".format(len(keys) - 8) if len(keys) > 8 else ""
                    parts += [label + " " + shown + more + ".", ""]
            rows = self.props(n)
            if rows:
                parts += ["| Field | Type | Description |", "|---|---|---|"]
                for name, label, desc in rows:
                    parts.append("| `{0}` | {1} | {2} |".format(name, esc(self.linked_label(label)), esc(desc)))
                parts.append("")
            example = self.examples.get(n)
            if example is not None:
                parts += ["Example:", "", "```json", json.dumps(example, indent=2, ensure_ascii=False), "```", ""]
        parts.append("### Enums")
        slugs.add("Enums")
        parts.append("")
        for n in enums:
            t = self.found[n]
            parts.append("#### " + n)
            parts.append("")
            if t.summary:
                parts += [t.summary, ""]
            parts += ["| Value | Description |", "|---|---|"]
            for m in t.members:
                parts.append("| `{0}` | {1} |".format(m[0], esc(m[2])))
            parts.append("")
        return parts


def curl(o, supplement, catalog):
    samples = supplement.get("samples", {})
    path = o.path
    for p in o.path_params:
        var = common.path_variable(o.path, p["name"], supplement)
        path = path.replace("{" + p["name"] + "}", str(samples.get(var, "<" + var + ">")))
    if o.paged and o.method == "GET":
        path += "?pageSize=10"
    line = ["curl -s"]
    if o.method != "GET":
        line.append("-X " + o.method)
    line.append('"' + BASE + path + '"')
    out = [" ".join(line)]
    if o.path == "/v1.0/token" and o.method == "POST":
        out.append('  -H "x-email: admin@mymorningnews" -H "x-password: password"')
    elif not o.anonymous:
        out.append('  -H "Authorization: Bearer $TOKEN"')
    if o.body_schema is not None:
        out.append('  -H "Content-Type: application/json" --data-binary @body.json')
    if o.response in ("file", "audio", "audio/wav", "image/png", "text/markdown"):
        out.append("  -o output.bin")
    return " \\\n".join(out)


def example_body(o, supplement):
    rb = supplement.get("routeBodies", {}).get(o.key)
    if rb is not None:
        return rb
    body = supplement.get("bodies", {}).get(o.body_name or "")
    if body is not None and o.body_is_array:
        return [body]
    return body


def render_route(o, supplement, catalog, slugs):
    parts = ["### {0} {1}".format(o.method, o.path), ""]
    slugs.add("{0} {1}".format(o.method, o.path))
    lead = "**" + o.summary.rstrip(".") + ".**"
    if o.description:
        lead += " " + o.description
    parts += [lead, ""]
    parts.append("- Auth: " + o.auth_text())
    parts.append("- Response: " + response_text(o.response, catalog))
    if o.notes:
        parts.append("- Notes: " + o.notes)
    parts.append("")
    if o.path_params:
        parts += ["| Path parameter | Type | Postman variable |", "|---|---|---|"]
        for p in o.path_params:
            parts.append("| `{0}` | {1} | `{{{{{2}}}}}` |".format(p["name"], common.describe_schema(p.get("schema")), common.path_variable(o.path, p["name"], supplement)))
        parts.append("")
    query = [p for p in o.query_params if not (o.paged and p["name"] in common.PAGING)]
    extra = [q for q in o.undeclared_query if not (o.paged and q[0] in common.PAGING)]
    if query or extra or o.paged:
        if query or extra:
            notes = supplement.get("parameters", {})
            parts += ["| Query parameter | Type | Notes |", "|---|---|---|"]
            for p in query:
                note = notes.get(o.key + ":" + p["name"]) or notes.get(p["name"], "")
                parts.append("| `{0}` | {1} | {2} |".format(p["name"], esc(common.describe_schema(p.get("schema"))), esc(note)))
            for name, kind in extra:
                note = notes.get(o.key + ":" + name) or notes.get(name, "")
                note = (note + " " if note else "") + "Read by the handler; not declared in the OpenAPI document."
                parts.append("| `{0}` | {1} | {2} |".format(name, esc(catalog.linked_label(kind)), esc(note)))
            parts.append("")
        if o.paged:
            parts += ["Also takes the [paging parameters](#paging-sorting-and-filtering) (`pageNumber`, `pageSize`, `sortBy`, `sortDirection`, `search`, `fromUtc`, `toUtc`).", ""]
    if o.body_schema is not None:
        label = ("array of " if o.body_is_array else "") + (catalog.link(o.body_name) if o.body_name else common.describe_schema(o.body_schema))
        parts += ["Request body: " + label + ".", ""]
        body = example_body(o, supplement)
        if body is not None:
            parts += ["```json", json.dumps(common.substitute(body, supplement.get("samples", {})), indent=2, ensure_ascii=False), "```", ""]
    codes = dict(o.status)
    if not o.anonymous:
        codes.setdefault("401", "Unauthorized")
    parts += ["| Status | Meaning |", "|---|---|"]
    for code in sorted(codes):
        parts.append("| {0} | {1} |".format(code, esc(status_meaning(code, codes[code]))))
    parts.append("")
    response = supplement.get("routeExamples", {}).get(o.key)
    if response is not None:
        parts += ["Response example:", "", "```json", json.dumps(response, indent=2, ensure_ascii=False), "```", ""]
    parts += ["```bash", curl(o, supplement, catalog), "```", ""]
    return parts


def status_meaning(code, label):
    """Status text plus the error codes or note the supplement gives."""
    text = STATUS_TEXT.get(code, "")
    label = (label or "").strip()
    if not label or label in (text, text.replace(" ", "")):
        return text
    if re.match(r"^[A-Z]\w*(, [A-Z]\w*)*$", label):
        if code[0] == "2":
            return label
        return text + ": " + ", ".join("`" + c + "`" for c in label.split(", "))
    return label if code[0] == "2" else text + ": " + label


def type_text(t, catalog):
    """A C# type string as linked Markdown: EnumerationResult<Topic> reads as a page of Topic."""
    t = t.strip()
    m = re.match(r"^EnumerationResult<(.+)>$", t)
    if m:
        return "a page ({0}) of {1}".format(catalog.link("EnumerationResult"), type_text(m.group(1), catalog))
    m = re.match(r"^List<(.+)>$", t)
    if m:
        return "array of " + type_text(m.group(1), catalog)
    simple = {"int": "integer", "long": "integer", "string": "string", "bool": "boolean", "double": "number"}
    if t in simple:
        return simple[t]
    return catalog.link(t)


def response_text(resp, catalog):
    if isinstance(resp, dict):
        return "object with " + ", ".join("`{0}` ({1})".format(k, type_text(v, catalog)) for k, v in resp.items())
    if resp in ("none", "file", "audio", "?") or "/" in resp:
        return common.response_label(resp)
    return type_text(resp, catalog)


def see_also(resp, catalog):
    names = [n for n in dict.fromkeys(common.unwrap_response(resp)) if n in catalog.anchors]
    if not names:
        return ""
    return " (see " + ", ".join(catalog.link(n) for n in names) + ")"


def render(spec, ops, supplement, prefix):
    slugs = Slugs()
    for h in headings(prefix):
        slugs.add(h)
    catalog = TypeCatalog(spec, ops, supplement)
    classes, enums = catalog.render(slugs)
    tags = supplement.get("tags", {})
    by_tag = {}
    for o in ops:
        by_tag.setdefault(o.tag, []).append(o)
    # Reserve anchors in document order: reference heading, tag sections with routes, then data types.
    plan = ["Route reference"]
    for tag, items in by_tag.items():
        plan.append(tag)
        plan += ["{0} {1}".format(o.method, o.path) for o in items]
    plan += ["Data types", "Objects"] + classes + ["Enums"] + enums
    probe = Slugs()
    probe.counts = dict(slugs.counts)
    anchor_of = {}
    for h in plan:
        anchor_of.setdefault(h, probe.add(h))
    catalog.anchors = {n: anchor_of[n] for n in classes + enums}
    route_anchor = {o.key: anchor_of["{0} {1}".format(o.method, o.path)] for o in ops}
    catalog.route_anchor = route_anchor

    parts = [START, "", "## Route reference", ""]
    slugs.add("Route reference")
    parts.append("{0} routes in {1} groups. Generated from `/openapi.json`, the route handlers, and `src/Tools/apidocs/supplement.json`; do not edit by hand.".format(len(ops), len(by_tag)))
    parts += ["", "| Group | Routes | What it covers |", "|---|---|---|"]
    for tag, items in by_tag.items():
        parts.append("| [{0}](#{1}) | {2} | {3} |".format(tag, anchor_of[tag], len(items), esc(tags.get(tag, ""))))
    parts.append("")
    for tag, items in by_tag.items():
        parts += ["## " + tag, ""]
        slugs.add(tag)
        if tags.get(tag):
            parts += [tags[tag], ""]
        parts += ["| Method | Path | Summary |", "|---|---|---|"]
        for o in items:
            parts.append("| {0} | [`{1}`](#{2}) | {3} |".format(o.method, o.path, route_anchor[o.key], esc(o.summary)))
        parts.append("")
        for o in items:
            parts += render_route(o, supplement, catalog, slugs)
    parts += catalog.body(slugs, classes, enums)
    for n in classes + enums:
        slugs.add(n)
    parts.append(END)
    return "\n".join(parts), catalog


def main():
    parser = argparse.ArgumentParser(description="Regenerate the reference section of REST_API.md.")
    common.add_source_arguments(parser)
    args = parser.parse_args()
    spec = common.load_openapi(args)
    supplement = common.load_supplement()
    ops = common.operations(spec, supplement)
    issues = common.problems(ops, supplement)
    text = DOC.read_text(encoding="utf-8")
    i, j = text.index(START), text.index(END) + len(END)
    generated, catalog = render(spec, ops, supplement, text[:i])
    updated = text[:i] + generated + text[j:]
    if "\u2014" in updated:
        raise SystemExit("REST_API.md would contain an em-dash; fix the source text")
    for line in issues:
        print("warning: " + line, file=sys.stderr)
    blocking = [p for p in issues if "no entry in supplement.json" in p or "has no description" in p or "has no matching route" in p]
    if common.write_or_check(DOC, updated, args.check, "src/Tools/apidocs/generate_rest_api.py"):
        print("REST_API.md: {0} routes, {1} data types".format(len(ops), len(catalog.order)))
    if blocking:
        print("{0} route(s) need supplement.json entries".format(len(blocking)), file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
