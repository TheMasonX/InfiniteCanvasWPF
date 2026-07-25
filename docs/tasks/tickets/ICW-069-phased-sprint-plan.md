# ICW-069: Phased sprint plan for high-ROI work and important fixes

## Status
In Progress

## Summary
Turn the recent human requests and the latest handoff guidance into a phased plan that prioritizes interaction polish, stability, and the highest-ROI rendering and architecture improvements.

## Planning basis
- The latest agent handoff focused on render coalescing, camera snapshot discipline, and buffer ownership safety.
- The newly transcribed human requests emphasized viewport scrollbars, configurable background tile tuning, a lightweight debug inspector, and licensing/attribution support.
- The existing backlog already contains several high-leverage fixes in the same area: exception safety, shutdown hardening, pixelometer lookup optimization, and render scheduler/overlay retention work.

## Phase 1 — Interaction polish and tuneability (1-2 days)
Priority: high ROI, low architectural risk
- Complete the new interaction and tuning work already captured in ICW-065 and ICW-066.
- Add a lightweight debug inspector surface in ICW-067 so developers can adjust generator and viewport settings without code changes.
- Add an About/licensing dialog in ICW-068 so attribution is discoverable and packaging is simple.

## Phase 2 — Stability and resilience (2-3 days)
Priority: high ROI, prevents costly regressions
- Finish the application-level exception safety net in ICW-014.
- Harden close-time shutdown ordering and active-operation disposal in ICW-029.
- Improve accessibility for the main viewport controls in ICW-037.
- Add a minimal CI and warning policy baseline in ICW-036.

## Phase 3 — Rendering and spatial performance (2-4 days)
Priority: medium-high ROI, performance-sensitive
- Optimize the pixelometer lookup path in ICW-020.
- Validate back-buffer handoff safety and buffered reuse semantics in ICW-021.
- Extract MainWindow logic into testable components in ICW-022.
- Continue the tile-cache and materialization benchmark work in ICW-064.

## Phase 4 — Architecture cleanup and consistency (ongoing)
Priority: medium ROI, keeps the codebase maintainable
- Address boundary semantics and placement consistency in ICW-033.
- Strengthen spatial query abstraction and count-oriented usage in ICW-032.
- Resolve overlay pooling and frame-shell retention in ICW-007 and ICW-028.
- Reconcile renderer/pixelometer blending in ICW-035.

## Suggested execution order
1. ICW-065 and ICW-066 (immediate user-visible value)
2. ICW-067 and ICW-068 (developer ergonomics and polish)
3. ICW-014 and ICW-029 (stability and crash safety)
4. ICW-020, ICW-021, and ICW-022 (performance and maintainability)
5. ICW-064 and the remaining architecture cleanup items

## Validation expectation
Use the narrowest relevant verification path for each slice:
- dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --configuration Debug
- dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release
