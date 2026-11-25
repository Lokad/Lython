# B1692 Borrow completed operands in async map

Short `map` rows read completed sources into the existing operand slots.
An incomplete source pull transfers the prefix to the awaited continuation,
which resumes at the same source. The existing callable binder handles a
pending callback. Wider rows still admit their argument packet before
pulling sources. Cancellation, refusal and cleanup keep their existing
boundaries.

The source pair is `f1c4f33ba` and the archived patch. The quiet Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 on November 25, 2026 UTC. The worker
images differ only in the core DLL. Two workers per image each supply seven
rotated batches. Ranges span all pairings of worker medians. Negative values
mean less time. Warm execution includes mediated RAM-backed input, output
verification, result disposal and request handling. Through result adds
compilation and compiled-owner disposal in the running worker.

| Workload and boundary | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Original unbound shipment, warm async | −12.05% to −9.35% | −11.21% to −9.01% |
| Bound shipment, warm async | −3.38% to −1.54% | −3.65% to −2.62% |
| File-fed shipment map, warm async | −7.91% to −1.20% | −7.50% to −1.61% |
| Original unbound shipment, warm sync | −2.76% to +1.41% | −2.45% to +1.26% |
| Bound defaulted readings, warm async | −25.67% to −20.09% | −24.60% to −17.95% |
| Defaulted rules, through result | −5.34% to +1.43% | −6.38% to +2.02% |
| Quoted invoice, warm sync | −6.06% to −1.40% | −5.94% to −2.15% |

Retain the narrower async continuation. All four affected async lanes
use less CPU. The original unbound shipment improves on the current
baseline. Its earlier adverse B1687 result remains in the record, since
this comparison does not remeasure that source pair. The two candidate
readings workers differ by 6.61% in elapsed and defaulted rules have 5.81%
reference spread. The sync invoice moves in the favorable direction
despite using the unchanged path, so this run establishes no causal
sync improvement.

Warm local allocation matches in five of six cases and varies by 56 B on
bound shipment. Runtime and compiler admitted peaks match, and budgets
return to baseline. The six full CPython 3.13.2 goldens and all 22 code
objects match. Windows .NET 11 and Ubuntu .NET 10 each pass 426 focused
public checks. The Ubuntu white-box suite passes nine. These include
suspending a later source, preserving aliases, cancellation during the
pull, one close, refusal and budget reuse. Both quiet gates and all 196
timed batches pass. Workers join, fixtures are removed and the archive
verifies under the lease.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Tapes](tape-summary.json) ·
[Observations](quiet-observations.tar.xz) · [Procedures and patch](procedures.tar.xz) ·
[Terminal status](quiet-terminal-status.json).
