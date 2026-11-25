# B1737c Confirm forwarded bindings with structured cancellation

This image uses the compiler's existing cancellation check during projection
selection. The body instructions match the first block, and local shipment
execution allocates 19,500 B less. Reference is `4e8e60923`. Measurement boundaries, hardware,
runtime and independent worker counts match the [first block](../README.md).
The workload order changes.

| Workload | Phase | Elapsed change | Process CPU change | Same-source elapsed spread, reference / candidate |
| --- | --- | ---: | ---: | ---: |
| Shipment callback report | Warm sync | −6.78% to −3.94% | −5.58% to −3.79% | 0.97% / 2.06% |
| Shipment callback report | Warm async | −8.93% to −7.15% | −8.54% to −6.60% | 0.91% / 1.04% |
| Shipment callback report | Compile-to-result | −5.77% to −4.94% | −6.98% to −3.98% | 0.47% / 0.41% |
| File-fed CSV quantity map | Warm async | +2.77% to +6.55% | +3.11% to +6.87% | 2.76% / 0.90% |
| Unicode text report | Warm sync | −7.98% to +5.07% | −6.75% to +3.75% | 11.47% / 2.44% |

Shipment warm CPU falls in both hosting modes for a second time. The
through-result range is favorable in this block and spans zero in the first.
The file-fed quantity map takes no new comprehension or append route, but its
higher CPU here warrants isolating the binding change from the shared display
append refactor. Text remains uncertain with wide reference spread.

Windows .NET 11 and Ubuntu .NET 10 each pass 979 focused Release checks, then
rerun the two strengthened scope cases. The custom iterator sees the bound
first row on its second pull, and an exact list preserves its element alias.
The subscription probe confirms suspension, cancellation and allowance reuse.
Four independent CPython goldens pass per image. Both quiet gates and the
archive verification pass after all workers have joined.

[Retained isolation](../isolation/README.md) · [Identity](identity.json) · [Tape shape](shape-summary.json) ·
[Execution allocation](allocation-summary.json) · [Compilation allocation](compile-allocation-summary.json) ·
[Timing summary](quiet-summary.json) · [Raw observations](quiet-observations.tar.xz) ·
[Terminal receipt](quiet-terminal-status.json) · [Procedures](procedures.tar.xz).
