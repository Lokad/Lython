# B1595 Isolate paired definition construction

Private `bbd4898d2` moves the pair producer out of the shared completion
wrapper and restores the original single-definition constructor. Both
paths share exact arithmetic and existing admission. The two runtime
drivers still select the pair after preparing the caller's unpack window.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Warm | −1.02 to +3.06% | −0.66 to +1.99% | 0.92 / 3.17% |
| Defaulted rules | Warm | −31.79 to −27.21% | −33.36 to −28.63% | 1.77 / 4.86% |
| Eager annotated rules | Warm | −4.44 to +0.86% | −3.98 to +0.38% | 2.62 / 2.85% |
| Postponed annotated rules | Warm | −0.30 to +6.28% | −0.53 to +4.92% | 5.26 / 1.27% |
| Short Unicode report | Warm | +5.34 to +13.40% | +3.69 to +12.91% | 2.22 / 5.32% |
| Quoted invoice | Warm | −1.94 to +3.62% | −0.60 to +2.82% | 1.11 / 4.51% |
| Defaulted rules | Compile to result | −31.35 to −30.24% | −31.74 to −28.93% | 1.53 / 0.09% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Seven rotated batches follow eight seconds of warmup in each
worker. The ranges cover both independent workers per image. The 32-file
images differ only in the core DLL. The reference is the same frozen
`04f87924c` image as B1594. Warm includes applicable mediated RAM-backed
I/O, verification and previous-result disposal. Compile to result also
includes compiling and disposing the compiled owner. Process CPU covers
the wider request. Startup and final teardown are separate.

The defaulted improvement repeats with matched admitted peaks and the
same allocated bytes on the rule workloads. Both annotated control
ranges span zero. Short text is slower on elapsed and wider CPU again.
It still uses the original `words_from_line` workspace and never uses
the direct pair. The Windows FullOpts single constructor returns to
1,134 bytes, while the shared `TryExecuteNativeCall` remains 3,902 bytes
against 3,784 in the reference. The code size comparison establishes a
remaining difference that can be isolated. It does not explain the
control's elapsed penalty. Keep this refinement private while moving
selection out of both shared callers.

Both platforms pass 59 white-box and 152 public focused Release checks,
including cancellation during the second default admission. A separate
instrumented core reports all 8,192 pair completions and zero defaulted
workspaces across four invocations. It verifies the ordinary controls.
Seven independent boundary programs agree with CPython 3.13.2. Both
images verify six script goldens. All 196 batches, quiet gates, worker
joins, guest fixture removal and persistent archive verification pass.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Route counts](route-counts.json) · [JIT observations](jit-summary.json) ·
[Images](identity.json) · [Observations](observations.tar.xz) ·
[Procedures and source](procedures.tar.xz) · [Terminal status](terminal-status.json) ·
[Reference core](../reference-core.dll.gz) · [Initial comparison](../README.md).
