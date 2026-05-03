# Lython Agent Guide

This repository contains Lython, an embeddable, contained-by-design Python runtime implemented in C#/.NET.

## Layout

- `src/Lokad.Lython/`: production library.
- `tests/Lokad.Lython.Tests/`: xUnit test suite.
- `benchmarks/Lokad.Lython.Benchmarks/`: BenchmarkDotNet benchmarks.
- `README.md`: project overview.
- `SPEC.md`: Lython language and runtime specification.

## .NET Commands

Use `--tl:off` to avoid dynamic terminal logger output.

```powershell
dotnet restore Lokad.Lython.slnx --tl:off -v minimal
dotnet build   Lokad.Lython.slnx --tl:off --nologo -v minimal
dotnet test    tests/Lokad.Lython.Tests/Lokad.Lython.Tests.csproj --tl:off --nologo -v minimal
dotnet pack    src/Lokad.Lython/Lokad.Lython.csproj --tl:off --nologo -v minimal --no-restore
```

## Safety Notes

Lython is intended to be host-mediated and safe-by-design. Runtime file, directory, subprocess, stdin, stdout, and stderr access must remain mediated through the public host abstractions.

