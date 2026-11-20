# B1207 Prepared captured-local stores

Measures private `7f583edfa` against production source `fbb10c14b`
on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on
2026-11-20 UTC. The same reference binary, workload catalog and
measurement boundaries were used in [B1206](../README.md).

| Warm workload | Elapsed change | Process CPU change | Duplicate elapsed spreads, reference / candidate |
| --- | ---: | ---: | ---: |
| Nested factories | −17.26% to −14.66% | −17.09% to −11.79% | 0.51% / 2.63% |
| Eager annotated rules | −7.65% to +2.48% | −5.31% to +3.16% | 3.57% / 7.15% |
| Postponed annotated rules | −1.88% to +7.17% | −0.76% to +6.14% | 2.80% / 6.25% |
| CSV invoice control | −2.21% to −0.02% | −2.63% to +1.72% | 0.89% / 1.34% |

Each revision had two independent processes with at least eight seconds
of warmup and five rotated rounds targeting 0.4-second requests. Ranges
cross the four process-median pairings. Warm elapsed includes mediated
RAM-backed I/O where applicable, golden checks and prior-result cleanup.
Compilation, startup, fixture reset and final teardown are outside that
clock. Process CPU covers the request loop. Local CLR allocation remains
effectively flat. Shared allowances return to baseline. The nested peak
falls by 208 bytes and the other accounted peaks match.

The compiler marks stores to captured locals during operand preparation,
using a free field in the existing instruction. Native stores to ordinary
locals continue to write directly. The definition guard leaves capture
map resolution to call sites that actually need it. Both execution paths
reuse the same capture builder and ownership. A frame created after a
transfer funds only the cells still to be materialized.

The nested gain survives the refinement on both clocks. Retain the refined
source. The earlier invoice regression does not repeat. Eager and postponed
rules have variable same-source processes, and several postponed pairings
remain adverse. These controls do not establish neutrality or identify the
cause of the remaining variation.

The 839 public focused Release checks passed on Windows .NET 11 and
Ubuntu .NET 10. Ubuntu also passed 1,438 white-box checks. Both verified
32-file images matched all four workload goldens and six CPython closure
probes in both execution modes. The 80 timed batches completed and all
workers joined. The persistent archive verified under the lease before
fixture teardown, and its local copy matched the recorded hash.

[Timing](summary.json) · [Allocation](allocation-summary.json) ·
[Observations](observations.tgz) · [Source and verification](source-and-checks.tar.xz).
