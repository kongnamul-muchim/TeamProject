---
description: Create research, spec, and phase plan artifacts from requirements
---

# /weave-prepare

Create the planning artifacts needed for phase-driven implementation.

Use the `weave` tool:

```txt
weave command=prepare docsPath="docs/"
weave command=prepare docsPath="docs/" planName="emotion-diary"
```

## Outputs

- Research notes from the provided documents or project context.
- Baseline spec under `.opencode/weave/specs/`.
- Phase plan under `.opencode/weave/plans/`.
- Sharded plans when a plan is too large and `splitPlans=true`.

After prepare, review the generated plan, optionally run `/weave-refine-plan`, then approve with `/weave-approve`.
