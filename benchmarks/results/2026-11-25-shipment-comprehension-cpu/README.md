# B1732 Shipment callback, comprehension and frame attribution

Two warm profiles use the exact `a420c3e05` image from the
[CPython comparison](../2026-11-25-arithmetic-call-cpython/README.md).
The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. Each worker warms for eight seconds or more, samples at 199 Hz
and completes 2,560 runs within the capture. Full result checks pass
before and after profiling. The capture includes mediated RAM-backed
file I/O, framing, fixture reset, verification and result cleanup.

| Warm lane | All samples | Resolved runtime ancestry | Nearest map advance | Nearest scope name resolution | Nearest generic prepared operation | Nearest frame or operand binding |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Sync | 1,332 | 1,296 | 100 | 30 | 27 | 2 |
| Async | 1,395 | 1,350 | 74 | 19 | 40 | 2 |

The generic prepared-operation column combines repository methods that
also serve other operations, including the shared async dispatcher.
Async zip pull and fixed unpack are nearest on another 56 samples.
The scope-name figures include ordinary dynamic names. Inlining and
unresolved leaves limit attribution. These counts establish neither
the arithmetic cost of an individual dispatch nor a speedup.

A separate local instrumented build of the same source measures actual
routes and verifies both output and file goldens in both hosting modes.
Each shipment run enters and exits 32 comprehension binding scopes,
with 4,096 reads, writes and append operations each. The reported helper
transfers once for each of its 16 calls at fixed tuple unpacking, before
either comprehension starts. Those transfers reuse existing admitted
workspace. The outer `report_shipments` call rents one continuation.
The file-fed map control does none of these transfers or scope operations.
Both workloads return allowance to baseline after result and script disposal.

Prioritize the repeated map call path over moving the borrowed handoff.
The iterator owns a fixed callable and source count. Preparation can
select complete positional binding once, while retaining ordered pulls,
operand ownership, protocol overflow and suspension. The earlier zip
continuation split measured adverse sync controls. A future comprehension
design needs to eliminate both a binding stage and a real downstream
handoff to justify altering scope representation.
[Earlier zip comparison](../2026-11-25-async-zip-pull/README.md).

The first storage inventory requested 16 MiB of free space and did not
start collection. The launch used the established 15 MiB staging
requirement, retiring only exact raw copies present in verified archives.
Both quiet gates passed, workers and profilers joined, and the persistent
capture verified before the lease was released.

[Profile identity](identity.json) · [Attribution](attribution.json) ·
[Route counts](routes.json) · [Observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz).
