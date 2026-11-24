# B1598 Complete a private closure call at its definition

Private source `689ad2b7c` prepares the immediate invocation of a
zero-argument lexical definition in a terminal callable pair. It leaves the
outer factory workspace in place. The native definition boundary verifies a
machine integer and runs the inner exact arithmetic under the usual nested
stack admission. The original tape handles observable bindings, dynamic
operands and suspension.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Captured rules | Warm | −17.76 to −14.79% | −18.32 to −14.02% | 2.80 / 0.79% |
| Defaulted rules | Warm | −1.52 to +0.76% | −2.29 to −1.19% | 1.89 / 0.42% |
| Eager annotated rules | Warm | +3.81 to +15.77% | +3.95 to +16.34% | 1.03 / 10.38% |
| Postponed annotated rules | Warm | +2.32 to +5.72% | +0.67 to +4.60% | 1.54 / 1.75% |
| Short Unicode report | Warm | +0.26 to +2.35% | 0.00 to +3.17% | 1.65 / 0.43% |
| Quoted invoice | Warm | −3.99 to +1.25% | −1.92 to +3.32% | 3.33 / 2.06% |
| Captured rules | Compile to result | −19.08 to −12.77% | −19.24 to −12.75% | 2.93 / 4.73% |

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. Seven rotated batches follow eight seconds of warmup per
worker. Ranges cover all four pairings of independent worker medians. The
32-file images differ only in the core DLL. Reference runtime source
matches `722b49f2d`. Warm includes applicable mediated RAM-backed I/O,
verification and previous-result disposal. Compile to result also includes
compilation and compiled-owner disposal. Startup and teardown are separate.

The instrumented image completes 8,192 private calls across four script
invocations with no intermediate workspaces. The outer factory retains
its 8,192 workspaces. The other five cases never enter the new route.
The captured script allocates 1,125,720 instead of 1,338,712 CLR bytes
per run, a 15.91% reduction. Peak admission falls from 2,016,607 to
1,557,855 bytes, a reduction of 448 KiB. Every run returns to the
shared budget baseline. Admitted peaks match on the other cases.

Both platforms pass 98 focused Release cases. Tests-only `b9d2f073f`
adds four passing checks per platform for refusal and cancellation during
the escaping lambda's admission. Both images match four independent
CPython programs and verify the six script goldens. The 196 timed batches,
worker joins, fixture removal, quiet gates and archive verification pass.

Keep this revision private. The elapsed and CPU improvement on captured
rules accompanies real storage removal. Both annotated controls take
longer, with 10.38% worker spread on eager. Short text also moves adversely.
Test whether the added operand selector in the shared arithmetic evaluator
accounts for the annotated costs. Keep the original observations.

[Timings](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Route counts](route-counts.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures and source](procedures.tar.xz) ·
[Storage custody](storage-custody.json) · [Terminal status](terminal-status.json) ·
[CPython checkpoint](../2026-11-24-closed-pairs-cpython/README.md).
