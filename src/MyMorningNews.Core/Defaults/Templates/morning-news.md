---
name: Morning News
description: The daily briefing across your topics.
tone: direct, skeptical, no hype
length: medium
audience: practitioner
citations: numbered
audio:
  enabled: true
  format: two-host
  targetMinutes: 8
  intro: "Good morning. Here is what moved in your topics."
---

# {{edition.name}} for {{date.long}}

Open with the single most important development across my topics and why it matters to me.

## Top of the news
<!-- mmn:section source=findings tier=MustRead limit=5 order=relevance -->
Lead with the single most important development. One paragraph per item.
Say why it matters to me specifically, using my topic briefs.

## By topic
<!-- mmn:section source=findings group=topic tier=WorthALook limit=4 -->
Two sentences per item, grouped under each topic name.

## Where this might go
<!-- mmn:section source=predictions status=new limit=5 -->
State each prediction, its confidence, and what would change my mind.

## Predictions to grade
<!-- mmn:section source=predictions status=due limit=5 -->
For each prediction that is due or has a proposed resolution, say what happened and whether it came true.

## Dig into next
<!-- mmn:section source=suggestions status=New limit=5 order=priority -->
One line per suggestion: what to do and why now.

## Sources
<!-- mmn:section source=citations -->
