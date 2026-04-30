---
description: Analyze codebase structure via GDC and optional Graphify
---

# /weave-map

Map the current codebase before requirements interviews or planning.

Use the `weave` tool:

```txt
weave command=map
weave command=map deep=true
```

## Behavior

- Detects project type and important configuration files.
- Uses GDC when available for structural context.
- Uses deeper Graphify analysis when `deep=true` and the environment supports it.
- Saves map artifacts under `.opencode/weave/maps/`.

This command is for understanding structure. It should not replace builds, tests, or smoke checks.
