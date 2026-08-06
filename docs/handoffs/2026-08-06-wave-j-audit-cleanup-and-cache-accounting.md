# Handoff: Wave J — Audit Corpus Privacy Cleanup Complete; Next Slice Is Cache Accounting and Lifecycle Hardening

Date: 2026-08-06

## Status

Wave I landed at HEAD `9863c42`. This session delivered a documentation-only
cleanup (ICW-331). It renamed the 11 supplied viewport audit reports and
sanitized the historical audit corpus. No source code changed.

The working tree now contains only documentation changes. The audit corpus is
secret-safe and the task tracker is valid.

## What Landed

### ICW-331 — Secret-safe audit corpus cleanup (expanded)

- Renamed 11 supplied audit reports to neutral viewport filenames under `docs/audits/`.
- Rewrote the reports with neutral viewport terminology.
- Sanitized the historical audit files. The changes replaced external audit
  document titles with neutral descriptors, removed the assistant product
  name, and neutralized repository archive and API host references.
- Updated `docs/requirements/functional-requirements-and-invariants.md` with
  the documentation safety requirement.
- Updated the ICW-331 ticket and both task trackers.

Validation evidence:

- Protected-term scan passed across all 101 audit files.
- General finding vocabulary remained present across the 11 reports (1,460 matching evidence lines).
- Task tracker validator passed: 218 task files validated, 5 legacy markdown files skipped.

## Recommended Wave J Scope

Close the source-backed cache-accounting and lifecycle-hardening slice. The
tickets already exist. No product decision is required.

1. `ICW-P0-LEASE-RELEASE` — replace the `ReleaseReservation` counter with an `ICacheReservation : IDisposable` lease.
2. `ICW-P1-PIXELCOST-MIPS` — replace `_pixelCost` with a sum-of-resident-mips method.
3. `ICW-134` — variant-aware background cache accounting and reuse. It depends on the two items above.
4. `ICW-110` — convert `async void` handlers to safe wrappers. It closes the shutdown and exception-surface findings.
5. `ICW-112` — expose a structured `TileCacheDiagnosticsSnapshot` API.

This scope maps to the cache findings in the cleaned bug-sweep deltas
(`ICW-DEEP2-072` through `ICW-DEEP2-076`) and the readiness report cache gate.

## Deferred or Product-Decision-Gated

Do not start these items.

- `ICW-313` (IInputHandler) and `ICW-314` (selection and tooltip ownership) are user-deferred. `ICW-314` waits on `ICW-031` typed metrics.
- `ICW-324` (seamless noise) and `ICW-325` (anisotropic mip selection) need product decisions and ADR-0005 alignment.
- `ICW-144` needs fresh fast-scroll BenchmarkDotNet evidence on target hardware. Pair it with `ICW-132` and `ICW-133` when profiler hardware is available.

## Files Touched

- docs/audits/ (11 renamed viewport reports and the sanitized historical corpus)
- docs/requirements/functional-requirements-and-invariants.md
- docs/tasks/tickets/ICW-331-secret-safe-audit-corpus-cleanup.md
- docs/tasks/active-tasks.md
- docs/tasks/JIRA.md
- docs/handoffs/2026-08-06-wave-j-audit-cleanup-and-cache-accounting.md

## Validation Commands

- `dotnet build InfiniteCanvasWPF.slnx -c Release`
- `dotnet test tests/InfiniteCanvas.Tests -c Release`
- `dotnet test tests/InfiniteCanvas.Windows.Tests -c Release`
- `pwsh -NoProfile -File scripts/Validate-TaskTracker.ps1 -Path docs/tasks`

## Open Items and Recommended Next Step

Start with `ICW-P0-LEASE-RELEASE`, then `ICW-P1-PIXELCOST-MIPS`, then
`ICW-134`. Add the `MemoryGovernor_AccountsForAllResidentMipLevelsNotJustNative`
and leak-detection tests that ICW-134 specifies.
