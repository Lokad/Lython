# Retained-runtime CPython checkpoint

2026-11-05, retained13d69be6, frozenec4b6378 built before commit metadata on
f8ebdc7c plus the D59 patch. Source hashes/git blobs verify the committed source.
Reference9c1d7e74/frozen6cb05f8e is the previously measured, fully qualified source;
its terminal qualification is already cached. D57 layout is absent. Same workerb94,
auxiliaries, CPython3.13.16, catalog and protocol as the earlier representative run.

B290 **440.25s**,16jobs, within480s target/540s owned bound. Seven useful scripts
each warm/compile-run, plus CSV/report compile-only. Normal GC/tiering,5s per-role/
job warmup,7rotated~150ms samples, independent identical-current process. Quiet
dedicated AzureF4as_v7/Ubuntu24.04.4/AMD EPYC9V45x4/16GiB/no swap, .NET11RC3.
Every source/fixture/library/golden/quiet/Closed/final-zero/worker-join/removal gate
passes. No missing job or abandoned work. Catalog/raw data remain persistent.

| Complete compile-to-result job | Current /CPython | Status |
| --- | ---: | --- |
| Native CSV |1.27–1.29×|Small source-pair improvement; diagnostic|
| Ordinary Python helper CSV |1.43–1.44×|Source pair favorable; diagnostic|
| Text helpers |1.89–1.96×|3.46% current spread; variable|
| JSON-lines |1.07–1.08×|Source pair favorable; diagnostic|
| JSON-to-Markdown report |1.45–1.47×|2.24/3.51% current IQR; variable|
| Config edit |1.03–1.04×|Small difference/dispersion; variable|
| Long-field CSV |.14×|16large records do not represent ordinaryCSV|

Ratio=current elapsed/CPython elapsed; current ranges are two independent medians,
not confidence intervals. [Current guide](../../../docs/BENCHMARKS.md) gives times,
inputs, warm table and independent compilation. No subtraction to infer compilation.

## Separate earlier-Lython comparison and uncertainty

Retained group versus9c1d: warm ordinaryCSV−6.53/8.82%,text−9.07/9.50%; freshJSON
−7.04/8.42%. These coupled source-pair observations do not isolate each change or
establish causalCPU/portable speedups. Warm source/nativeCSV and JSON have process
control spread; full medians/IQR/CPython/reference samples remain in summary/raw.

**Adverse:** fresh report+.65/1.72% versus9c1d, with current IQR2.24/3.51% and
reference3.04%; variable, not erased by a favorable warm observation. Config fresh
near-flat/+1.68%, warm IQR~4–5%; compilation small/inconclusive. NativeCSV compile
69.920–71.692µs vsCPython54.857µs(1.27–1.31×), report50.048–50.778µs vs37.522µs
(1.33–1.35×). No general compiler-gain claim. D55 mixed-Unicode debt and all earlier
adverse observations remain; this standard text fixture does not close them.

## Boundary and delivery

Included: invocation, actual mediated /dev/shm I/O, full stdout/file checks and
prior-result cleanup. Fresh adds compilation/disposal. Compile-only excludes
execution/I/O. Excluded: startup, staging/reset/transport/response hashing and
final cached program/result/owner/process/fixture teardown. Final results remain
funded through response delivery. Both APIs' genuinely suspended behavior is
tested independently; these timings use synchronous Run. Fresh globals, persistent
CPython process module caches and isolated Lython execution caches, unchanged.
No allocation/causalCPU/RSS/host-latency/startup measurement or claim.

Full local4143internal/16004public per actual.NET11/10 passes. Retained13d platform/
original stress/installed consumers [CI42074585232](https://github.com/Lokad/Lython/actions/runs/42074585232)
is GREEN/all8 on13d69be6, with its own
[cached terminal receipt](../2026-11-05-frame-exit-cleanup/delivery-ci-status.json).
Priorb69 delivery remains separate. Publication postponed; broader performance
and independent regex work continue.

[Contract](source-contract.md), [summary](summary.json), [identity](identity.json),
[raw source/observations/gates](observations.tgz), [archive receipt](archive-receipt.json).
[Previous guide](previous-guide.md) is a byte snapshot from docs/BENCHMARKS.md;
its relative links describe that original source location.
ArchiveSHA256`28c7ce29ea44f2336b20b34526f3f5ce91503ac120aee71047ab7c14c302f78c`.
