# Lython Benchmarks

This folder contains the dedicated microbenchmark project for `Lokad.Lython`.

The goal is not to benchmark every Python feature. It is to keep a small, explicit harness around the runtime hot paths that matter most to Lython's design:

- UTF-8 string handling through the public execution API
- list and dictionary construction/copy behavior
- runtime-to-public projection costs
- core execution overhead for representative short scripts
- contained ZIP archive behavior and scaling (entry counts, compression ratios, appends)
- SequenceMatcher similarity workloads and async collection materialization paths
- gzip flush scaling and OpenPyXL persistence
- integer magnitude-guard paths (shift/power operand sizing)

## Project

- `Lokad.Lython.Benchmarks/`: BenchmarkDotNet project

## Build

From the repository root:

```powershell
dotnet build --tl:off --nologo -v minimal benchmarks/Lokad.Lython.Benchmarks/Lokad.Lython.Benchmarks.csproj
```

## Run

From the repository root:

```powershell
dotnet run --project benchmarks/Lokad.Lython.Benchmarks/Lokad.Lython.Benchmarks.csproj -c Release
```

BenchmarkDotNet will generate its usual artifacts under the benchmark project's output folders.

## Scope

These benchmarks are intended to support design and regression tracking, especially when:

- refining lowered execution
- changing list and dictionary storage strategies
- tightening UTF-8-native text paths
- changing projection or rendering behavior

They are not a substitute for scenario tests. The unit and scenario suites remain the source of truth for correctness.
