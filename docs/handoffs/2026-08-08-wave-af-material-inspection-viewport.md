# Wave AF Handoff, Material Inspection Viewport

Date: 2026-08-08
Status: Complete for Wave AF, follow-up tasks open

## Review Result

Wave AF reviewed the recent viewport and materializer work. The wave fixes selected mip request suppression, complete cache identity through raster lookup, semantic stale-frame rejection, frozen raster ownership, and pre-commit layer publication.

The wave does not prove external host parity, item state immutability, same-epoch completion ordering, callback rollback, or WPF runtime stress.

## Delivered

- Active app requests use `BackgroundTileMaterializer`.
- Selected mip requests continue when an older resident payload exists.
- Pixelometer reads use exact materializer mip-zero residents.
- Raster payload maps use complete `BackgroundTileCacheKey` identity.
- `CanvasFrameIdentity` carries source session and semantic revisions.
- `CanvasControl` rejects stale same-session frames and accepts new source sessions.
- `CanvasFrame` owns the item sequence and requires frozen raster input.
- `CanvasFrame.LayerPlan` carries ordered layer descriptors.
- `FrameLayersPublishing` runs after stale checks and before raster and view-model mutation.
- `SampleImageTileSource` adapts synthetic tiles to the source-neutral materializer contract.

## Review Findings

- P2 Standards finding: fallback selection logic remains duplicated in the materializer and Windows raster helper.
- P1 Spec finding: published items remain mutable after sequence ownership.
- P1 Spec finding: `CanvasControl` does not own the typed scene-change subscription.
- P1 Spec finding: layer callback failure has no rollback contract.
- P1 Spec finding: same-epoch completion ordering lacks direct evidence.
- P1 Spec finding: legacy tile-owned materialization remains reachable.

The findings remain assigned to ICW-076, ICW-338, ICW-339, ICW-340, and ICW-341. No new task key is required.

## Task State

- ICW-076 remains In Progress. Legacy ownership and same-epoch completion evidence remain open.
- ICW-338 is In Progress. Item stability and concurrent-read evidence remain open.
- ICW-339 is In Progress. Control-owned scene-change subscription remains open.
- ICW-340 is In Progress. Callback rollback and immutable layer content remain open.
- ICW-337 remains Proposed. External readiness is partial only.
- ICW-341 remains Proposed. Runtime stress evidence has not started.

## Changed Surface

The implementation surface includes `CanvasControl`, `CanvasFrame`, `CanvasFrameIdentity`, `ICanvasSceneSource`, `BackgroundTileContracts`, `BackgroundTileMaterializer`, `SampleImageTileSource`, `SampleImageTile`, `ZeroCopyBitmapFactory`, and `MainWindow`.

The test surface includes materializer, source adapter, scene contract, frame shell, consumer-host, and colliding-identity raster tests.

Durable records include the Wave AF audit, ICW-076, ICW-337 through ICW-341, both task trackers, and the functional requirements registry. Existing prior-agent source and documentation changes remain in the worktree.

## Evidence

Focused evidence recorded before this handoff:

- `CanvasControlConsumerHostTests`: 14/14 passed.
- `BackgroundTileMaterializerTests`: 6/6 passed.
- Materializer and source adapter focused tests: 9/9 passed.
- `SampleImageTileSourceTests`: 4/4 passed.
- Colliding-ID raster tests: 2/2 passed.
- Task validation: 234 task files validated, 5 legacy markdown files skipped.
- `git diff --check`: passed.

Final validation commands:

```text
dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release
dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release
dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release
pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks
git diff --check
```

Final validation results:

- `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Release`: 214/214 passed.
- `dotnet test tests/InfiniteCanvas.Windows.Tests/InfiniteCanvas.Windows.Tests.csproj --configuration Release`: 38/38 passed.
- `dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release`: passed.
- `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`: 234 task files validated, 5 legacy markdown files skipped.
- `git diff --check`: passed.

## Known Blockers

- The worktree contains unrelated prior-agent changes. Preserve them during review and delivery.
- External material readiness remains incomplete until the open P1 findings receive evidence.
- No commit or push has occurred.

## Next Step

Review the complete diff for accidental scope. Commit and push the preserved worktree as one coherent batch. Continue ICW-076, ICW-338, ICW-339, ICW-340, and ICW-341 with the open evidence gaps.
