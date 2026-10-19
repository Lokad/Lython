# Executable local imports, 2026-10-19 UTC

Local module roots now use the existing executable engine. Module final blocks
complete naturally; executed top-level returns retain their separate rejection.
Ordinary imported functions/lambdas keep executable code. Existing lowered class,
annotation and opcode consumers remain. No new dispatcher, backend or owner layer.

The migration also exposed two older namespace defects: copied exports diverged
from function globals, and a builtin-name filter dropped explicit user bindings.
Script modules now use their existing context and canonical namespace stores;
mutable attributes bypass stable builtin-member caching. The obsolete cache-update
hook and copied export dictionary disappear. New-field admission/rollback remains.

Baseline: `397c9eb0b024c6b110a4545d5aea1b768b7423b4`. Candidate: that revision plus
the [frozen three-file patch](candidate.patch). This is a named historical capture,
not a measurement of whichever revision is currently HEAD.

| API / boundary | Case | Before allocation | After allocation | Before elapsed | After elapsed |
| --- | --- | ---: | ---: | ---: | ---: |
| Run / warm-entry-fresh-import | no-import control | 25,296 B | 25,296 B | 0.0502 ms | 0.0514 ms |
| Run / warm-entry-fresh-import | import helper + 64-step sum | 155,928 B | 155,048 B | 0.3582 ms | 0.3878 ms |
| Run / fresh-compile-to-result | no-import control | 78,472 B | 78,472 B | 0.1751 ms | 0.1762 ms |
| Run / fresh-compile-to-result | import helper + 64-step sum | 205,800 B | 204,920 B | 0.4495 ms | 0.4620 ms |
| RunAsync / warm-entry-fresh-import | no-import control | 25,544 B | 25,544 B | 0.0634 ms | 0.0589 ms |
| RunAsync / warm-entry-fresh-import | import helper + 64-step sum | 156,296 B | 155,416 B | 0.3802 ms | 0.3580 ms |
| RunAsync / fresh-compile-to-result | no-import control | 78,720 B | 78,720 B | 0.1747 ms | 0.1720 ms |
| RunAsync / fresh-compile-to-result | import helper + 64-step sum | 206,168 B | 205,288 B | 0.4776 ms | 0.4590 ms |

Numbers are medians of seven samples, eight jobs per sample, after eight warmup
jobs. Both lanes include host creation/seeded input, execution, result projection
and cleanup. The fresh lane also compiles the entry. The warm lane reuses only
the entry: each execution still prepares its imported module. Inputs and numeric
results match; every execution returns its shared allowance to the compiled
baseline. Startup and actual filesystem latency are excluded.

This short local capture is unqualified. Extra executable preparation is an
additional source-level cost; these samples do not isolate its latency. Async
medians were higher in the earlier candidate capture and all initial samples are
retained. No elapsed improvement, embedding tail-latency benefit, retained-heap
saving or CPython timing ratio is established. The no-import allocation medians
remain unchanged. CPython remains the external timing baseline in [BENCHMARKS.md](../../../BENCHMARKS.md).

The initial candidate predates the namespace corrections: [raw samples](initial-candidate-after.json),
[its patch](initial-candidate.patch). Its 360 B median reduction applies only to
that earlier patch. The final table uses after.json; no favorable recapture was
selected. The extra capture was required by the correctness/source change.

Windows10.0.26200 X64, i7-14700KF, 28 logical processors; .NET runtime 10.0.13,
SDK 10.0.300-preview.0.26177.108. No forced GC, BenchmarkDotNet lane or new benchmark framework.
Total managed allocation includes all process threads; it is not live guest memory
and is not used as a runtime budget. Conservative code/module ownership remains;
metadata can remain retained until the host batch ends.

Six independent import/namespace fixtures match CPython3.13.2 and the focused
public tests run them under both APIs, including real delayed I/O. Four namespace
regressions fail on the old binary and pass after the correction. An earlier probe
mistakenly used unsupported function.__globals__; the confirmed witnesses use the
existing globals() API. Original failures are retained privately, with no new
attribute support inferred. Full qualification is recorded in the private plan.

[Scope](measurement-scope.json), [before](before.json), [after](after.json),
[all medians](summary.json), [diagnostic source](diagnostic.cs),
[baseline source identity](baseline-source.json), [candidate source identity](candidate-source.json),
[CPython imports](cpython-imports.json), [CPython namespace corrections](cpython-namespace.json).
