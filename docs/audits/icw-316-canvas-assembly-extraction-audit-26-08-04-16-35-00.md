# ICW-314 Next Slice Audit

> **Provenance note (2026-08-06):** this file is a byte-for-byte copy of
> `docs/audits/icw-314-next-slice-audit-26-08-04-16-20-00.md` (identical MD5
> `B0EA13324A8562539073228C8DA053D0`). Despite the ICW-316 filename, its
> content audits only the ICW-314 selection/tooltip slice. It does not review
> the ICW-316 assembly extraction. Do not cite this file as an assembly-
> extraction audit. The Wave H extraction was delivered under ICW-316 (commit
> `c552830`). See the audit synthesis report of 2026-08-06.

Report ID: 84a0cdb5f817-314c
Audited scope: proposed `ICW-314` selection and tooltip ownership, in the context of the current `ICW-312` / `ICW-315` boundary and ADR-0007 alignment.
Baseline commit: `84a0cdb5f8178286ae4784e1f6221cd7ae06e7f1`

## Executive summary

`ICW-314` is the first slice that actually moves the canvas toward the reusable component promised by ADR-0007. The boundary commit already landed the data-source seam (`ICW-312`) and the frame boundary (`ICW-315`), so the remaining work is no longer about how the canvas receives data. It is about whether the canvas itself owns selection, hover, and tooltip behavior. fileciteturn1file0 fileciteturn23file0 fileciteturn24file0

The problem is that the current item contract is still too shallow for that move. `ICanvasItem` only exposes `Id` and `Bounds`, while ADR-0007 says the canvas should own selection and tooltip hover against an item contract that includes hit testing, tooltip payload, and a visual template. That mismatch means `ICW-314` is valid as the next slice, but not yet as a small one. It needs a contract extension, not just a handler relocation. fileciteturn5file0 fileciteturn24file0

## Findings

### 1) `ICW-314` is under-specified relative to ADR-0007
The task label says “selection and tooltip ownership,” but the ADR makes the contract broader: the control must own hit testing, selection state, and tooltip display against a richer item abstraction. The current code only gives the host and canvas a minimal item surface. That is enough for visibility queries, but not enough for a fully-owned interaction path. fileciteturn24file0 fileciteturn5file0

### 2) `MainWindow` still owns the shape-specific interaction path
The current design still keeps selection state and tooltip creation in the host, and overlay composition is still repopulated after frame publication. That means the component boundary is not yet reusable in the interaction sense: another host could supply a different scene source, but it would still need to reimplement the interactive behavior. fileciteturn1file0 fileciteturn24file0

### 3) The item contract needs to grow before the move can be honest
ADR-0007 explicitly names item capabilities that are not present in `ICanvasItem` yet. The codebase therefore has a choice: either extend the item contract now, or accept that `ICW-314` will be another half-step that needs follow-up work later. The stronger path is to make the contract carry the real interaction payload before moving selection and tooltip ownership into the control. fileciteturn24file0 fileciteturn5file0

### 4) `CanvasViewModel.VisibleItems` is still a bridge, not a destination
`CanvasViewModel` still stores visible items so the host can render against them. That is acceptable as a transition, but it also shows where the ownership boundary still stops. If `ICW-314` lands properly, the canvas should own the interaction path directly and the view model should stop being the place where interactive item state is threaded through as a temporary escape hatch. fileciteturn6file0

### 5) The assembly extraction slice and the interaction slice are orthogonal
`ICW-316` is about where the canvas lives. `ICW-314` is about what the canvas owns. Those are separate concerns. `ICW-314` can still be audited and prepared now, but it should not be mistaken for a packaging task. Its value is behavioral: it is the slice that would finally move the UI interaction logic out of the host. fileciteturn23file0 fileciteturn24file0

## Corrections to the task tracker

`ICW-314` should be tightened so the scope reads more like:

“Move selection and tooltip hover into `CanvasControl`, extend `ICanvasItem` with the interaction payloads needed for hit testing and tooltip rendering, and keep `CanvasViewModel` only as the bridge until the control owns the full interaction path.”

That wording matches ADR-0007 more closely and makes the contract extension explicit. It also prevents the slice from being mistaken for a simple event-handler move, which would undercut the reusable-component goal. fileciteturn24file0 fileciteturn5file0

## Audit verdict

`ICW-314` is the right next functional slice after the current boundary work, but it is not ready to land cleanly until the item contract grows to support real selection and tooltip behavior. The architecture is pointed in the right direction; the remaining work is to make the abstraction honest enough that the control can truly own the interaction path. fileciteturn24file0 fileciteturn1file0
