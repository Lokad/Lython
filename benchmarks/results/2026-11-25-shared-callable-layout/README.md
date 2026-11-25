# B1672 Share the parameter layout through executable code

The executable body keeps the parameter layout computed during compilation.
Function descriptors and callable instances derive it from that code.
This removes a reference from each instance and the duplicate constructor
argument. Standalone generated binders continue to own their own layout.

The reference is the B1670 image whose production source matches
`01f1f6118`. Both 32-file images match five complete CPython 3.13.2 goldens.
Their 114 executable bodies and 1,488 instructions, source spans and retained
compilation admission match. Windows and Ubuntu each pass 3,176 focused
checks on each Release target, including ordinary functions, generators,
stack admission, refusal and binder cleanup.

| Complete script | Reference allocation | Candidate allocation | Retained execution peak |
| --- | ---: | ---: | ---: |
| Defaulted rule pairs | 737,992 B | 705,216 B | 2,786,655 B in both |
| Eager annotated rules | 325,016 B | 308,624 B | 2,444,081 B in both |
| Postponed annotated rules | 570,896 B | 538,120 B | 2,902,945 B in both |
| Generated ordinary definitions | 282,200 B | 269,896 B | 971,730 B in both |
| Quote-heavy invoice | 3,281,602 B | 3,281,398 B | 9,040,266 B in both |

The Windows .NET 11 allocation diagnostic recovers 12,304 B of the 24,608 B
ordinary-definition increase in B1670. The defaulted report saves another
32,776 B. Peaks match and every allowance returns to baseline.

## Initial quiet comparison

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. Each of four workers warms for five seconds and supplies seven
rotated batches with a 0.65-second target. Ranges cover the four pairings of
worker medians. Elapsed includes applicable mediated RAM-backed I/O, output
checks and previous-result disposal. Time to result also includes compilation
and disposal of compiled state. Wider CPU covers fixture reset and request
framing. Process startup is outside the clocks.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Defaulted rule pairs | Warm | −1.54 to +0.38% | −0.85 to +1.54% | 1.48 / 0.46% |
| Eager annotated rules | Warm | −1.35 to +4.97% | −2.85 to +4.00% | 0.71 / 5.65% |
| Generated ordinary definitions | Warm | −1.55 to +1.43% | −1.55 to +0.89% | 1.48 / 1.53% |
| Quote-heavy invoice | Warm | +1.33 to +2.49% | +0.98 to +1.44% | 0.84 / 0.30% |
| Defaulted rule pairs | Compile to result | −0.06 to +3.71% | −0.23 to +3.17% | 0.48 / 3.27% |
| Postponed annotated rules | Warm | −9.37 to +2.90% | −9.78 to +3.22% | 3.58 / 9.62% |

The invoice has a small adverse range in elapsed and CPU. The rule phases
and ordinary-control timing span zero. Eager and postponed worker variation
limits their interpretation. Both quiet gates, 168 batches, worker joins,
fixture cleanup and the persistent archive verify.

## Longer confirmation

The same images repeat three lanes with a 0.87-second target. Both quiet
gates, 84 batches, worker joins, cleanup and the persistent archive verify.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Quote-heavy invoice | Warm | −1.59 to +0.40% | −1.05 to 0.00% | 1.19 / 0.82% |
| Defaulted rule pairs | Compile to result | −4.76 to −3.43% | −5.72 to −3.83% | 1.20 / 0.19% |
| Generated ordinary definitions | Warm | +1.20 to +2.76% | +1.14 to +4.25% | 0.60 / 0.93% |

The invoice's adverse initial range does not repeat. Defaulted rules use
less CPU through result in this block. The ordinary-definition control
uses more CPU despite recovering half its allocation increase in B1670.
Keep the prototype private because the same control already slowed under
B1670. Revisit the compiled layout as part of a binding change that can
also reduce repeated layout reads and remove their forwarding accessors.
The combined cost of B1670 and B1672 has not been timed directly.

[Identity](identity.json) · [Allocation](allocation-summary.json) ·
[Tapes](tape-summary.json) · [Timings](quiet-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal status](quiet-terminal-status.json) ·
[Procedures](procedures.tar.xz) · [Confirmation timings](confirmation/quiet-summary.json) ·
[Confirmation observations](confirmation/quiet-observations.tar.xz) ·
[Confirmation terminal status](confirmation/quiet-terminal-status.json) ·
[Confirmation procedures](confirmation/procedures.tar.xz).
