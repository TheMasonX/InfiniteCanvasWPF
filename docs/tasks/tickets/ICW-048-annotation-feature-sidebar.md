# ICW-048: Annotation Feature Sidebar

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Expose the selected annotation's feature metadata in a sidebar DataGrid so an inspection selection has a concrete detail view.

## Scope

- Bind selection from the canvas to a feature-row collection.
- Show feature name and value columns in the existing side-panel layout.
- Provide an explicit empty state after selection clears or scene regeneration.

## Validation

- Add focused selection-to-detail mapping coverage where logic can remain UI-independent.
- Run the Release app build and Windows UI smoke test.

## Findings

- `SampleAnnotation.Features` already supplies the required metadata, while current canvas selection changes only overlay state.

## Next Step

- Select a view-model or code-behind data-binding shape consistent with the current side-panel implementation.