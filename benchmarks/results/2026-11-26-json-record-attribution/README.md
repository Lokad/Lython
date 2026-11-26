# B1779 JSON record and decoder entry attribution

The November 26, 2026 UTC profile uses the `e222c5221` runtime after
[B1776](../2026-11-26-json-ready-construction/README.md). The same 32-file
image ran on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3. Each
warm synchronous worker ran at least eight seconds before a six-second
199 Hz capture. Samples include RAM-backed mediated I/O, verification,
reset, request framing and cleanup.

| Observation | JSON service totals | JSON Lines helper report |
| --- | ---: | ---: |
| Verified invocations during capture | 8,704 | 2,048 |
| All-thread samples | 1,262 | 1,415 |
| Samples with a resolved runtime ancestor | 1,070 | 1,363 |
| Nearest iterative construction loop | 89 | 134 |
| Nearest container push | 31 | 24 |
| Nearest explicit append | 18 | 0 |
| Nearest decoder entry state machine | 4 | 82 |
| Nearest admission reservation | 160 | 119 |
| Of those, nearest caller creates a record | 49 | 30 |
| Of those, nearest caller copies exact UTF-8 | 107 | 29 |

The append count covers resolved frames only. Inlined and unresolved
work may appear under the construction loop or other frames. Counts
locate sampled work and do not estimate the removable share of CPU.
Both workloads need final record backing, and equal keys already reuse
their preceding layout. [Earlier profile](../2026-11-26-current-json-attribution/README.md)

The decoder entry has the same 82 nearest samples in JSON Lines as it
did before B1776. That workload repeatedly decodes with immutable,
hook-free options. Testing a direct default entry can remove option
preparation and an outer state machine while keeping dynamic callbacks
and managed stack checks. The document runs the default entry once and
provides a low-frequency control. [Earlier general entry trial](../2026-11-26-ready-json-decoder-entry/README.md)

Both independent CPython goldens and all 32 image hashes verify. Both
quiet gates pass, workers and profilers join, and the persistent archive
verifies. The VM reuse retained the full verified older archives and
removed 3.5 MB of byte-identical expanded records. The first inspection
found no remaining profile-only duplicates to reclaim.

[Attribution](attribution.json) · [Frozen image](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Verification and procedures](procedures.tar.xz)
