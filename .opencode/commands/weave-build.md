---
description: Run, resume, stop, list, or inspect autonomous build loops
---

# /weave-build

Run the Maskweaver autonomous build loop for approved phases, or manage an existing build loop.

Use the `weave` tool:

```txt
weave command=build
weave command=build action=run phaseIds="P1,P2"
weave command=build action=status buildId="build-20250428-a1b2"
weave command=build action=stop buildId="build-20250428-a1b2"
weave command=build action=list
weave command=build action=resume buildId="build-20250428-a1b2"
weave command=build action=sync buildId="build-20250428-a1b2"
```

## Actions

- `run`: execute selected phases, or the next approved work when phase IDs are omitted.
- `status`: inspect one build loop.
- `stop`: request a running loop to stop.
- `list`: show known build loops.
- `resume`: continue a blocked or stopped build.
- `sync`: import squad/runtime results into the build record.

The short `/build` command is also installed for opencode users who expect build to appear as a direct command.
