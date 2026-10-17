# Complete CSV job with annotated helper — 2026-10-17 UTC

| Engine | Measured revision | Fresh compile/run median | IQR / median |
| --- | --- | ---: | ---: |
| Lython | efccd974c1077ff603198c210bf65ca17dbd7eba | 17.358 ms | 45.85% |
| CPython | 3.13.2, ordinary GIL and cyclic GC | 14.348 ms | 29.76% |

This one local diagnostic is **inconclusive**. It establishes no qualified
Lython/CPython ratio. It measures the named runtime, not subsequent HEADs, and
cannot be compared directly with the quiet-VM CSV snapshots.

Separately, earlier Lython a5a2fc0f02121fd8f040aff0c26c98c537ce8ca7 measured
14.818 ms with 48.73% spread. The candidate median is 17.1% higher; the spreads
leave the direction uncertain. No elapsed improvement or regression is established.
The annotated-helper change has separate allocation evidence; these whole-job
totals do not isolate it or explain the overall CPython gap.

The [script](../../scripts/csv-summary-helpers.py) calls an ordinary annotated
integer-parsing helper for every CSV row, aggregates accepted amounts, writes a
sorted report and prints the accepted count. Input: 1,024 blocks / 4,096 records /
57,361 UTF-8 bytes, including quoted newlines, accented and supplementary text.
Source: 599 UTF-8 bytes. Output: `3072\n` and the complete three-line report.
The original [CSV script](../../scripts/csv-summary.py) remains unchanged.

Both exact Lython runtimes and CPython first pass independent stdout/file goldens
for all four CSV/text/JSON/file helper jobs at 0, 16 and 1,024 blocks: 240 responses.
All 35 responses in the subsequent CSV capture preserve its full output/files.
The [catalog](catalog.json), [baseline verification](baseline-verify.json),
[candidate verification](candidate-verify.json) and [raw capture](micro.json)
retain the source, fixtures, digests and responses. The maintained representative
catalog now includes these variants; the timing used the frozen adapter described
below, before that catalog addition.

Boundary: warm worker processes, fresh compilation and invocation state per job.
Timed work includes compilation, staged inputs/output reset, execution, public
capture/projection, full stdout/file checks, per-job compilation disposal and
previous-result cleanup. Startup, transport and response hashing are outside.
Compile-only, startup and warm execution were not separately sampled here;
their contributions cannot be inferred from this total.

Both engines use the same logical fixture/reset/output work. Lython keeps public
containment, cooperative admission and explicit result ownership; CPython uses
ordinary reference counting/cyclic GC without equivalent in-process containment.
The existing comparison host supplies efficient file overrides. These practical
differences are declared, not subtracted.

Machine: active Windows 11 Business development workstation, build 26200, x64;
Intel Core i7-14700KF, 20 cores / 28 logical processors. Release .NET SDK
10.0.300-preview.0.26177.108 / runtime 10.0.13; CPython 3.13.2, MSC v.1942 x64,
`-I -S -X utf8`, no debug/free-threaded mode. Normal GC/tiering, no runtime
overrides or forced collections; BenchmarkDotNet is not loaded.

Each frozen Lython worker contains 50 files. Only Lokad.Lython.dll differs,
extracted from the explicit Release packages for the respective revisions.
Both use the same adapter built at a5a2fc0f; its source did not change through
efccd974. Loaded runtime revisions, DLL hashes, package SHA512 values and both
full manifests are recorded in [measurement.json](measurement.json).
efccd974 is qualified by [CI39014909664](https://github.com/Lokad/Lython/actions/runs/39014909664).

One seven-round capture took 10.119 seconds under the existing 28-second process
bound plus one-second cleanup. All 100 frozen files remain unchanged; the seven
correctness/timing workers and fixture directories are gone. No retuning,
second capture, long VM lane, qualified ratio or embedding-latency claim.
