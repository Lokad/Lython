# B1146 and B1147 live keyword-default lookup

Compared `c76c59159` and prototype `a7b43ff7c` on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 19, 2026 UTC. The runtime
change landed at `684130bc6` alongside an independent regex correction.
Both complete 32-file images matched six CPython goldens before staging.

The compiler records each original keyword default's position in the
shared key layout. The binder checks the current dictionary and key at
that position, then reads the current value. A live value update can
reuse the slot. Removed keys, different layouts and custom key behavior
use the general lookup. Binding still reads defaults after argument
evaluation and reentrant admission.

| Run and workload | Phase | Elapsed change | Process CPU change | Reference / candidate elapsed spread |
| --- | --- | ---: | ---: | ---: |
| B1146 ordinary rules | Warm | −3.80% to −1.45% | −4.78% to −1.53% | 0.61 / 1.83% |
| B1146 annotated rules | Warm | −6.14% to −0.66% | −4.76% to −1.23% | 4.16 / 1.61% |
| B1146 reused defaults | Warm | −9.51% to −3.17% | −8.39% to −1.85% | 3.93 / 2.95% |
| B1146 captured rules | Warm control | −2.79% to −0.05% | −2.42% to +1.01% | 1.49 / 1.30% |
| B1146 CSV invoice | Warm control | +0.23% to +4.30% | −0.69% to +4.72% | 1.36 / 2.66% |
| B1146 annotated rules | Compile-to-result | −6.16% to −1.75% | −6.38% to −2.57% | 2.31 / 2.35% |
| B1147 CSV invoice | Warm control | +1.24% to +3.24% | 0.00% to +2.67% | 0.37 / 1.60% |
| B1147 annotated rules | Warm | −6.78% to −2.20% | −7.02% to −1.43% | 0.48 / 4.40% |
| B1147 reused defaults | Warm | −9.86% to −4.02% | −9.77% to −5.66% | 4.50 / 1.89% |

Ranges cover all four pairs of duplicate process medians. B1147 repeats
three lanes with identical images, verified protocol and reversed worker
order for each case. Annotated rules and reused defaults improve again
with lower CPU. Duplicate medians limit precision. The invoice has no
keyword defaults and is slower in both runs. The comparison leaves its
cause unresolved. Retain the rule gains and carry the invoice regression
into the next call-path checkpoint.

Warm allocation is unchanged for rule scripts, reused defaults and invoice.
The validator control allocates 56 additional bytes. Compilation allocates
24 extra bytes for ordinary rules and eight extra bytes each for annotated
rules and reused defaults. All measured peaks stayed equal and budgets
returned to baseline. Each platform passed 363 focused public checks and
365 white-box checks. The shared-key alias, mutation, deletion and
reinsertion case also matched CPython.

All 180 timed batches passed over 324.63 and 166.48 seconds. Both quiet
gates passed in each collection. Workers joined, fixtures were removed
and the VM verified each persistent archive under its lease. Guest warm
time includes mediated I/O, golden checks and previous-result cleanup.
Compile-to-result also measures compilation and compiled-owner disposal.
Startup, fixture reset and final teardown are outside guest elapsed.
Process CPU covers the wider request.

[B1146 summary](summary.json) · [B1146 observations](observations.tgz) ·
[B1147 summary](confirmation/summary.json) · [B1147 observations](confirmation/observations.tgz) ·
[Checks and procedures](checks-and-procedures.tar.xz).
