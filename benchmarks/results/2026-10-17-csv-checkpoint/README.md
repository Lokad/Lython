# Complete CSV helper job on the dedicated VM — 2026-10-17 UTC

| Engine | Measured revision | Fresh compile/run median | IQR / median |
| --- | --- | ---: | ---: |
| Lython | 463b7a7a847234396c695e2a214fbea05ab3300e | 8.581 ms | 4.36% |
| CPython | 3.13.16, PGO/LTO, ordinary GIL and cyclic GC | 3.718 ms | 0.85% |

Lython took nominally **2.308 times** the CPython time in this one short
diagnostic. This is **not a qualified multiplier or confidence interval**. The
result measures the named revision, not later HEADs. It establishes a remaining
compile-to-result gap for this job without identifying its cause.

Separately, earlier Lython a35ee62ceb712694e8b0d3fb79f85c4315628f7e measured
8.432 ms with 5.86% spread. The candidate median is 1.77% higher, within the
observed spreads: no improvement or regression is established. The intervening
exact-byte truth reduction is not exercised by this CSV script; its allocation
evidence is a separate result.

The unchanged [script](../../scripts/csv-summary-helpers.py) calls an annotated
integer-parsing helper for every CSV row, aggregates accepted amounts, writes a
sorted report and prints the accepted count. Input: 1,024 blocks / 4,096 records /
57,361 UTF-8 bytes, including quoted newlines, accented and supplementary text.
Source: 599 UTF-8 bytes. Stdout is `3072\n`; the entire 30-byte output file is:

```csv
kind,total
blue,7168
red,3072
```

Both immutable Lython runtimes and CPython pass independent complete stdout/file
goldens for the existing empty, small and large CSV-helper cases before timing:
48 output-checked responses and 12 compile-only smoke responses across
[baseline](baseline-verify.json) and [candidate](candidate-verify.json)
verification. All 35 responses in the single
[timing capture](micro.json) preserve source, fixture, stdout and output-file
digests. The [catalog](catalog.csv.json) is a three-case subset of the existing
representative catalog, not a new workload or retuned fixture.

Boundary: warm worker processes, fresh compilation and invocation state per job.
The timer includes compilation, input staging/output reset, execution, public
capture/projection, complete output checks, result cleanup and compilation
disposal. Startup, transport and response hashing are outside. No separate warm,
compile-only or startup performance collection was made; their contributions cannot be
inferred from this total or from unrelated historical medians.

Both engines perform the same logical fixture/reset/output work. The existing
comparison host supplies efficient file overrides. Lython retains its public
limits, cooperative admission and explicit ownership; CPython retains ordinary
reference counting and cyclic GC without equivalent in-process containment.
These are declared application costs; none is subtracted.

Machine: dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, x86_64,
kernel 6.17.0-1022-azure, four AMD EPYC 9V45 vCPUs, approximately 16 GiB RAM,
no swap. The [machine/cleanup receipt](final-cleanup.json) and
[before](before-capture.json)/[after](after-capture.json) snapshots retain exact
identities and low observed load. This short capture does not perform milestone
quiet-machine qualification.

Execution toolchain: pinned .NET SDK 10.0.401 / runtime 10.0.12 and CPython
3.13.16 built with GCC 13.3.0, PGO/LTO, no debug/free-threaded mode or JIT,
launched with `-I -S -X utf8`. The system Python 3.12.3 is not the reference.
Workers keep ordinary GC/tiering with no overrides; BenchmarkDotNet is not loaded.
Only the supervisor uses the existing Linux tiering override, which the worker
launcher removes before creating workers.

The transferred managed binaries were built in Release with SDK
10.0.300-preview.0.26177.108; **10.0.401 is the VM execution SDK, not their build
compiler**. Each frozen worker contains 54 files. Only `Lokad.Lython.dll`
differs, extracted from explicit Release NuGet packages for the two revisions.
Both use the identical adapter built at a35ee62c, whose source and scripts are
unchanged through 463b7a7a. Loaded identities, package SHA512 values, executable
hashes and full frozen manifests are in [measurement.json](measurement.json),
[staging-proof.json](staging-proof.json) and the raw capture. The candidate
runtime was already qualified by
[CI 39050072941](https://github.com/Lokad/Lython/actions/runs/39050072941).

One existing `--compare micro --lane compile-run` capture used a one-second
warmup per worker, two-second settling period and seven rotating-order batches
calibrated to 25 ms. Its collection deadline was 15 seconds. The shared VM
`flock` lease and owned systemd service enforced `RuntimeMaxSec=30`,
`TimeoutStopSec=0` and `KillMode=control-group`. The service succeeded in
8.602 seconds; the exact command and observed exit are in
[micro-service.json](micro-service.json) and [micro-service.log](micro-service.log).

All 108 frozen files remained unchanged. All seven correctness/timing workers
and their fixture directories are gone, the service is inactive and the shared
lease is reusable. There was no second capture, long lane, framework change,
allocation/RSS/embedding-latency measurement or causal speedup claim. Preserve
the older Windows CSV measurements as separate historical observations.
