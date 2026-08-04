---
id: ICW-148
author: Copilot
key: ICW-148
title: Register FastNoise2Bindings as a proper git submodule
status: Done
type: Task
priority: P2
tags:
  - submodule
  - build
  - infra
  - vendoring
dependsOn: []
related:
  - ICW-131
links:
  - .gitmodules
  - submodules/FastNoise2Bindings
  - src/InfiniteCanvas.Rendering/InfiniteCanvas.Rendering.csproj
created: 2026-08-02
updated: 2026-08-02
---

## Summary

Convert `submodules/FastNoise2Bindings` from an unregistered gitlink into a proper git submodule. The path had a gitlink in the index and HEAD but no `.gitmodules` mapping, so `git submodule` commands failed with "no submodule mapping found".

## Scope

- Add `.gitmodules` entry mapping the path to `https://github.com/Auburn/FastNoise2Bindings.git`.
- Run `git submodule init` to register the submodule in the local config.
- Run `git submodule absorbgitdirs` to move the inner `.git` directory into `.git/modules/submodules/FastNoise2Bindings`.
- Commit the local vendor patches inside the submodule so the recorded gitlink reproduces the buildable state on a fresh clone.
- Pin the parent gitlink to the patched submodule commit.

## Acceptance Criteria

- `git submodule status` reports a clean, registered submodule.
- `git submodule deinit -f` followed by `git submodule update --init` checks out the patched vendor files (nullable pragmas, `partial class FastNoise`, `LibraryImport`).
- `dotnet build .\src\InfiniteCanvas.App\InfiniteCanvas.App.csproj --configuration Release` succeeds with 0 errors.

## Validation

- `git submodule status` shows `fae174e` with no dirty or mismatch prefix.
- Deinit/re-update cycle reproduced commit `fae174e81092f02796347e776ad89c501d2686a8` with the vendor patches intact.
- Release app build succeeded, 0 errors.
- Parent commits: `f80bd31` (registration), submodule commit `fae174e` (vendor patches).

## Notes

- The submodule `master` is now one commit ahead of `origin/master` (`fae174e`), which holds the local build patches. This is the standard vendored-fork pattern; the parent pins the patched commit.
- The local patches convert `DllImport` to source-generated `LibraryImport` and add nullable pragma disables, matching the project's `net10.0` + `Nullable` context.
