# B1605 CPU attribution for defaulted rules and the Unicode report

Two serial Linux profiles use the frozen `d6437c26f` image that matches
`0400f185c`. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on
November 24, 2026 UTC. Each profile samples whole-batch CPU at 199 Hz
for about six seconds after at least eight guest seconds of warmup.
These are source attribution diagnostics. B1597 and B1604 report elapsed
comparisons at their respective sources.

| Complete script | Invocations | All samples | Resolved runtime ancestry | Unmapped leaf |
| --- | ---: | ---: | ---: | ---: |
| Generated rules with defaults | 5,760 | 1,189 | 1,179 | 72 |
| Short Unicode report with nonlocal helper | 4,480 | 1,205 | 1,130 | 172 |

Defaulted rules resolve 496 samples under the definition constructor.
The nearest leaf is `CreateClosedPairMember` in 210 and reservation in
144. `make_rules` already returns its pair before workspace setup. Its
tape still feeds seven arithmetic operations and five default stores
into construction. The two returned callables already use direct
integer returns. Investigate preparing the repeated default stores
from compiler facts once and retaining the same live Python containers.

The report resolves 514 samples under the native workspace entry for
its nine-instruction line helper. Within that group, 98 resolve nearest
the native loop, 68 call dispatch and 96 cased-word preparation. The
helper calls `len`, computes and stores a nonlocal width, then returns
the existing cased-split result. The dictionary loop accounts for 158
samples elsewhere. File or host I/O has 125 inclusive runtime samples,
and fixture work has 57 outside the runtime. The tape and stacks support
separate work on the helper and the default constructor. Stack counts
include necessary semantic work and do not predict a speedup.

Both scripts passed their goldens and the before and after quiet gates.
Workers joined, fixtures were removed and the raw archive verified under
the lease. The first launch stopped before staging because the VM had
3,997,696 free bytes. The retry retired only remote replicas of files
verified byte for byte in the existing archived B1601 trace. The original
launch result, recovery record and archive remain available.

[Attribution](attribution.json) · [Frozen identity](identity.json) ·
[Raw profiles](observations.tar.xz) · [Procedures and tapes](procedures.tar.xz) ·
[Terminal status](terminal-status.json) · [Storage recovery](space-recovery.json) ·
[B1597 timing](../2026-11-24-closed-pairs-cpython/README.md) ·
[B1604 timing](../2026-11-24-closed-capture-cpython/README.md) ·
[B1601 archive](../2026-11-24-private-call-source-cpu/README.md).
