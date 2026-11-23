# B1478 CPython checkpoint after fixed tuple admission

The November 23, 2026 UTC checkpoint measures Lython `a94f78fc7` against
stock CPython 3.13.16. The Lython core was built at `f5852aa99` with
identical production and test sources. All 32 files in the measured
image and the Python executable hashes verify on Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3.

Ratios divide Lython elapsed by CPython elapsed. Each range spans the
four pairings of process medians from two workers per engine. Each
worker warms over full requests and contributes seven rotated timed
batches. Warm timings include applicable RAM-backed I/O, verification
of stdout and files, and disposal of the previous result. Lython
uses a mediated host with efficient ranged reads and a shared memory
allowance. CPython uses its native file APIs. Compilation includes
code-owner disposal. Compile to result includes both phases in running
processes. Fixture reset and request transport sit outside elapsed.
The wider process CPU is available separately.

| Workload | Boundary | Lython | CPython | Lython / CPython | Worker spread, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Native typed ASCII CSV meter | Warm | 2.686–2.701 ms | 2.508–2.521 ms | 1.065–1.077× | 0.58 / 0.54% |
| CSV meter with explicit float | Warm | 3.809–3.895 ms | 2.814–2.870 ms | 1.327–1.384× | 2.27 / 1.97% |
| Short nonlocal word report | Warm | 1.3716–1.4158 ms | 0.9429–0.9572 ms | 1.433–1.502× | 3.23 / 1.52% |
| Quote-heavy CSV invoice | Warm | 7.253–7.335 ms | 6.507–6.521 ms | 1.112–1.127× | 1.12 / 0.22% |
| Configuration edit | Compilation only | 0.01896–0.01904 ms | 0.03400–0.03406 ms | 0.557–0.560× | 0.42 / 0.17% |
| Configuration edit | Compile to result | 0.1070–0.1073 ms | 0.1232–0.1246 ms | 0.859–0.871× | 0.31 / 1.14% |
| Postponed nested factories | Compile to result | 1.0503–1.0599 ms | 0.6084–0.6115 ms | 1.718–1.742× | 0.91 / 0.51% |

The short nonlocal report and postponed factories retain substantial
gaps. In the quote-heavy invoice and postponed factory lanes both
engines take longer than in B1467. The controlled B1477 source pair
leaves the isolated CSV elapsed effect of tuple admission unresolved.
This checkpoint describes the integrated source and contains no startup
measurement.

Six independent stdout/file goldens and all 196 timed batches verify.
The run takes 317 seconds. Both quiet gates pass, workers join, guest
fixtures are removed and the raw archive verifies under the lease.

[Timings and wider CPU](summary.json) · [Identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz) ·
[Measured core and isolated comparison](../2026-11-23-fixed-tuple-admission/README.md) ·
[Earlier CPython checkpoint](../2026-11-23-numeric-capture-cpython/README.md).
