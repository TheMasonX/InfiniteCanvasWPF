# ADR-0008: External Material and Annotation Adapters

- Status: Proposed
- Date: 2026-08-08

## Context

The reusable canvas has source-neutral tile requests and cache keys.
The active application still uses sample tile types and sample annotation types.
The raster compositor accepts `SampleImageTile` and `SampleAnnotation` directly.
The sample generator also owns tile placement and random annotation creation.

The external viewport needs two scanner columns with horizontal overlap.
The host must choose whether the left or right tile wins in the overlap.
Tiles within one scanner column must not overlap vertically.
The external host also owns its annotation objects and display settings.
The host can provide defects, markers, regions, and other domain types.

## Decision

1. Keep background tile requests and payloads source-neutral.
2. Allow tile descriptors to carry arbitrary world bounds and camera-column metadata.
3. Carry horizontal overlap preference as explicit composition data.
4. Validate that tiles in one camera column do not overlap vertically.
5. Keep overlap resolution independent from input list order.
6. Accept annotation data through a host adapter.
7. Keep annotation contracts independent from external domain object types.
8. Let the annotation adapter provide identity, bounds, kind, draw order, display settings, and optional tooltip or image data.
9. Keep sample generation and random data in an application or test fixture boundary.
10. Keep deterministic seeds and sample adapters for regression tests.

The exact record and interface names remain implementation work under ICW-343.
The first implementation must preserve the requirements in the registry.

## Consequences

- External tile sources can describe scanner geometry without using the sample grid.
- The raster path can resolve horizontal overlap deterministically.
- External hosts can adapt several annotation types without changing Core contracts for each type.
- Reusable rendering assemblies no longer need sample generation ownership.
- Tests retain deterministic sample coverage through an explicit fixture adapter.
- The layer plan must carry overlap and annotation revisions with the accepted frame.

## Validation

- Add a two-column overlap test with left preference.
- Add the same test with right preference.
- Reject vertical overlap within one camera column.
- Render two equal tile IDs with distinct source identity and overlap metadata.
- Host defects, markers, and regions through one neutral annotation adapter.
- Run the Core tests, Windows tests, App build, and task tracker validator.

## Related Work

- ICW-076
- ICW-314
- ICW-337
- ICW-339
- ICW-340
- ICW-341
- ICW-343