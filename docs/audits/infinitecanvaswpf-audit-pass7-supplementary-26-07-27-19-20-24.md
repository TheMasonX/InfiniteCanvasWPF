# InfiniteCanvasWPF — Audit Pass 7 (Supplementary, Same HEAD)

**HEAD:** `139a8b62fa2d6363615eb6a819d07a76aa8c55c2` — unchanged since pass 6. I checked (via GitHub's commit feed and a direct `task-tracker.md` content diff) and confirmed `main` has not advanced. Rather than manufacture findings against nothing new, I used this pass to finish sweeping files at this same HEAD that changed recently but weren't yet reviewed in depth: `GeneratorOptions.cs`, `MipOptions.cs`, `AnnotationGenerator.cs`, `App.xaml.cs`, `SerilogHost.cs`, and the `MainViewModel.cs` diff.

---

## 1. Verified fix, with a caveat: global exception handling now exists but swallows unconditionally

**Good news first:** my pass-1 report's §2.1 finding ("no global crash safety net") is now resolved. `App.xaml.cs:OnStartup` wires up all three of `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, and `TaskScheduler.UnobservedTaskException`, each logging via the newly-added Serilog host (`SerilogHost.cs`, itself correctly assigned to the ambient `Log.Logger` before any other logging call runs). Good, confirmed resolution.

**The caveat (new finding, Low-Medium severity, confidence 80%):**
```csharp
private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
{
    Log.Error(e.Exception, "Unhandled exception on the WPF dispatcher");
    e.Handled = true;
}
```
`e.Handled = true` is set **unconditionally, for every exception, with no differentiation**. This means the app can now never crash from a dispatcher-thread exception — which fixes the original problem — but it also means there's no distinction between a recoverable glitch and an exception indicating genuinely corrupted state (e.g., something throwing mid-render leaving `_tiles`/`_annotations`/the coordinator's internal dictionaries in a half-updated state). The app will just log and keep running in whatever state it was in when the exception fired, silently, with no user-facing indication anything went wrong. Blanket top-level exception suppression is itself a recognized anti-pattern for exactly this reason: it can turn "the app crashes and someone notices" into "the app quietly limps along with corrupted state and nobody notices until much later," which is arguably a worse failure mode for a project this focused on avoiding hidden defects.

**Recommendation:** Not urgent, but worth a follow-up ticket: consider surfacing a lightweight, non-blocking user notification on unhandled dispatcher exceptions (e.g., a status-bar message) rather than fully silent recovery, and/or reserve `e.Handled = true` for exception types known to be safe to continue past, letting anything else propagate (or trigger a deliberate, controlled restart) rather than blanket-swallowing everything. Low priority relative to everything else in the backlog, but cheap to note now while it's fresh.

---

## 2. [LOW] `GeneratorOptions.ImageCount`'s default value is a self-acknowledged placeholder that means nothing
**File:** `src/InfiniteCanvas.Rendering/GeneratorOptions.cs:3-4`
**Confidence: 90%**

```csharp
public sealed record GeneratorOptions(
    int ImageCount = SampleImageGenerator.DefaultPixelWidth, // placeholder, overwritten by default usage
    int PixelWidth = SampleImageGenerator.DefaultPixelWidth,
    ...
```
`ImageCount`'s default is literally `SampleImageGenerator.DefaultPixelWidth` (`= 8192`) — a pixel-dimension constant reused as a placeholder for a completely unrelated "how many images" default, with the record's own inline comment admitting it's meaningless and "overwritten by default usage." I confirmed `GeneratorOptions` has exactly one construction site in the whole codebase (`SampleImageGenerator.cs:108`, the forwarding overload), and it does explicitly set `ImageCount: imageCount` — so this placeholder is never actually hit today. But `GeneratorOptions` is a `public sealed record`, not `internal`, so nothing stops a future caller (a new call site, a test, a benchmark, or a `with` expression that forgets this one property) from constructing it directly and silently getting a "default" of 8,192 images — a nonsensical value with no error or warning, since `GenerateSet(GeneratorOptions options)`'s own validation (`if (options.ImageCount <= 0) throw ...`) only rejects `<= 0`, not "suspiciously large."

**Recommendation:** Give `ImageCount` a default that's actually meaningful on its own (e.g., `64`, matching the tile-count default used elsewhere in the app) or remove the default entirely (making it a required positional parameter) so there's no silent fallback to a value that was never meant to be used. Small, mechanical fix; flagging now mainly because "the default is admitted-in-a-comment to be fake" is exactly the kind of brittle, self-aware-but-unaddressed landmine worth closing out while the codebase is still small enough that fixing it costs nothing.

---

## Status

No other new findings this pass — the remaining diffed files (`MainViewModel.cs`'s new noise-parameter plumbing, `CameraTransform.cs`'s trivial using-removal, `CanvasUserSettings.cs`'s new noise fields) were checked and are consistent/correctly wired (noise settings round-trip through persistence with matching validation ranges and defaults). Will pick back up with a full delta pass once new commits land.

