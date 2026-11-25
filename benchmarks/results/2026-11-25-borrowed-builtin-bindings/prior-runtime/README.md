# B1715b Borrowed math calls against the prior runtime

The candidate is the exact B1715 image. The reference is `60ecf0d3b`,
from before the short direct-binding and fixed-reservation change.
Each complete 32-file image and the tolerance report's CPython 3.13.2
golden verify. The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3
on November 25, 2026 UTC.

Two workers per source warm for five full-request seconds and
contribute seven rotated batches each. Ranges span all four
worker median pairings. Elapsed includes result verification and
cleanup. Wider CPU also covers reset and request framing.

| Warm phase | Elapsed change | Wider CPU change | Elapsed worker spread reference / candidate |
| --- | ---: | ---: | ---: |
| Three-argument tolerance report, sync | −10.70 to +2.51% | −10.92 to +4.62% | 3.36 / 11.07% |
| Three-argument tolerance report, async | −4.57 to +0.38% | −2.98 to −0.06% | 1.66 / 3.47% |

The warm sync comparison to the prior runtime remains inconclusive.
Async CPU falls slightly across all pairings. Together with B1715,
this supports keeping the shared binder and stack storage. The local
allocation reduction and the CPU improvement relative to the interim
runtime are separate observations. Preserve the adverse interim
results and the candidate's wider sync spread.

All 56 batches and both quiet gates pass. Workers join, guest fixtures
are removed and the archive verifies under the lease.

[B1715](../README.md) · [Image identity](identity.json) ·
[Timing](quiet-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
