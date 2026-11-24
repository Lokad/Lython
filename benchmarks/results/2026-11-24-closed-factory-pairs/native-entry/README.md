# B1596 Select direct pairs at native call entry

Private source `67be15bd0` restores both shared call drivers to their
production source and selects the pair before allocating a callee workspace.
The original single-definition constructor and the shared dispatcher return
to 1,134 and 3,784 bytes in the Windows FullOpts listing.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Warm | +2.17 to +3.61% | +2.95 to +3.33% | 0.62 / 0.78% |
| Defaulted rules | Warm | −29.70 to −27.50% | −29.06 to −28.57% | 2.73 / 0.40% |
| Eager annotated rules | Warm | −4.47 to +0.40% | −5.37 to +0.76% | 4.36 / 0.70% |
| Postponed annotated rules | Warm | −0.94 to +2.99% | +0.69 to +4.54% | 0.27 / 3.69% |
| Short Unicode report | Warm | −4.79 to +3.19% | −5.18 to +3.23% | 1.39 / 6.91% |
| Quoted invoice | Warm | −0.32 to +1.68% | −0.11 to +2.12% | 0.20 / 1.80% |
| Defaulted rules | Compile to result | −32.30 to −30.05% | −34.04 to −30.60% | 1.39 / 1.90% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Seven rotated batches follow eight seconds of warmup per worker.
Ranges cover the four pairings of two independent medians per image.
The 32-file images differ by their core DLL. The frozen `04f87924c`
reference is the same as B1594 and B1595. Warm includes applicable mediated
RAM-backed I/O, verification and previous-result disposal. Compile to
result also includes compilation and disposal of the compiled owner.
Wider CPU covers the request. Startup and final teardown are separate.

The instrumented route diagnostic completes 8,192 defaulted pairs with
zero callee workspaces across four invocations. No controls enter the
pair producer. Admitted peaks match across all six scripts. Allocations
match on rules and differ by at most 56 bytes on file controls. Both
platforms pass 211 focused Release checks each. Both images verify all
six goldens and seven independent CPython programs. All 196 timed batches,
quiet gates, worker joins, guest fixture removal and archive verification pass.

Retain this version for the repeated defaulted gain. The short report
no longer has a consistent adverse direction. Captured rules are
consistently slower with low worker spread. Postponed CPU also rises
with elapsed spanning zero. Carry both into the next call/frame comparison.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Route counts](route-counts.json) · [JIT observations](jit-summary.json) ·
[Images](identity.json) · [Observations](observations.tar.xz) ·
[Procedures and source](procedures.tar.xz) · [Terminal status](terminal-status.json) ·
[Reference core](../reference-core.dll.gz) · [B1595](../refinement/README.md) ·
[B1594](../README.md).
