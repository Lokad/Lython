# B1206 Capture cells in inline lexical factories

Compares private candidate `2e99ca428` with production source
`fbb10c14b` on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and
.NET 11 RC3 on 2026-11-20 UTC. The reference binary was built from
source-equivalent `32c5f2ef7`. Workloads use the verified B1202 catalog.
Each role ran in two independent processes with eight seconds or more
of warmup, followed by five rotated rounds targeting 0.4-second requests.
Ranges cross the four pairings of process medians.

| Warm workload | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | ---: | ---: | ---: |
| Nested factories | −20.13% to −17.87% | −19.80% to −19.00% | 1.98% / 0.84% |
| Eager annotated rules | −1.49% to +8.26% | −3.10% to +5.92% | 3.98% / 5.69% |
| Postponed annotated rules | +1.02% to +4.88% | +2.19% to +6.95% | 1.33% / 2.45% |
| CSV invoice control | +1.39% to +2.19% | +1.72% to +4.24% | 0.74% / 0.05% |

Warm elapsed includes mediated RAM-backed I/O where applicable, golden
checks and previous-result cleanup. Compilation, process startup, fixture
reset and final teardown are outside warm elapsed. Process CPU covers
request handling. Local allocation is effectively flat in all four lanes.
The nested accounted peak falls by 208 bytes. All three other peaks
match and all runs restore the borrowed allowance.

Eligible captured factories finish using the existing managed inline
operand buffer. A local remains a normal slot until its first capture.
The execution owner admits the cell before creation. Any ordinary frame
materialized later adopts the same cell and admits remaining cells.
Both call paths read the same prepared capture map. The initial variant
checks for a cell on every native local store. The nested factory gain
is clear in both clocks. The eager controls vary between identical
processes, and postponed rules and invoice move adversely. These
observations motivate refining the local-store and definition checks.

Focused Release on the candidate passed 839 public checks on each
platform, and 1,438 white-box checks on Ubuntu. Six CPython closure
probes passed on both frozen images in both modes. The new public
cases cover mutation around a real suspension, awaited cancellation
and refusal before the closure escapes. All four workload goldens
passed with both 32-file images. All workers joined, the persistent
archive verified under the lease and no guest fixtures remained.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Observations](observations.tgz) · [Source and verification](source-and-checks.tar.xz).
