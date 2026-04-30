---
description: Initialize weave workspace files and probe GDC integration
---

# /weave-init

Initialize the Weave workspace for the current project.

Use the `weave` tool:

```txt
weave command=init
```

## What It Creates

- `.opencode/weave/state.yaml`
- `.opencode/weave/plans/`
- `.opencode/weave/specs/`
- `.ignore` entry that lets AI tools read `.opencode/weave/` even when `.opencode/` is gitignored

## GDC Probe

When GDC is present, init probes the workspace and reports status. Missing or unavailable GDC should be reported as a setup hint, not as a failed Weave initialization.

Run this once per project root. Worktrees should be created with `/weave-worktree` so Weave artifacts are bootstrapped correctly.
