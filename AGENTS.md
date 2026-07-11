# Lython Agent Guide

This repository contains Lython, an embeddable, contained-by-design Python runtime implemented in C#/.NET.

## Compatibility Perspective

Lython is intended to feel like regular Python to a fresh coding agent. Treat it
as a strict but extensive Python subset: supported behavior should follow Python
semantics, and unsupported behavior should fail explicitly.

## Layout

- `src/Lokad.Lython/`: production library.
- `tests/Lokad.Lython.Tests/`: xUnit test suite.
- `benchmarks/Lokad.Lython.Benchmarks/`: BenchmarkDotNet benchmarks.
- `tools/LythonProbe/`: CLI for independent, pure compatibility probes through
  Lython's public API. It accepts `-c`, a `.py` file, plain stdin, or batch JSON;
  `--compare-python` runs the same trusted snippet under isolated local CPython.
  Its deliberately capability-free host does not exercise file, directory,
  stream, subprocess, or local-import APIs. Invoke it concisely as
  `./tools/LythonProbe/lythonprobe.ps1 ...`; see its `README.md` for examples.
- `README.md`: project overview.
- `SPEC.md`: Lython language and runtime specification.

## Packaging

Treat NuGet artifacts as release outputs only when produced by an explicit
Release pack. Debug build/test outputs are not release artifacts.

## Safety Notes

Lython is intended to be host-mediated and safe-by-design. Runtime file, directory, subprocess, stdin, stdout, and stderr access must remain mediated through the public host abstractions.

