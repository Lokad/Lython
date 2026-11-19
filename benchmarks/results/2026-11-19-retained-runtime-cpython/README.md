# B1040 retained runtime against CPython

Measured Lython `b491250cb` with core `ad414955` against stock CPython
3.13.16 on 2026-11-19 UTC. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3. The collection reused the retained B1038/B1039 image
and the five unchanged, regex-free B1030 catalog cases. Both worker
images and expected outputs verified before staging.
[Runtime comparison](../2026-11-19-final-method-format/README.md) ·
[B1030 snapshot](../2026-11-19-shared-metadata-cpython/README.md).

| Workload | Input | Compilation only | Warm | Compile-to-result |
| --- | --- | ---: | ---: | ---: |
| Expanded forwarding report | 1,024 records / 26,112 B | 0.84× | 1.73–1.78× | 1.61–1.67× |
| Unannotated rule closures | 2,048 inputs / 4,096 definitions | 0.79× | 4.26–4.39× | 4.10–4.23× |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 0.80–0.81× | 5.08–5.21× | 4.83–4.97× |
| Shipment report with format selectors | 3,072 records / 189,441 B | 0.93–0.97× | 1.35–1.40× | 1.31–1.40× |
| CSV invoice | 4,096 data records / 127,031 B | 0.96–0.98× | 1.10–1.13× | 1.09–1.12× |

Ratios divide Lython elapsed by CPython elapsed across all four pairs of
process medians. Lython warm rule controls differed by 1.61–2.13%. CPython
forwarding controls differed by about 3%. The selector report's
compile-to-result controls differed by 3.32% in Lython and 3.66% in CPython.
Use the paired Lython comparison to attribute changes to the formatter.

The five jobs completed in 310.27 seconds. Each used two processes per engine
with all three phases and five rotated rounds. All 300 measured responses
passed protocol and golden checks. Both quiet gates passed, workers joined,
guest fixtures were removed and the archive was verified under the lease.

Warm elapsed includes mediated RAM-backed I/O, golden checks and prior-result
cleanup. Compilation alone includes disposal of compiled storage.
Compile-to-result includes compilation, execution and cleanup. Cold startup,
fixture reset, request framing and final cache teardown are outside elapsed.
Process CPU includes the wider request. Platform, stress and package-consumer
checks are tracked in the delivery run.
[CI](https://github.com/Lokad/Lython/actions/runs/44453736908).

[Per-process elapsed and CPU](summary.json) · [Raw observations](observations.tgz) ·
[Verified procedures and image hashes](procedures.tar.xz).
