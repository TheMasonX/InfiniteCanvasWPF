# ICW-067: Debug property editor

## Status
Proposed

## Summary
Add a lightweight debug property editor surface that lets developers inspect and modify the current generator and viewport-related settings at runtime.

## Scope
- Add an expandable debug section in the side panel or a dedicated dialog.
- Surface the current settings for tile generation, background noise, display options, and viewport state via editable controls.
- Keep the implementation lightweight and avoid introducing a broad new architecture unless the UI proves it is worth it.

## Validation
- dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release

## Findings
- The current app already has a display panel and cached debug controls. A compact editor should reuse the existing settings path rather than introducing another state store.

## Next Step
Design the smallest editor surface that can edit a few high-value settings and bind directly to existing state.
