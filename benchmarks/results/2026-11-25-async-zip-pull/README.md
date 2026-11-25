# B1697 Async zip pull and strict exhaustion

The prototype advances available sources in an ordinary loop. An incomplete
pull transfers the already funded destination and pull position to the async
continuation. Tuple materialization and fixed unpack share that loop. Strict
exhaustion probes use the same continuation when a later source suspends.

The source pair is `f6cf9796c` and the archived patch. The quiet Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Ranges span all pairings of medians from two workers per image, each with
seven rotated batches. Warm clocks include applicable RAM-backed mediated
I/O, result disposal and output verification. Through result adds compilation
and compiled-owner disposal. Wider process CPU also includes request framing
and fixture reset.

| Workload | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Original shipment, warm async | −2.27% to −0.25% | −3.13% to +2.00% |
| File-fed shipment, warm async | −7.45% to +3.39% | −7.30% to +2.89% |
| Retained-tuple shipment, warm async | −2.14% to −1.13% | −1.56% to −0.67% |
| Strict shipment, warm async | −5.77% to +1.76% | −5.73% to +1.06% |
| Original shipment, warm sync | +1.99% to +5.67% | +2.05% to +5.93% |
| Quoted invoice, warm sync | +0.66% to +5.99% | −0.60% to +5.62% |
| Defaulted rules, through result | −2.96% to +0.48% | −3.20% to +1.57% |

The retained-tuple lane improves in both clocks with narrow worker spreads.
The original async shipment improves slightly in elapsed time, with
inconclusive CPU. The unchanged sync shipment is adverse across pairings.
The file-fed and strict lanes also vary across same-source workers. Keep
the patch private. These measurements leave open whether the async
improvement can survive a simpler common dispatch with stable controls.

Both images verify six complete CPython 3.13.2 goldens. Synchronous
allocation diagnostics show matched peaks and budgets return to baseline.
The prototype passes 143 Windows .NET 11 and 388 Ubuntu .NET 10 public
Release checks, plus 77 Ubuntu white-box checks. A delayed host regression
cancels both a partial row and a strict exhaustion probe and checks cleanup.
All 196 batches, both quiet gates, worker joins, fixture removal and archive
verification pass.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Procedures and patch](procedures.tar.xz) · [Terminal status](quiet-terminal-status.json).
