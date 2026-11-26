# B1742 Borrow native inputs in composed iterators

The prototype lets `map` and `zip` reuse an input that already implements
the native iterator protocol. Sequences and Python-defined protocols still
use an owning cursor. This removes five cursor objects per shipment batch
and one per file-fed map run. The Windows .NET 11 allocation diagnostic
measures 7,040 B less per shipment run and 88 B less per file-fed map run.
Peak shared admission matches on all four workloads and returns to baseline.

| Workload | Warm lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Shipment callback report | Sync | −2.49% to +1.55% | −2.74% to +1.86% |
| File-fed CSV quantity map | Async | −1.60% to +5.69% | −1.91% to +2.96% |
| Shipment native `map(int, …)` control | Sync | +0.66% to +2.77% | +2.22% to +3.48% |
| Shipment callback report | Async | −2.90% to +2.50% | −3.02% to +1.52% |
| Unchanged Unicode text report | Sync | −12.36% to −7.19% | −11.54% to −7.98% |

The reference is `6c6445cee`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4
and .NET 11 RC3 on November 25, 2026 UTC. Ranges span the four pairings of
process medians. Each process warms for seven seconds and provides seven
rotated batches. Warm elapsed includes mediated RAM-backed I/O, result
cleanup and output verification. Wider CPU also includes request framing
and fixture reset. Each 32-file image passed four independent CPython
output and file goldens. Both quiet gates and all 140 batches passed.
Workers joined, fixtures were removed and the persistent archive verified.

Shipment and file-fed timing remain uncertain. The native-map control used
more CPU across pairings. The unchanged text control moved sharply in the
other direction. The reduction in cursor objects is established. Keep the
performance patch archived until a design also simplifies the remaining
dispatch and preserves the native-map control. Windows .NET 11 and Ubuntu
.NET 10 each passed 2,038 focused Release checks and the three targeted
tests for retained input and cancellation during a partial row or strict
exhaustion probe.

A separate isolated Windows probe found that the reference and candidate
can exhaust the CLR stack while pulling a deep native `map` chain in
either hosting mode. That finding drove a separate stack-admission fix.

[Identity](identity.json) · [Timing summary](quiet-summary.json) ·
[Allocation summary](allocation-summary.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures and patch](procedures.tar.xz).
