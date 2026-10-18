# Complete CSV helper comparison, 2026-10-18 UTC

| Boundary, 4,096 records / 57,361 input bytes | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| Fresh compile + synchronous execution | 8.549 ms | 3.962 ms | 2.158× | Diagnostic; IQR/median 6.72% / 0.63% |
| Warm public RunAsync, completed host operations | 9.321 ms | 3.759 ms | 2.480× | Diagnostic; IQR/median 3.84% / 0.59% |

Lython is the immutable revision **34d4da357c2fbd553fa8c049dd424e5149fae3e8**.
The external reference is **CPython 3.13.16**, PGO/LTO, ordinary GIL and cyclic
GC, without JIT/debug/free-threaded mode. Ratios divide Lython elapsed time by
CPython elapsed time; above one means Lython takes longer. These two short
captures demonstrate a remaining gap at these boundaries. They do not qualify
its multiplier, a speedup, an embedding-latency improvement or later HEADs.

The unchanged [script](source.py) calls an annotated parsing helper, reads CSV
through DictReader, filters negative amounts, aggregates by key, writes the
complete sorted [report](expected-report.csv) and prints [3072](expected-stdout.txt).
The [input](input.csv) includes quoted newlines, accented and supplementary text.
The catalog's size of 1,024 means 1,024 blocks / 4,096 data records. Source is
599 UTF-8 bytes and the report is 30 bytes. Every response checks complete stdout/files;
independent Python CSV aggregation also reproduces both goldens.

## Boundaries and fairness

| Included | Fresh compile/run | Warm RunAsync |
| --- | --- | --- |
| Fresh compilation of source for every job | Yes, both engines | No, compiled code reused |
| Fresh guest namespace/invocation, result cleanup and capture | Yes | Yes |
| Input staging/output reset, execution and complete output-file checks | Yes | Yes |
| Worker startup, protocol transport and response hashing | Excluded | Excluded |

Both workers run inside already-started host processes. Fresh compilation calls
the real compiler on every iteration, without an identical-script cache. Lython
uses public Run in that lane and public RunAsync in the warm lane. Host operations
complete synchronously; this does not measure genuine suspended I/O. The two
lanes cannot be subtracted to estimate compilation cost or compared to attribute
an async penalty. Standalone compilation and startup remain unmeasured here.

Lython keeps ordinary public limits, cancellation, stack admission, cooperative
ownership and allocation preflights. A shared unbounded host allowance funds
compilations/results; prior results are disposed inside timing, each fresh
compilation is disposed, and the final result survives response writing. Cached
compilations are disposed on worker exit. CPython retains ordinary reference
counting and cyclic GC without equivalent in-process containment. Nothing is
subtracted. No refused admission or finite-budget streaming is qualified here.

## Comparison with earlier Lython

These are separate from the external baseline:

| Same boundary | Earlier Lython `1b6a1531` | Lython `34d4da35` | Nominal time change |
| --- | ---: | ---: | ---: |
| Fresh compile/run | 9.185 ms | 8.549 ms | −6.92% |
| Warm RunAsync | 9.881 ms | 9.321 ms | −5.66% |

Earlier revision: `1b6a15318b00ec056af02e23b72fa64f861964df`, the last immutable CSV
snapshot. Its IQR/median is 12.43% fresh and 4.36% warm. One capture per boundary
does not establish an improvement; no individual runtime change gets credit.
The comparison implementation, CPython worker and script are byte-identical
between revisions. No runtime, dependency, regex or benchmark framework change
was made for these measurements. The earlier frozen baseline was verified and
reused without modifying its source or binaries.

## Provenance, sampling and cleanup

Dedicated Azure Standard_F4as_v7: Ubuntu 24.04.4 LTS, Linux 6.17.0-1022-azure,
four AMD EPYC 9V45 vCPUs, 16 GiB RAM, no swap. Both clean Release source builds use
SDK 10.0.401 and run on .NET 10.0.12. CPython uses GCC 13.3.0 with PGO/LTO and
`-I -S -X utf8`. Actual loaded hashes/versions and runtime overrides are in the
[fresh receipt](compile-run/micro.json) and [warm receipt](warm-async/micro.json).
[Inspection](inspection.json), [preflight](preflight.json),
[preparation](preparation.json), [old build](build-old.log) and
[current build](build-candidate.log) preserve machine/build provenance.

Each existing micro command has a 15-second collection deadline, one-second
warmup per engine, two-second settling and seven rotating-order batches
calibrated to 25 ms. There was exactly one capture per boundary, no retries or
ten-minute lane. Owned systemd groups enforce 28 seconds plus at most 1 second
cleanup and kill the whole group. The actual runs took 8.648 and 9.170 seconds.
Only the supervisor disables tiering; worker launch removes that override.
Worker overrides are all null, ordinary GC/tiering remain, and BenchmarkDotNet
is not loaded. Initial VM load was zero; the recorded post-build load is not
milestone noise qualification.

All 70 responses / 957 invocations preserve source, fixture, stdout and file digests.
All timing inputs are unchanged, immutable source trees stay clean, all six
worker processes and fixture directories are gone, service groups are empty
and the shared lease is available. [Validation](validation.json) recomputes every
median and IQR from raw batches; [final cleanup](final-cleanup.json) preserves
the terminal evidence. Both [fresh](compile-run/ownership.json) and
[warm](warm-async/ownership.json) ownership records and launch/terminal receipts
are retained. The [raw archive](raw-evidence.tar.gz) contains the unchanged
catalog, launch/build scripts and complete receipts; SHA256:
`37e365bdd30b24e2c4fb2dc5bc6bc7f155e6edf39873a935506866fc9baf9b31`.

This bounded comparison is closed. Ratios remain diagnostic; there is no rerun,
trace or broader qualification campaign implied by sample spread. Runtime
qualification is the already-cached green Windows/Ubuntu
[CI39097603483](https://github.com/Lokad/Lython/actions/runs/39097603483) and its
Release package consumers.
