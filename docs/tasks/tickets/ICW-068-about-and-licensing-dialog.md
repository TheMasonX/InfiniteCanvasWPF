# ICW-068: About and licensing dialog

## Status
Proposed

## Summary
Add a discoverable About dialog with project attribution, third-party licensing information, and links to bundled license text.

## Scope
- Add a small About entry to the UI, likely in the header or a help menu.
- Show the core project attribution, used libraries, and a scrollable license list.
- Copy a licenses folder into the build output and link each item to the bundled text.

## Validation
- dotnet build src/InfiniteCanvas.App/InfiniteCanvas.App.csproj --configuration Release

## Findings
- The request is comparatively low-risk and can be implemented as a lightweight dialog with minimal impact on rendering and interaction logic.

## Next Step
Add the dialog UI and a simple license asset bundle for the current project dependencies.
