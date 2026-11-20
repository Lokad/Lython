# B1191 Direct statement emission

Private `2131039a4` emits module, function and imported suites directly
from validated statements. It removes the intermediate lowered statement
tree for those suites, the lambda's synthetic return statement list and
the generic executable statement fallback. Type aliases retain their lazy
expressions and live defining namespace through a specific operation.
Class bodies still execute their lowered statements.

| Workload | Phase | Managed allocation change | Quiet elapsed change | Quiet process CPU change |
| --- | --- | ---: | ---: | ---: |
| Generated validator | Compilation | −2.45% | −2.92% to +1.72% | −2.80% to +2.58% |
| Generated validator | Compile to result | | −1.97% to −0.33% | −2.46% to +2.04% |
| Configuration manifest | Compilation | −1.17% | +0.34% to +0.99% | −2.28% to +0.33% |
| Configuration manifest | Compile to result | | −0.68% to −0.07% | +0.25% to +2.60% |
| Eager annotated rules | Warm | 0.00% | −0.48% to +6.80% | −0.29% to +7.27% |
| File-backed CSV invoice | Warm | 0.00% | −2.94% to −1.04% | −4.54% to −1.30% |

All five measured workloads allocate less during compilation. Warm allocation
and accounted peaks match. The validator's compile-only reference duplicate
spread is 4.63%. Eager warm's reference spread is 5.46%. The manifest's wider
process CPU rises slightly in every compile-to-result pairing while elapsed
falls. The invoice control moves in the other direction. This comparison
kept the candidate private pending a longer comparison of disputed lanes.
[B1192 confirmation and retention](confirmation/README.md).

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC.
The reference is `b418fa7d8`. Each process warmed for at least 10 seconds,
then ran five rotated rounds targeting half-second requests. Ranges cover
the four pairings of duplicate process medians. Warm includes mediated I/O
where used, golden checks and prior-result cleanup. Compile to result adds
compilation and code-owner disposal. Compile-only also includes disposal.

Windows .NET 11 and Ubuntu .NET 10 each passed 5,188 focused Release checks.
Both complete worker images matched five CPython workload goldens and six
independent probes in both execution modes. Shared allowance returned to
baseline in the allocation checks. Both quiet gates and 120 timed batches
passed. All workers joined, the archive was verified under the lease and
fixtures were removed.

[Summary](summary.json) · [Allocations](allocation-summary.json) ·
[Observations](observations.tgz) · [Checks and procedures](checks-and-procedures.tar.xz).
