---
description: Apply structured plan-note directives to the active plan
---

# /weave-refine-plan

Apply structured review notes to the active plan before approval.

Use the `weave` tool:

```txt
weave command=refine-plan
weave command=refine-plan notesPath="tasks/plan-notes.md"
```

## Note Examples

```txt
@plan vision: Keep the first release focused on login and dashboard.
@phase P1 done_when: A user can sign in with email and password.
@phase P1 add_checklist: Failed login shows a clear message.
@phase add P4: Observability | done=errors are logged | hours=3
@phase remove P7
```

Run `/weave-approve` again after changing the active plan.
