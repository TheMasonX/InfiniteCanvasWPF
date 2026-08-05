# InfiniteCanvasWPF — Delta Report: `BoundedNumeric.TryParse`'s Integer Path Can Throw Instead of Returning False

**Previous reports:** eighteen prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**. This session reads `BoundedNumeric.cs` (new since session 16, not yet read) and traces its one current consumer, `SliderTextBox`, across all eight current usages to verify a suspected edge case rather than just flagging it theoretically.

---

## 1. Finding: `BoundedNumeric.TryParse`'s `Integer` branch throws `ArgumentException` instead of returning `false` when the configured bounds contain no integer

**Good news first:** `BoundedNumeric` is exactly the kind of consolidated, single-source-of-truth parse/clamp/format helper this series has recommended repeatedly for the noise-parameter validation duplication (my report 3's §2.5, and the `ICW-301`/`304` family) — it's well-documented, WPF-free for testability, and is already the shared path for both the slider and text-box halves of the new `SliderTextBox` control. This is a genuine improvement over the prior four-copies-of-the-same-validation state.

**The gap:**
```csharp
case NumericKind.Integer:
    if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        return false;
    value = Math.Clamp(intValue, (int)Math.Ceiling(minimum), (int)Math.Floor(maximum));
    return true;
```
`Math.Clamp(T, T, T)` throws `ArgumentException` if its `min` argument is greater than its `max` argument. `Math.Ceiling(minimum)` and `Math.Floor(maximum)` can invert the ordering even when `minimum <= maximum` themselves: any range narrower than 1.0 that doesn't straddle a whole number produces exactly this. For example, `minimum = 5.5, maximum = 5.9` (a legal, `minimum < maximum` range) gives `Ceiling(5.5) = 6` and `Floor(5.9) = 5`, so `Math.Clamp(intValue, 6, 5)` throws on **every single successful integer parse** — a `TryParse` method, whose entire contract is "return false on invalid input, don't throw," throwing an unhandled exception instead for a configuration its own signature doesn't forbid.

**I checked whether this is reachable today, not just theoretically possible.** All eight current `SliderTextBox` usages, in `TileBackgroundNoiseSettingsView.xaml`, use whole-number bounds for every field visually suggesting integer semantics (Target value 0–255, Defect circles 0–8, Noise octaves 1–12) — `Ceiling`/`Floor` are no-ops on whole numbers, so none of today's usages can trigger this. `SliderTextBox.xaml.cs`'s `NumericType` dependency property has no `ValidateValueCallback` or cross-check against `Minimum`/`Maximum` at the property-system level either — nothing anywhere in the current stack guards against a future `Kind="Integer"` declaration with fractional or sub-1.0-wide bounds. This is the same "safe today only because every current caller happens to avoid it" shape this series has now found three times in freshly-written code (`CameraSnapshot`'s division, `CanvasViewModel.ComputeMinimumZoom`'s division, and now this) — worth noting as a recurring pattern across the codebase's newest additions, not just three unrelated one-offs.

**Recommendation:** clamp using the raw `minimum`/`maximum` as doubles first, then round/convert — e.g. `value = (int)Math.Round(Math.Clamp(intValue, minimum, maximum));` — which can never invert the min/max ordering, since it clamps against the caller's original (already-valid, `minimum <= maximum`) bounds directly rather than derived ceiling/floor values. Add a unit test with a narrow fractional range (e.g., `TryParse("6", NumericKind.Integer, 5.5, 5.9, out _)`) asserting it returns `false` or a sane clamped value rather than throwing, alongside `BoundedNumericTests.cs`'s existing coverage.

**Confidence:** 95% (the exact throw condition is a direct, verifiable fact about `Math.Clamp`'s documented behavior combined with the method's own arithmetic; the "not reachable by any current caller" claim is confirmed by reading all eight current `SliderTextBox` declarations and the `NumericType` property's complete lack of cross-validation).

---

## 2. Corrections Summary Table

| Item | Status | Finding | Basis |
|---|---|---|---|
| `BoundedNumeric.TryParse` (`NumericKind.Integer` branch) | New shared utility, otherwise well-designed | **New finding**: throws `ArgumentException` instead of returning `false` when `Ceiling(minimum) > Floor(maximum)` — reachable by any future narrow-fractional-range `Kind="Integer"` declaration, not reachable by any of the 8 current usages. Recommend clamping against the original double bounds before rounding, plus a regression test. | §1 |

---

## 3. Assumptions & Open Questions

- I did not read `DeferredAnnotationToolTip.cs`, `FrameBufferPool.Windows.cs`, or `SliderTextBox.xaml.cs` in full this session (only grepped the latter for the specific `NumericType` property) — all three are new since session 16 and remain unread; good candidates for a future session, particularly `FrameBufferPool.Windows.cs` given this series' history of finding real issues in the zero-copy/buffer-lifecycle code specifically.
- `BoundedNumericTests.cs` exists (confirmed present in the new-files list from session 16's diff) but was not read this session to check whether it already covers or contradicts this finding — worth checking first in a future session before assuming this is entirely uncaught by existing tests.

---

*Methodology note: this session read `BoundedNumeric.cs` in full, and rather than flagging the `Math.Clamp`/`Ceiling`/`Floor` interaction as a theoretical concern, traced every current call site (`TileBackgroundNoiseSettingsView.xaml`'s eight `SliderTextBox` declarations, plus `SliderTextBox.xaml.cs`'s `NumericType` property definition) to confirm whether the edge case is reachable today, consistent with this session's standing instruction to verify against actual files rather than assume.*
