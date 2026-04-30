---
description: Show weave workflow help
---

# /weave-help

Weave is Maskweaver's phase-driven workflow for moving from requirements to verified implementation.

## Main Flow

```txt
/weave-init
/weave-map
/weave-interview
/weave-prepare
/weave-refine-plan
/weave-approve
/weave-craft
/build
/weave-verify
/weave-archive
```

## Commands

| Command | Purpose |
| --- | --- |
| `/weave-init` | Initialize workspace files and probe GDC integration |
| `/weave-map` | Analyze codebase structure |
| `/weave-interview` | Clarify requirements before planning |
| `/weave-prepare` | Create research, spec, and plan artifacts |
| `/weave-refine-plan` | Apply structured notes to the active plan |
| `/weave-approve` | Approve a plan or finalize a verified phase |
| `/weave-craft` | Prepare phase execution context |
| `/build` | Run or manage the autonomous build loop |
| `/weave-build` | Long-form build command |
| `/weave-status` | View plan and phase progress |
| `/weave-worktree` | Manage git worktrees for parallel work |
| `/weave-verify` | Run build/test verification |
| `/weave-archive` | Archive a verified active change |
| `/weave-troubleshoot` | Search or record solution knowledge |
| `/weave-repair` | Repair corrupted plan YAML |
| `/weave-agents` | Sync generated agent files and config |

Use `/build` for the common autonomous build path. Keep `/weave-*` for workflow commands so opencode built-in commands stay clear.
