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

Anonymous GitHub API calls are capped at 60/hour: space CI status checks minutes apart, never poll in a tight loop.

## Packaging

Treat NuGet artifacts as release outputs only when produced by an explicit
Release pack. Debug build/test outputs are not release artifacts.

## Safety Notes

Lython is intended to be host-mediated and safe-by-design. Runtime file, directory, subprocess, stdin, stdout, and stderr access must remain mediated through the public host abstractions.

