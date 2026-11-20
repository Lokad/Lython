# B1203 Factory delivery against CPython

Measured Lython `50d63cf31` using the source-matched `32c5f2ef7` image
against stock CPython 3.13.16 on the quiet Azure F4as_v7, Ubuntu 24.04.4
and .NET 11 RC3, on 2026-11-20 UTC.

| Workload | Input | Phase | Lython | CPython | Lython / CPython |
| --- | --- | --- | ---: | ---: | ---: |
| Eager annotated rules | 2,048 inputs and definitions | Warm | 1.057 ms | 0.428–0.434 ms | 2.44–2.47× |
| Eager annotated rules | Same script | Compilation | 0.01992–0.02021 ms | 0.03225–0.03236 ms | 0.615–0.627× |
| Eager annotated rules | Same script | Compile to result | 1.086–1.088 ms | 0.462–0.471 ms | 2.31–2.36× |
| Nested annotated factories | 2,048 inputs and 4,096 definitions | Warm | 1.774–1.797 ms | 0.589–0.594 ms | 2.99–3.05× |
| Postponed annotated rules | 2,048 inputs and definitions | Warm | 0.967–0.975 ms | 0.397–0.398 ms | 2.43–2.46× |
| CSV invoice | 4,096 records and 127,031 B | Warm | 6.597–6.719 ms | 6.343–6.360 ms | 1.04–1.06× |

Ratios cross both independent Lython process medians with both CPython
medians. Warmup per process was at least eight seconds for warm, six for
compile to result and four for compilation. Five rotated rounds targeted
0.4-second requests. Duplicate elapsed spreads are at most 1.93%.
Per-process IQR reaches 2.76% on the eager warm workload.

Compilation includes compiled-owner disposal. Warm includes execution,
mediated RAM-backed I/O where applicable, goldens and prior-result cleanup.
Compile to result combines both phases. Startup, fixture reset and final
teardown are outside guest elapsed. Wider process CPU covers the request.
Lython runs with shared-budget accounting and CPython uses native file APIs.

B1202 isolates the factory change within Lython. The earlier B1200 eager
warm ratio was 3.30–3.47×, while B1203 measures 2.44–2.47×. Nested factories
retain the largest warm gap of the three measured factory workloads.

All 32 worker files, four goldens, both quiet gates and 120 timed batches
verified. Workers joined and the archive verified under the VM lease.

[Timing](summary.json) · [Observations](observations.tgz) ·
[Source and procedure](procedures.tar.xz) ·
[B1202 Lython-only comparison](../2026-11-20-inline-lexical-definitions/README.md) ·
[Earlier B1200 comparison](../2026-11-20-cpython-lexical-class-cells/README.md).
