# Current helper warm and compile-to-result in the same process

B986 measured `bfe076110` / frozen `610d403d` against stock CPython
3.13.16 on the quiet Azure F4as_v7 VM on 2026-11-18 UTC. The .NET 11 RC3
and CPython workers used normal GC and tiering. Both engines matched the
existing file and stdout goldens. Each worker warmed both phases, then
alternated them in five rotated batches. The helper warmed eight seconds
per phase and process. The configuration control warmed three seconds.
Two unmodified processes represent each engine and case.

| Complete script | Engine | Warm | Compile-to-result | Same-process compile-to-result / warm |
| --- | --- | ---: | ---: | ---: |
| Helper normalization | Lython | 1.062–1.105 ms | 1.147–1.186 ms | 1.074–1.081× |
| Helper normalization | CPython | 0.747–0.748 ms | 0.840–0.844 ms | 1.125–1.128× |
| Configuration edit | Lython | 0.0764–0.0778 ms | 0.1353–0.1356 ms | 1.739–1.775× |
| Configuration edit | CPython | 0.0615–0.0620 ms | 0.1220–0.1251 ms | 1.968–2.036× |

For the helper, the elapsed ratios across all engine pairs are **1.42–1.48×**
warm and **1.36–1.41×** compile-to-result. CPython's compile-to-result time
exceeds warm time in both processes. The inversion from B984's separately
warmed phases did not recur. The CPython warm median shifted substantially
from B984's 0.880–0.900 ms. The same-process setup and a later Lython revision
prevent attributing the ratio change to either compiler. Distinct source and
phase observations remain in the benchmark guide.

Warm includes mediated RAM I/O, full output checking and prior-result cleanup.
Compile-to-result adds compilation and compiled-owner disposal. Startup,
fixture reset, protocol and final cache teardown remain outside elapsed.
Wider process CPU supports the positive compile-to-result versus warm direction.
The separate instrumented Python process records 0.705 ms warm guest exec,
0.719 ms fresh guest exec and 0.084 ms fresh compilation. Its inclusive
clocks and natural-GC hooks add work. They cannot correct the main timings,
isolate cleanup, or explain the earlier inversion.

Three jobs completed in 120.32 seconds. Both quiet endpoints passed, all
users joined, and guest fixtures were removed. One exact raw working
replica was retired after checking the committed archive, UID, hashes and
alias count. The local instrumentation preflight originally omitted the
protocol's required verify request. It was corrected before any VM work.
The corrected warm/compile-to-result/warm sequence matched the goldens and
confirmed the same warmed code was reused.

This diagnostic supports investigating execution costs in the helper. It
establishes no new source speedup or dominant runtime stage by itself.
[Separately warmed B984](../2026-11-18-line-count-cpython-checkpoint/README.md) ·
[Earlier same-process protocol](../2026-11-17-helper-phase-attribution/README.md).

[Phase medians](summary.json) · [Raw observations](observations.tgz) ·
[Identity and procedures](procedures.tar.xz)
