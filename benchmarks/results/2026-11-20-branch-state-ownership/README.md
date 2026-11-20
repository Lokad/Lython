# B1175 owned branch-state joins

Private `9290558f0` reuses incoming binding maps for one arm or handler
in `if` and `try` analysis. One join mutates the completed left branch
in place and keeps the existing left-hand spans. Diagnostics retain
source order, and branch-only bindings cannot enter the continuation.
The reference is `bbbf360cd`.

| Workload | Boundary | Windows compile allocation | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: | ---: |
| Generated validator | Compilation | −6.77% | −7.42 to +3.19% | −7.01 to +2.51% |
| Generated validator | Compile-to-result | — | −3.35 to +0.34% | −1.42 to +1.72% |
| Configuration manifest | Compilation | −0.22% | −0.27 to +3.67% | −0.49 to +4.80% |
| Eager rules | Compilation | Flat | −0.32 to +1.45% | +0.63 to +3.84% |
| Positional invoice | Warm | — | −0.77 to +1.62% | −0.03 to +2.03% |

Negative values favor the candidate. These are the four pairings of
duplicate process medians on the quiet Azure F4as_v7 with Ubuntu 24.04.4
and .NET 11 RC3 on 2026-11-20 UTC. Compilation includes compiled-owner
disposal. Compile-to-result adds warm execution including mediated I/O,
goldens and previous-result disposal. Process CPU covers the wider invocation
loop. Warm allocation and accounted peaks matched, and borrowed allowance
returned to baseline. Both frozen images matched four CPython goldens.

Windows .NET 11 and Ubuntu .NET 10 each passed 1,660 focused Release checks,
including the join checks and compiler cancellation. The first Ubuntu launch
used an out-of-tree output missing the compiled-pattern fixture. The rerun
verified the fixture bytes and passed. Both quiet machine gates and all
100 timed batches completed. The users joined and the persistent archive
verified under the lease.

The validator's compile elapsed reference and candidate duplicate spreads
are 6.36% and 4.79%. The allocation reduction stands, while CPU and elapsed
remain inconclusive. Eager-rule CPU rises in every pairing. The prototype
stays private pending a bounded check of the continuation that removes
the extra merge result from the remaining loop and match statement paths.

[Timing summary](summary.json) · [Allocation](allocation-summary.json) ·
[Raw observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).
