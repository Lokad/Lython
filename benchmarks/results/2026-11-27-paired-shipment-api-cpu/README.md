# B1851 Active CPU for the two shipment APIs

On November 27, 2026 UTC, the quiet Azure F4as_v7 ran the same wide shipment
source and 27,350-byte CSV fixture through both APIs. Both workers use the
`9cee6f423` image from [B1849](../2026-11-27-post-ready-insertion-cpython/README.md),
.NET 11 RC3 and Ubuntu 24.04.4. The 2,048-record script matches independent
CPython output and files on Windows and on the VM. Each worker verifies its
results before and after capture.

Workers warm for at least eight seconds of command time. `perf` samples
active CPU at 199 Hz for approximately six seconds per lane. Captures
include mediated RAM-backed I/O, verification, framing, fixture reset
and result cleanup. The counts use the nearest resolved runtime frame.
The collector can include inlined work in that frame. These counts locate
candidate stages and give no estimate of removable elapsed time.

| Observation | Run | RunAsync |
| --- | ---: | ---: |
| Verified invocations | 3,072 | 3,072 |
| All active CPU samples | 1,196 | 1,371 |
| Unresolved leaf samples | 104 | 147 |
| Samples with resolved runtime ancestry | 1,156 | 1,322 |
| Nearest instruction prefix | 206 | 241 |
| Nearest fixed-unpack entry | 31 | 78 |
| Nearest fixed-unpack completion | 19 | 20 |
| Nearest map advance | 46 | 50 |
| Nearest starmap advance | 36 | 42 |
| Nearest generator advance | 16 | 24 |
| Nearest generic loop advance | 6 | 24 |

Both APIs already use fixed-unpack operand storage. The compiled zip path
still packages a ready row count and then its jump decision in completed
task values. The source supports testing direct ready returns through
both boundaries. Keep the existing pending pull loop, stack-owned row
slots, strict-tail behavior and exception routing. The capture also shows
I/O and general dispatch costs that this change leaves open.

Staging reuses all 32 verified worker files and streams 86,408 B of inputs.
The worker and profiler processes join, both quiet checks pass, RAM fixtures
are removed and the persistent archive verifies under the lease.

[Attribution](attribution.json) · [Image](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Checks and procedures](procedures.tar.xz).
