# InfiniteCanvasWPF — Delta Report: `CanvasControl.xaml` Hardcoded Positioning, and a Council-Flagged Cleanup Confirmed Still Outstanding

**Previous reports:** seventeen prior audits, all in `docs/audits/`.
**This report's commit:** `main` tip — full recursive diff against the last session's tree confirms **zero changes since the last report**. This session closes the two gaps flagged in the last two reports: `CanvasControl.xaml` (markup) and the full `canvas-data-source-abstraction-council-review-26-08-04.md`.

---

## 1. New finding: `LoadingOverlay`'s vertical position is a hardcoded pixel margin, not layout-driven — undermines the "any host, any size" goal this exact effort is working toward

`CanvasControl.xaml`:
```xml
<TextBlock x:Name="LoadingOverlay" Text="BUILDING INITIAL SNAPSHOT"
           HorizontalAlignment="Center" VerticalAlignment="Top"
           FontFamily="Cascadia Mono" FontSize="14" FontWeight="SemiBold"
           Foreground="{StaticResource AccentBrush}" Margin="0,446,0,0" Grid.Row="1" />
```
Horizontal centering is done properly (`HorizontalAlignment="Center"`), but vertical positioning is not — `VerticalAlignment="Top"` combined with a **hardcoded `446`-pixel top margin** to fake vertical centering, instead of `VerticalAlignment="Center"`. This value is tied to whatever viewport height the control happened to be authored/tested at; on a host of a different size — including, per ADR-0007's own explicit goal, "another application" hosting this control at whatever size it chooses — the loading text will sit at a fixed 446px from the top of its row rather than centered, potentially far off-center or entirely off-screen on a smaller host. This is a direct, concrete instance of exactly the kind of "arbitrary choice that undermines reusability" this extraction effort is trying to eliminate everywhere else (data sources, frame boundary, input handling) — just at the XAML layout level rather than the C# API level this series has focused on the last two sessions.

I checked for an existing ticket first: `ICW-146` (loading indicator, Done) covers *when* `RenderBusyBar`/`LoadingOverlay` become visible (wiring visibility to tile-generation activity), not *where* `LoadingOverlay` is positioned — no overlap, and no other ticket mentions this file's layout specifically.

**Recommendation:** change `VerticalAlignment="Top"` to `VerticalAlignment="Center"` and remove the `Margin="0,446,0,0"`, letting WPF's layout system center it the same way `HorizontalAlignment="Center"` already does horizontally. Trivial fix, but worth doing before or alongside `ICW-316`'s assembly extraction — a hardcoded pixel value tuned to one window size is a bad thing to ship as part of a "reusable component" library's default template.

**Secondary, lower-confidence observation from the same file:** the outer `Grid.RowDefinitions` use `Height="17*"` and `Height="925*"` — an oddly-specific star-ratio (≈1:54.4) that reads like it was captured from a designer tool's pixel-to-proportion conversion at one specific window size, rather than deliberately chosen. I'm not confident this causes a visible problem (star-sizing scales correctly regardless of the specific ratio chosen), so I'm noting it only as a "looks accidental" observation, not a defect — worth a quick sanity check by whoever next edits this file, not urgent on its own.

**Confidence:** 90% for the `LoadingOverlay` margin (direct XAML read, straightforward WPF layout reasoning); 40% for the row-definition ratio being meaningfully wrong rather than just unusual-looking.

---

## 2. Confirmed still outstanding: the council review's own "cleanup noted" item (dead `InfiniteCanvas.Spatial` reference) has not been removed yet

`canvas-data-source-abstraction-council-review-26-08-04.md`'s Follow-up Tickets section ends with: *"Cleanup noted: remove the dead `InfiniteCanvas.Spatial` project reference from `InfiniteCanvas.ViewModels.csproj` inside the zero-reference gate."* I checked directly rather than assuming this was already actioned given how recent the review is: `src/InfiniteCanvas.ViewModels/InfiniteCanvas.ViewModels.csproj` still contains `<ProjectReference Include="..\InfiniteCanvas.Spatial\InfiniteCanvas.Spatial.csproj" />`, with no `.cs` file in the project actually using anything from `InfiniteCanvas.Spatial` (consistent with `CanvasViewModel.cs`'s own `using` list, read in full last session: only `CommunityToolkit.Mvvm.ComponentModel` and `InfiniteCanvas.Core`).

**This isn't a new finding** — the council review already identified it — but it's directly relevant right now because `ICW-312`'s own **Acceptance Criteria, gate 1 ("Zero-reference gate")** requires `CanvasViewModel.cs` to be free of `InfiniteCanvas.Spatial` — and while the *source code* already satisfies that at the `using`-statement level, the *project reference* itself is still present, which is the more complete/correct interpretation of a "zero-reference gate" for a library that's eventually moving to its own assembly (`ICW-316`). A stray project reference doesn't show up in a `.cs`-file content scan but would still pull in the entire `InfiniteCanvas.Spatial` assembly (and its NuGet dependency, NetTopologySuite, per earlier sessions' reads of that project) for any consumer of `InfiniteCanvas.ViewModels.csproj` — exactly the kind of hidden coupling a "zero-reference gate" should be built to catch. Worth flagging precisely so whoever implements `ICW-312`'s gate-1 automation (the "scan test" the acceptance criteria mention) checks `.csproj` references, not just `.cs` file contents, or this exact stray reference will pass a naive content-only scanner while still violating the gate's intent.

**Confidence:** 95% (the `.csproj` file read directly; the council review's own text quoted verbatim).

---

## 3. Corrections Summary Table

| Item | Status | Finding | Basis |
|---|---|---|---|
| `CanvasControl.xaml`'s `LoadingOverlay` | Not covered by any existing ticket | **New finding**: hardcoded `Margin="0,446,0,0"` fakes vertical centering instead of using `VerticalAlignment="Center"` — breaks on any host viewport size other than whatever it was tuned against. Trivial fix. | §1 |
| `InfiniteCanvas.ViewModels.csproj`'s dead `InfiniteCanvas.Spatial` reference | Already noted by the council review as a cleanup item | **Confirmed still present** as of this session — not yet removed. Flag precisely that `ICW-312`'s "zero-reference gate" scan test should check `.csproj` references, not just `.cs` file contents, or this exact case will pass a naive scanner while still violating the gate's intent. | §2 |

---

## 4. Assumptions & Open Questions

- Both flagged gaps from the prior two reports (`CanvasControl.xaml` and the full council review) are now closed; I don't have a specific next-file target queued for a future session beyond what the council review's own "Open Questions" and "Follow-up Tickets" sections already name (`ICW-315`, `ICW-316`, and the four open questions about hit-testing ownership, change-event vs. polling, `JIRA.md` canonicity, and `ICW-315`/`ICW-314` sequencing).
- The row-definition ratio observation in §1 is speculative and not verified against how `MainWindow`'s equivalent (pre-extraction) markup handled the same layout — a quick comparison against `MainWindow.xaml`'s history (if this control's markup was lifted from there) could confirm or dismiss it in a future session.

---

*Methodology note: this session read `CanvasControl.xaml` and `canvas-data-source-abstraction-council-review-26-08-04.md` in full — the two specific gaps named in the previous two reports' "Assumptions & Open Questions" sections — then, per this session's practice of verifying rather than assuming, directly re-checked the council review's one already-flagged cleanup item against the current `.csproj` file rather than assuming it had already been actioned given the review's recency.*
