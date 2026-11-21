# B1284 compiled call metadata

The compiled code object holds the checked operand and pattern storage
extents. Each callable reads these extents directly. The parameter layout
owns the ordinary positional-binding eligibility flag. This removes two
per-callable flag fields, a nullable capacity field and duplicate
positional checks before borrowing operands.

Reference `47fc47d91` uses the exact production-equivalent image from
`a887d3e8a`. The candidate is `4dbd1b94c`. The collection ran November 21,
2026 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
Two processes per source warmed for at least eight seconds and
contributed seven rotated batches per lane. Ranges compare the four
pairings of process medians. Warm elapsed covers mediated RAM-backed
I/O, goldens and prior-result disposal. Compilation covers compilation
and code-owner disposal. Time to result includes both costs. Process
CPU covers the full request. Startup and final teardown are separate.

| Complete script | Boundary | Elapsed change | CPU change | Local warm CLR allocation |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −5.04% to −2.81% | −4.20% to −1.77% | −5.57% |
| Postponed nested factories | Compile to result | −7.69% to +2.50% | −7.33% to +2.81% | — |
| Eager nested factories | Warm | −1.55% to +1.79% | −0.32% to +2.10% | −5.07% |
| Fully supplied helper | Warm | −2.53% to +1.13% | −1.69% to +0.68% | −0.01% |
| Keyword dataclasses | Compile | −1.96% to +1.20% | −1.66% to +0.94% | — |

The local seven-script allocation diagnostic returns every execution
allowance to baseline. Accounted execution and compilation estimates
match. Compilation of the keyword dataclass fixture allocates 408 more
CLR bytes in the local diagnostic. The isolated source comparison,
before the first-write override change was integrated, shows factory
allocation reductions of 3.52–5.14%. The table uses the final source pair.

Both complete 32-file images reproduce seven local goldens and the
four goldens in the exact quiet catalog. Windows .NET 11 and Ubuntu
.NET 10 each pass 116 white-box and 388 public focused checks in the
measured revision. The focused cases cover positional binding, live
defaults, generators, pattern storage and retained wrapper metadata.

Retain shared call metadata. Both postponed-factory workers improve warm elapsed by 2.81–5.04% across pairings and process CPU by 1.77–4.20%. Local CLR allocation declines 3.72–5.57% across the three factory scripts. Eager nested factories, fully supplied calls and keyword dataclass compilation span zero on elapsed and CPU. Postponed time to result also spans zero, with larger unchanged control spreads. The simplification removes duplicate per-callable facts and adds a small amount of code-owned compile storage.

All 140 timed batches passed. The workers joined, RAM fixtures were
removed and the persistent observations verified under the lease.

[Timing](summary.json) · [Execution allocation](allocation-summary.json)
· [Compilation allocation](compile-allocation-summary.json)
· [Raw observations](observations.tgz)
· [Source, checks and procedures](checks-and-procedures.tar.xz).
