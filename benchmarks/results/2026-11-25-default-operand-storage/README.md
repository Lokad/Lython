# B1670 Keep singleton positional integers in their default slot

Computed singleton positional defaults can retain a private integer in the
callable's existing operand representation. Closed arithmetic reads the slot
directly. Binding that can expose the value and `__defaults__` both publish
the value in the owner before returning an alias. The general frame keeps its
existing private-default retirement marker. Both admission points and the
same committed allowance cover the callable and its default storage.

The reference is the exact production `a45b6da7f` image from B1669. Both
frozen 32-file images match five complete CPython 3.13.2 goldens. Windows
and Ubuntu each pass 377 focused checks per Release target. The repeated
default result, canonical metadata alias, replacement, late definition failure,
cancellation and allowance restoration are covered.

| Complete script | Reference allocation | Candidate allocation | Retained execution peak |
| --- | ---: | ---: | ---: |
| Defaulted rule pairs | 803,512 B | 737,992 B | 2,786,655 B in both |
| Eager annotated rules | 357,768 B | 325,016 B | 2,444,081 B in both |
| Postponed annotated rules | 570,880 B | 570,896 B | 2,902,945 B in both |
| Generated ordinary definitions | 257,592 B | 282,200 B | 971,730 B in both |
| Quote-heavy invoice | 3,281,438 B | 3,281,420 B | 9,040,266 B in both |

The Windows Release diagnostic reduces allocation 8.15% for defaulted rules
and 9.15% for eager rules. It raises the ordinary-definition control's
allocation 9.55%. The wider slot adds one scalar word per ordinary callable.
Peaks match and all allowances return to baseline after cleanup.

## Initial quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Four workers warm for five seconds each and supply seven rotated batches
against a 0.65-second target. Ranges cover the four pairings of worker
medians. Warm elapsed includes mediated RAM-backed I/O where applicable,
output checks and previous-result disposal. Time to result also includes
compilation and compiled-owner disposal. Wider CPU covers fixture reset
and request framing. Process startup is outside these clocks.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Defaulted rule pairs | Warm | −10.12 to −7.04% | −10.12 to −7.87% | 2.15 / 1.26% |
| Eager annotated rules | Warm | −5.60 to +0.93% | −6.51 to +0.49% | 3.38 / 3.42% |
| Generated ordinary definitions | Warm | +1.20 to +2.75% | +1.45 to +3.02% | 0.74 / 0.78% |
| Quote-heavy invoice | Warm | −1.06 to +0.99% | −0.41 to +1.54% | 0.89 / 1.17% |
| Defaulted rule pairs | Compile to result | −9.67 to −0.42% | −9.46 to −1.29% | 3.98 / 6.03% |
| Postponed annotated rules | Warm | −8.03 to −1.41% | −6.53 to −1.74% | 5.55 / 1.56% |

The defaulted report takes less CPU warm and through result. Time-to-result
spread limits the size of that gain. Ordinary definitions use more CPU, and
the eager and invoice ranges span zero. Both quiet gates, all 168 timed
batches, worker joins and persistent output verification pass.

## Longer confirmation

The same images repeat defaulted warm and time-to-result execution, the
ordinary-definition control and eager rules with a 0.87-second target.
Both quiet gates and all 112 batches verify after cleanup.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Defaulted rule pairs | Warm | −13.45 to −7.13% | −12.73 to −7.22% | 6.90 / 0.37% |
| Generated ordinary definitions | Warm | +2.40 to +4.62% | +1.91 to +4.47% | 0.36 / 1.81% |
| Defaulted rule pairs | Compile to result | −5.43 to −2.82% | −5.36 to −2.35% | 1.79 / 0.96% |
| Eager annotated rules | Warm | −5.69 to +3.01% | −4.84 to +3.13% | 0.73 / 8.43% |

Retain the slot. It eliminates eager boxing for direct arithmetic while
preserving one default owner and canonical identity. Both runs establish a
defaulted-rule gain and an ordinary-definition cost. The latter allocation
and CPU rise remain delivery debt for the next callable-layout change.
The eager elapsed and CPU remain inconclusive. The postponed control has
one favorable block and unchanged allocation.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Terminal status](quiet-terminal-status.json) · [Procedures](procedures.tar.xz) ·
[Confirmation timings](confirmation/quiet-summary.json) ·
[Confirmation observations](confirmation/quiet-observations.tar.xz) ·
[Confirmation terminal status](confirmation/quiet-terminal-status.json) ·
[Confirmation procedures](confirmation/procedures.tar.xz).
