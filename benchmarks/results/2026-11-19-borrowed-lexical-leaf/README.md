# B1115 borrowed lexical leaf calls

The `cabe0e295` prototype runs fully supplied positional leaf calls through
the existing borrowed instruction dispatcher. The calling factory can finish
without allocating an invocation context or attaching the ordinary child
continuation. A child that needs a context transfers its exact instruction
position and funded storage to the current frame driver. Both borrowed and
materialized returns use the existing fixed-unpack destination.

Reference `d0fc1bb93` has identical runtime and test sources to the delivered
`a52ddb876` checkpoint. B1115 ran on the quiet Azure F4as_v7, Ubuntu 24.04.4,
.NET 11 RC3 on November 19, 2026 UTC. Both complete 32-file images passed
the five CPython workload goldens. Each row reports the range of changes
across all four pairings of duplicate-process medians. Negative is lower.

| Workload | Lane | Guest elapsed change | Process CPU change | Local warm allocation change |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Warm | −9.34…−7.58% | −8.46…−7.14% | −26.90% |
| Captured rules | Compile-to-result | −11.39…−9.00% | −12.02…−8.19% | — |
| Ordinary rules | Warm | −6.08…−3.09% | −6.03…−4.71% | −10.29% |
| Annotated rules | Warm | +5.73…+8.60% | +5.49…+8.16% | 0.00% |
| Escaping nonlocal siblings | Warm | +0.92…+2.24% | −0.39…+1.30% | 0.00% |
| CSV invoice | Warm | −2.17…−1.03% | −2.04…−0.79% | +0.00% |

Warm elapsed includes mediated RAM-backed I/O, golden checks and previous
result cleanup. Compile-to-result adds compilation and compiled-owner
disposal. Process CPU includes the worker around the guest boundary. The
local allocation checks kept accounted peaks and returned the borrowed
allowance to baseline in all five cases. Windows .NET 11 and Ubuntu .NET 10
passed 238 focused tests each. The nested child tests exercised live capture
aliases, the resumed instruction after genuine host I/O, cancellation and
budget cleanup.

The prototype stays private. The annotated slowdown exceeds its duplicate
process spread of 0.51% / 2.20%, with CPU moving in the same direction. Its
factory returns a single function and has no nested explicit call. The
added shared dispatch and call-entry work are hypotheses to isolate with a
smaller source pair. The captured and ordinary allocation gains justify
refinement, with the annotated result retained as a control. Test the
refinement against the same complete scripts before promotion.

All 120 timed batches completed in 323.44 seconds. Both quiet gates passed,
workers joined and fixtures were removed. The initial staging attempt stopped
for storage headroom before any collection started. The retry reclaimed a
single B1096 VM replica after proving its core and observation hashes against
the committed archive. The headroom threshold remained the same.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Verified lease receipt](archive-receipt.json) ·
[Checks, source patch and procedures](checks-and-procedures.tar.xz).
