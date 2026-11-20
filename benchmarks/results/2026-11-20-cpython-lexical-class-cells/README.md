# B1200 Lexical class-cell delivery against CPython

Measures Lython `f65be4da5` using the source-identical frozen
`537b4b3dd` image from B1199. The baseline is stock CPython 3.13.16.
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC.

| Workload | Input | Phase | Lython | CPython | Lython / CPython |
| --- | --- | --- | ---: | ---: | ---: |
| Inherited dataclass configuration | 1 base, 48 subclasses and 48 instances | Warm | 0.199–0.202 ms | 5.051–5.080 ms | 0.039–0.040× |
| Inherited dataclass configuration | Same script | Compilation | 0.21421–0.22031 ms | 0.46459–0.46501 ms | 0.461–0.474× |
| Inherited dataclass configuration | Same script | Compile to result | 0.434–0.442 ms | 5.684–5.739 ms | 0.076–0.078× |
| Eager annotated rules | 2,048 inputs and definitions | Warm | 1.443–1.472 ms | 0.424–0.438 ms | 3.30–3.47× |
| CSV invoice | 4,096 data records and 127,031 B | Warm | 6.763–6.779 ms | 6.294–6.332 ms | 1.07–1.08× |

Ratios divide Lython elapsed by CPython elapsed. Ranges cover the four
pairings of duplicate process medians. Warmup per process was at least
four seconds for compilation, six for compile to result and eight for warm
execution. Each phase used five rotated rounds of roughly 0.4-second
requests. Duplicate spreads reach 3.25% for the eager-rule CPython workers.
The maximum per-process IQR is 2.79% on the same workload.

Compilation includes compiled-owner disposal. Warm includes execution,
mediated RAM-backed I/O where applicable, output and file checks, and
previous-result disposal. Compile to result combines both boundaries.
Startup, fixture reset and final teardown are outside guest elapsed.
Wider process CPU includes the request. Lython runs with shared-budget
accounting. CPython uses native file APIs.

The dataclass script constructs and decorates 49 classes on every run.
The measurement closes the external comparison for the lexical delivery.
B1199 isolates the class-cell preparation change within Lython. Eager
rules remain the largest warm gap in this comparison, with the difference
between their duplicate processes reflected in the range.

All 32 worker files and workload goldens verified. Both quiet gates
and all 100 timed batches passed. Workers joined, fixtures were removed and
the persistent archive verified under the VM lease.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Verification and procedures](procedures.tar.xz) ·
[Paired Lython comparison](../2026-11-20-lexical-class-cells/README.md) ·
[Earlier class-suite comparison](../2026-11-20-cpython-executable-classes/README.md).
