# B1634 CPU attribution after forwarded integer slots

Three six-second CPU profiles use the sealed `d6a168491` worker image
from the preceding CPython checkpoint. The Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3 on November 24, 2026 UTC. Each process warms
for at least eight seconds of guest elapsed time before sampling at
199 Hz. The compile-to-result profiles cover compilation, execution,
output verification and cleanup in an already-running worker. The
warm profile uses a compiled script. These rule workloads use no guest
file I/O.

| Profile | Invocations | All samples | Resolved runtime ancestry | Under definition construction | Nearest frontend | Unmapped leaf |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Defaulted rules, warm | 6,656 | 1,189 | 1,180 | 528 | 5 | 93 |
| Defaulted rules, time to result | 6,400 | 1,204 | 1,171 | 543 | 38 | 91 |
| Eager rules, time to result | 11,776 | 1,191 | 1,128 | 478 | 63 | 106 |

Definition construction accounts for 45–46% of resolved runtime
ancestry on defaulted rules and 42% on eager rules. The defaulted profile
has 233 warm and 250 through-result samples nearest the closed pair
member constructor. Reservation is nearest in 158 and 149 samples under
construction. Eager rules have 124 nearest the computed-definition
producer and 117 nearest reservation under construction. Existing
admission-order tests require default backing and callable admission to
remain distinct. [Admission evidence](../2026-11-24-closed-definition-construction/README.md).

The profiles support targeting definition construction across the two
factory shapes. Compiler work and runtime entry remain visible. Ancestry
counts include descendant work, and some leaves are unresolved. Use
these counts for locating work and retain the uninstrumented paired
comparisons for timing. [CPython comparison](../2026-11-24-forwarded-integers-cpython/README.md).

Both quiet gates pass. The catalog and all 32 worker files verify before
and after collection. Each worker verifies its output, joins and clears
its fixture directory. The raw archive verifies under the VM lease.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Raw profiles](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
