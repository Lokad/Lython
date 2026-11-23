# B1432 Keep a prepaid singleton integer in its callable

This tests the allocation opportunity from B1428 after B1431 made
prepared integer defaults directly readable by closed returns. The
default keeps its original preflight. A fresh integer travels in the
funded definition slot and moves into the callable without boxing or
constructing its tuple. Observation or ordinary binding materializes
the canonical tuple before a guest alias can escape. Noninteger and
generator defaults still have their ordinary tuple owners.

Private `d7fb4a219` embeds a slot in every function. Private
`1814befe9` moves the integer field to a subtype for callees that use
it. The refined source is compared with `8402528d6`, whose executable
core matches the archived B1431 candidate. Only the core differs across
the two verified 32-file images. The quiet Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.

| Complete script | Boundary | Elapsed change | Wider process CPU change | Elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Postponed nested factories | Warm | −3.71 to +1.48% | −3.73 to +0.28% | 2.77 / 2.55% |
| Postponed nested factories | Compile to result | −3.18 to +3.65% | −2.05 to +3.59% | 5.96 / 1.03% |
| Eager nested factories | Warm | −3.05 to −0.36% | −2.41 to −1.36% | 2.57 / 0.20% |
| Factories reading defaults metadata | Warm | +2.81 to +6.07% | +2.65 to +5.95% | 2.14 / 1.00% |
| Fully supplied helper | Warm | −0.62 to +0.82% | +0.78 to +1.56% | 0.56 / 0.89% |
| Quote-heavy CSV invoice | Warm | −1.78 to +0.02% | −1.52 to 0.00% | 1.19 / 0.63% |

Ranges cover the four pairings of independent process medians. Each
worker warms for eight seconds and supplies seven rotated batches.
Warm elapsed includes applicable mediated RAM-backed I/O, verification
and previous-result disposal. Time to result adds compilation and
compiled-owner disposal. Wider CPU includes resets and protocol.
Startup and final teardown are outside elapsed.

On Windows, initial factory allocation falls 7.43%. The observer
allocates 5.35% more and the fully supplied script's one ordinary
callable costs 16 additional bytes. The refined factories allocate
114,688 fewer bytes each (13.00%) and the observer allocates 16,384
more (1.34%). Fully supplied allocation matches and the quoted
invoice is effectively flat. All admitted peaks match and the
allowance returns after cleanup.

Keep both candidates private. Field isolation lowers the observer's
allocation penalty, while its elapsed and CPU loss is stable. The
postponed time-to-result comparison spans zero and its reference
workers differ by 5.96%. Eager warm improves modestly. Fully supplied
elapsed spans zero, while its wider CPU range is adverse. The extra
callable representation and late publication are disproportionate to
these timings. The unboxed allocation improvement is established
independently of timing. Preserve the earlier B1428 conditions.

Each variant passes 446 focused Release checks on Windows .NET 11
and Ubuntu .NET 10. The refined image also passes 290 Windows
white-box checks for executable function shapes. Both frozen pairs
verify the same five CPython goldens. An additional parity program
matches CPython on defaults replaced during keyword comparison,
decorator observation, generator aliases and negative integer
transport. The initial build updated two white-box call sites for
the prototype's internal signature.

All 168 batches and both quiet gates pass in 329 seconds. Workers
join, fixtures are removed and the persistent archive verifies under
the lease. The first staging attempt stops before mutation because
an old donor archive is absent. The retry verifies all aliases of two
different archived core replicas before making space. No CPython
elapsed ratios were collected.

[Source and images](source.json) · [Timings](summary.json) ·
[Allocation and admission](local-allocation.json) · [Observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Reference core](../2026-11-23-direct-bound-integer-refined/README.md) ·
[B1428 earlier variant](../2026-11-23-singleton-positional-default/README.md).
