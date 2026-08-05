# ICW-316 Next Slice Audit

Report ID: 84a0cdb5f817-316a
Audited scope: proposed `ICW-316` assembly extraction, current `ICW-312` / `ICW-315` boundary, and ADR-0007 alignment.
Baseline commit: `84a0cdb5f8178286ae4784e1f6221cd7ae06e7f1`

## Executive summary

`ICW-316` is the right next slice, but the current ticket is under-scoped for the boundary that now exists in code. The repo already landed `ICW-312` and `ICW-315`, with `CanvasControl` consuming injected scene sources and `CanvasFrame` carrying the published frame across the host/control boundary. The extraction work therefore cannot be treated as a simple move of `CanvasControl` and `CanvasViewModel`; it now also has to account for `CanvasFrame` and any other frame-boundary contract types that are currently living in the App assembly. fileciteturn1file0 fileciteturn23file0

The good news is that the direction is coherent: Core now owns `ICanvasItem`, `ICanvasSceneSource`, `ICanvasSpatialQuerySource`, and `CanvasPixelSample`, and the control boundary no longer owns the raster shell. The bad news is that the reusable-component story is still leaky: `CanvasControl` still exposes overlay surfaces to the host, `MainWindow` still downcasts items back to `SampleAnnotation`, and the proposed extraction ticket does not yet say what happens to `CanvasFrame`. fileciteturn2file0 fileciteturn3file0 fileciteturn4file0 fileciteturn18file0

## Next slice recommendation

Treat the next slice as:

**“Extract the reusable canvas surface into a dedicated WPF control library, and relocate the frame-boundary contract types that are currently App-owned.”**

That means the move should include `CanvasControl`, `CanvasViewModel`, and `CanvasFrame` at minimum, with the contracts location explicitly frozen before the move starts. ADR-0007 already says the component should ship as a separate assembly, and `ICW-316` already flags the contracts-location decision; the implementation plan should make that decision explicit rather than postponing it into the middle of the extraction. fileciteturn24file0 fileciteturn23file0

## Findings

### 1) `ICW-316` is currently too narrow for the actual boundary
The ticket says “move `CanvasControl` and `CanvasViewModel` into their own library,” but the current `CanvasControl` API already depends on `CanvasFrame`, and `CanvasFrame` lives under `InfiniteCanvas.App`. If the extraction starts without moving that contract type, the new library will either keep an App dependency or reintroduce a redundant boundary shim. That would defeat the point of the slice. fileciteturn18file0 fileciteturn23file0

### 2) The reusable-component boundary is still shallow
`CanvasControl` now accepts `IReadOnlyList<ICanvasItem>`, but the host immediately downcasts those items back to `SampleAnnotation` when building annotation visuals. That means the boundary is still “abstract for transport, concrete for rendering.” For a reusable library, that is a fragile half-step: the component can consume a source contract, but it still cannot render or interact with arbitrary items without host-specific knowledge. fileciteturn1file0

### 3) The ticket should name the contracts strategy, not just “decide it before the move”
`ICW-316` currently says to “decide the contracts location (Core vs a new contracts assembly) before the move.” That is correct, but too open-ended for implementation. The existing code already uses Core for `ICanvasItem`, `ICanvasSceneSource`, `ICanvasSpatialQuerySource`, and `CanvasPixelSample`, so the real decision is now narrower: either keep the current Core placement and relocate only frame/control/view-model types, or introduce a new shared boundary assembly specifically for frame and control contracts. Leaving that as an open question inside the task invites thrash during the move. fileciteturn2file0 fileciteturn3file0 fileciteturn4file0 fileciteturn23file0

### 4) The current zero-reference gates are good, but they are text-based and therefore easy to game
`CanvasBoundaryZeroReferenceTests` checks for forbidden tokens in source files. That is a useful guardrail, but it is still a lexical scan, not a structural dependency check. For a library extraction, the stronger gate is project-reference inspection plus compile-time API surface checks. Otherwise, the code can satisfy the test while still remaining architecturally coupled in a subtler way. fileciteturn16file0

### 5) `ICW-316` should explicitly preserve the XAML/designer path
The ticket already notes that the parameterless constructor must survive for XAML and designer support in the current control. That is good and should remain part of the acceptance criteria, because the WPF move will otherwise be fragile in a way that only shows up after the assembly split. The current control already exposes a designer-friendly construction path, so the extraction should preserve that exact behavior. fileciteturn23file0 fileciteturn1file0

### 6) The host still owns shape-specific overlay composition
`MainWindow.OnCanvasFramePublished` repopulates the tile-grid and annotation canvases after `CanvasFrame` publication, and the annotation layer still requires `SampleAnnotation` downcasts. That means the actual rendering model is not yet reusable across hosts. If `ICW-316` lands before `ICW-314`, the new library will extract visuals without extracting the interaction/render contract. That is viable, but only if the ticket explicitly treats `ICW-314` as the next follow-on rather than an implicit part of the extraction. fileciteturn1file0 fileciteturn24file0

## Corrections to the task tracker

`ICW-316` should be updated so the scope reads more like:
“Extract the reusable canvas surface into its own assembly, including the frame boundary types required by `ICW-315`, while keeping `CanvasViewModel` in a non-WPF net10.0 project. Freeze the contracts location before moving files.”

That wording better matches the code that already exists and avoids the false impression that only two files need to move. It also prevents duplication with `ICW-314`, because selection/tooltip ownership is still a separate contract problem, not just a file-move problem. fileciteturn23file0 fileciteturn24file0

## Audit verdict

`ICW-316` is a valid next slice, but only after it is widened to include `CanvasFrame` and the contracts-location decision. Without that adjustment, the extraction will either keep an App dependency alive or scatter the reusable boundary across too many assemblies. The architecture is ready for the move; the ticket just needs one more pass so it matches the codebase that now exists. fileciteturn1file0 fileciteturn23file0
