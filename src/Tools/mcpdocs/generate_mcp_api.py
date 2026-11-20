#!/usr/bin/env python3
"""Regenerate the tool, resource, and prompt reference in MCP_API.md from the MCP server's --describe output.

Usage (from the repository root):
    python3 src/Tools/mcpdocs/generate_mcp_api.py            # build, describe, rewrite MCP_API.md
    python3 src/Tools/mcpdocs/generate_mcp_api.py --check    # exit 1 when MCP_API.md is out of date

The section between <!-- generated:start --> and <!-- generated:end --> is replaced; everything else is hand-written.
(c) 2026 Lattice AI LLC
"""
import json
import pathlib
import subprocess
import sys

ROOT = pathlib.Path(__file__).resolve().parents[3]
DOC = ROOT / "MCP_API.md"
START = "<!-- generated:start -->"
END = "<!-- generated:end -->"


def describe():
    project = ROOT / "src" / "MyMorningNews.McpServer" / "MyMorningNews.McpServer.csproj"
    out = subprocess.run(["dotnet", "run", "--project", str(project), "-f", "net10.0", "--", "--describe"], check=True, capture_output=True, text=True).stdout
    return json.loads(out[out.index("{"):])


def type_of(schema):
    t = schema.get("type", "any")
    if isinstance(t, list):
        t = " or ".join(t)
    if schema.get("enum"):
        return "string (" + ", ".join("`" + v + "`" for v in schema["enum"]) + ")"
    if t == "array":
        return "array of " + type_of(schema.get("items", {}))
    if t == "object" and schema.get("properties"):
        return "object with " + ", ".join("`" + k + "`" for k in schema["properties"].keys())
    if t == "integer" and "minimum" in schema and "maximum" in schema and schema["maximum"] < 10**9:
        return "integer ({0} to {1})".format(schema["minimum"], schema["maximum"])
    if t == "string" and schema.get("maxLength"):
        return "string (up to {0})".format(schema["maxLength"])
    return t


def table(schema, required_label):
    props = schema.get("properties", {})
    if not props:
        return "None.\n"
    required = set(schema.get("required", []))
    lines = ["| Name | Type | " + required_label + " | Description |", "|---|---|---|---|"]
    for name, s in props.items():
        desc = s.get("description", "").replace("|", "\\|")
        lines.append("| `{0}` | {1} | {2} | {3} |".format(name, type_of(s), "yes" if name in required else "no", desc))
    return "\n".join(lines) + "\n"


def render(d):
    parts = [START, "", "## Tools", "", "{0} tools. Every tool returns structured content that matches its output schema, and the same data as JSON text.".format(len(d["tools"])), ""]
    parts.append("| Tool | Title | Read only | Destructive |")
    parts.append("|---|---|---|---|")
    for t in d["tools"]:
        parts.append("| [`{0}`](#{0}) | {1} | {2} | {3} |".format(t["name"], t["title"], "yes" if t["readOnly"] else "no", "yes" if t["destructive"] else "no"))
    parts.append("")
    for t in d["tools"]:
        parts += ["### " + t["name"], "", "**" + t["title"] + ".** " + t["description"], "", "Input:", "", table(t["inputSchema"], "Required"), "Output:", "", table(t["outputSchema"], "Always")]
    parts += ["## Resources", "", "| URI | Name | Description |", "|---|---|---|"]
    for r in d["resources"]:
        parts.append("| `{0}` | {1} | {2} |".format(r["uri"], r["name"], r["description"]))
    parts += ["", "All resources are `text/markdown`. A missing ID returns JSON-RPC error -32002.", "", "## Prompts", ""]
    for p in d["prompts"]:
        parts += ["### " + p["name"], "", "**" + p["title"] + ".** " + p["description"], ""]
        if p["arguments"]:
            parts += ["| Argument | Required | Description |", "|---|---|---|"]
            for a in p["arguments"]:
                parts.append("| `{0}` | {1} | {2} |".format(a["name"], "yes" if a["required"] else "no", a["description"]))
            parts.append("")
    parts += [END]
    return "\n".join(parts)


def main():
    d = describe()
    text = DOC.read_text()
    i, j = text.index(START), text.index(END) + len(END)
    updated = text[:i] + render(d) + text[j:]
    if "--check" in sys.argv:
        if updated != text:
            print("MCP_API.md is out of date; run src/Tools/mcpdocs/generate_mcp_api.py")
            sys.exit(1)
        print("MCP_API.md is current")
        return
    DOC.write_text(updated)
    print("MCP_API.md: {0} tools, {1} resources, {2} prompts".format(len(d["tools"]), len(d["resources"]), len(d["prompts"])))


if __name__ == "__main__":
    main()
