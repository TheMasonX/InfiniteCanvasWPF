# Audit Synthesis Report Format

Use filenames in the form `{task-description}-{yy-mm-dd-hh-mm-ss}.md`.
Use US Central time for the timestamp. Use ASCII unless a source requires other characters.
Use a stable, short task description in lowercase kebab case.

This format supports external-audit synthesis and independent review. Treat external findings as claims until direct evidence verifies them.

## How to Fill Out This Report

1. Set the fixed point before reviewing claims. Record the commit, branch, tag, or `HEAD` that you inspected.
2. Assign stable source IDs in reading order. Mark every source as read directly or not read directly.
3. Extract external claims before judging them. Preserve the original finding ID and source.
4. Verify each claim against source, tests, benchmarks, logs, requirements, and history as applicable.
5. Record one finding per mechanism. Use the provenance disposition to distinguish new work from corroboration, correction, extension, duplication, or rejection.
6. Keep severity and confidence independent. Severity describes impact. Confidence describes evidence quality.
7. Link accepted actions to existing tasks before creating a new ICW key.
8. Record coverage limits, assumptions, unresolved questions, and requests for evidence or decisions.
9. Reconcile the finding counts in the Executive Summary with the table and the source ledger.
10. Run the task tracker validator after changing task records. Report pre-existing validator failures separately.

Use `Confirmed`, `Partially confirmed`, `Refuted`, `Unverified`, or `Duplicate` for verification.
Use `Net-new`, `Corroboration`, `Correction`, `Extension`, `Duplicate`, or `Rejected` for provenance disposition.
Use `Keep`, `Update`, `Create`, `Close`, `Reject`, or `Defer` for task disposition.

```markdown
# Report Title

**Description:** {one or two sentences}
**Repo:** `{repository name or link}`
**Fixed point:** `{commit, branch, tag, or HEAD}`
**Latest commit:** `{commit hash}` - `{commit message}`
**ID Hash:** `{stable report identifier}`
**Author:** {AI model or human author}
**Timestamp:** {YYYY-MM-DD HH:MM US Central}
**Review mode:** {full reconciliation | net-new only | diff review}
**Scope:** {files, subsystems, reports, or requirements reviewed}

## Executive Summary

State the review scope, source count, candidate claim count, verification counts, accepted finding count, and highest-risk result. Identify material provenance corrections and validation limits.

## Review Method and Coverage

State the fixed point, independent checks, validation commands, and sources or subsystems not inspected.

## Table of Findings

Rank findings by severity. Include rejected, duplicate, and unresolved claims when they affect provenance or coverage. Keep severity and confidence independent.

| ID | Short name | Axis | Disposition | Verification | Severity | Confidence | Task | Sources |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| {F-001} | {short name} | {Standards or Spec} | {Keep, Update, Create, Close, Reject, Defer} | {Confirmed, Partially confirmed, Refuted, Unverified, Duplicate} | {P0-P3 or none} | {0-100% or none} | {ICW key or none} | {S1, S2} |

## Findings

### {ID} {Short Name}

**Axis:** {Standards or Spec}
**Provenance:** {Net-new | Corroboration | Correction | Extension | Duplicate | Rejected}
**Task disposition:** {Keep | Update | Create | Close | Reject | Defer}
**Verification:** {Confirmed | Partially confirmed | Refuted | Unverified | Duplicate}
**Severity:** {P0-P3 or none}
**Confidence:** {0-100% or none}, with one sentence explaining the evidence limit
**Origin:** {external finding IDs and source IDs, or independent analysis}

#### Description

State the observed behavior, trigger, impact, and affected boundary. Separate observed facts from inferred risk.

#### Rationale

Explain the control-flow, data-flow, lifecycle, contract, or requirement evidence. Cite every material claim with source IDs and exact repository file and line references where applicable. Use repository-relative Markdown links for stored reports.

#### Counter-evidence and Deduplication

Record tests, commits, design decisions, or source facts that weaken the claim. Explain why related findings are the same mechanism or remain separate.

#### Recommendation and Validation

Give a scoped, testable remediation. Name the cheapest discriminating test, benchmark, reproduction, or evidence request. Do not describe implementation as complete unless the requested implementation was performed and validated.

#### Related Artifacts

| Type | ID or path | Relationship |
| --- | --- | --- |
| Task, ADR, requirement, report, or test | {reference} | {relationship} |

#### Finding Sources

List the source IDs that support this finding. The complete definitions appear in the master source ledger.

---

## Assumptions

| ID | Assumption | Effect if false | Evidence needed | Owner |
| --- | --- | --- | --- | --- |
| A-{n} | {assumption} | {impact} | {check} | {owner or unassigned} |

## Open Questions

| ID | Question | Why it matters | Cheapest resolution | Owner |
| --- | --- | --- | --- | --- |
| Q-{n} | {question} | {impact} | {test, source read, log, or decision} | {owner or unassigned} |

## Requests

List human-targeted requests in priority order. Request only information or decisions that can change a finding, severity, confidence, disposition, or acceptance criteria. Write `None` when no request remains.

| Priority | Request | Rationale | Required response |
| --- | --- | --- | --- |
| P1 | {source, log, reproduction, decision, or safety interlock} | {why it is needed} | {specific response} |

## Source Ledger

Use stable IDs throughout the report. Record whether each source was read directly.

| ID | Source | Type | Revision or date | Read directly | Use and limitation |
| --- | --- | --- | --- | --- | --- |
| S1 | {path or artifact} | {code, test, audit, ADR, task, log} | {commit or date} | {yes or no} | {role and limitation} |

## Task and Sprint Updates

| Finding | Task action | Tracker locations | Sprint impact |
| --- | --- | --- | --- |
| {ID} | {create, update, close, or none} | {paths} | {ordering or no change} |
```

## Authoring Rules

- Verify every external claim against source. Do not cite a report as proof of its own conclusion.
- Use `Confirmed`, `Partially confirmed`, `Refuted`, `Unverified`, or `Duplicate` for verification.
- Use `Net-new`, `Corroboration`, `Correction`, `Extension`, `Duplicate`, or `Rejected` for provenance disposition.
- Use `Keep`, `Update`, `Create`, `Close`, `Reject`, or `Defer` for task disposition.
- Keep distinct mechanisms separate even when they share a file, type, or task family.
- Assign confidence from evidence quality. Do not use confidence as a second severity score.
- Cite code-path claims with repository-relative file links and line numbers when the report is stored in the repository.
- Record uninspected sources and unresolved assumptions. Do not hide coverage limits.
- In net-new mode, omit unchanged findings but retain corrections and dependency context.
- Update task trackers only for accepted actions. Do not create duplicate ICW keys.
- Reconcile summary counts, table rows, finding sections, and source-ledger entries before completion.