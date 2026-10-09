# Lython Benchmarks

This folder contains the dedicated microbenchmark project for `Lokad.Lython`.

The comparative CPython campaign is specified separately in
[COMPARISON.md](COMPARISON.md). Its primary lane compares warm public Lython
invocations with warm CPython executions of identical Python source. The
catalog export, explicit CPython correctness check and supervised comparison smoke
are available, together with an exactly-once fresh-process smoke. Opt-in machine
checks, paired collection with strict eligibility, checkpoint resumption and
raw-evidence report rendering are documented in the comparison contract.
The [2026-10-09 run](results/2026-10-09-quick/README.md) finished the four lanes in
14m57s total, within ten minutes per lane. It records absolute timings and all
exclusions; no workload ratio qualified.
The `core-loop` profile narrows follow-up collection to a basic integer reduction
at three sizes and two controls, with scalar checksum output. It uses the same
public invocation boundary and ten-minute cap; see the comparison contract.
BenchmarkDotNet results below do not include a
Python baseline. An explicit, hash-verified Linux toolchain preparation command
is documented in that contract.

The goal is not to benchmark every Python feature. It is to keep a small, explicit harness around the runtime hot paths that matter most to Lython's design:

- UTF-8 string handling through the public execution API
- list and dictionary construction/copy behavior
- runtime-to-public projection costs
- core execution overhead for representative short scripts
- contained ZIP archive behavior and scaling (entry counts, compression ratios, appends)
- SequenceMatcher similarity workloads and async collection materialization paths
- gzip flush scaling and OpenPyXL persistence
- six-column csv.DictReader retain-versus-early-break scaling at the default budget
- bounded governor-path matrix (scalar/file scans, slice/set temporaries) at realistic budgets
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

Smoke-check one class without a full measurement run (validates execution once per benchmark):

```powershell
dotnet run --project benchmarks/Lokad.Lython.Benchmarks/Lokad.Lython.Benchmarks.csproj -c Release -- --filter *GovernorBenchmarks* --job dry
```

Record quick comparable figures with a short job instead of the default:

```powershell
dotnet run --project benchmarks/Lokad.Lython.Benchmarks/Lokad.Lython.Benchmarks.csproj -c Release -- --filter *GovernorBenchmarks* --job short
```

BenchmarkDotNet writes run logs and reports under `BenchmarkDotNet.Artifacts/` at the repository root (git-ignored); build outputs stay under the benchmark project's `bin/`. Benchmark classes compile their scripts once in the constructor, so measured iterations reuse warmed literals and precompiled scripts: figures exclude cold compilation except the `Compile*` benchmarks, which measure it directly.

## Scope

These benchmarks are intended to support design and regression tracking, especially when:

- refining lowered execution
- changing list and dictionary storage strategies
- tightening UTF-8-native text paths
- changing projection or rendering behavior

They are not a substitute for scenario tests. The unit and scenario suites remain the source of truth for correctness.
