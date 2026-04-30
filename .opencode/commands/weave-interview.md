---
description: Ask clarifying questions until requirements and structural changes are clear
---

# /weave-interview

Use codebase context and optional requirement documents to ask the questions needed before planning.

Use the `weave` tool:

```txt
weave command=interview
weave command=interview docsPath="docs/"
```

## Goal

- Identify unclear requirements.
- Detect structural changes that need explicit agreement.
- Separate must-have scope from later work.
- Produce enough clarity for `/weave-prepare`.

Use this before planning when requirements are fuzzy, architectural impact is likely, or multiple stakeholders need their assumptions surfaced.
