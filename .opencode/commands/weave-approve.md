---
description: Approve the plan or finalize a crafted phase
---

# /weave-approve

Pass the approval gate before implementation, or finalize a phase after craft and verify have completed.

Use the `weave` tool:

```txt
weave command=approve
weave command=approve phaseId="P1"
```

## Behavior

- Without `phaseId`, approve the active plan so implementation can begin.
- With `phaseId`, finalize the crafted phase after verification.
- `applyNotes=true` applies structured notes before approval when plan notes are present.

Do not treat approval as a substitute for verification. Use `/weave-verify` or `/build` for evidence before finalizing work.
