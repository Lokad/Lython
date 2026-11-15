# Helper-rich configuration and manifest phases

R386/B774, 2026-11-15 UTC. Committed **810379be / frozen 8fbfa3f0**, fully qualified by
[43689267617](../2026-11-15-function-slot-binding/delivery/README.md), versus stock
CPython 3.13.16 on the quiet Azure F4as_v7: Ubuntu 24.04.4, four EPYC 9V45 vCPUs,
16 GiB/no swap, .NET 11 RC3, ordinary workstation GC/tiering and CPython GIL/GC.

The [maintained script](../../scripts/config-manifest.py) has six ordinary helpers,
comprehensions, text normalization, configuration editing and manifest output:
**1,877 source bytes / 833 input bytes**. Expected records/files are built independently:
three enabled normalized targets, four de-duplicated sorted dependencies with last
version winning; original defaults/options retained and two exact UTF-8 JSON outputs.
Isolated CPython 3.13.2 and the unchanged public Lython worker agree before timing.

| Phase | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| compile | 202.568–202.781 µs | 156.855–157.823 µs | 1.28–1.29× | Diagnostic 8103; spreads 0.11/0.62%; max IQR 1.26% |
| warm | 191.507–192.335 µs | 154.809–155.556 µs | 1.23–1.24× | Diagnostic 8103; spreads 0.43/0.48%; max IQR 1.87% |
| compile-run | 495.332–496.371 µs | 460.923–464.835 µs | 1.07–1.08× | Diagnostic 8103; spreads 0.21/0.85%; max IQR 1.46% |

Above one means Lython slower. Two independent processes per engine, five rotating
rounds; ranges span all four process-median pairings, not confidence intervals.
Five-second full-request warmups and 0.5 s request batches retain every observation.

Warm includes mediated RAM I/O, stdout/file verification and prior-result cleanup;
excludes compilation, startup, fixture staging/reset, protocol and final cache
teardown. Compile-run adds compilation and compiled-owner disposal. Independently
warmed phase medians are not additive. Wider process CPU includes reset/protocol.

Fresh is about 1.07–1.08× CPython despite compilation about 1.28–1.29× and warm about
1.23–1.24×. These phases fill a real generated-script measurement gap; they do not
identify a dominant redundant compiler pass or justify a rewrite by pass count.
Close this bounded assessment without a speculative compiler patch. New concrete
workloads/attribution can reopen it; this is not a permanent design ban or a claim
that all remaining performance gaps are solved. No before/after gain is inferred.

All three jobs finish in 101.821 s with exact worker/core/provider/catalog/fixture/
source/output identity, goldens, quiet-before/after, ownership/Closed/join/fixture
cleanup and persistent archive verification under lease. No new framework, image
upload or retirement; existing immutable 8fbf worker image reused. The 254/252 MiB floors
remain, free space is 266,711,040 B after archive; no VM users or RAM fixtures remain.

Original local preparation failures are retained: direct Windows stdout used CRLF
until capture matched the existing worker convention; an incorrectly named local
fixture directory tripped the supervisor ownership guard before guest execution.
Its first stderr was lost through failing pipe cleanup, explicitly missing; the
corrected diagnostic captured the guard, then the prescribed directory convention
passed. Workload semantics/goldens were unchanged. No benchmark collection restart.
No startup, heap/RSS, tail-latency or universal CPU claim. Publication remains held.

[Exact phase observations and wider CPU](summary.json), [input/golden design](fixture-design.json),
[full raw, machine gates, catalog, workers and collection procedures](observations.tgz),
[local controls/failures](local-controls.tar.xz), [receipt](archive-receipt.json),
[terminal state](terminal-status.json). The earlier 96 B configuration observations
and every replaced row remain in the [prior guide](prior-BENCHMARKS.md).
