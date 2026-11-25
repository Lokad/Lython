# B1673 Read the shared callable layout once per binding phase

This private refinement keeps the parameter layout on the executable body,
as in B1672. Positional supply, default omission and each binding phase
hold the immutable layout locally. Live arguments and defaults keep their
existing owners through guest callbacks and suspensions.

The paired reference uses the committed `01f1f6118` runtime from B1670.
Both 32-file images match five complete CPython 3.13.2 goldens. The 114
executable bodies and 1,488 instructions, spans and retained compilation
admission match. Windows and Ubuntu pass 3,176 focused .NET 11 Release
checks each. The allocation diagnostic recovers 12,304 B in the ordinary
control and saves 32,776 B on defaulted rules. All admitted peaks match,
and each owned allowance returns to baseline.

## Quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. Two workers per source
warmed for five seconds and supplied seven rotated batches with a 0.82-second
target. Ranges cover the four pairings of worker medians. Warm execution
includes applicable mediated RAM-backed I/O, verification and previous-result
disposal. Time to result adds compilation and compiled-owner disposal.
Wider process CPU includes fixture reset and request framing.

| Workload | Boundary | Elapsed change | Wider CPU change | Elapsed worker spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Generated ordinary definitions | Warm | +1.79 to +2.39% | +1.58 to +2.96% | 0.52 / 0.07% |
| Defaulted rules | Warm | −2.55 to +1.04% | −2.10 to +1.07% | 1.61 / 2.04% |
| Quote-heavy invoice | Warm | −2.79 to +1.80% | −2.27 to +2.01% | 2.09 / 2.57% |
| Defaulted rules | Compile to result | −0.71 to +0.75% | −0.86 to +0.54% | 0.61 / 0.85% |

Both quiet gates, 112 batches, worker joins, fixture cleanup and archival
verification pass. The ordinary-definition CPU increase persists after
hoisting the repeated reads, while the defaulted-rule and invoice ranges
cross zero. Keep this source private. Profile the ordinary-definition
execution path before selecting another representation or binding change.

[Identity](identity.json) · [Timings](quiet-summary.json) ·
[Allocations](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Terminal status](quiet-terminal-status.json) ·
[Procedures and checks](procedures.tar.xz).
