# B1248 retained dictionary operations against CPython

Measures Lython `c62457d49` against stock CPython 3.13.16 on November
20, 2026 UTC. The quiet Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11
RC3. The Lython image reuses the verified 32-file candidate from
[B1246/B1247](../2026-11-20-direct-native-text-dict-keys/README.md).

| Workload and phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Generated validator, warm | 0.07333–0.07371 ms | 0.03980–0.04074 ms | 1.80–1.85× |
| Generated validator, compilation | 1.643–1.655 ms | 1.824–1.837 ms | 0.894–0.907× |
| Generated validator, compile-to-result | 1.701–1.723 ms | 1.955–1.960 ms | 0.868–0.881× |
| Alternating JSON shipment, warm | 8.840–8.953 ms | 6.564–6.674 ms | 1.325–1.364× |

The validator checks 96 fields from a 1,714-byte JSON file. All integer
conversions succeed, and the script includes its negative-value branch.
The shipment reads 3,072 alternating-shape records from 759,222 bytes
and writes a report. This fixture differs from the earlier validator
with malformed values.

Warm elapsed includes guest execution, mediated RAM-backed file I/O,
golden validation and previous-result disposal. Compile-to-result
adds compilation and disposal of the compiled owner. Compilation only
excludes guest execution. Process startup, fixture reset and final
teardown sit outside the guest clocks. Each ratio divides a Lython
elapsed median by a CPython elapsed median, across two independently
warmed processes per engine. The maximum control spread is 2.36%, and
the alternating report's maximum within-worker IQR is 4.68%.

The first launch failed before sampling because the worker identity
lacked auxiliary-file hashes. Its archive and terminal state were
verified before the empty fixture mount was removed. The first
corrected stage encountered the persistent-space preflight. The final
stage reused the exact input and binaries after verifying and retiring
older staged replicas of an already archived image. All 112 timed
batches and both quiet gates passed. The workers joined, the fixture
mount was removed and the persistent sample archive was verified
under the lease.

[Timings and controls](summary.json) · [Raw observations](observations.tgz) ·
[Procedures](procedures.tar.xz) · [Startup failure](initial-failure/failure-status.json) ·
[Space preflight](initial-failure/space-preflight.log).
