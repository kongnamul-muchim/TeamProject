---
description: Prepare execution context for a phase
---

# /weave-craft

Prepare one approved phase for implementation.

Use the `weave` tool:

```txt
weave command=craft
weave command=craft phaseId="P1"
```

## Behavior

- Selects the next runnable phase when `phaseId` is omitted.
- Loads plan, spec, masks, and execution context for the chosen phase.
- Gives the implementer the bounded task context needed to start work.

After implementation, run `/weave-verify` or `/build` before using `/weave-approve` to finalize the phase.
