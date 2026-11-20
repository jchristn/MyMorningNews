#!/usr/bin/env python3
"""Build benchmarks/datasets/mymorningnews-live.json, a frozen snapshot of the product's own documentation.

Each section listed in manifest.json becomes one page: the heading is the title, and its prose (list items become
sentences; code blocks, tables, and sub-headings are dropped; Markdown emphasis, code spans, and links are reduced to
text) becomes the paragraphs. Only paragraphs whose first sentence is 3 to 40 words are kept, so every page has
quotable evidence spans. Relevance comes from the manifest (hand-assigned). Probes are generated: surfaced for
relevance 2 and 3, suppressed for relevance 0, and one ranking, dedupe-precision, and guardrail probe.

The snapshot is frozen: the committed JSON is the dataset. Rebuild only to take a new snapshot, and change the
snapshot date in manifest.json when you do. Standard library only.
"""

import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.normpath(os.path.join(HERE, "..", "mymorningnews-live.json"))


def clean(text):
    text = re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", text)
    text = text.replace("**", "").replace("`", "")
    text = re.sub(r"(?<!\w)_([^_]+)_(?!\w)", r"\1", text)
    return " ".join(text.split())


def first_sentence(p):
    m = re.search(r"[.?!] ", p)
    return p[: m.start() + 1] if m else p


def section(path, heading):
    with open(os.path.join(ROOT, path), encoding="utf-8") as f:
        lines = f.read().split("\n")
    out, inside, fence = [], False, False
    for line in lines:
        if line.startswith("## "):
            if inside:
                break
            inside = line[3:].strip() == heading
            continue
        if not inside:
            continue
        if line.strip().startswith("```"):
            fence = not fence
            continue
        if fence or line.startswith("|") or line.startswith("#"):
            out.append("")
            continue
        out.append(line)
    paragraphs, current = [], []
    for line in out + [""]:
        stripped = line.strip()
        item = re.match(r"^(?:[-*]|\d+\.)\s+(.*)$", stripped)
        if not stripped or item:
            if current:
                paragraphs.append(clean(" ".join(current)))
            current = [item.group(1)] if item else []
            continue
        current.append(stripped)
    kept = []
    for p in paragraphs:
        if not p.endswith((".", "?", "!", ":")):
            p += "."
        words = len(first_sentence(p).split())
        if 3 <= words <= 40:
            kept.append(p)
    return kept[:5]


def build():
    with open(os.path.join(HERE, "manifest.json"), encoding="utf-8") as f:
        manifest = json.load(f)
    c = manifest["corpus"]
    items, probes = [], []
    for s in manifest["sections"]:
        paragraphs = section(s["file"], s["heading"])
        if not paragraphs:
            raise SystemExit("section has no quotable prose: " + s["file"] + " / " + s["heading"])
        items.append({"id": s["id"], "title": re.sub(r"^\d+\.\s*", "", s["heading"]), "paragraphs": paragraphs, "rounds": [1],
                      "hoursAgo": 24.0, "relevance": s["relevance"], "url": None, "aliasOf": None, "quotes": "verbatim"})
        if s["relevance"] >= 2:
            probes.append({"id": "surfaced-" + s["id"], "type": "surfaced", "category": "relevance", "round": 1, "negative": False,
                           "item": s["id"], "of": None, "items": [], "above": None, "below": None, "note": None})
        if s["relevance"] == 0:
            probes.append({"id": "suppressed-" + s["id"], "type": "suppressed", "category": "relevance", "round": 1, "negative": True,
                           "item": s["id"], "of": None, "items": [], "above": None, "below": None, "note": None})
    for kind, cat, note in [("ranking", "relevance", "nDCG@10 of the shown findings against graded relevance."),
                            ("dedupe-precision", "dedupe", "Findings flagged as duplicates are true duplicates."),
                            ("guardrail", "scope", "Every model request leads with the scope guardrail.")]:
        probes.append({"id": kind + ("-r1" if kind != "guardrail" else ""), "type": kind, "category": cat, "round": 1, "negative": False,
                       "item": None, "of": None, "items": [], "above": None, "below": None, "note": note})
    corpus = {"id": c["id"], "description": c["description"], "rounds": 1, "topic": c["topic"], "items": items, "feedback": [], "probes": probes}
    return {"name": "mymorningnews-live", "description": "Frozen snapshot of the product's own operator docs, sectioned into pages.",
            "tier": "live", "snapshot": manifest["snapshot"], "corpora": [corpus]}


def main():
    text = json.dumps(build(), indent=2, ensure_ascii=False) + "\n"
    if "--check" in sys.argv:
        print("the live snapshot is frozen; --check only verifies the manifest still resolves against docs/")
        return 0
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote " + OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
