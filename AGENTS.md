# Lython Agent Guide

This repository contains Lython, an embeddable, contained-by-design Python runtime implemented in C#/.NET.

## Compatibility Perspective

Lython is intended to feel like regular Python to a fresh coding agent. Treat it
as a strict but extensive Python subset: supported behavior should follow Python
semantics, and unsupported behavior should fail explicitly.

## Layout

- `src/Lokad.Lython/`: production library.
- `tests/Lokad.Lython.Tests/`: white-box subsystem suite (production sources
  compile into the test assembly).
- `tests/Lokad.Lython.PublicApi.Tests/`: public-boundary suite against the
  built library.
- `tests/Lokad.Lython.WorkerFixture/`: controlled subprocess faults for the
  benchmark supervisor tests. It builds automatically with the public suite
  for the matching configuration and needs no separately installed Python.
- `benchmarks/Lokad.Lython.Benchmarks/`: BenchmarkDotNet benchmarks.
- `tools/LythonProbe/`: CLI for independent, pure compatibility probes through
  Lython's public API. It accepts `-c`, a `.py` file, plain stdin, or batch JSON;
  `--compare-python` runs the same trusted snippet under isolated local CPython.
  Its deliberately capability-free host does not exercise file, directory,
  stream, subprocess, or local-import APIs. Invoke it concisely as
  `./tools/LythonProbe/lythonprobe.ps1 ...` (auto-builds Debug); see its `README.md`
  for examples. `dotnet test` does not build the probe (no test project
  references it), so before running probe-dependent tests build it explicitly
  for the matching configuration:
  `dotnet build tools/LythonProbe/LythonProbe.csproj -c <Debug|Release>`.
- `README.md`: project overview.
- `SPEC.md`: Lython language and runtime specification.

## CI

Use focused Release regressions during development; see [TESTING.md](TESTING.md).
Ordinary test runs exclude `Category=Stress` by default. An explicit test filter
replaces that default. Preserve original stress coverage and batch its execution,
full Windows/Ubuntu checks and explicit Release package consumers at meaningful
checkpoints. Tests invoking process-wide GC or finalizer waits must share the
existing nonparallel collection in their assembly. Never relax timeouts or
containment assertions to make iteration faster.

Anonymous GitHub API calls are capped at 60/hour: space CI status checks minutes apart, never poll in a tight loop.

## Packaging

Treat NuGet artifacts as release outputs only when produced by an explicit
Release pack. Debug build/test outputs are not release artifacts.

## Safety Notes

Lython must remain fully managed and memory-safe. This is a lasting project
constraint: do not enable unsafe compilation or introduce unsafe C#, Unsafe
APIs, raw pointers, P/Invoke, or native stack probing for performance or
protection. Preserve managed stack admission and its validated nesting bounds.
New runtime dependencies must be assessed against this constraint before promotion.
Performance changes must preserve allocation preflights, live-memory protection,
cooperative cancellation, cycle detection and bounded public projection.

Memory work follows cooperative admission and explicit ownership lifetimes.
Do not introduce general guest-heap tracing, root inventories or dormant collector
scaffolding. Replace weak reclamation and forced GC with working ownership scopes;
do not wrap the old subsystem in a new scope API. Preserve conservative estimates,
allocation rollback, deterministic cleanup and funding for retained aliases.
Use Lython-owned budget/scope/reservation hooks sharing the borrowed host root.
Scope creation reserves nothing and introduces no independent allowance. Establish
surviving result/cache owners before allocation; await all users before disposal.
Use rough major-capacity and replacement estimates, with deliberate headroom for
minor uninstrumented storage. See [MEMORY.md](MEMORY.md). Initial long streaming
integration uses bounded host-driven executions, not guest-reference tracing.
Refused admission fails the command: preserve its first structured origin and stop
guest work, even through exception suppression or finally returns. Keep native
cleanup/refunds available and awaited; never retry admission or skip optional
storage to recover. Explicit guest-raised MemoryError remains catchable.

Lython is intended to be host-mediated and safe-by-design. Runtime file, directory, subprocess, stdin, stdout, and stderr access must remain mediated through the public host abstractions.

