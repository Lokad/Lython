# B1407 Factory and word-report costs at the current checkpoint

Profiled the exact B1406 `55424591f` image on November 22, 2026 UTC on
the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The same
two scripts, fixtures and outputs verify against CPython. Each of
the three boundaries uses one worker and a managed JIT map at 199 Hz.
The profiles cover all worker threads, result verification and
prior-result disposal. The text script also includes mediated RAM-backed
I/O. Compile to result includes compilation and compiled-owner cleanup.
[B1406 comparison](../2026-11-22-current-source-cpython/README.md).

| Complete workload | Samples | Callable construction | Admission | Native body loop | Generic binding | Dictionary loop | Casing preparation |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Nonlocal word report, warm | 1,092 | — | 87 | 119 | — | 161 | 121 |
| Postponed factories, warm | 1,090 | 149 | 101 | 125 | — | — | — |
| Postponed factories, compile to result | 1,106 | 180 | 94 | 90 | 34 | — | — |

The table uses nearest resolved managed frames. The callable column
groups the construction methods. A dash leaves a small or irrelevant
group unreported. Inlining and unresolved frames limit attribution.
Of the 101 warm factory reserve samples, 64 resolve to callable
construction and 33 to default storage. The corresponding first-use
counts are 60 and 30 of 94. The older B1399 attribution found a similar
distribution. For the word report, admission includes 38 samples from
casing preparation and 36 from publication of text windows.
[B1399](../2026-11-22-current-runtime-attribution/README.md).

The factory has separate definition and invocation costs. The dominant
observed reserve callers are still real storage owners. Treat their
funding independently from any work removed in the argument binder.
The profiles are a source-level guide and carry no between-image timing
claim.

All 174 profile batches matched the goldens. Both quiet gates passed.
The workers joined and the persistent archive verified before the
guest mount was removed.

[Attribution](attribution.json) · [Exact image](identity.json) ·
[Raw traces](observations.tgz) · [Procedures](procedures.tar.xz).
