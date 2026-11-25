# B1698 Direct native iterator routing

Native iterators opt in to asynchronous pulls where their operations can
suspend. Synchronous iterators lose the inherited async wrapper. Generator
expressions advance an acquired native iterator directly, retaining its
position and operand representation. Independently supplied CLR async
enumerators keep acquisition precedence and awaited disposal. The patch
removes the native async enumerable adapters and a net 40 production lines.

The source pair is `f6cf9796c` and the archived core patch. The quiet Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25, 2026 UTC.
Two workers per image each contributed seven rotated batches. Ranges span
all four pairings of worker medians. Warm includes applicable RAM-backed
mediated I/O, output verification and result disposal. Through result adds
compilation and compiled-owner disposal. Wider process CPU also includes
request framing and fixture reset.

| Workload | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| Original shipment, warm async | −1.64% to +1.16% | −1.35% to +1.67% |
| Generator over map shipment, warm async | −2.64% to +2.27% | −4.18% to +2.62% |
| File-fed shipment, warm async | −7.62% to +11.97% | −8.40% to +10.63% |
| Bound starmap shipment, warm async | −1.16% to +0.66% | −1.04% to +2.00% |
| Retained-tuple shipment, warm async | −0.52% to +3.85% | −0.90% to +3.00% |
| Original shipment, warm sync | −2.09% to +1.82% | −2.02% to +0.83% |
| Quoted invoice, warm sync | −0.67% to +0.71% | −1.06% to +1.06% |
| Defaulted rules, through result | −5.90% to −0.65% | −7.60% to +0.00% |

Retain the simpler routing. Every affected async CPU and elapsed range
spans zero. File-fed shipment has same-source elapsed spreads of 10.55%
and 9.64%. The retained-tuple range permits a small regression. The
unchanged sync shipment and invoice cross zero, and the favorable
defaulted-rule elapsed is on a control. These data establish no CPU gain.

Both images verify seven complete CPython 3.13.2 goldens. The local async
generator-over-map case allocates 2,232 B less per run. Other allocations
are effectively unchanged, all admitted peaks match, and budgets return to
baseline. Windows .NET 11 and Ubuntu .NET 10 each pass 755 focused public
and 172 white-box Release checks. Tests preserve generator and retained
source aliasing across delayed reads and verify awaited cleanup. Both
quiet gates, all 224 batches, worker joins, fixture removal and archive
verification pass.

[Identity](identity.json) · [Timing](quiet-summary.json) ·
[Allocation](allocation-summary.json) · [Observations](quiet-observations.tar.xz) ·
[Procedures and patches](procedures.tar.xz) · [Terminal status](quiet-terminal-status.json).
