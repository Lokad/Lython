# B1659 Return existing operands without building a callee workspace

Both executable drivers use the existing direct return path for stable
object aliases and literals. The previous path checked that the selected
operand was a machine integer before returning it. Other values caused
the call to create and fill a workspace even when the selected arm only
returned a bound value. A single unconditional return also qualifies.
Arithmetic still requires machine integers, and a guest truth operation
still takes the normal call path. Return publication, cancellation, stack
admission and live default binding keep their existing semantics.

The timing pair uses `694c1645b` with the exact recorded source patch on
the candidate. The capture-rollback correction landed separately. Focused
Release checks apply the same patch on top of `9146b47a0` and pass all 522
checks on each platform and target. The frozen 32-file images verify
four complete CPython 3.13.2 workload goldens. The tape check covers 23
code objects and 593 instructions across five sources. Emitted instructions,
spans and retained admission match. The two routing helpers were already
eligible for exact-value returns. The identity and literal examples
gain eligibility without tape changes.

The shipment report routes 2,048 CSV records through policy helpers and
counts a returned alias with Python identity. It returns dictionaries or
strings already owned by the current execution. In the Windows diagnostic,
its managed allocation varies by 212 B around 1.075 MB per invocation.
All peak admission values match and all allowances return to baseline.

## Quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC.
Ranges use the four pairings of process medians. Four workers each warm for
five seconds and supply seven rotated batches. Warm time includes mediated
RAM I/O, output and file verification and previous-result disposal.
Compile to result also includes compilation and compiled-owner disposal.
Wider CPU includes fixture reset and protocol framing. Process startup is
outside the clocks.

| Complete script | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Shipment routing report | Warm | −15.39 to −14.17% | −15.51 to −15.16% | 0.83 / 0.61% |
| Defaulted rules | Compile to result | −4.04 to +1.97% | −3.55 to +1.44% | 4.86 / 1.34% |
| Quote-heavy invoice | Warm | +1.55 to +2.81% | +0.54 to +2.47% | 0.28 / 0.96% |
| Shipment routing report | Compile to result | −14.15 to −11.18% | −14.22 to −10.71% | 0.20 / 3.25% |
| Defaulted rules | Warm | −7.53 to +1.79% | −6.49 to +1.35% | 5.76 / 4.08% |
| Configuration manifest | Compile to result | −1.08 to +1.55% | −0.53 to +2.71% | 0.67 / 1.98% |

The shipment report improves in both phases. Defaulted rules and the
manifest remain inconclusive. The invoice's adverse result motivates a
same-image repeat with longer batches. That repeat also covers the
asynchronous shipment report and defaulted-rule time to result.

## Async report and longer control batches

The same images use a 0.68-second batch target for this block. The initial
block used 0.52 seconds.

| Complete script | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Shipment routing report | Warm async | −13.47 to −12.89% | −13.42 to −11.53% | 0.34 / 0.34% |
| Quote-heavy invoice | Warm | −0.32 to +0.95% | −0.54 to +0.39% | 0.64 / 0.62% |
| Defaulted rules | Compile to result | +1.34 to +2.18% | +1.39 to +1.89% | 0.13 / 0.70% |

The invoice slowdown does not repeat. Defaulted-rule time to result
increases in the repeat. Keep that narrower regression visible beside
the inconclusive initial block. It is measured against the pair-owned
default reference and does not resolve the earlier pair-ownership debt.
Retain the direct return path for the measured shipment improvement and
revisit the defaulted-rule regression at the next affected checkpoint.

Both sets of quiet gates, all 252 batches, worker joins, fixture cleanup
and verified archival pass. The procedures record the disk preflight and
the redundant binary links and unpacked copies retired only after local
and archived copies were verified.

[Identity](identity.json) · [Tape](tape-summary.json) ·
[Allocation](allocation-summary.json) · [Initial timings](quiet-summary.json) ·
[Initial observations](quiet-observations.tar.xz) · [Initial terminal receipt](quiet-terminal-status.json) ·
[Longer timings](confirmation/quiet-summary.json) ·
[Longer observations](confirmation/quiet-observations.tar.xz) ·
[Longer terminal receipt](confirmation/quiet-terminal-status.json) ·
[Procedures](procedures.tar.xz).
