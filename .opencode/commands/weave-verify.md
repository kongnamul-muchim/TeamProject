---
description: Run build and test verification for the current worktree
---

# /weave-verify

Run project-appropriate build and test checks in the current worktree.

Use the `weave` tool:

```txt
weave command=verify
weave command=verify verifyMode=quick
weave command=verify projectType="go"
```

## Behavior

- Detects common project types from files in the project root.
- Recommends or runs build/test commands for Node, Go, Rust, Python, .NET, and similar stacks.
- Reports command output and failure tails so the next action is clear.

Verification evidence matters more than workflow metadata. Treat failing builds/tests as blockers unless the failure is clearly unrelated and already known.
