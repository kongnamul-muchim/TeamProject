---
description: Run or manage the Maskweaver autonomous build loop
---

Use the `weave` tool with `command="build"`.

Forward the user arguments from `$ARGUMENTS` to the build command:

- No arguments: run the default build loop.
- `status <buildId>`: call `weave` with `command="build"`, `action="status"`, and `buildId`.
- `stop <buildId>`: call `weave` with `command="build"`, `action="stop"`, and `buildId`.
- `list`: call `weave` with `command="build"` and `action="list"`.
- `resume <buildId>`: call `weave` with `command="build"`, `action="resume"`, and `buildId`.
- `sync <buildId>`: call `weave` with `command="build"`, `action="sync"`, and `buildId`.
- Otherwise, treat `$ARGUMENTS` as phase IDs or build options for `action="run"`.

Do not run shell build commands directly unless the `weave` tool asks for verification.
