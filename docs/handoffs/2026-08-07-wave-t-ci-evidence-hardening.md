# Wave T CI Evidence Hardening Handoff

Status: Complete
Date: 2026-08-07
Commit target: ICW-333

## Summary

Wave S was committed and pushed at `89dc821`.
Local `HEAD` matched `origin/main`, and the working tree was clean before Wave T.

The critical review found one CI evidence gap.
The workflow ran `git diff --check` without a range after checkout.
That command did not inspect the pushed change on a clean runner.

## Implementation

- Set `fetch-depth: 0` in the checkout step.
- Use the pull request base SHA for pull request validation.
- Use the push predecessor SHA for push validation.
- Use `HEAD^..HEAD` as the first-push fallback.
- Record the fix in ICW-333.

## Validation

- Workflow contract check passed.
- Solution Release build passed.
- Core tests passed.
- Windows tests passed.
- Benchmark project Release build passed.
- Task tracker validation passed.
- `git diff --check HEAD^ HEAD` passed.

## Review Findings

Wave S correctly included the benchmark project because the solution file contains it.
The benchmark restore path remains valid through the solution restore.
The known App warning for unused `_frameClaimantId` remains unchanged.

## Next Step

Run the GitHub Actions workflow for the pushed revision and confirm the event-aware diff command succeeds on the hosted runner.
