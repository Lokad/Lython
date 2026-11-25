# B1683 Borrow prepared call operands

The private candidate borrows existing materialized arguments in both
executable drivers. An exact integer call/update borrows its one local
argument and checks live omitted-default eligibility once. Calls that need
the generic binder still publish their evaluated operands before transfer.
Only the runtime changes against `c1984f96b`.

Both frozen images match eight complete CPython 3.13.2 goldens. Their 219
bodies and 2,107 instructions, source spans and retained compilation
admission match. All execution peaks match and every allowance returns to
baseline. Local allocation matches on seven workloads. The shipment median
differs by 224 B on about 1.32 MB. Windows passes 697 focused public checks
on each Release target. Ubuntu .NET 11 passes 697 public and 120 white-box
checks, including call binding, live defaults, refusal and async unwind.

## Quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Four workers warm for five seconds each and run seven rotated batches with
a 0.72-second target. Ranges cover all four pairings of worker medians. Warm
elapsed includes execution, applicable mediated RAM I/O, verification and
prior-result disposal. Time to result adds compilation and compiled-owner
disposal. Wider CPU includes fixture reset and request framing. Startup
stays outside these clocks.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated ordinary definitions | Warm | −2.20 to −0.97% | −2.00 to −0.36% | 0.45 / 0.80% |
| Fully supplied lexical helper | Warm | −1.20 to +0.84% | −0.97 to +0.75% | 1.58 / 0.48% |
| Defaulted rule pairs | Warm | −4.38 to −2.36% | −5.73 to −1.75% | 1.71 / 0.39% |
| Eager annotated rules | Warm | +2.41 to +15.73% | +1.60 to +16.84% | 0.95 / 11.94% |
| Defaulted rule pairs | Compile to result | −5.68 to +4.15% | −5.71 to +5.40% | 4.66 / 5.50% |
| Postponed annotated rules | Warm | −19.36 to −3.34% | −20.19 to −3.80% | 15.31 / 3.96% |
| Quote-heavy invoice | Warm | +1.51 to +2.16% | +0.00 to +2.70% | 0.03 / 0.61% |
| Shipment callback report | `RunAsync` warm | +2.98 to +6.14% | +2.35 to +6.33% | 1.29 / 1.76% |

All 224 batches, both quiet gates, verified worker joins and archival checks
pass. Ordinary and defaulted definitions spend less warm CPU. The easier
lexical control and defaulted time to result cross zero. The annotation
cases have wide same-source spreads. The async shipment control has a
consistent increase across workers, and invoice elapsed is adverse. Keep
this source private. Isolate the exact call/update change from the broader
forwarded-slot handoff before attributing either effect.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal status](quiet-terminal-status.json) ·
[Procedures and source](procedures.tar.xz).
