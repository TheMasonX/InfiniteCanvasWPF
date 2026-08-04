---
id: ICW-110
key: ICW-110
title: Audit and convert `async void` handlers to safe wrappers
status: To Do
type: Task
priority: P1
tags:
  - stability
  - runtime
  - app
dependsOn:
  - ICW-014
related:
  - ICW-078
created: 2026-07-26
updated: 2026-07-26
owner: unassigned
---

# ICW-110 - Audit and convert `async void` handlers to safe wrappers

## Summary

Multiple `async void` event handlers exist across WPF code-behind (notably `MainWindow.xaml.cs`). These handlers can crash the process when exceptions escape and are not observed; they also complicate testability. This task audits all `async void` handlers, migrates them to `async Task` where possible, and introduces a safe wrapper pattern for unavoidable `async void` entry points.

## Scope

- Audit `src/**` for `async void` handlers and list occurrences.
- Replace event-handler signatures with `async Task` where WPF wiring permits and update XAML/code-behind wiring as needed.
- Implement a `SafeAsyncHandler(Func<Task> handler)` helper and a small `SafeEventHandler` adapter to catch/log exceptions and forward to global handlers.
- Add unit/integration tests verifying that thrown exceptions are routed to the global logging path and the app does not crash.

## Acceptance Criteria

- No unchecked `async void` handlers remain in `src/` except where WPF signature prevents change; those remaining must be wrapped with a tested safe wrapper.
- Regression test `AsyncVoidHandlerSafetyTests` demonstrates exception containment for representative handlers (mouse, keyboard, and timer-based handlers).
- Continuous integration passes with the new tests.

## Validation

- Command: `dotnet test tests/InfiniteCanvas.Tests/InfiniteCanvas.Tests.csproj --filter AsyncVoidHandlerSafetyTests`

## Notes

- Related: ICW-014 (global exception safety net) and ICW-078 (render epoch guarding). Use global handlers for final user-visible crash reporting.
