# B1570 Module regex index protocols

Module-level `re` calls now await guest index protocols when executed
with `RunAsync`. Three previously failing module cases complete their
host file reads. The module keeps its existing coercion and pattern
resolution order. Cancellation during a read awaits file closure
before refunding the execution allowance. Module methods reuse shared
callable definitions with runtime provider and cache state per run.

| Complete warm script | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| Module log search | −1.40 to −0.33% | −0.89 to +1.08% |
| Direct compiled-pattern log search | −0.44 to +3.47% | 0.00 to +4.57% |
| Saved compiled-pattern method | −2.89 to +0.86% | −2.52 to 0.00% |
| Quoted invoice | +0.51 to +1.35% | 0.00 to +3.29% |

Keep the correctness change and the shared definitions. Module search
allocates 393,160 fewer bytes per invocation, a 2.68% reduction. The
timing shows slightly lower module-search elapsed with mixed CPU.
The quoted invoice is slightly slower. All four admitted peaks match
and allowances return to baseline. Windows and Ubuntu each pass 557
focused .NET 11 Release checks.

The reference build uses `7f5458107`, with the runtime and tests in
`f524a9c46`. The frozen candidate is `2334fafec`, merged as `8e950721b`. Only the core DLL
differs among 32 verified files. The quiet Azure F4as_v7 runs Ubuntu
24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Two workers per source
warm for eight seconds and contribute seven rotated samples each. Elapsed
includes mediated RAM-file I/O, golden checks and prior-result disposal.
Wider CPU includes fixture reset and request overhead. All 112 batches
match their CPython 3.13.16 goldens. Both quiet gates, worker joins,
fixture cleanup and persistent archive verification pass. An initial
local staging check stopped on an indentation mismatch in the launch
template. The corrected stage verifies both images before collection.

[Timing](summary.json) · [Allocation and admission](allocation-summary.json) ·
[Image identity](identity.json) · [Raw observations](observations.tar.xz) ·
[Procedures, original regressions and exact patch](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
