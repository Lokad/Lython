# B1643 Representative scripts against CPython

This collection compares Lython `b9b20aa98` with stock CPython 3.13.16.
The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC.
Each lane uses two warmed workers per engine and seven rotated batches per
worker. Ratios divide Lython by CPython elapsed time. Ranges span the four
pairings of worker medians.

| Script | Boundary | Lython | CPython | Elapsed ratio | Wider CPU ratio | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Generated rules with defaults | Warm | 0.88961–0.90533 ms | 0.68258–0.69674 ms | 1.277–1.326× | 1.288–1.339× | 1.77 / 2.07% |
| Generated rules with defaults | Compilation only | 0.02437–0.02474 ms | 0.04256–0.04315 ms | 0.565–0.581× | 0.559–0.587× | 1.53 / 1.38% |
| Generated rules with defaults | Compile-to-result | 0.93820–0.94499 ms | 0.72759–0.73066 ms | 1.284–1.299× | 1.284–1.300× | 0.72 / 0.42% |
| Configuration manifest | Warm | 0.18590–0.18848 ms | 0.15368–0.15581 ms | 1.193–1.226× | 1.053–1.087× | 1.39 / 1.39% |
| Configuration manifest | Compile-to-result | 0.38295–0.38747 ms | 0.46271–0.46322 ms | 0.827–0.837× | 0.817–0.820× | 1.18 / 0.11% |
| Short Unicode word-count report with a nonlocal helper | Warm | 0.89681–0.92250 ms | 0.93302–0.94764 ms | 0.946–0.989× | 0.943–0.983× | 2.86 / 1.57% |
| Short Unicode word-count report with a nonlocal helper | Compile-to-result | 0.94947–1.03345 ms | 1.07671–1.08030 ms | 0.879–0.960× | 0.863–0.967× | 8.84 / 0.33% |
| Quote-heavy CSV policy invoice | Warm | 6.41158–6.48784 ms | 6.37358–6.39124 ms | 1.003–1.018× | 0.964–1.000× | 1.19 / 0.28% |
| Generated comprehension rules | Compilation only | 1.09478–1.10758 ms | 2.86768–2.89394 ms | 0.378–0.386× | 0.371–0.381× | 1.17 / 0.92% |

Warm elapsed includes applicable RAM-backed I/O, output and file verification,
and disposal of the previous result. Lython uses a mediated host with efficient
ranged reads and append plus a shared memory allowance. CPython uses its file
APIs. Compilation includes compiled-owner disposal. Time to result includes
both phases in one request. Process CPU also includes framing and fixture
reset. The clocks exclude process startup and final teardown.

Defaulted rules have the largest measured gap at 1.277–1.326× warm and
1.284–1.299× through result. The manifest takes 1.193–1.226× warm and
0.827–0.837× through result. The quote-heavy invoice is within 2% for
elapsed. The short report's through-result range includes 8.84% Lython
worker spread. Comparisons across separate collections retain that uncertainty.

The comprehension compiler workload contains 72 lazy rules. Use the
[paired Lython comparison](../2026-11-24-shared-comprehension-facts/README.md)
to attribute the compiler improvement to source changes. The external
comparisons are diagnostic. All 252 batches, both quiet gates, exact worker
and CPython identities, complete goldens, worker joins, fixture cleanup and
the persistent archive verify.

[Summary](summary.json) · [Identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz).
