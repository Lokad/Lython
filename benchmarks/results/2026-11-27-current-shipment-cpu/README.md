# B1836 Active CPU in wide shipment and file-fed map

On November 27, 2026 UTC, the quiet Azure F4as_v7 captured CPU samples
from `777750e6b` on Ubuntu 24.04.4 with .NET 11 RC3. Both workloads use
the async API and the comparison host from `05c1059de`. Both frozen
catalogs verify against independent CPython outputs and files, including
stock CPython 3.13.16 on the VM.

The workers warmed for at least eight seconds of command time. Linux
`perf` sampled active CPU at 199 Hz for about six seconds per workload.
Samples cover the whole request, including mediated RAM-backed I/O,
fixture reset, validation, response framing and cleanup. Method counts
use the nearest resolved runtime frame. Inlining and unresolved frames
limit attribution. These are diagnostic counts without a timing claim.

| Observation | Five-item shipment | File-fed map |
| --- | ---: | ---: |
| Verified invocations during capture | 2,560 | 12,288 |
| All CPU samples | 1,284 | 1,218 |
| Unresolved leaf samples | 131 | 280 |
| Samples with resolved runtime ancestry | 1,242 | 1,048 |
| Nearest instruction prefix | 215 | 0 |
| Nearest starmap entry or state machine | 72 | 0 |
| Nearest fixed-unpack advance | 58 | 0 |
| Nearest converted dictionary update | 65 | 0 |
| Nearest map advance | 45 | 163 |
| Nearest CSV record emission | 28 | 138 |

Starmap runs an async state machine for each advance, including when
the source row and callback both complete before suspension. A direct
ready prefix can remove that step while preserving the existing
admitted row and callback continuations. Compare the ordinary and
bound zip shipment scripts with file-fed map and the synchronous lane.
These samples identify the route but cannot predict the speedup.

Staging reused 31 immutable files, streamed the changed core and
verified all 32 files. The initial service hit its persistent disk
floor before producing a profile. Consolidating additional
byte-identical files from an archived image preserved its existing
paths and archived evidence. The recovered service passed both quiet
gates, joined both workers and profilers, removed the RAM-backed
fixtures and verified the persistent archive under its lease.

[Attribution](attribution.json) · [Image](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures and verification](procedures.tar.xz).
