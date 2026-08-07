# Wave X Handoff, Tooltip Detach Cleanup

## Status

Wave X completes ICW-314 after a lifecycle review of Wave W.

## Review Finding

Wave W cleared registered tooltips before accepted frames and during ClearFrame.
DetachFrameShell did not clear them. A host that detached the shell directly could retain deferred tooltip objects on detached visuals.

## Delivered

- DetachFrameShell now clears registered item tooltips before it drops shell references.
- Added a Windows consumer-host regression test for direct shell detachment.
- Marked ICW-314 Done in the ticket and active tracker.

## Evidence

- Focused consumer-host tests pass, 7/7.
- Core tests pass, 197/197.
- Windows tests pass, 28/28.
- App Release build passes with the existing warning.
- Task tracker validation passes.
- git diff --check passes.

## Next Step

Keep the ICW-314 tooltip boundary stable. Start ICW-313 only if input-handler abstraction is reprioritized from its deferred P3 status.