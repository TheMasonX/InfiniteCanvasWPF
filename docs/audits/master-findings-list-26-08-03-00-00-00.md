# Master Findings List

**Description:** Pre-council extraction of claims from the 15 requested Wave E and ICW delta audits. This ledger preserves provenance and does not accept report conclusions as evidence.
**Repo:** `InfiniteCanvasWPF`
**Fixed point:** `b5e1e8b210d3bf3c79caa0366d7ce052e6883cd5`
**Author:** GitHub Copilot
**Timestamp:** 2026-08-03 00:00 US Central
**Review mode:** Full reconciliation, pre-council claim extraction

## Scope

The ledger covers the 15 audit reports named in the user request. It does not yet independently verify source behavior, runtime impact, or task status. Verification will occur during the three-seat council review.

## Source IDs

| ID | Source |
| --- | --- |
| S1 | [infinitecanvaswpf-icw-delta-findings-26-08-03-03-49-42.md](infinitecanvaswpf-icw-delta-findings-26-08-03-03-49-42.md) |
| S2 | [icw-wave-e-audit-delta-4.md](icw-wave-e-audit-delta-4.md) |
| S3 | [icw-wave-e-audit-delta-3.md](icw-wave-e-audit-delta-3.md) |
| S4 | [icw-wave-e-audit-delta-2.md](icw-wave-e-audit-delta-2.md) |
| S5 | [icw-wave-e-audit.md](icw-wave-e-audit.md) |
| S6 | [infinitecanvaswpf-icw-delta-findings-26-07-31-05-34-52.md](infinitecanvaswpf-icw-delta-findings-26-07-31-05-34-52.md) |
| S7 | [infinitecanvaswpf-icw-delta-findings-26-07-31-20-50-16.md](infinitecanvaswpf-icw-delta-findings-26-07-31-20-50-16.md) |
| S8 | [infinitecanvaswpf-icw-delta-findings-26-08-01-04-24-56.md](infinitecanvaswpf-icw-delta-findings-26-08-01-04-24-56.md) |
| S9 | [infinitecanvaswpf-icw-delta-findings-26-08-02-00-23-17.md](infinitecanvaswpf-icw-delta-findings-26-08-02-00-23-17.md) |
| S10 | [infinitecanvaswpf-icw-delta-findings-26-08-02-00-50-33.md](infinitecanvaswpf-icw-delta-findings-26-08-02-00-50-33.md) |
| S11 | [infinitecanvaswpf-icw-delta-findings-26-08-02-02-53-23.md](infinitecanvaswpf-icw-delta-findings-26-08-02-02-53-23.md) |
| S12 | [infinitecanvaswpf-icw-delta-findings-26-08-02-14-41-44.md](infinitecanvaswpf-icw-delta-findings-26-08-02-14-41-44.md) |
| S13 | [infinitecanvaswpf-icw-delta-findings-26-08-02-15-38-22.md](infinitecanvaswpf-icw-delta-findings-26-08-02-15-38-22.md) |
| S14 | [infinitecanvaswpf-icw-delta-findings-26-08-02-21-41-46.md](infinitecanvaswpf-icw-delta-findings-26-08-02-21-41-46.md) |
| S15 | [infinitecanvaswpf-icw-delta-findings-26-08-02-22-51-06.md](infinitecanvaswpf-icw-delta-findings-26-08-02-22-51-06.md) |

## Candidate Claims

All rows are unverified claims extracted from reports. The preliminary class is a provenance hypothesis only.

| ID | Compact claim | Report IDs or topics | Proposed task | Preliminary class | Sources |
| --- | --- | --- | --- | --- | --- |
| C1 | Earlier tracker-sync complaint was later fulfilled. | Tracker synchronization | None | Correction | S1 |
| C2 | Duplicate ticket IDs remain or changed across reports. | ICW-081 and duplicate observations | ICW-081 | Corroboration, correction | S1, S3, S6, S9, S12, S15 |
| C3 | Wave E claimed duplicate-ID validation, but the validator lacked duplicate detection or tracker coverage. | ICW-081 | ICW-081 | Correction | S5 |
| C4 | ICW-081 was marked Done while ICW-100 remained duplicated. | ICW-081 | ICW-081 | Correction | S5 |
| C5 | ICW-078 remained stale in one tracker despite completed implementation evidence. | ICW-078, ICW-100 | ICW-081 | Correction | S5 |
| C6 | Benchmark scenario counts were described inconsistently with benchmark methods and parameterization. | ICW-144 | ICW-144 | Correction | S5 |
| C7 | `TileWorkItem.GetClaimantIds()` became orphaned after the interest-set fix. | ICW-143 | ICW-143 follow-up | Net-new or corroboration | S5 |
| C8 | Queued-item cancellation leaves claimant registrations attached until tokens fire. | ICW-P0-ACTIVECOUNT residuals | Lease or coordinator follow-up | Net-new | S5 |
| C9 | `default(ViewportInterestSet)` bypasses constructor null guards. | ICW-143 | ICW-143 follow-up | Net-new | S5 |
| C10 | `ViewportInterestSet` XML parameter names do not match constructor parameters. | ICW-143 | ICW-143 follow-up | Net-new | S5 |
| C11 | `CancelWorkItem` relies on an undocumented caller-held-lock contract. | Coordinator lock contract | New coordinator task | Net-new | S6 |
| C12 | `BackgroundTileCacheKey.SourceId = "synthetic"` is duplicated across production and tests. | ICW-018 extension | ICW-018 | Net-new extension | S6 |
| C13 | `ISpatialIndexService<T>` lacks publish-timestamp capability and causes a concrete downcast. | Spatial interface shape | New spatial task | Net-new, impact correction | S6, S11, S13 |
| C14 | `GeneratorOptions` is active while `MipOptions` is unreferenced. | ICW-188 | ICW-188 | Correction | S6 |
| C15 | The option-based `GenerateSet` entry point exists, but deprecation, direct callers, and parity tests are missing. | ICW-189 | ICW-189 | Correction | S6 |
| C16 | ICW-088 extraction remains incomplete because a dead duplicate method and unwired option consolidation remain. | ICW-088 | ICW-088 | Correction | S6 |
| C17 | ICW-188 and ICW-189 ticket files are absent from active trackers. | ICW-188, ICW-189 | Tracker follow-up | Correction | S6 |
| C18 | Noise defaults exist in four locations and `NoiseOctaves` differs between them. | Settings validation | ICW-P1-SETTINGS-VALIDATION | Net-new extension | S6 |
| C19 | `CanvasUserSettings.IsValid` permits `BackgroundNoise` above the effective consumer range. | Settings validation | ICW-P1-SETTINGS-VALIDATION | Net-new extension | S6 |
| C20 | `DrawDefectPatch` reads `DefectBitmap` into an unused local value. | Rendering cleanup | Existing defect bitmap cluster | Net-new | S2 |
| C21 | `DefectBitmap` duplicates `DefectPixels` and may be removable. | Defect bitmap lifecycle | ICW-102, ICW-103 | Extension | S2 |
| C22 | Pixelometer logic performs duplicate queries and uses inconsistent max-wins versus last-wins composition. | ICW-035, ICW-100, ICW-055 | Existing pixelometer tasks | Corroboration, correction | S2, S4, S7 |
| C23 | Pixelometer fallback allocates and sorts mip candidates under a mouse-movement lock. | ICW-020, ICW-055 | Pixelometer follow-up | Net-new | S4 |
| C24 | Pixelometer hover can start asynchronous tile generation when a mip is absent. | ICW-P0-PIXELOMETER-READOUT | ICW-076 or snapshot follow-up | Correction | S8, S11 |
| C25 | Pixelometer accounting was fixed, but the registry combines accounting and no-acquisition clauses. | ICW-P0-PIXELOMETER-READOUT | Follow-up task | Correction | S8 |
| C26 | Projection benchmarks exercise an unused point-cloud overload instead of the shipped tile compositor path. | ICW-133 | ICW-133 | Corroboration, sharpening | S9 |
| C27 | Closed and half-open spatial-boundary semantics differ across query and rendering paths. | Boundary semantics | ICW-064 boundary ticket | Corroboration, correction | S9 |
| C28 | `CanvasViewportViewModel<T>` runs per frame but is not bound to the window, while `MainViewModel` overlaps displayed work. | ICW-017 | ICW-017 | Correction | S13, S14 |
| C29 | README claims `IAsyncRelayCommand`, `RefreshCommand`, and `IsRunning` behavior not present in the shipped orchestration. | README MVVM | Documentation follow-up | Documentation correction | S13, S14 |
| C30 | `SelectedAnnotationFeatures` has a dead XAML binding because code-behind overwrites `ItemsSource` without notification. | Feature grid binding | New task or ICW-022 | Net-new | S7 |
| C31 | Numeric scene settings use unbounded text boxes while comparable settings use bounded sliders. | Settings UI | ICW-P1-SETTINGS-VALIDATION | Extension | S7 |
| C32 | Scrollbar handlers repeat axis-selection ternaries. | ICW-077 | ICW-077 | Extension | S7 |
| C33 | MainWindow XAML lacks automation properties and equivalent accessibility markup. | ICW-037 | ICW-037 | Correction | S7 |
| C34 | Cache reservation can evict a non-pinned tile whose generation is queued, contrary to ICW-064 notes. | ICW-064 | ICW-064 or ICW-104 | Correction | S3 |
| C35 | Queue promotion can repeat full scans and rebuilds under adversarial stale and visible layouts. | ICW-144 | ICW-144 | Net-new extension | S3, S5 |
| C36 | `Queue<T>` creates structural costs for mid-queue removal and priority reordering. | ICW-144 | ICW-144 | Extension | S5 |
| C37 | Claimant completion and failure callback exceptions are swallowed by empty catches. | ICW-143 | Coordinator cleanup | Net-new | S5 |
| C38 | Queue scan-ahead allocates two lists for a non-visible head. | ICW-144 | ICW-144 | Net-new extension | S6 |
| C39 | ADR-0006 specifies center-distance and mip-suitability tie-breakers absent from ICW-143. | ICW-143, ADR-0006 | Scheduling follow-up | Correction | S10 |
| C40 | Deleting `IBackgroundTileSource` would conflict with ADR-0005 architectural intent. | ICW-018, ADR-0005 | ICW-076 | Correction, retraction | S11 |
| C41 | Pixelometer acquisition is an unstarted ADR-0005 migration step, not wholly new work. | ICW-076, ADR-0005 | ICW-076 | Correction, redirection | S11 |
| C42 | DesignDoc leaves zoomed-out overdraw open, `MinimumSparseTilePixelSize` is unused, and ICW-004 is a stub. | ICW-004, ICW-099 | ICW-004 | Corroboration, correction | S12 |
| C43 | Resize debouncing exists and resolves the related DesignDoc open question. | DesignDoc resize question | None | Rejected as finding | S12 |
| C44 | README MVVM claims conflict with actual MainViewModel and coalescing design. | README, ICW-016 | Documentation follow-up | Corroboration | S13, S14 |
| C45 | ICW-017 still describes a removed `RefreshCommand`. | ICW-017 | ICW-017 | Correction | S14 |
| C46 | ICW-082 background-image persistence remains correct in current code. | ICW-082 | None | Rejected as finding | S14 |
| C47 | The first audit series re-derived findings already present in the prior master synthesis. | Prior synthesis provenance | None | Correction | S15 |
| C48 | The first audit series added four granular duplicate-ID data points not enumerated in the prior synthesis. | ICW-055, ICW-100, ICW-064, ICW-004 | ICW-081 | Corroboration | S15 |

## Explicit Evidence Requests

The following claims remain hypotheses until direct evidence resolves them: complete duplicate-ID counts, runtime frequency and impact of pixelometer acquisition, performance impact of fallback sorting and queue scans, absence of indirect `DefectBitmap` consumers, benchmark coverage through non-literal paths, the correct noise-default policy, full diagnostics coverage, exact async-handler counts, and causal explanations for historical tracker changes.

## Handoff to Council

The council must independently verify each accepted mechanism against source, tests, benchmarks, ADRs, requirements, and trackers. It must preserve separate Standards and Spec judgments, retain dissent, assign severity independently from confidence, and create or update no task until duplicate identity and existing coverage are confirmed.
