---
id: ICW-P1-PIXELCOST-MIPS
author: External Audit (Integration-1)
key: ICW-P1-PIXELCOST-MIPS
title: Replace _pixelCost with sum of all resident mip payload bytes
status: Done
type: Bug
priority: P1
tags:
  - cache
  - accounting
  - mipmaps
  - memory
dependsOn: []
related:
  - ICW-134
  - ICW-P0-LEASE-RELEASE
  - ICW-076
links:
  - src/InfiniteCanvas.Rendering/SampleImageTile.cs
  - docs/audits/infinitecanvaswpf-icw-implementation-audit-26-07-30-16-40-49.md
created: 2026-07-30
updated: 2026-08-06
---

# ICW-P1-PIXELCOST-MIPS — Replace `_pixelCost` with sum of all resident mip payload bytes

## Summary

**Critical gap:** `SampleImageTile._pixelCost = checked(pixelWidth * pixelHeight)` is computed once at construction from mip-0 dimensions only and never revised. `TileCacheBudget.TryReserve` and `Release` charge against this fixed value. Once a tile accumulates 2-3 resident mip payloads (each smaller but nonzero), `TileCacheBudget.UsedBytes` undercounts actual heap usage by up to ~33%+.

**Confidence:** 95% (exact line confirmed at `SampleImageTile.cs:16,67,105`).

## Root Cause

`_pixelCost` is an `int` field set in the constructor via `checked(pixelWidth * pixelHeight)`. It is:
- Never updated when mip payloads (`_mipPixels`) are populated.
- The single value used by `TileCacheBudget.TryReserve` and `TileCacheBudget.Release`.
- The only cost reference in `TileCacheBudget`'s accounting.

The mip fields (`_mipPixels`) can hold up to 8 levels (per ICW-076's 8-level ceiling policy via `BackgroundTileMipPolicy`). Each level is a `byte[]` of size `(mipWidth * mipHeight)` where each level is roughly 1/4 the pixel count of the previous. Summed, they represent ~33% more bytes than mip-0 alone.

## Scope

### Required Changes

1. **Replace `_pixelCost` field** with a property or method that sums all currently-resident mip payload byte counts:

   ```csharp
   // Under _cacheGate in SampleImageTile
   public int ResidentByteCount
   {
       get
       {
           lock (_cacheGate)
           {
               int total = _pixels?.Length ?? 0;
               foreach (var mip in _mipPixels.Values)
                   total += mip.Length;
               return total;
           }
       }
   }
   ```

2. **Update `TileCacheBudget.TryReserve`** to use `tile.ResidentByteCount` instead of `tile.PixelCost`.

3. **Update `TileCacheBudget.Release`** to use `tile.ResidentByteCount` instead of `tile.PixelCost`.

4. **Remove or deprecate the `PixelCost` property** — replace all references to `tile.PixelCost` in cache accounting with `tile.ResidentByteCount`.

5. **Update eviction policy** in `TileCacheBudget.TryReserve` to consider resident byte count, not just `PixelCost` (which currently undercounts tiles with mip levels, making them less likely to be evicted than they should be).

### Test Requirements

6. **`MemoryGovernor_AccountsForAllResidentMipLevelsNotJustNative`** regression test:
   - Create a tile with native payload + 3 mip levels.
   - Call `TryReserve` and assert `UsedBytes` increases by the sum of all payload lengths.
   - Release and assert `UsedBytes` decreases by the same amount.

7. **Eviction parity test:** Assert that eviction order is not distorted by tiles with mip levels being cheaper in per-byte cost than they should be.

### Acceptance Criteria

- `TileCacheBudget.UsedBytes` reflects the sum of all resident pixel arrays, not just mip-0.
- After releasing a tile with multiple resident mips, `UsedBytes` decreases by the full amount.
- Eviction decisions use accurate byte counts.
- No regression in cache budget behavior for tiles with only native (mip-0) payloads.

## Files to Change

| File | Change |
|---|---|
| `src/InfiniteCanvas.Rendering/SampleImageTile.cs` | Replace `PixelCost` with `ResidentByteCount` (sum of all resident payloads under `_cacheGate`) |
| `src/InfiniteCanvas.Rendering/BackgroundTileContracts.cs` | Update `IBackgroundTile` or equivalent interface if `PixelCost` is abstracted |
| `tests/InfiniteCanvas.Tests/SampleImageTileTests.cs` | Add MIP-aware byte-count tests |
| `tests/InfiniteCanvas.Tests/TileCacheBudgetTests.cs` | Add byte-accounting tests for multi-mip tiles |

## Validation

```
dotnet test tests/InfiniteCanvas.Tests --configuration Release --filter "MipByteCount|ResidentByteCount|UsedBytes"
```

## Notes

This ticket should be implemented together with ICW-P0-LEASE-RELEASE. The lease fix is untrustworthy if it releases the wrong byte count; the byte-count fix is untrustworthy if the release mechanism doesn't actually call `TileCacheBudget.Release` on all code paths. They are two halves of the same accounting correctness problem.

## Related Tasks

- ICW-P0-LEASE-RELEASE: must land together (same accounting correctness problem)
- ICW-134: variant-aware cache accounting (scope includes this defect)
- ICW-076: source-agnostic mip levels (establishes the mip payload pattern this ticket fixes)
