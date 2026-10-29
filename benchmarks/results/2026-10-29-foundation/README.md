# 0.11.0 foundation diagnostic baseline

Collected once on 2026-10-29. All 27 jobs completed and matched complete stdout and output-file goldens. Quiet checks passed before and after collection. The matrix took 211.09 seconds. **No result is performance-qualified.** Several short script measurements vary heavily; these observations neither establish a .NET 11 regression nor qualify current HEAD.

Candidate: `bed371f69f1f9fb47e847256a6a7eca57aba440a`, core 0.11.0 development, .NET 11 RC3 runtime/SDK `11.0.100-rc.3.26522.148`. Reference: CPython 3.13.16, GCC 13.3, PGO/LTO, ordinary GIL/GC, no JIT, `-I -S`. Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 x64, four EPYC 9V45 vCPUs, 16 GiB RAM, no swap. Exact identities and quiet gates are retained in the JSON files.

The earlier-Lython comparator loads the **actual published 0.10.0 core DLL/PDB**, source `aff8965fc18a722f19be819a03f4ea16128dd31c`, under .NET 10.0.12. Its core producer SDK is 10.0.300-preview.0.26177.108; its benchmark harness was rebuilt using SDK 10.0.401. This is a release-plus-runtime comparison, not an isolated source optimization.

## Fresh compile and one run: CPython primary baseline

Every job includes fresh compilation, invocation setup, full file work, reset/output checks and declared cleanup in an already-running host. It excludes process startup, protocol transport and fixture hashes. Identical scripts, input bytes and full goldens apply to every engine. Medians below are milliseconds; ratio is candidate / CPython. IQR is candidate interquartile range / median, not a confidence interval.

| Workload | Input bytes | Candidate ms | CPython ms | Ratio | Candidate IQR |
| --- | ---: | ---: | ---: | ---: | ---: |
| [csv-summary-helpers.small](00-compile-run-scripts.csv-summary-helpers.small.json) | 913 | 1.658 | 0.406 | 4.09x | 53.9% |
| [csv-summary-helpers.large](01-compile-run-scripts.csv-summary-helpers.large.json) | 57,361 | 16.859 | 3.751 | 4.49x | 9.7% |
| [text-summary-helpers.small](02-compile-run-scripts.text-summary-helpers.small.json) | 560 | 1.603 | 0.309 | 5.18x | 29.6% |
| [text-summary-helpers.large](03-compile-run-scripts.text-summary-helpers.large.json) | 35,840 | 13.359 | 1.470 | 9.09x | 18.6% |
| [json-summary-helpers.small](04-compile-run-scripts.json-summary-helpers.small.json) | 1,728 | 2.369 | 0.397 | 5.96x | 21.3% |
| [json-summary-helpers.large](05-compile-run-scripts.json-summary-helpers.large.json) | 110,592 | 24.471 | 4.427 | 5.53x | 169.9% |
| [config-edit.small](06-compile-run-scripts.config-edit.small.json) | 181 | 2.129 | 1.526 | 1.40x | 19.9% |
| [config-edit.large](07-compile-run-scripts.config-edit.large.json) | 7,177 | 2.474 | 1.828 | 1.35x | 92.6% |
| [file-manifest-helpers.small](08-compile-run-scripts.file-manifest-helpers.small.json) | 75 | 2.193 | 0.582 | 3.77x | 30.1% |
| [file-manifest-helpers.large](09-compile-run-scripts.file-manifest-helpers.large.json) | 4,107 | 1.978 | 0.614 | 3.22x | 24.8% |
| [file-search.small](10-compile-run-scripts.file-search.small.json) | 682 | 1.446 | 0.266 | 5.44x | 4.4% |
| [file-search.large](11-compile-run-scripts.file-search.large.json) | 42,010 | 1.838 | 0.393 | 4.67x | 184.5% |

JSON-large and several tiny-file cases are especially inconclusive. Keep their measured rows visible; do not interpret large ratios as stable runtime costs. CSV-large has a narrower within-run IQR but its apparent old/new gap does not reproduce in the separate profiled batches below.

## Earlier Lython and the separate execution boundaries

Warm execution reuses compiled code; warm-async uses RunAsync with synchronously completed host operations. It does **not** measure genuine I/O suspension. These medians include per-invocation setup/work/output checks and cleanup. Compilation and startup have not been measured separately in this collection; subtracting unrelated medians would be misleading.

| Lane / workload | Published 0.10 net10 ms | Foundation net11 ms | CPython ms | Old / new / Python IQR |
| --- | ---: | ---: | ---: | --- |
| [compile-run / scripts.csv-summary-helpers.small](00-compile-run-scripts.csv-summary-helpers.small.json) | 1.721 | 1.658 | 0.406 | 54.2% / 53.9% / 1.9% |
| [compile-run / scripts.csv-summary-helpers.large](01-compile-run-scripts.csv-summary-helpers.large.json) | 8.757 | 16.859 | 3.751 | 5.5% / 9.7% / 0.6% |
| [compile-run / scripts.text-summary-helpers.small](02-compile-run-scripts.text-summary-helpers.small.json) | 1.533 | 1.603 | 0.309 | 26.3% / 29.6% / 1.4% |
| [compile-run / scripts.text-summary-helpers.large](03-compile-run-scripts.text-summary-helpers.large.json) | 12.376 | 13.359 | 1.470 | 45.5% / 18.6% / 1.8% |
| [compile-run / scripts.json-summary-helpers.small](04-compile-run-scripts.json-summary-helpers.small.json) | 1.869 | 2.369 | 0.397 | 35.1% / 21.3% / 2.3% |
| [compile-run / scripts.json-summary-helpers.large](05-compile-run-scripts.json-summary-helpers.large.json) | 9.912 | 24.471 | 4.427 | 10.0% / 169.9% / 0.9% |
| [compile-run / scripts.config-edit.small](06-compile-run-scripts.config-edit.small.json) | 2.026 | 2.129 | 1.526 | 21.4% / 19.9% / 7.3% |
| [compile-run / scripts.config-edit.large](07-compile-run-scripts.config-edit.large.json) | 2.546 | 2.474 | 1.828 | 82.7% / 92.6% / 3.8% |
| [compile-run / scripts.file-manifest-helpers.small](08-compile-run-scripts.file-manifest-helpers.small.json) | 1.746 | 2.193 | 0.582 | 32.5% / 30.1% / 0.9% |
| [compile-run / scripts.file-manifest-helpers.large](09-compile-run-scripts.file-manifest-helpers.large.json) | 2.049 | 1.978 | 0.614 | 22.3% / 24.8% / 2.6% |
| [compile-run / scripts.file-search.small](10-compile-run-scripts.file-search.small.json) | 1.543 | 1.446 | 0.266 | 8.4% / 4.4% / 1.1% |
| [compile-run / scripts.file-search.large](11-compile-run-scripts.file-search.large.json) | 1.615 | 1.838 | 0.393 | 177.0% / 184.5% / 2.1% |
| [warm / scripts.csv-summary-helpers.large](12-warm-scripts.csv-summary-helpers.large.json) | 7.713 | 33.156 | 3.495 | 4.3% / 7.7% / 1.1% |
| [warm / scripts.text-summary-helpers.large](13-warm-scripts.text-summary-helpers.large.json) | 5.783 | 39.647 | 1.308 | 12.1% / 56.0% / 1.4% |
| [warm / scripts.json-summary-helpers.large](14-warm-scripts.json-summary-helpers.large.json) | 7.769 | 15.040 | 4.268 | 6.7% / 185.3% / 0.4% |
| [warm / scripts.config-edit.large](15-warm-scripts.config-edit.large.json) | 2.112 | 2.065 | 1.778 | 42.6% / 68.9% / 6.1% |
| [warm / scripts.file-manifest-helpers.large](16-warm-scripts.file-manifest-helpers.large.json) | 1.704 | 1.702 | 0.446 | 10.5% / 11.1% / 1.4% |
| [warm / scripts.file-search.large](17-warm-scripts.file-search.large.json) | 1.430 | 1.406 | 0.270 | 4.8% / 155.5% / 1.9% |
| [warm-async / scripts.csv-summary-helpers.large](18-warm-async-scripts.csv-summary-helpers.large.json) | 9.989 | 9.294 | 3.540 | 4.4% / 5.4% / 2.8% |
| [warm-async / scripts.text-summary-helpers.large](19-warm-async-scripts.text-summary-helpers.large.json) | 7.606 | 16.327 | 1.309 | 5.6% / 158.4% / 1.7% |
| [warm-async / scripts.json-summary-helpers.large](20-warm-async-scripts.json-summary-helpers.large.json) | 10.257 | 59.291 | 4.317 | 5.6% / 47.5% / 0.9% |
| [warm-async / scripts.config-edit.large](21-warm-async-scripts.config-edit.large.json) | 2.265 | 2.213 | 1.790 | 56.9% / 67.5% / 5.7% |
| [warm-async / scripts.file-manifest-helpers.large](22-warm-async-scripts.file-manifest-helpers.large.json) | 1.551 | 1.619 | 0.437 | 4.5% / 4.1% / 0.8% |
| [warm-async / scripts.file-search.large](23-warm-async-scripts.file-search.large.json) | 1.403 | 2.452 | 0.270 | 116.7% / 79.5% / 2.5% |
| [warm / loops.integer.large](24-warm-loops.integer.large.json) | 1.076 | 1.059 | 0.577 | 1.4% / 0.8% / 0.2% |
| [warm / calls.method.large](25-warm-calls.method.large.json) | 7.898 | 7.975 | 0.875 | 1.3% / 1.5% / 0.2% |
| [warm / generators.drain.large](26-warm-generators.drain.large.json) | 6.747 | 7.306 | 0.839 | 9.2% / 11.6% / 1.1% |

## Separate CPU, allocation and GC diagnostics

The fixed follow-up uses CSV-helper large, warm execution, unchanged worker/script/goldens. Captures are separate from timing. The EventPipe pair runs 64 jobs; the perf cpu-clock pair runs 256 jobs with diagnostic-only DOTNET_PerfMapEnabled=1. The [compressed raw evidence](foundation-profiles-20261029.tar.gz) contains traces, perf data/maps, full records and analyses; ETLX conversion caches and redundant text exports are omitted.

| Capture | Published net10 | Foundation net11 |
| --- | ---: | ---: |
| EventPipe: 64-job elapsed | 496.8 ms | 479.1 ms |
| Active worker OS CPU | 470 ms | 460 ms |
| Matched SuspendForGC intervals | 36 / 14.02 ms total / 0.569 ms max | 35 / 13.44 ms total / 0.541 ms max |
| Other CLR suspension intervals | 448 / 8.27 ms total | 436 / 8.04 ms total |
| perf: 256-job elapsed | 1,824.8 ms | 1,798.2 ms |
| Active worker OS CPU | 1,770 ms | 1,750 ms |
| Separate JIT-thread OS CPU | 510 ms | 730 ms |
| Active-worker on-CPU leaf samples | 352 | 348 |

EventPipe managed leaves misleadingly concentrate in Monitor.Enter_Slowpath (old) and Thread.PollGCWorker (new), despite similar actual CPU time. **Do not use those leaves as CPU shares or claim that locks/GC explain the gap.** perf samples actual on-CPU IPs; mapping with saved JIT ranges finds executable dispatch (25/352 old, 39/348 new), monitor enter/exit (42/352, 44/348), CSV parsing and dictionary/name work. Unknown leaves are 7/352 and 36/348. The sample is small and provides leads, not causal attribution. No governor deletion is justified.

Allocation ticks attribute roughly 50–57 MB each to executable-frame interpreters, temporary reservations and object arrays over 64 CSV jobs. PyString, BigInteger and execution-context allocations are also substantial. Ticks are sampled interval-byte attribution, not exact object counts or retained-memory measurements. The remaining phase/ownership attribution is pending M3.

The paired diagnostic elapsed times are close, unlike several short timing rows. Tiering/JIT state is a hypothesis requiring a bounded check, not an established explanation. No result was rerun to obtain a favorable median. Failed preparation attempts (wrong worker fixture directory and a perf acknowledgement delimiter assumption) ended before measured work; corrected captures are named v2. Offline analysis initially treated every CLR suspension as GC and used an unsupported perf sort key; the retained analysis now separates reasons and maps actual leaf IPs.

## Mapping correctness follow-up

The fixed [formatting catalog](format-mapping-catalog.json) performs 256 paired
format_map/format calls with identical dictionaries and complete CPython output.
One [warm comparison](format-mapping/comparison.json) uses net11 for both source
revisions: foundation bed371f6 459.3 microseconds/job (3.6% IQR), mapping fix
b13179a0 533.0 microseconds (3.4%), CPython 157.0 microseconds (0.8%). Quiet gates
pass. This narrow, unqualified observation is about 16% slower after the fix;
there is no speedup claim. The corrected mapping semantics and real suspension
support remain valuable, but shared callback/formatting overhead stays visible
for subsequent structural work. No retiming or special-case optimization follows.
It does not alter the foundation matrix or qualify overall 0.11 performance.

## Reproduction and limits

Use [COMPARISON.md](../../COMPARISON.md) and the frozen [collection driver](collect-baseline.py). It uses the existing comparison micro command, seven rotating 25 ms batches after one second of warmup, a shared VM lease and a 30-second systemd control-group kill boundary per job. The complete collection stayed within its five-minute diagnostic budget. Full catalogs are either JSON or losslessly gzip-compressed; the receipt hashes identify the original bytes.

[profile-workers.py](profile-workers.py), [the managed collector/analyzer](diagnostics/Program.cs) and [analyze-cpu.py](analyze-cpu.py) preserve the diagnostic procedure. On Linux, perf data created by sudo requires read permission or a sudo analysis invocation. No runtime instrumentation, unsafe production code or new benchmark framework is introduced. In-flight I/O timing, frontend phase attribution, precise allocations/retained balances and stable milestone qualification remain outstanding.
