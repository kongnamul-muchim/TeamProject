---
description: Search global knowledge for solutions or record a new one
---

# /weave-troubleshoot

Search prior solution knowledge for an error, or record a new solution.

Use the `weave` tool:

```txt
weave command=troubleshoot error="Cannot find module 'xyz'"
weave command=troubleshoot record=true error="..." solution="..." context="..."
```

## Modes

- Search mode: provide `error`.
- Record mode: set `record=true` and provide `solution`.

Record only solutions that were actually validated. Prefer the exact command, config change, or code path that fixed the issue.
