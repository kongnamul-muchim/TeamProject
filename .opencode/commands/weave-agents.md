---
description: Sync generated agent files and initialize runtime configuration
---

# /weave-agents

Manage the agent files that Maskweaver exposes to opencode.

Use the `weave` tool:

```txt
weave command=agents sync=true
weave command=agents init=true
```

## Modes

- `sync=true`: regenerate `.opencode/agents/*.md` from the configured dummy-human pool.
- `init=true`: create the default runtime and plugin configuration files when they do not exist.
- `sync=true init=true`: initialize config and refresh generated agent files in one pass.

Prefer this command when agent markdown files are missing, stale, or out of sync with `maskweaver.config.json`.
