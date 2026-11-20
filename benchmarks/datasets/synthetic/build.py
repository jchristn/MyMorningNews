#!/usr/bin/env python3
"""Build benchmarks/datasets/mymorningnews-synthetic.json from the committed sources (standard library only).

Each corpus has a <corpus>.json (topic, rounds, feedback, hand-written probes) and a <corpus>.md with the pages:

    === <item-id>
    rounds: 1, 2
    hoursAgo: 6
    relevance: 3
    quotes: verbatim | typography | fabricated | fabricated-only | altered | short
    url: https://outside.example/...        (optional: served from outside the stub, such as a blocked domain)
    aliasOf: <item-id>                       (optional: the same page under a tracking-parameter URL)
    probes: surfaced, evidence, seen@2, duplicate@2:<of>, update@2:<of>, canonical:<of>, blocked, suppressed, injection

    # Title

    Paragraph one.

    Paragraph two.

Probe specs are type[@round][:of]. The script also adds a ranking and a dedupe-precision probe per round and one
guardrail probe per corpus. Run with --check to verify the committed JSON is current (CI does).
"""

import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "mymorningnews-synthetic.json"))
CORPORA = ["heat-pumps", "transit", "quantum-negative"]
SNAPSHOT = "2026-11-20"
NEGATIVE = {"suppressed", "blocked", "seen", "canonical", "injection", "empty"}
CATEGORY = {
    "surfaced": "relevance", "suppressed": "relevance", "ranking": "relevance", "order": "feedback", "empty": "relevance",
    "duplicate": "dedupe", "cluster": "dedupe", "update": "dedupe", "dedupe-precision": "dedupe", "seen": "dedupe",
    "canonical": "dedupe", "blocked": "constraints", "evidence": "evidence", "injection": "scope", "guardrail": "scope",
}


def parse_items(path):
    items = []
    with open(path, encoding="utf-8") as f:
        blocks = f.read().split("\n=== ")
    for raw in blocks:
        raw = raw.strip()
        if raw.startswith("=== "):
            raw = raw[4:]
        if not raw:
            continue
        lines = raw.split("\n")
        item = {"id": lines[0].strip(), "meta": {}, "title": "", "paragraphs": []}
        i = 1
        while i < len(lines) and lines[i].strip():
            key, _, value = lines[i].partition(":")
            item["meta"][key.strip()] = value.strip()
            i += 1
        body = "\n".join(lines[i:]).strip()
        parts = [p.strip() for p in body.split("\n\n") if p.strip()]
        if parts and parts[0].startswith("# "):
            item["title"] = parts[0][2:].strip()
            parts = parts[1:]
        item["paragraphs"] = [" ".join(p.split()) for p in parts]
        items.append(item)
    return items


def probe_id(kind, item, rnd):
    return kind + "-" + item + ("" if rnd == 1 else "-r" + str(rnd))


def build_corpus(name):
    with open(os.path.join(HERE, name + ".json"), encoding="utf-8") as f:
        corpus = json.load(f)
    items_out = []
    probes = []
    for item in parse_items(os.path.join(HERE, name + ".md")):
        meta = item["meta"]
        rounds = [int(r) for r in meta.get("rounds", "1").split(",")]
        out = {"id": item["id"], "title": item["title"], "paragraphs": item["paragraphs"], "rounds": rounds,
               "hoursAgo": float(meta.get("hoursAgo", "12")), "relevance": int(meta.get("relevance", "0")),
               "url": meta.get("url") or None, "aliasOf": meta.get("aliasOf") or None, "quotes": meta.get("quotes", "verbatim")}
        if out["aliasOf"]:
            out["title"] = ""
            out["paragraphs"] = []
        items_out.append(out)
        for spec in [s.strip() for s in meta.get("probes", "").split(",") if s.strip()]:
            of = None
            if ":" in spec:
                spec, of = spec.split(":", 1)
            rnd = rounds[0]
            if "@" in spec:
                spec, r = spec.split("@", 1)
                rnd = int(r)
            probe = {"id": probe_id(spec, item["id"], rnd), "type": spec, "category": CATEGORY[spec], "round": rnd,
                     "negative": spec in NEGATIVE, "item": item["id"], "of": of, "items": [], "above": None, "below": None, "note": None}
            probes.append(probe)
    for rnd in range(1, corpus["rounds"] + 1):
        probes.append({"id": "ranking-r" + str(rnd), "type": "ranking", "category": "relevance", "round": rnd, "negative": False,
                       "item": None, "of": None, "items": [], "above": None, "below": None, "note": "nDCG@10 of the shown findings against graded relevance."})
        probes.append({"id": "dedupe-precision-r" + str(rnd), "type": "dedupe-precision", "category": "dedupe", "round": rnd, "negative": False,
                       "item": None, "of": None, "items": [], "above": None, "below": None, "note": "Findings flagged as duplicates are true duplicates."})
    probes.append({"id": "guardrail", "type": "guardrail", "category": "scope", "round": corpus["rounds"], "negative": False,
                   "item": None, "of": None, "items": [], "above": None, "below": None, "note": "Every model request leads with the scope guardrail."})
    for p in corpus.get("probes", []):
        full = {"id": p["id"], "type": p["type"], "category": p.get("category", CATEGORY.get(p["type"], "other")), "round": p.get("round", 1),
                "negative": p["type"] in NEGATIVE, "item": p.get("item"), "of": p.get("of"), "items": p.get("items", []),
                "above": p.get("above"), "below": p.get("below"), "note": p.get("note")}
        probes.append(full)
    return {"id": corpus["id"], "description": corpus["description"], "rounds": corpus["rounds"], "topic": corpus["topic"],
            "items": items_out, "feedback": corpus.get("feedback", []), "probes": probes}


def build():
    return {
        "name": "mymorningnews-synthetic",
        "description": "Fictional Harbor City news written for the product: relevant, irrelevant, duplicate, update, blocked, alias, misquoted, and injected pages over two rounds, plus a negative corpus whose correct briefing is empty.",
        "tier": "synthetic",
        "snapshot": SNAPSHOT,
        "corpora": [build_corpus(c) for c in CORPORA],
    }


def main():
    text = json.dumps(build(), indent=2, ensure_ascii=False) + "\n"
    if "--check" in sys.argv:
        with open(OUT, encoding="utf-8") as f:
            if f.read() != text:
                print("mymorningnews-synthetic.json is out of date; run benchmarks/datasets/synthetic/build.py")
                return 1
        print("mymorningnews-synthetic.json is current")
        return 0
    with open(OUT, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote " + OUT)
    return 0


if __name__ == "__main__":
    sys.exit(main())
