# B1078 retained rule construction against CPython

Lython `eb444b988` ran against stock CPython 3.13.16 on the quiet Azure
F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3, on 2026-11-19 UTC. The complete
32-file Lython image is the source-equivalent B1076 candidate. The three
rule scripts and forwarding have the same sources, fixtures and stdout
goldens as B1057. The retained siblings repeat the B1069 case.

| Workload | Input | Compilation only | Warm | Compile-to-result |
| --- | --- | ---: | ---: | ---: |
| Unannotated rule closures | 2,048 inputs / 4,096 definitions | 0.78–0.79× | 4.03–4.07× | 3.89–3.96× |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 0.79–0.82× | 4.74–4.98× | 4.62–4.74× |
| Captured rule closures | 2,048 inputs / 4,096 returned closures | 0.79× | 3.98–4.14× | 3.83–4.00× |
| Retained nonlocal siblings | 240 factories / 720 returned closures | 0.82–0.84× | 1.06–1.09× | 1.00–1.02× |
| Expanded forwarding report | 1,024 records / 26,112 B | 0.82× | 1.67–1.75× | 1.57–1.65× |

Ratios divide Lython elapsed by CPython elapsed across all four pairs of
process medians. Above one means slower in Lython. Same-engine spread is
at most 3.25%, and process IQR is at most 3.30%. The rules are the widest
remaining warm gaps here. Compilation by itself takes less time in
Lython on all five scripts. These comparisons establish the approximate
gap. The paired Lython measurements establish the effects of individual
changes.

Warm includes mediated RAM-backed I/O, output goldens and previous-result
cleanup. Compilation includes compiled-owner disposal. Compile-to-result
includes compilation, execution and their cleanup. CPython uses native
file I/O. Startup, fixture reset, request framing and final cache teardown
are outside guest elapsed. Process CPU measures a wider boundary.

Both machine gates and all 300 timed batches verified in 350.28 seconds.
Workers joined, guest fixtures were removed and the persistent archive
verified under the lease. The five catalog goldens matched through the
existing verifier before staging. The archive includes a separate local
432-byte lexical-frame allocation probe, which does not measure guest
latency or predict achieved savings.

[Elapsed and CPU observations](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) ·
[B1057](../2026-11-19-retained-capture-cpython/README.md) ·
[B1069](../2026-11-19-retained-trim-cpython/README.md) ·
[B1076](../2026-11-19-prepared-dispatch-confirmation/README.md).
