# B1411 Exact histogram totals in their existing slots

Compared `55424591f` with `249cf4e8a` on November 22, 2026 UTC on the quiet
Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Focused checks used the same
production source with the boundary regressions at `38ae6465b`. The measured
core is the sole difference in the frozen 32-file images. The verified
runtime and test patch is retained at `1884824e8`. The delivery tree also
includes a separately tested adapter lifetime repair.

Both histogram paths now keep a private running total as an exact integer
in its current slot between inner-loop passes. The next pass can reopen
the total without boxing it. Ordinary object reads publish it through the
existing slot path and preserve alias identity. The same signed fast range
and general-integer fallback apply. Mirrored reporting still publishes each
visible update. Existing cancellation, preflight and cleanup remain active.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed worker spread old / new |
| --- | --- | ---: | ---: | ---: |
| Nonlocal word report | Warm | −12.34 to +0.31% | −10.67 to −0.25% | 7.11 / 6.83% |
| Nonlocal word report | Compile to result | −4.60 to +1.38% | −4.23 to +1.07% | 3.26 / 2.91% |
| Ordinary histogram | Warm | −10.26 to −2.38% | −10.50 to −0.96% | 4.87 / 3.72% |
| Dense Unicode line | Warm | −6.61 to +1.99% | −4.90 to +1.04% | 7.19 / 1.89% |
| Quote-heavy CSV invoice | Warm | −1.21 to +0.67% | −2.70 to +1.52% | 0.55 / 1.34% |
| Fully supplied helper | Warm | −2.38 to +0.06% | −2.02 to −0.48% | 0.14 / 2.35% |

Ranges cover every pairing of two worker medians per source. Workers have
eight seconds of warmup and seven rotating batches per lane. Warm elapsed
includes mediated RAM-backed I/O, verification and prior-result cleanup.
Compile to result adds compilation and compiled-owner cleanup. Process CPU
also covers resets and protocol. Process startup and final teardown sit
outside the elapsed boundaries. Both images match the same five CPython
goldens. This run adds no external timing ratio.

Windows .NET 11 allocation falls from 446,144 to 380,512 bytes per nonlocal
report and from 1,805,328 to 1,739,774 bytes per ordinary histogram. These
are reductions of 14.71% and 3.63%. Admitted peaks match and the shared
allowance returns to baseline on all seven local diagnostics. Retain the
change for the eliminated boxing, the ordinary histogram improvement and
the lower nonlocal warm CPU. The wide nonlocal worker spreads limit the
elapsed conclusion. First use and dense elapsed/CPU span zero.

Windows .NET 11 and Ubuntu .NET 10 each pass 215 focused Release checks.
The added cases verify both histogram forms crossing the fast range,
existing aliases and result funding, through both public execution APIs.
All 168 timed batches pass. Both quiet gates pass and all workers join.
The fixtures are removed and the raw archive verifies under the lease.
The first launch stopped before staging because an old remote archive
copy had already been removed. The successful launch verified that older
campaign's pushed archive and copied its exact reference core to persistent
local storage before retiring only the VM duplicate under the lease.

[Sources and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) · [Observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json).
