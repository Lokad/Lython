# Benchmark tools and evidence

The current performance account is [docs/BENCHMARKS.md](../docs/BENCHMARKS.md).
This directory contains tooling and revision-specific evidence, not current
runtime documentation.

| Location | Purpose |
| --- | --- |
| `Lokad.Lython.Benchmarks/` | BenchmarkDotNet and the supervised Lython/CPython comparison worker |
| [COMPARISON.md](COMPARISON.md) | Existing commands, timing boundaries and qualification rules |
| [scripts](scripts/README.md) | VM/comparison setup and invocation helpers |
| `results/` | Frozen scripts, inputs, goldens, captures, profiles and receipts |
| [HISTORY.md](HISTORY.md) | Archived comparison account and earlier-Lython improvements |
| [EXPERIMENTS.md](EXPERIMENTS.md) | Historical investigations and rejected approaches |
| [Baselines.md](Baselines.md) | Historical BenchmarkDotNet baselines |

```powershell
dotnet build benchmarks/Lokad.Lython.Benchmarks/Lokad.Lython.Benchmarks.csproj -c Release
dotnet run --project benchmarks/Lokad.Lython.Benchmarks -c Release --no-build -- --list flat
```

Build before measurement. Use focused short diagnostics during development;
full lanes are infrequent declared milestones. BenchmarkDotNet and CPython
comparison boundaries differ; compilation, warm execution and startup are
reported separately. Complete output checks and declared owners remain active.

Preserve raw evidence, including hash-sealed READMEs. Historical relative links,
contracts and reproduction sources describe the named source revision and may
refer to root guides available in Git at that revision. They are not current
runtime instructions. Do not rewrite an artifact to imply newer qualification.
