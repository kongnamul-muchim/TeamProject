---
description: Manage git worktrees for parallel phase or feature work
---

# /weave-worktree

Manage git worktrees so multiple phases or features can be worked on in separate directories.

Use the `weave` tool:

```txt
weave command=worktree worktreeAction=create name="feature-login"
weave command=worktree worktreeAction=list
weave command=worktree worktreeAction=open name="feature-login"
weave command=worktree worktreeAction=merge name="feature-login"
weave command=worktree worktreeAction=remove name="feature-login"
weave command=worktree worktreeAction=remove name="feature-login" deleteBranch=true
```

## Policy

- Use worktrees for independent phases or features that can progress in parallel.
- Avoid parallel worktrees when multiple tasks will edit the same lockfile, schema, generated artifact, or central config.
- Bootstrap Weave artifacts into the new worktree so the user does not need to run `/weave-init` again.
- Merge from the main worktree, resolve conflicts, then run integration verification once.

This command explains git behavior without hiding it: each worktree has its own directory and branch, but merge conflicts are still possible when the same files change.
