# B1091 combined runtime checkpoint against CPython

Lython `06b40a712` ran against stock CPython 3.13.16 on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC. The full
32-file image includes the retained branch-target, function-construction
and inline-keyword changes. Five workloads repeat B1084, and the inline
report with dynamic keys is the validated B1090 workload.

| Workload | Input | Compilation only | Warm | Compile-to-result |
| --- | --- | ---: | ---: | ---: |
| Unannotated rule closures | 2,048 inputs / 4,096 definitions | 0.77–0.79× | 3.72–3.91× | 3.62–3.77× |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 0.80–0.81× | 4.06–4.25× | 3.99–4.08× |
| Captured rule closures | 2,048 inputs / 4,096 returned closures | 0.80× | 3.87–4.04× | 3.73–3.87× |
| Inline forwarding with dynamic keys | 1,024 records / 26,112 B | 0.89–0.90× | 1.64–1.70× | 1.55–1.59× |
| Expanded forwarding through a named dictionary | 1,024 records / 26,112 B | 0.83–0.84× | 1.70–1.73× | 1.60–1.62× |
| CSV invoice | 4,096 records / 127,031 B | 0.96–0.98× | 1.10–1.13× | 1.08–1.10× |

Ratios divide Lython elapsed by CPython elapsed across all four pairs of
process medians. Above one means slower in Lython. Same-source spreads
are at most 4.43% and process IQRs are at most 3.27%. Rule workloads have
the largest warm gaps in this set. Each forwarding row uses its own
equivalent source under both engines. The B1090 source pair measures
the isolated Lython change on each report.

Warm includes mediated RAM-backed I/O, stdout and file goldens, and
prior-result cleanup. CPython uses native file I/O. Compile-to-result adds
compilation and compiled-owner disposal. Compile-only includes compiled-owner
disposal. Startup, fixture reset, request framing and final cache teardown
are outside guest elapsed. Process CPU measures a wider boundary.

Both quiet gates and 360 batches verified in 418.59 seconds. Workers joined,
guest fixtures were removed and the persistent archive verified under the
lease. The exact image passed all six independent CPython catalog goldens
before staging. This collection measures elapsed and process CPU.

[Elapsed and CPU](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) ·
[B1084](../2026-11-19-compiled-binding-cpython/README.md) ·
[B1090](../2026-11-19-private-keyword-mapping/README.md).
