# B1190 Shipment reports against CPython

This checkpoint uses source `6ba60528d` and the source-identical frozen
`f8f223df2` image from B1189. It ran on Ubuntu 24.04.4 and .NET 11 RC3 on
the quiet Azure F4as_v7 on November 20, 2026 UTC. The external baseline is
stock CPython 3.13.16 with the unchanged comparison worker.

| Workload | Phase | Lython | CPython | Lython / CPython |
| --- | --- | ---: | ---: | ---: |
| Named shipment | Warm | 3.786–3.842 ms | 3.233–3.314 ms | 1.14–1.19× |
| Named shipment | Compile to result | 3.915–4.031 ms | 3.324–3.370 ms | 1.16–1.21× |
| Named shipment | Compilation | 0.02498–0.02535 ms | 0.03346–0.03397 ms | 0.74–0.76× |
| Positional shipment | Warm | 3.737–3.750 ms | 2.861–2.893 ms | 1.29–1.31× |
| Positional shipment | Compile to result | 3.865–3.902 ms | 2.966–3.085 ms | 1.25–1.32× |
| Positional shipment | Compilation | 0.02489–0.02539 ms | 0.03288–0.03296 ms | 0.76–0.77× |
| CSV invoice | Warm | 6.517–6.520 ms | 6.104–6.151 ms | 1.06–1.07× |

Both reports read the same 189,441-byte JSON fixture with 3,072 records and
write identical output. The invoice reads 127,031 bytes and 4,096 data rows.
Lython warm includes mediated RAM-backed I/O, output verification and
prior-result cleanup with shared memory accounting enabled. CPython uses
its native file APIs. Compile to result includes the full warm boundary plus
compilation and code-owner cleanup. Compilation alone includes code-owner
cleanup. Each phase was independently warmed. Process startup, fixture reset
and final teardown were outside the guest clocks.

Ranges span the four pairings of duplicate process medians. Each process
ran five rotated rounds of roughly quarter-second batches per phase. Warm
shipment duplicate elapsed spreads are at most 2.50%. Positional shipment's
compile-to-result CPython spread is 4.04%. Cross-collection differences
include measurement variation. B1189 isolates the source change and records
the adverse positional control. This collection adds an external baseline.

Both quiet gates, all 140 timed batches and three CPython workload goldens
passed. The complete 32-file image was verified. Workers joined, fixtures
were removed and the archive was verified under the VM lease. The first
local launch exited before SSH because its console redirection collided
with the launch receipt path. The run used separate paths and the original
failure is preserved.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Checks and procedures](procedures.tar.xz) ·
[Paired Lython comparison](../2026-11-20-prepared-format-keywords/README.md).
