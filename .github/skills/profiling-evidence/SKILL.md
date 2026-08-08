---
name: profiling-evidence
description: 'Investigate WPF performance with local Serilog logs, structured runtime diagnostics, BenchmarkDotNet, Visual Studio Profiler, and ETW captures. Use for choppy rendering, frame-time spikes, allocation pressure, fast-scroll behavior, or reproducible A/B measurements.'
argument-hint: 'Describe the scenario, build configuration, logs, profile artifacts, and comparison needed'
---

# Profiling Evidence

## Outcome

Produce a source-backed performance finding or a precise request for the next evidence artifact.
Preserve raw captures, record the exact scenario, and separate observations from hypotheses.

## When to Use

Use this skill when a user reports:

- choppy pan, zoom, scroll, or resize behavior
- slow frame presentation or visible tile starvation
- high allocation, garbage collection, or WPF visual-tree churn
- a suspected hot method or rendering stage
- a need to compare two implementations or runtime modes
- a request for Visual Studio Profiler, BenchmarkDotNet, or ETW evidence

## Operating Rules

- Inspect local application logs and repository benchmark artifacts before requesting a user capture.
- Read the source that emits each diagnostic field. Do not infer field meaning from a label alone.
- Keep the raw log or trace unchanged. Write normalized exports beside the source artifact.
- Label each statement as an observed fact, an inference, or an open hypothesis.
- Keep build, commit, hardware, debugger state, scenario, and sample count with every result.
- Treat debugger-attached and debugger-detached runs as different experiments.
- Do not claim a performance cause from one method percentage or one unmatched run.
- Do not add per-item logging to a hot render path when an aggregate counter answers the question.
- Keep diagnostics secret-safe. Do not log source records, credentials, customer data, or private paths.

## Inputs

Collect these values when available:

- user scenario and exact reproduction steps
- build configuration and runtime version
- git revision and working-tree state
- debugger attached or detached state
- machine CPU, memory, GPU, display scale, and refresh rate
- local application log path
- BenchmarkDotNet output or profiler artifact
- comparison mode, feature flag, or commit

## Procedure

### 1. Define the performance question

Write one falsifiable question before changing code.

Examples:

- Does annotation composition exceed the frame budget during rapid vertical scanning?
- Does tile generation continue after the camera leaves the tile region?
- Does retained visual reuse reduce allocation and visual-tree churn?

Record the user action, scene size, viewport size, zoom range, scan duration, and warm-up period.
Use 16.67 ms as the nominal 60 Hz frame budget unless the target refresh rate differs.

### 2. Inspect local evidence first

Find the newest application logs without asking the user to attach them:

```powershell
Get-ChildItem "$env:LOCALAPPDATA\InfiniteCanvas\logs\infinitecanvas-*.log" |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 5 FullName, Length, LastWriteTime
```

The application writes daily rolling logs under:

```text
%LOCALAPPDATA%\InfiniteCanvas\logs\infinitecanvas-YYYYMMDD.log
```

Search the relevant diagnostic events:

```powershell
Select-String -Path "$env:LOCALAPPDATA\InfiniteCanvas\logs\infinitecanvas-20260808.log" `
  -Pattern 'FrameDiag:|AnnotationDiag:|RenderingDiag:|TileCacheDiag:'
```

Inspect repository artifacts under `BenchmarkDotNet.Artifacts/results` and `docs/benchmarks/runs`.
Read the emitting source before interpreting counters.

For InfiniteCanvasWPF, `FrameDiag` is an aggregate frame and coordinator record.
`AnnotationDiag` is an aggregate overlay timing, pool, fast-path, and visual-tree record.
`RenderingDiagnostics` contains stage and per-mip counters when the instrumentation is enabled.

### 3. Normalize and correlate the evidence

Create a CSV or JSON export when the comparison requires more than a few rows.
Keep the raw input path and parser version in export metadata.
Count parsed and unparsed rows.
Fail the export when no expected mode-aware or diagnostic rows exist.

Compare these fields when present:

- sample count, mean, p95, and maximum duration
- frame duration against the target frame budget
- allocations and garbage collections
- queued, active, completed, canceled, stale, and failed work
- resident fallback and useful completion counts
- overlay fast-path hits, created states, pool hits, and visual-tree adds or removes
- mip level, cache state, scene size, and viewport state

Do not compare arbitrary historical rows as a formal A/B result.
Use matched scenario, build, warm-up, duration, and sample-count boundaries.

### 4. Add diagnostic logging when evidence is missing

Instrument the smallest owning abstraction that can distinguish the hypotheses.
Prefer the repository's existing `Serilog` and `RenderingDiagnostics` patterns.

Use these rules:

- Record named properties with stable names and units.
- Aggregate hot-path values over a bounded interval, such as 120 frames or two seconds.
- Include the scenario, mode, build, and revision when the data can contain multiple runs.
- Record stage duration, count, outcome, and relevant payload size together.
- Use `Stopwatch` for elapsed timing and avoid wall-clock subtraction for durations.
- Keep logging outside locks and avoid synchronous file or UI work in the render path.
- Add a focused test for diagnostic field presence or snapshot semantics.
- Add a small parser or exporter when operators must compare repeated captures.

Existing human-readable Serilog templates are useful for immediate inspection.
For repeated export, prefer a second JSON Lines sink with `CompactJsonFormatter` or `JsonFormatter`.
Keep the text sink for normal diagnosis and preserve the same event names and property names in both sinks.

### 5. Request a user profile only when local evidence is insufficient

Give the user one exact capture recipe.
Do not request a generic screenshot or an unspecified trace.

Ask for:

1. The exact Release or Debug configuration.
2. The debugger state, attached or detached.
3. The app revision and launch arguments.
4. The reproduction path, duration, warm-up, and stop condition.
5. The matching application log file.
6. The profiler artifact and a short exported summary.

For Visual Studio Profiler:

1. Build the target configuration from the requested revision.
2. Start Visual Studio Performance Profiler with `Alt+F2`.
3. Select `CPU Usage` for method and call-tree attribution.
4. Select `Memory Usage` only when allocation or heap retention is part of the question.
5. Start the target application from the profiler.
6. Complete the fixed reproduction after the warm-up period.
7. Stop collection immediately after the scenario.
8. Save the `.diagsession` file with the build, mode, and scenario in its name.
9. Export or record the top inclusive CPU, self CPU, call tree, thread, allocation, and collection values.

Request the `.diagsession` file when deeper inspection is needed.
Request the exported top-method table when the artifact is too large to transfer.
A screenshot alone is not sufficient because it loses call-tree and timing context.

If Visual Studio cannot collect the required evidence, request an ETW or WPR capture.
Use a `.etl` file with the exact recording profile and scenario metadata.
Use WPA to inspect UI thread, compositor, CPU, disk, and scheduling events.

### 6. Run controlled A/B comparisons

Change one variable at a time.
Use the same binary, data, machine, window size, display scale, refresh rate, warm-up, path, and duration.
Run each mode at least twice when startup or cache state affects the result.
Alternate the order when thermal or background-load drift can bias the result.

Record a run boundary in the log or use a separate process and log file for each run.
Include the mode as a structured field in every aggregate event.
Require equal or explicitly explained sample counts before reporting a percentage difference.

Report both the benchmark and its limitations.
State when a result is indicative because the runs are unmatched.

### 7. Choose the export format

Use the smallest artifact that preserves the needed evidence:

- Serilog JSON Lines for repeated runtime diagnostics and machine parsing.
- CSV for per-sample A/B comparisons and spreadsheet review.
- JSON metadata for input paths, revision, machine, mode, and parser status.
- BenchmarkDotNet CSV, Markdown, HTML, and JSON for repeatable microbenchmarks.
- Visual Studio `.diagsession` plus a top-method table for application CPU and memory profiles.
- WPR or ETW `.etl` plus WPA notes for compositor, scheduler, or system-wide analysis.

Keep the original artifact beside every derived export.
Do not replace the source log with a hand-edited summary.

### 8. Close the evidence loop

Before declaring a profiling task complete, verify:

- the raw log or trace remains available
- the export records its source and parser status
- parsed and unparsed counts are known
- the scenario and build metadata are recorded
- the result distinguishes observation from explanation
- focused tests, build, or benchmark commands pass
- the task tracker records the evidence and the next step
- unmatched runs remain labeled as indicative rather than conclusive

## User Capture Request Template

Use this concise request when a profile is required:

> Please run the application in `Release` with the debugger [attached/detached].
> Start Visual Studio Performance Profiler with `CPU Usage` selected.
> Warm up for 10 seconds, then repeat this exact action for 20 seconds: [action].
> Stop collection immediately after the action.
> Send the matching `infinitecanvas-YYYYMMDD.log`, the saved `.diagsession`, and the top 20 inclusive and self CPU methods.
> Include the git revision, launch arguments, mode, display refresh rate, and whether the run used a cold or warm cache.

## Completion Report

Summarize the result in this order:

1. Reproduction and experiment metadata.
2. Observed log and profile values.
3. Most likely controlling code path.
4. Discriminating check or next capture.
5. Limitations, including debugger and sample-count differences.
6. Files, exports, and validation commands.
