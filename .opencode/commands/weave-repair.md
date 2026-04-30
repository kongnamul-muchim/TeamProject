---
description: Scan and auto-repair corrupted plan YAML files
---

# /weave-repair

Repair malformed Weave plan YAML when status, craft, or approve cannot load the plan.

Use the `weave` tool:

```txt
weave command=repair
```

## Behavior

- Scans `.opencode/weave/state.yaml` and plan files.
- Reports files as OK, fixed, or failed.
- Creates backups before risky repairs when supported.
- Gives manual recovery guidance when automatic repair is not possible.

Use this for artifact recovery only. Do not use it to rewrite requirements or change implementation scope.
