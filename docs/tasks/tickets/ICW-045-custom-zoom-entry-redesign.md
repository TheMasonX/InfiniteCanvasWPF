# ICW-045: Custom Zoom Entry Redesign

- Status: To Do
- Date: 2026-07-24
- Owner: InfiniteCanvas Agent

## Summary

Remove the current custom zoom UI. Replace it later with a Custom dropdown value that exposes a textbox and Apply button, with Enter invoking the same apply action.

## Scope

- Remove the rejected standalone custom zoom control now.
- Retain standard preset command behavior.
- Future implementation places custom entry in the dropdown flow.
- Apply is available through both a button and Enter key.

## Validation

- Verify the app builds after removal.
- Future implementation adds command and keyboard interaction coverage.

## Findings

- The current custom zoom UI does not fit the intended interaction model.
- The rejected standalone textbox, Apply button, and Custom preset item have been removed.
- The replacement remains intentionally deferred until its inline dropdown interaction is designed.

## Next Step

- Add a Custom dropdown value with inline textbox, Apply action, and Enter handling now that ICW-044 semantics are stable.