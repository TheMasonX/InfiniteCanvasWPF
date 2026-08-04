---
id: AGT-002
key: AGT-002
status: Proposed
title: Review: Five small, safe, high-ROI ICW tasks selected for next-slice
type: Task
priority: P3
tags: [agent, review, task-tracker]
created: 2026-07-26
updated: 2026-07-26
---

Summary:
- Capture agent review selecting five small, low-risk tasks that yield high value and are safe to land in short slices.

Selected tickets (brief rationale):
- ICW-307: Document `Bgra32BufferLayout` overflow behavior and validate dimensions earlier — small, low-risk, prevents obscure OverflowException and improves error messages.
- ICW-306: Harden pixel-format and stride handling in defect/mask rendering — small validation/workflow changes that avoid silent misrendering on unsupported formats.
- ICW-302: Document and enforce bitmap lifetime semantics for `ZeroCopyBitmapFactory` — documentation + XML docs and small tests reduce user surprise and API footguns.
- ICW-192: Centralize byte conversion in generator pipeline — micro-refactor that improves readability and reduces redundant casts with minimal behavior change.
- ICW-191: Extract `DefectTemplateFactory` to isolate bitmap allocation and disposal — focused refactor that clarifies ownership and enables later lifecycle fixes.

Why selected:
- Low estimated effort and low risk per ticket front-matter.
- Directly improves developer ergonomics, API clarity, or prevents runtime surprises.
- Each change is verifiable with focused unit tests and existing validation commands.

Next step:
- Await user direction to prioritize and implement one or more of these tickets; each is ready for a small PR.
