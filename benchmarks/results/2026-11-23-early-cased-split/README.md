# B1537 start the fused cased split at the member lookup

Private `e9a842f66` recognizes an exact string at the first prepared
member instruction. It bypasses publishing the receiver and builtin
method and removes the later cased-split branches from both call
dispatchers. The existing UTF-8 scanner, eager admission and
private-iteration contract stay in place. Custom receivers use their
original lookup and call instructions, with awaited fallback when needed.
The reference is `e4392967b`.

| Complete warm script | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Short Unicode word report | −7.73% to +2.92% | −6.91% to +5.31% | 1.43% / 9.98% |
| Mixed-width Unicode histogram | −6.77% to +14.22% | −7.03% to +12.81% | 2.08% / 20.03% |
| Quote-heavy policy invoice | −4.18% to +1.60% | −2.30% to +2.50% | 5.91% / 0.11% |
| Function-scoped supplied arithmetic | +1.39% to +3.86% | −1.59% to +6.65% | 1.75% / 0.67% |

Keep this prototype private. The target workers diverge and leave the
effect on both text workloads uncertain. The supplied-call control
takes longer with steadier workers and mixed CPU. Local allocation
is flat within 56 bytes per run, admitted peaks match, and all
budgets return to baseline. One refinement worth considering is
keeping the extra opcode selection out of ordinary member loads.

A separate Windows diagnostic disables tiering to inspect full
optimization. Native call code falls from 3,476 to 3,043 B and the
ordinary native-call selector falls from 3,665 to 3,570 B. The native
runner grows from 5,444 to 5,568 B and the shared member loader grows
from 947 to 1,021 B. The main interpreter loop remains at 12,501 B.
This establishes where code moved. The quiet timing used normal
tiering and supplies no causal code-size attribution.

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November
23, 2026 UTC. Ranges span four pairings of worker medians after eight
seconds of warmup and seven rotated batches per worker. Warm includes
mediated RAM-backed I/O, verification and prior-result disposal.
Wider process CPU includes request handling. All 112 batches and both
quiet gates verify. Workers join and fixtures are removed before
the persistent archive is verified.

Both 32-file images match four CPython goldens. Windows .NET 11 and
Ubuntu .NET 10 each pass 234 public and 461 white-box focused tests,
including custom method suspension, cancellation and word ownership.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Identity](identity.json) · [Observations](observations.tar.xz) ·
[Procedure and checks](procedures.tar.xz) · [JIT diagnostic](jit-checks.tar.xz) ·
[Terminal status](terminal-status.json).
