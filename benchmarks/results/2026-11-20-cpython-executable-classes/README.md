# B1196 Executable classes against CPython

Measures Lython `53d3b45d5` using the source-identical frozen `cf059b2a2`
image from B1195. The external baseline is stock CPython 3.13.16.
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC.

| Workload | Input | Phase | Lython | CPython | Lython / CPython |
| --- | --- | --- | ---: | ---: | ---: |
| Inherited dataclass configuration | 1 base, 48 subclasses and 48 instances | Warm | 0.201–0.204 ms | 5.007–5.147 ms | 0.039–0.041× |
| Inherited dataclass configuration | Same script | Compilation | 0.23994–0.24064 ms | 0.46485–0.46598 ms | 0.515–0.518× |
| Inherited dataclass configuration | Same script | Compile to result | 0.463–0.464 ms | 5.629–5.772 ms | 0.080–0.082× |
| Eager annotated rules | 2,048 inputs and definitions | Warm | 1.480–1.513 ms | 0.442–0.449 ms | 3.30–3.42× |
| CSV invoice | 4,096 data records and 127,031 B | Warm | 6.697–6.760 ms | 6.325–6.421 ms | 1.04–1.07× |

The ratio divides Lython elapsed by CPython elapsed. Ranges cover the four
pairings of duplicate process medians. Each process warmed for at least
four seconds on compilation, six on compile to result and eight on warm
execution. Each phase used five rotated rounds of roughly 0.4-second
requests. Duplicate spreads are under 2.80%, and per-process IQRs are
under 2.75%.

Compilation includes compiled-owner disposal. Warm includes execution,
mediated RAM-backed I/O where applicable, golden output and file checks,
and previous-result disposal. Compile to result combines both boundaries.
Fixture reset, process startup and final teardown are outside elapsed
timing. Wider process CPU includes the request and is preserved separately.
Lython uses shared-budget accounting. CPython uses its native file APIs.

These results describe the class-suite delivery revision. The dataclass
workload constructs and decorates all 49 classes at each execution.
B1195 isolates the class-engine change within Lython and records the cold
regression. The eager factory remains a larger warm gap than the invoice.
The earlier dataclass and rule observations remain available for their
measured revisions.

All 32 worker files and workload goldens verified. Both quiet gates and
all 100 timed batches passed. Workers joined, fixtures were removed and
the persistent archive verified under the VM lease. The collection took
185 seconds across the five phases.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) ·
[Paired Lython comparison](../2026-11-20-executable-class-suites/refinement/README.md) ·
[Earlier comparison](../2026-11-20-cpython-dataclasses-and-factories/README.md).
