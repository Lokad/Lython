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
- `README.md`: project overview.
- `SPEC.md`: Lython language and runtime specification.

## Safety Notes

Lython is intended to be host-mediated and safe-by-design. Runtime file, directory, subprocess, stdin, stdout, and stderr access must remain mediated through the public host abstractions.

