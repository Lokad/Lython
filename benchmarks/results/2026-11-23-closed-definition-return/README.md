# B1482 Return closed lexical definitions directly

`9cea1fc0a` retains the production and test sources measured at
`f2ac9fa89`. An exact native positional call that only creates and
returns a lexical definition uses the existing constructor directly
from the consumed operand. The measured factories preserve a fresh
callable and the one by-value capture. They skip the native activation,
argument copy into its workspace and interpreter dispatch. Allocations,
admitted peaks and their reuse all match.

Compared with `64a0f0217`, whose production source matches the frozen
B1481 candidate, on November 23, 2026 UTC on the quiet Azure F4as_v7,
Ubuntu 24.04.4 and .NET 11 RC3. Only the core differs across the two
verified 32-file images. Two workers per source warm for eight seconds
each and supply seven rotated batches. Ranges span pairings of worker
medians. Warm elapsed includes applicable RAM-backed I/O,
verification and prior-result disposal. Compile to result adds
compilation and compiled-owner disposal. Wider CPU includes resets
and protocol.

| Complete script | Phase | Elapsed change | Wider process CPU change | Worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −15.04% to −10.19% | −15.19% to −11.25% | 3.88 / 1.77% |
| Eager nested factories | Warm | −13.84% to −7.37% | −13.69% to −8.12% | 6.62 / 0.83% |
| Factories observing defaults | Warm | −7.58% to −3.59% | −6.22% to −3.12% | 4.27 / 0.05% |
| Fully supplied helper | Warm | −0.57% to +2.45% | −1.03% to +0.17% | 1.59 / 1.42% |
| Quote-heavy CSV invoice | Warm | −2.93% to +2.02% | −1.75% to +1.79% | 3.14 / 1.89% |
| Postponed nested factories | Compile to result | −12.74% to −9.35% | −12.64% to −9.09% | 1.28 / 2.56% |

Retain the direct definition path. All three factory workloads use
less warm elapsed and process CPU. The postponed result also improves
end to end. Both unchanged controls cross zero. Windows Tier 1 still
clears the outer inline buffer before the branch. Its stack subtraction
changes from 504 to 520 bytes, excluding saved registers. The production
change eliminates activation work from the selected body and leaves the
outer CLR stack cost in place.

Windows .NET 11 and Ubuntu .NET 10 each pass 1,310 white-box and 2,649
public focused Release checks, including exact retained aliases, capture
refusal and pending I/O cleanup. The first Windows pass found the wrong
`dotnet` on PATH for the subprocess runner's version check. Prepending
the pinned SDK resolves it with the same filter. Both full images verify
five CPython goldens. Inspection identifies the same two-instruction
outer factory in the three affected scripts. The remaining executable
tapes and operand layouts match.

All 168 timed batches verify in 283 seconds.
Both quiet gates pass, workers join, guest fixtures are removed and
the raw archive verifies under the lease. The first launch found an
already-retired core. The recovered launch stages the same comparison
after byte-verifying two working replicas against their retained Git
archives and freeing only those replicas.

[Timings](summary.json) · [Allocation and admission](allocation-summary-six.json) ·
[Selected bodies](selected-returns.json) · [JIT diagnostic](jit-summary.json) ·
[Identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz).
