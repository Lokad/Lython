# B1006 retained execution transport against CPython

Measured `03bb07cf5` / `d97f6f98` on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3 on 2026-11-18 UTC against stock CPython 3.13.16
with PGO/LTO, the GIL and no JIT. The same two processes per engine
alternated compilation, warm execution and compile-to-result. Five rounds
per process and phase took 203.71 seconds in total. Both quiet
gates, exact file and stdout checks and owned cleanup passed.

| Script | Lython / CPython compilation | Warm | Compile-to-result |
| --- | ---: | ---: | ---: |
| Expanded forwarding report | 0.87–0.89× | 1.98–2.02× | 1.86–1.88× |
| Rule closures | 0.79–0.82× | 4.38–4.51× | 4.23–4.29× |
| Helper text normalization | 1.02–1.03× | 1.40–1.41× | 1.29–1.33× |

Ranges combine the two elapsed medians for each engine. Rule closures and
forwarding still have the largest warmed gap despite quicker compilation.
Helper-text warm IQR reaches 10.08%. Compilation medians for rule closures
have 3.07% Lython control spread. These are diagnostic engine comparisons.
B1004 and B1005 measure the individual source changes in controlled pairs.

Warm includes mediated RAM-backed guest I/O, exact output checks and
prior-result cleanup. Compile-to-result adds compilation and disposal of
its owner. Compilation also includes compiled-owner disposal. Startup,
fixture reset, framing and final cache teardown are outside guest elapsed.
The archived process CPU covers a wider request boundary. CPython calls its
native filesystem APIs and Lython runs through the comparison host.

[Times, CPU and control spreads](summary.json) · [Observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Previous checkpoint](../2026-11-18-direct-defaults-cpython-checkpoint/README.md)
