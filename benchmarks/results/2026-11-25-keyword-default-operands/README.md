# B1681 Keep evaluated singleton keyword defaults in operand slots

The prototype keeps a singleton keyword integer private through definition
evaluation, construction and exact arithmetic. Binding that exposes the
value and reading `__kwdefaults__` publish its canonical box in the owner.
Replacing an unobserved slot refunds its backing. Replacing a published
scalar keeps its charge for aliases while refunding the unused backing.
The two definition admission points remain in their original order.

The reference is `a39d00228`. Both frozen 32-file images match seven CPython
3.13.2 goldens, 216 bodies, 2,075 instructions, spans and retained compiler
admission. Windows .NET 11 passes 493 focused public checks. Ubuntu .NET 11
passes 493 public and 37 white-box checks. The added regressions cover
canonical identity for negative and wide machine integers and allowance
retention after an observed default is replaced. A separate CPython run
produces the same identity transcript.

| Complete script | Reference allocation | Candidate allocation | Admitted peak in both |
| --- | ---: | ---: | ---: |
| Generated ordinary definitions | 249,464 B | 212,648 B | 971,730 B |
| Generated default-free definitions | 197,968 B | 210,264 B | 249,652 B |
| Defaulted rule pairs | 737,992 B | 770,768 B | 2,786,655 B |
| Eager annotated rules | 325,016 B | 275,872 B | 2,444,081 B |
| Postponed annotated rules | 570,896 B | 603,672 B | 2,902,945 B |
| Quote-heavy invoice | 3,281,470 B | 3,281,486 B | 9,040,266 B |
| Shipment callback report | 1,315,464 B | 1,315,488 B | 2,817,644 B |

Ordinary and eager definitions allocate 14.76% and 15.12% less. The wider
callable increases allocation in the default-free, defaulted and postponed
controls. All shared allowances restore their baseline.

## First quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Two workers per image warmed for five seconds and ran seven rotated batches
with a 0.74-second target. Ranges span four worker-median pairings. Warm
execution includes applicable mediated RAM-backed I/O, output checks and
previous-result disposal. Wider CPU includes fixture reset and request
framing. Compilation and startup are outside these clocks.

| Workload | Warm elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | ---: | ---: | ---: |
| Generated ordinary definitions | +0.51 to +1.28% | −0.18 to +2.38% | 0.04 / 0.73% |
| Generated default-free definitions | −0.15 to +0.90% | −0.92 to +1.26% | 0.74 / 0.31% |
| Defaulted rule pairs | +2.02 to +7.39% | +1.06 to +7.57% | 0.28 / 4.96% |
| Eager annotated rules | −8.13 to −3.55% | −7.80 to −3.93% | 3.35 / 1.59% |
| Postponed annotated rules | −4.88 to +2.99% | −4.64 to +3.76% | 5.81 / 2.33% |
| Quote-heavy invoice | −1.14 to +0.65% | −0.86 to +0.97% | 1.16 / 0.64% |

All 168 batches, both quiet gates, worker joins and verified archival pass.
Eager rules use less CPU. Defaulted rules spend more, with candidate worker
variation limiting the size of that increase. Ordinary CPU spans zero as
elapsed rises. Keep this wider callable private. The refinement retains the
existing slot ownership tag through callable construction and removes both
separate flags to recover the physical layout cost.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocations](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal status](quiet-terminal-status.json) ·
[Procedures and source](procedures.tar.xz).
