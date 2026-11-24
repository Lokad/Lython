# B1658 Own both private default sets in the pair constructor

The closed pair operation owns each member's defaults from admission until
publication. Member construction writes to that backing directly. This removes
the success-path copy and reset and the member-local rollback. An interrupted
pair refunds the second member's private backing before the first. The
default backing and callable admission points remain distinct.

The reference is `6b41db7b3`, whose production source matches the candidate
patch's base `b37403891`. Both exact 32-file images verify five complete
CPython 3.13.2 workload goldens and two untimed boundaries. Windows and
Ubuntu each pass 177 focused Release checks on .NET 11 and .NET 10.
The new regression checks both arithmetic failure orders with the scope
still alive. The first fixture also charged the module namespace. The
corrected fixture starts with the live factory's separate owner accounted
for. Initial logs are retained.

The affected script's allocation remains 803,512 B per run with the
same 2,786,655 B admitted peak. All four controls keep their admitted peaks.
The invoice's allocation varies by 56 B. Each allowance returns to
baseline when the result ends.

## Quiet comparison

The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026
UTC. The ranges cover all four pairings of worker medians. Workers warm for
five seconds and supply seven rotated batches. Warm execution includes
applicable mediated RAM-backed I/O, verification and previous-result
disposal. Time to result also includes compilation and compiled-owner
disposal. Wider process CPU includes fixture reset and request framing.
Process startup is outside the clocks.

| Script and phase | Initial elapsed | Initial wider CPU | Confirmation elapsed | Confirmation wider CPU |
| --- | ---: | ---: | ---: | ---: |
| Defaulted pairs, warm | −5.00 to +0.47% | −6.15 to −0.57% | −3.93 to −0.38% | −4.07 to +0.94% |
| Defaulted pairs, time to result | +0.26 to +2.85% | −0.47 to +4.67% | −0.55 to +2.14% | +0.70 to +2.31% |
| Eager annotated rules, warm | −2.53 to +2.04% | −2.78 to +2.98% | — | — |
| Generated ordinary definitions, warm | +0.21 to +1.63% | +1.02 to +2.84% | −1.64 to −0.18% | −1.55 to +0.18% |
| Quote-heavy invoice, warm | −3.12 to −1.84% | −4.25 to −3.08% | −2.98 to +2.99% | −3.26 to +2.86% |
| Postponed annotated rules, warm | −0.52 to +5.03% | +0.73 to +6.57% | −5.41 to +1.31% | −7.55 to +1.51% |

The initial six lanes target 0.52-second batches. The repeat uses the same
images and five lanes with a 0.68-second target. Generated and postponed
CPU increases do not repeat. The defaulted script shows some benefit when
warm, while its confirmed time-to-result CPU increases 0.70–2.31%. Keep
the single rollback owner and carry that CPU increase forward to the next
call-entry checkpoint. The two blocks establish mixed timing.

All 308 batches, both sets of machine gates, worker joins and archival
checks pass. The staging receipt covers two remote links to one old
reference binary, retired after verifying the local copies and both
surviving archives.

[Identity](identity.json) · [Allocation](allocation-summary.json) ·
[Initial timing](quiet-summary.json) · [Initial observations](quiet-observations.tar.xz) ·
[Initial terminal receipt](quiet-terminal-status.json) ·
[Confirmation timing](confirmation/quiet-summary.json) ·
[Confirmation observations](confirmation/quiet-observations.tar.xz) ·
[Confirmation terminal receipt](confirmation/quiet-terminal-status.json) ·
[Procedures and live-budget probes](procedures.tar.xz).
