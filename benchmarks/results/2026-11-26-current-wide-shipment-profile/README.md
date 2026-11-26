# B1807 Current wide async shipment profile

The capture uses production `cb258fe7f` and the verified five-item
shipment script. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3
on November 26, 2026 UTC. The worker warmed for at least eight guest
seconds before 199 Hz `cpu-clock` sampling. The capture covers 2,560
verified invocations and 6.68 guest seconds. Whole-worker samples also
include mediated RAM-backed I/O, file and output checks, request framing,
fixture reset and result cleanup.

| Observation | Samples |
| --- | ---: |
| All threads | 1,363 |
| With a resolved runtime ancestor | 1,319 |
| With a runtime ancestor and an unresolved leaf | 84 |
| Nearest instruction prefix | 232 |
| Nearest converted dictionary update | 52 |
| Nearest map advance | 47 |
| Nearest async starmap entry or continuation | 49 |
| Nearest list construction | 25 |
| Nearest reservation called by list construction | 15 |
| Nearest wider callback row setup | 15 |

The shipped frame-input path already avoids a separate call packet.
The measured generator still sends a new list into each starmap call.
In the actual short and wide fixture tapes, the compiler emits a
three- or five-item `MakeList` immediately before `Yield` on the same
operand slot. The next milestone should transfer the already-evaluated
row directly to an eligible callback at generator suspension. The
generator must finish the yield and leave its active frame before the
callee can advance it again. Fund scalar publication, and keep the
ordinary result for escaping displays, open-ended rows and unsupported
consumers. This is attribution from one image. Nearest-frame counts
include inlined work and give no estimate of removable CPU.
[Compiled row census](row-yield-tape.json).

The 32-file image and the independent CPython output and file golden
verify. Both quiet checks pass. The worker and profiler join, guest
fixtures are removed, and the persistent archive verifies under the
lease.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Verification and procedures](procedures.tar.xz).
