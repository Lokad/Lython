# B1675 Carry evaluated operands through ordinary definitions

The private prototype carries the evaluated positional integer in its existing
operand through default storage and callable construction. It removes the
ordinary path's eager box and reconstructs the callable's ordinary slot when
storage transfers. The definition's transient slot owns the reservation until
creation succeeds. Default backing and callable admission remain separate.

Both complete images match six CPython 3.13.2 goldens, with the same 118
executable bodies, 1,656 instructions, spans and retained compilation admission.
Windows runs 206 focused public checks per Release target. Ubuntu .NET 11
runs those 206 plus six focused white-box checks. The focused default test
preserves Python identity after a later default suspends or fails, including
negative values and the signed 64-bit minimum. The host closes the file and
restores the shared allowance. Its output matches a separate CPython run.

| Complete script | Allocated per run, shipped to candidate | Admitted peak in both |
| --- | ---: | ---: |
| Generated ordinary definitions | 282,200 → 249,464 B | 971,730 B |
| Defaulted rule pairs | 737,992 → 737,992 B | 2,786,655 B |
| Quote-heavy invoice | 3,281,420 → 3,281,420 B | 9,040,266 B |
| Shipment routing | 1,315,520 → 1,315,464 B | 2,817,644 B |

Ordinary definitions allocate 11.60% less than production `01f1f6118` and
3.15% less than the B1670 pre-slot diagnostic. The other two annotation
controls are unchanged. All six budgets return to baseline. These figures
measure .NET allocation independently of shared admitted capacity.

## Initial quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Two workers per image warmed for five seconds and ran seven rotated batches
with a 0.82-second target. These ranges cover four pairings of worker medians.
Warm elapsed includes applicable mediated RAM-backed I/O, verification and
previous-result disposal. Time to result adds compilation and compiled-owner
disposal. Wider CPU includes fixture reset and request framing. Startup is
outside both clocks.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated ordinary definitions | Warm | −2.16 to −0.43% | −1.98 to +0.58% | 1.23 / 0.54% |
| Defaulted rule pairs | Warm | −3.14 to −0.09% | −4.38 to −1.22% | 0.84 / 2.29% |
| Quote-heavy invoice | Warm | −0.56 to +1.40% | 0.00 to +1.56% | 0.90 / 1.06% |
| Shipment routing | Warm | +0.70 to +3.48% | +0.59 to +3.36% | 0.12 / 2.63% |
| Generated ordinary definitions | Compile to result | −0.53 to +0.91% | +0.03 to +2.22% | 0.12 / 1.33% |

All 140 batches, both quiet gates, image checks, worker joins and archival
verification pass. The ordinary-definition warm elapsed range is favorable and
its CPU crosses zero. Shipment shows adverse elapsed and CPU, with candidate
worker spread limiting precision. The confirmation uses the same frozen images.

## Confirmation and retention

The repeat uses 1.05-second target batches and rotates the workload order.
All 112 batches, image and quiet gates, joins and archive verification pass.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Shipment routing | Warm | −1.72 to +1.86% | −1.62 to +2.86% | 1.66 / 1.95% |
| Generated ordinary definitions | Warm | −4.37 to −2.62% | −4.82 to −2.44% | 1.47 / 0.35% |
| Generated ordinary definitions | Compile to result | −2.36 to +1.58% | −2.37 to +1.57% | 0.19 / 3.84% |
| Quote-heavy invoice | Warm | −1.36 to +0.66% | −1.20 to +0.35% | 0.55 / 1.49% |

Retain the operand transfer for the measured allocation reduction and warm
ordinary-definition gain. The first shipment increase is retained above.
Shipment and ordinary compile-to-result CPU span zero in the confirmation.
Carry both uncertainties into the next shared call-entry comparison.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocations](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal status](quiet-terminal-status.json) ·
[Procedures and checks](procedures.tar.xz) ·
[Confirmation timings](confirmation/quiet-summary.json) ·
[Confirmation observations](confirmation/quiet-observations.tar.xz) ·
[Confirmation terminal status](confirmation/quiet-terminal-status.json) ·
[Confirmation procedures](confirmation/procedures.tar.xz).
