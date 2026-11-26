# B1798 Current async shipment and file-fed map attribution

The capture uses the production runtime at `5c0fbe556` and the two
existing shipment fixtures. Source and fixtures match `53a348cca`.
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November
26, 2026 UTC. Workers warmed for at least eight seconds before sampling
`cpu-clock` at 199 Hz. Samples cover warm `RunAsync`, mediated RAM-backed
I/O, output verification, fixture reset, framing and cleanup.

| Observation | Shipment callbacks | File-fed map |
| --- | ---: | ---: |
| Verified invocations during capture | 3,584 | 12,288 |
| All-thread samples | 1,400 | 1,251 |
| Samples with a resolved runtime ancestor | 1,342 | 1,075 |
| Of those, samples with an unresolved leaf | 90 | 147 |
| Nearest `PyMapIterator.TryMoveNextAsync` | 95 | 152 |
| Nearest CSV parsing or row construction | 120 | 260 |
| Nearest runtime host I/O | 60 | 179 |
| Nearest `ExecuteInstructionPrefix` | 207 | 0 |
| Nearest `SumAsync` continuation | 0 | 89 |

Map advancement remains visible in both captures. The common entry
takes source values through completed `ValueTask<PyIterationResult>`
and then fills the callback's short operand window. The file-fed map
also returns each result through an async pull to `sum`. A candidate
should remove an actual completed-result handoff while keeping the
same continuation for pending source and callback work. Preserve
ordered pulls, cursor aliases and exhaustion, selected-callback semantics,
cancellation and funded suspended arguments. Check both hosting APIs
and keep the protected file-search lane as an unchanged control. Earlier
prepared callback selection and borrowed cursor candidates remain in
the evidence. This is a single-image profile. The nearest-frame counts
include inlined work and leave the earlier timing movements unexplained.
[Prepared callback selection](../2026-11-25-prepared-map-callback/README.md) ·
[Borrowed map sources](../2026-11-26-borrowed-map-sources/README.md).

Both independent CPython goldens and all 32 image hashes verify. The
capture reuses existing VM binaries. Both quiet checks pass. Workers
and profilers join, temporary guest fixtures are removed, and the
persistent archive verifies under the lease.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Verification and procedures](procedures.tar.xz).
