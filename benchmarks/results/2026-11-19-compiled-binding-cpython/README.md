# B1084 compiled definition binding against CPython

Lython `44fe04178` ran against stock CPython 3.13.16 on the quiet Azure
F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-19 UTC. The
32-file worker image matches the B1082 candidate. The verified sources,
fixtures and goldens repeat the five B1078 cases and the B1069 CSV invoice.

| Workload | Input | Compilation only | Warm | Compile-to-result |
| --- | --- | ---: | ---: | ---: |
| Unannotated rule closures | 2,048 inputs / 4,096 definitions | 0.79–0.80× | 3.69–4.00× | 3.60–3.86× |
| Annotated rule closures | 2,048 inputs / 2,048 definitions | 0.80–0.82× | 4.23–4.29× | 4.02–4.11× |
| Captured rule closures | 2,048 inputs / 4,096 returned closures | 0.80–0.81× | 3.95–4.04× | 3.78–3.88× |
| Retained nonlocal siblings | 240 factories / 720 returned closures | 0.82–0.84× | 1.05–1.10× | 0.99–1.03× |
| Expanded forwarding report | 1,024 records / 26,112 B | 0.82–0.84× | 1.71–1.79× | 1.60–1.67× |
| CSV invoice | 4,096 data records / 127,031 B | 0.97–0.99× | 1.09–1.11× | 1.08–1.11× |

Ratios divide Lython elapsed by CPython elapsed across all four pairs of
process medians. Above one means slower in Lython. The unchanged Lython
ordinary-rule processes differ by 6.65% warm and 7.11% compile-to-result.
All other same-engine spreads are at most 4.54% and all process IQRs are
at most 4.59%. The annotated gap is smaller than B1078's 4.74–4.98× warm
and 4.62–4.74× compile-to-result. Forwarding's ratio is higher than its
earlier 1.67–1.75× warm and 1.57–1.65× compile-to-result. The independent
collections do not isolate the effect of an individual code change.

Warm includes mediated RAM-backed I/O, output goldens and previous-result
cleanup. Compile-to-result adds compilation and compiled-owner disposal.
Compilation-only includes compiled-owner disposal. CPython uses native
file I/O. Startup, fixture reset, request framing and final cache teardown
are outside guest elapsed. Process CPU measures a wider boundary.

Both machine gates and all 360 timed batches verified in 419.38 seconds.
Workers joined, guest fixtures were removed and the persistent archive
verified under the lease. The exact Lython image passed the six catalog
goldens before staging. This diagnostic measures elapsed and process CPU.

[Elapsed and CPU observations](summary.json) · [Raw observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) ·
[B1078](../2026-11-19-retained-definitions-cpython/README.md) ·
[B1069](../2026-11-19-retained-trim-cpython/README.md) ·
[B1082](../2026-11-19-compiled-definition-publication/README.md).
