# Architecture Decision Records

| ADR | Status | Decision |
| --- | --- | --- |
| [ADR-0001](0001-benchmark-project-targeting-and-baselines.md) | Accepted | Multi-target the benchmark harness and keep timing out of test thresholds |
| [ADR-0002](0002-inspection-raster-and-annotation-layers.md) | Accepted | Compose Gray8 inspection tiles beneath retained interactive annotation elements |
| [ADR-0003](0003-live-hybrid-spatial-indexing.md) | Proposed | Adopt snapshot + hot-buffer + publishing-buffer merge model for live spatial queries |
| [ADR-0004](0004-zero-copy-buffer-lifecycle-and-handoff-policy.md) | Proposed | Define ownership, handoff, and reuse policy for memory-mapped zero-copy rendering buffers |

New decisions should use the next four-digit identifier and remain in this directory after supersession.

Behavioral requirements that do not change architecture but do affect product invariants should also be recorded in [../requirements/functional-requirements-and-invariants.md](../requirements/functional-requirements-and-invariants.md).
