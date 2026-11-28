# B1921 Compare the reader checkpoint with CPython

The November 28, 2026 UTC run uses the four-vCPU Azure F4as_v7 with AMD
EPYC 9V45, Ubuntu 24.04.4, .NET 11 RC3 and stock CPython 3.13.16. The
32-file Lython image matches `bfde55b6b`. Six independent output and file
content checks pass locally and on the VM.

Each of two workers per engine warms independently for at least five
seconds and supplies seven rotated batches per lane. Ratios divide Lython
by CPython and span all worker-median pairings. Warm execution uses
compiled scripts and includes applicable RAM-backed I/O, output and file
verification, and previous-result disposal. Compile-only runs on resident
source and includes previous compiled-owner cleanup. Compile-to-result
also includes execution, I/O and result disposal. Process CPU includes
fixture reset and request framing. Startup is outside these boundaries.

| Workload | Phase | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Iterable-row shipment | Warm | 2.01259–2.03177 ms | 1.95870–1.95885 ms | 1.027–1.037× | 1.027–1.031× |
| Iterable-row shipment | Warm `RunAsync` | 2.26645–2.35056 ms | 1.94349–1.96357 ms | 1.154–1.209× | 1.152–1.218× |
| Nested JSON document summary | Warm | 0.64000–0.65729 ms | 0.67334–0.68318 ms | 0.937–0.976× | 0.948–0.977× |
| Nested JSON document summary | Warm `RunAsync` | 0.65244–0.65863 ms | 0.67966–0.69197 ms | 0.943–0.969× | 0.978–1.000× |
| File-fed CSV quantity map | Warm `RunAsync` | 0.40809–0.43424 ms | 1.37398–1.37725 ms | 0.296–0.316× | 0.329–0.351× |
| File search, match at line 1 | Warm | 0.06673–0.06707 ms | 0.05071–0.05124 ms | 1.302–1.322× | 1.005–1.020× |
| Generated ordinary definitions | Compile only | 0.51016–0.51341 ms | 1.09577–1.10337 ms | 0.462–0.469× | 0.462–0.469× |
| Configuration edit | Compile to result | 0.09619–0.09630 ms | 0.12406–0.12461 ms | 0.772–0.776× | 0.728–0.744× |

Dense JSON is below CPython on elapsed time in both APIs. Dense async CPU
spans parity. Iterable shipment still spends 15–22% more process CPU in
Lython `RunAsync`, while sync uses about 3% more. This separates the
remaining async shipment gap from the faster file-fed map, which has a
6.41% Lython elapsed spread. The first-line search gap stays near 16 µs
elapsed with 0.5–2.0% more process CPU. Cross-checkpoint changes in ratios
include both engines and separate collections. [Earlier B1914 run](../2026-11-28-current-cpython-24218276/README.md).

All 224 batches pass in 331.5 seconds. Both quiet gates pass, all workers
join and the RAM-backed fixtures are removed. The persistent archive
verifies under the lease. Staging reuses 31 immutable files. The reclaimed
B1914 core replica has a verified local copy and its observations remain
archived.

[Timing and CPU](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Staging](stage-custody.json) · [Procedures](procedures.tar.xz) ·
[Current local profiles](../2026-11-28-reader-checkpoint-profile/README.md).
