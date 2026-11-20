# B1176 owned statement joins confirmation

Measured `48c661256` against `bbbf360cd`. The continuation keeps finished
branch results in their existing maps across `if`, `try`, `for`, `while`
and `match` analysis. It removes the second branch snapshot where the
incoming maps can serve that branch and avoids constructing intermediate
join maps. Source-ordered diagnostics, left-hand spans, independent sibling
facts and per-state expression-cache invalidation are preserved.

| Workload | Boundary | Windows compile allocation | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: | ---: |
| Generated validator | Compilation | −6.77% | −8.15 to −0.47% | −10.64 to −1.33% |
| Generated validator | Compile-to-result | — | −2.55 to −0.43% | −1.69 to −0.85% |
| Configuration manifest | Compilation | −0.52% | −1.00 to +3.20% | −0.69 to +3.75% |
| Eager rules | Compilation | −0.56% | −0.56 to +2.44% | −0.40 to +1.18% |
| Positional invoice | Compilation | −1.03% | — | — |

Negative changes favor the candidate. Ranges cover all four pairings of
duplicate worker medians on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3 on 2026-11-20 UTC. This run changed the phase order and allowed
12 seconds of warmup per worker, compared with 10 seconds in B1175.
Compilation includes compiled-owner disposal. Compile-to-result adds complete
execution, mediating I/O, verifying goldens and releasing the previous result.
Process CPU covers the wider invocation loop.

The validator uses less CPU and takes less time in every pairing. Its
7.10% reference duplicate spread limits the precision of compile-only
gains. Compile-to-result duplicate spreads are 1.75% and 0.43%. The
eager CPU range spans zero on the broader implementation, and the
manifest remains mixed. Keep the original B1175 adverse observations.
Warm allocation and accounted peaks match. Allowance returns to baseline.

Both complete frozen images matched four CPython goldens. The final source
passed 594 focused Release checks per platform on Windows .NET 11 and
Ubuntu .NET 10. Both machine gates and all 80 timed batches passed. Workers
joined, temporary fixtures were removed and the archive verified under the
lease. The allocation record combines the before/after runs of the two
source pairs, sharing the same intermediate image and catalog.

[Timing summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz) ·
[Original B1175 conditions](../2026-11-20-branch-state-ownership/README.md).
