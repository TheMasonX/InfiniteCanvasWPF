# ADR-0005: Source-Agnostic Background Tile Mip Requests

- Status: Proposed
- Date: 2026-07-25

## Context

`SampleImageTile` currently owns one `Func<byte[]>` or Windows bitmap factory, and the rasterizer
always samples that full-resolution payload. This makes the canvas depend on the synthetic source
shape and prevents zoomed-out views from asking a future external provider for an appropriately
sized variant. The renderer also starts generation from `DrawTile`; it cannot await an external
provider safely, and its cache is keyed by tile identity and budgets one full-resolution pixel
cost. Neither behavior is sufficient when a tile can have multiple cached resolutions.

The background imagery source will eventually be an external API. Canvas, renderer, and cache
contracts must therefore express source identity, requested resolution, and returned payload
without assuming that pixels are generated locally or that a Windows `Bitmap` is available.

## Decision

Introduce source-neutral background-tile contracts in `InfiniteCanvas.Rendering`:

- `BackgroundTileDescriptor` supplies the stable, source-scoped tile identity, immutable content
  revision, world bounds, native pixel dimensions, and placeholder value. It contains no annotation
  association, generation delegate, bitmap, or external-client type. Annotation ownership remains
  separate from the background-raster contract.
- `BackgroundTileRequest` identifies a descriptor and requested canonical mip level. Mip level zero
  is native resolution. Levels one through seven use `max(1, ceil(nativeDimension / 2^L))` for each
  dimension. The descriptor revision and mip level form a `BackgroundTileCacheKey`; an ID alone is
  not globally safe once external sources and refreshes exist.
- `BackgroundTilePayload` transfers an immutable-by-contract Gray8 buffer, canonical width and
  height, requested mip level, and actual byte cost. Construction validates finite positive
  dimensions, the canonical dimensions for the request, and `pixels.Length == width * height` with
  checked arithmetic. A provider must not silently substitute a different mip or resolution; it
  must fail the request or expose an explicit future negotiation contract. This keeps cache keys,
  sampling density, and diagnostics truthful.
- `IBackgroundTileSource` asynchronously resolves a request and may represent a synthetic generator,
  local file decoder, memory cache, HTTP client, or server-side tile service. It takes a cancellation
  token and returns only source data, never WPF objects. The caller owns cancellation of its wait;
  cancellation of one caller must not cancel an already-shared cache-fill operation needed by other
  frames.

Keep mip choice a deterministic pure policy that maps the captured camera snapshot and descriptor
dimensions to the coarsest mip whose canonical texel density remains at or above one texel per
screen pixel on both axes. For non-uniform scale, the larger camera scale is the binding axis because
it has the highest texel density. The policy clamps to levels zero through seven and is evaluated
once per visible tile before rasterization. The materializer/cache receives that request; the
renderer only receives an already-resident payload or the descriptor placeholder and samples using
the payload's validated dimensions. It neither selects a mip nor invokes an asynchronous source.

The synthetic source derives every mip from the same deterministic native image definition using a
deterministic low-pass reduction rule. It must aggregate the covered native texel footprint (box
average is adequate for the first implementation) before quantizing one Gray8 output texel. Simple
floor-coordinate point sampling is rejected because it aliases sparse noise and small defects,
causing visible instability across mip transitions. An external provider may return a precomputed
canonical mip with equivalent visual intent, but must satisfy the request's canonical dimensions and
mip level.

The materializer owns request coalescing, cache admission, completion notification, and eviction.
Cache entries are keyed by `BackgroundTileCacheKey`, budget their actual payload byte cost, and retain
the existing non-blocking placeholder behavior. Visible-frame pinning uses the same variant key, so
it protects the payload actually sampled rather than every variant for a tile. A failed or canceled
fill releases its reservation only when no shared waiter remains. A coarser resident mip is not
silently substituted for a requested finer one; fallback behavior must be explicit in a later
request/result contract so image quality and metrics remain explainable.

Pixel inspection is a separate source-neutral request at mip zero. Until a dedicated point-sampling
capability is introduced, it may use the same asynchronous materializer and show the established
placeholder while the native payload is unavailable; hover handling must not synchronously decode or
generate a native tile on the UI or raster thread.

## Consequences

Benefits:

- canvas inputs and the renderer remain independent of synthetic generation and `System.Drawing`,
- zoomed-out frames avoid full-resolution generation, conversion, and cache residency,
- an HTTP or service-backed provider can be introduced without changing camera, raster, or overlay
  contracts,
- source latency, cache hits, requested mips, and payload sizes can be measured consistently.

Trade-offs:

- the cache and asynchronous completion logic must move from one payload per tile to one payload per
  tile/mip key,
- pixelometer behavior must explicitly request mip zero or a separately documented inspection
  sampling policy,
- payload validation and cancellation become source-boundary responsibilities.

## Implementation Sequence

1. Add the descriptor, cache key, request, payload, source interface, and pure mip-selection policy
  with core tests for canonical dimensions, the eight-level clamp, non-uniform camera thresholds,
  payload rejection, and source/revision-safe cache keys.
2. Add a background-tile materializer/cache that coalesces equal requests, preserves non-blocking
  placeholders, pins requested variants, accounts for actual bytes, releases failed reservations,
  and exposes completion without allowing stale scene completions to redraw a replacement scene.
3. Implement `SyntheticBackgroundTileSource` from the existing deterministic noise/circle definition
  with deterministic low-pass mip reduction. Test identical serial and concurrent requests, exact
  canonical dimensions, and a high-frequency fixture that distinguishes reduction from point
  sampling.
4. Migrate `SampleImageTile` into descriptor-plus-annotation scene data, migrate pixelometer reads to
  an explicit mip-zero materializer request, and migrate renderer sampling to resident payload
  dimensions while preserving placeholders and visible-frame pinning.
5. Add a narrow Windows raster regression proving a zoomed-out frame samples a coarse payload and a
  focused benchmark comparing full and coarse materialization, cache residency, and frame timing.

## Follow-ups

- ICW-076 implements this decision.
- ICW-064 cache metrics must report residency and bytes by mip level after migration.
- ICW-004 should use the new policy boundary for zoomed-out overdraw measurements.
