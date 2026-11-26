# B1752 Attribute shipment costs after direct zip operands

Two warm `RunAsync` CPU profiles use the exact `0a0e32c16` image from
[B1750](../2026-11-26-short-path-cpython/README.md). The only later runtime
source change at `3507bdd2d` is in JSON decoding. Neither shipment runs a
JSON decoder. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3
on November 26, 2026 UTC. The workers warmed for eight seconds or more and
sampled `cpu-clock` at 199 Hz for about six seconds. The capture includes
mediated RAM-backed file I/O, checks, fixture reset, framing and disposal.

| Observation | Bound `starmap` shipment | Ordinary `map` shipment |
| --- | ---: | ---: |
| Verified requests during capture | 2,560 | 2,560 |
| All-thread samples | 1,388 | 1,268 |
| Samples with resolved Lython runtime ancestry | 1,343 | 1,225 |
| Those with an unresolved leaf | 87 | 74 |
| Nearest `ExecuteInstructionPrefix` | 222 | 225 |
| Nearest `PyMapIterator.TryMoveNextAsync` | 52 | 98 |
| Nearest `starmap` entry or state machine | 74 | 0 |
| Nearest zip fixed-unpack state machine | 40 | 23 |
| CSV parsing or row-construction ancestry | 141 | 119 |
| Frame and operand-binding ancestry | 1 | 1 |

Counts locate work and do not estimate removable percentages. The ordinary
executable dispatch and CSV routes remain broad contributors. The two
smaller, identifiable stages are the outer `starmap` async wrapper and the
zip fixed-unpack wrapper. Both currently start a state machine when their
underlying work has already completed. Test a direct ready-result handoff for
these two consumers. Preserve ordered pull, fault and cancellation propagation,
strict exhaustion, partial-row ownership and suspended callback cleanup.

The independent CPython output and file goldens and all 32 image files
verified before staging. Both quiet checks and post-capture goldens pass,
workers and profilers joined, and the archive verified under the lease.

[Identity](identity.json) · [Attribution](attribution.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
