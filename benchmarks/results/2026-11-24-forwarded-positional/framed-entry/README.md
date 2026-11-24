# B1624 Use the existing framed call entry for forwarded operands

This refinement keeps native user-function selection in the existing
framed dispatcher. A forwarded argument travels in a managed slot at
that boundary. The separate dispatch method tried in B1623 is gone.
The compiler still removes six instructions from the defaulted-rule
loop, and the eager-rule tape and metadata still match the reference.
Both images pass the same five complete CPython goldens.
[Original trial](../README.md).

## Quiet comparison

The reference is `48790b2c7`, with the same runtime image used in B1623.
The candidate uses the refined framed entry. The Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Two workers
per image each warm for five seconds and supply seven rotated batches.
Ranges span all four pairings of worker medians. Warm elapsed includes
applicable mediated RAM-backed I/O, verification and previous-result
cleanup. Compile to result includes compilation and compiled-owner
disposal. Wider CPU also includes request framing and fixture reset.
Process startup is measured separately in the benchmark guide.

| Complete script and boundary | Elapsed change | Wider CPU change | Elapsed spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Defaulted rules, warm | −6.67 to +3.98% | −8.18 to +4.27% | 4.19 / 6.93% |
| Defaulted rules, compile to result | −4.34 to −1.88% | −4.58 to −3.72% | 1.45 / 1.10% |
| Eager annotated rules, warm | −0.77 to +5.06% | +0.70 to +5.27% | 4.51 / 1.30% |
| Captured rules, warm | −7.64 to −0.54% | −4.81 to −0.33% | 1.54 / 6.05% |
| Short Unicode report, warm | −13.48 to −10.79% | −11.22 to −10.55% | 1.05 / 2.03% |
| Quoted CSV invoice, warm | −0.70 to +9.63% | −3.45 to +11.15% | 0.16 / 10.23% |

Retain the refined implementation. It removes repeated operand
staging, improves the Unicode report and captured rules across both
blocks, and improves defaulted rules through result in this block.
Defaulted warm timing varies by worker. The eager CPU cost persists,
with unclear magnitude and mechanism. The quote control is inconclusive
because one candidate worker moves substantially. Carry both cases
into the next whole-script checkpoint. Allocation and peak admission
match on all five local diagnostics.

Windows and Ubuntu each pass 325 focused .NET 11 Release checks on the
refined source. Both images match CPython 3.13.2 on 15 probes in each
mode. All 168 measured batches and quiet gates pass. Workers join, the
fixture mount is cleaned up and the archive verifies. The first launch
stopped before staging because the VM lacked workspace. The retry
preserved a byte-identical archived catalog through the verified
immutable copy, and the failed conditions remain archived.

[Images](identity.json) · [Timings](quiet-summary.json) ·
[Tape census](call-tape-summary.json) · [Allocation](allocation-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Procedures and patch](procedures.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json).
