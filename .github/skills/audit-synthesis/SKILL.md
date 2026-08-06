---
name: audit-synthesis
description: 'Synthesize external audits into a verified, de-duplicated findings report. Use when reconciling audit reports, performing independent evidence review, correcting provenance, creating ICW tasks, or updating sprint plans.'
argument-hint: 'Audit files, review scope, fixed point, and whether to include net-new findings only'
---

# Audit Synthesis

## Outcome

Produce a skeptical synthesis of external audit claims and independent analysis. The output preserves valid findings, rejects unsupported claims, records provenance, and links accepted findings to durable ICW work.

Use [docs/formats/report-FORMAT.md](../../docs/formats/report-FORMAT.md) as the report contract.

## Operating Rule

Do not make source-code or configuration changes during an audit unless the user explicitly requests implementation. Audit work may create or update reports, task records, sprint plans, and related documentation.

Treat every external claim as unverified until source evidence supports it. Do not infer file contents, runtime behavior, commit history, or task coverage from a report alone.

## Inputs

- External audit files and any prior synthesis reports.
- Audit scope, review depth, and net-new or full-reconciliation mode.
- Fixed point for diff review, when applicable.
- DesignDoc.md, README.md, ADRs, requirements, source, tests, benchmarks, and task trackers.
- Optional runtime logs, traces, reproductions, or user decisions.

## Procedure

### 1. Establish the recovery workspace

Before extraction or delegation, resolve a user-supplied temporary directory.
Use the supplied path when present. Otherwise use `D:\Temp\Subagents\<run-id>\`.
Create the directory and record its path in the run manifest.

All intermediate audit artifacts must remain in this directory, including the source ledger, extracted claims, evidence pack, seat prompts, seat reports, synthesis notes, and validation output.
Subagents must write every intermediate and final delegated artifact to their assigned child directory under this temporary root.
Do not use the operating system temporary directory for delegated work.
If the recovery directory cannot be created or written, stop before delegation and report the filesystem error.

The final synthesis report and durable planning records remain under `docs/`.
Copy or summarize temporary artifacts into the final report only after verification.

### 2. Establish scope and fixed point

- State the scope, exclusions, review mode, and timestamp.
- For diff review, verify the reference with `git rev-parse`, then inspect `git diff <ref>...HEAD` and `git log <ref>..HEAD --oneline`.
- Stop if a required fixed point does not resolve or the required diff is empty.

### 3. Inventory sources before reading claims

Create a source ledger with a stable source ID for every audit, report, code file, test, log, task, ADR, and requirement used.

Record:

- path or artifact name
- source type and date
- commit or fixed point
- scope covered
- whether the source was read directly
- limitations or provenance concerns

Read prior audits and the current trackers before accepting a finding. This prevents rediscovery from being reported as a net-new result.

### 4. Extract claims without endorsing them

Create a master findings list before any council review.
Store the list in the recovery workspace and include one row for every extracted claim.
Do not deduplicate or assign final disposition during extraction.
Use stable candidate IDs, source IDs, original wording, affected behavior, impact, severity, confidence, and proposed task key.
The council evidence pack must use this master list as its common input.

Convert each external finding into a claim record. Preserve the original finding ID and wording in a compact form. Add:

- candidate claim ID
- external source IDs
- affected code or behavior
- claimed impact
- claimed severity and confidence
- proposed task key, if any

Do not merge claims during extraction. Similar names do not prove identical mechanisms.

### 5. Verify each claim independently

For every candidate:

1. Open the cited source directly.
2. Trace the relevant control flow, data flow, lifecycle, or contract.
3. Check tests, benchmarks, logs, and recent commits that can confirm or disconfirm the claim.
4. Record exact file and line evidence. Use source IDs in the report and clickable repository links when the format supports them.
5. Mark the result as `Confirmed`, `Partially confirmed`, `Refuted`, `Unverified`, or `Duplicate`.

A report statement is not evidence. A plausible mechanism is not a reproduced defect. Separate observed facts, inferred risk, and open questions.

### 6. Review standards and specification separately

Evaluate each accepted claim on two independent axes:

- **Standards:** documented repository rules and the Fowler smell baseline. A smell is a judgement call unless a documented rule is breached.
- **Spec:** behavior against the originating requirement, DesignDoc, ADR, acceptance criteria, or user request.

Keep the axes separate. A clean implementation can still violate the spec, and a spec-compliant implementation can still breach a documented standard.

### 7. Deduplicate by mechanism, not vocabulary

Compare candidate claims using:

- affected code path
- failure mechanism
- trigger and preconditions
- user or system impact
- proposed remediation
- source and time

Classify each candidate as `Net-new`, `Corroboration`, `Correction`, `Extension`, `Duplicate`, or `Rejected`. Keep distinct defects separate when they share a type or file but require different fixes.

For provenance corrections, state what was independently verified and what was already known. Do not claim discovery when the evidence shows prior coverage.

### 8. Assign severity and confidence independently

Use:

- `P0`: crash, data loss, security issue, or major correctness failure.
- `P1`: high reliability or performance risk likely to affect users.
- `P2`: medium-risk architectural limitation or maintainability issue.
- `P3`: consistency, documentation, or low-impact cleanup.

Confidence measures evidence quality, not impact:

- 95-100%: directly demonstrated by source and a focused test, reproduction, or invariant.
- 80-94%: direct source proof with one meaningful validation gap.
- 60-79%: credible mechanism, but reproduction or an important dependency remains open.
- Below 60%: retain only as an explicit hypothesis or validation request.

Use calibrated percentages. Explain the evidence and the uncertainty behind every non-trivial confidence value.

### 9. Run the supplementary council review

After extraction and initial verification, invoke the `council` skill for high-impact or multi-source reconciliations.
Use three independent seats by default, plus the main-agent synthesizer.
Increase the seat count only when the scope requires additional specialist coverage.
Pass every seat the same evidence pack and master findings list from the recovery workspace.
Store each seat prompt and result in a separate child directory under the recovery root.
Keep seat reviews independent until synthesis.

The council must record agreement, dissent, rejected claims, evidence gaps, and task actions.
Use the council result to polish the master findings list, not to erase unresolved claims.

### 10. Decide disposition and task action

For each accepted finding, choose one disposition:

- `Keep as finding`
- `Update existing task`
- `Create task`
- `Close as fixed`
- `Reject`
- `Defer pending evidence`

Cross-reference `docs/tasks/active-tasks.md`, `docs/tasks/task-tracker.md`, and ticket files. Do not create duplicate ICW keys. Every created task needs scope, acceptance criteria, validation, findings or blockers, and next step.

### 11. Write the synthesis report

Create a timestamped file under `docs/audits/` using the report format. Include:

- executive summary with review scope and result counts
- all accepted findings in severity order
- provenance and verification status
- separate Standards and Spec axis labels
- exact rationale and source IDs
- recommendations and task disposition
- assumptions, open questions, and prioritized requests
- complete source ledger

In net-new mode, omit unchanged findings but retain enough cross-reference to explain corrections and dependencies.

### 12. Update durable planning records

Update the relevant task ticket, `active-tasks.md`, `task-tracker.md`, and sprint plan when the synthesis changes execution order or coverage. Add an ADR only when the synthesis changes architecture or a system boundary.

Run the tracker validator. Existing unrelated legacy errors do not justify hiding new errors. Report the baseline failures and verify that the new records introduce none.

## Report Quality Checks

Before completion, confirm:

- Every finding has direct source evidence or an explicit `Unverified` disposition.
- Every code-path claim cites a file and line or a source ID that resolves to one.
- Severity and confidence are independent and justified.
- External claims are labeled as corroborated, corrected, duplicated, rejected, or accepted.
- Similar findings are merged only when mechanism and remediation match.
- Standards and Spec evaluations remain separate.
- Every accepted new task appears in both trackers and has a ticket file.
- Assumptions, open questions, and requests have owners or a next evidence step.
- The report records what was not inspected.
- The report does not claim implementation unless the user explicitly requested it and the change was validated.

## Filling Out the Report

Use the template as a contract, not as prose to copy without evidence.

- Replace every placeholder with a verified value. Use `None` when a section has no entries.
- Use stable `S` IDs for sources and stable `F` IDs for findings. Do not reuse an external audit ID as the local finding ID unless the report states that choice.
- Keep the table row, finding heading, source ledger, and task update synchronized.
- Put provenance and task disposition in separate fields. Provenance explains the claim's relationship to earlier work. Task disposition explains the planning action.
- Cite source IDs for every material claim. Add exact repository-relative file links with line numbers for code-path evidence.
- Include rejected or unresolved claims when omitting them would hide a coverage or provenance decision.
- Check that the Executive Summary counts match the findings table and the verification outcomes.
- Use the report timestamp and filename timestamp in US Central time.

The complete template and field definitions are in [docs/formats/report-FORMAT.md](../../docs/formats/report-FORMAT.md).

## Suggested Prompts

- `/audit-synthesis reconcile all audits in docs/audits; produce net-new findings only`
- `/audit-synthesis independently verify the latest external audit against source at HEAD`
- `/audit-synthesis correct provenance and task duplication across the audit series`
- `/audit-synthesis review the rendering audit with Standards and Spec axes`

