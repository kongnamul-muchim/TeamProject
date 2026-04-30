---
description: Archive the verified active change artifact
---

# /weave-archive

Archive the active change after implementation has been verified and the phase or plan no longer needs to stay active.

Use the `weave` tool:

```txt
weave command=archive
```

## When To Use

- The active change has passed build/test verification.
- The user has accepted the implementation result.
- The plan should be preserved for history but removed from the active workflow.

Do not archive unverified work. Run `/weave-verify` or `/build` first and keep the verification result in the change record.
