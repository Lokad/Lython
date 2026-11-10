# R209 — completed construction checkpoint against CPython

Measured 2026-11-10 on Azure F4as_v7, Ubuntu 24.04.4, four EPYC 9V45 vCPUs,
16 GiB/no swap; stock .NET 11 RC3 and isolated CPython 3.13.16. Committed source
**e543bf159d1d037348c95083b6f511a8339e822b**, fresh forced postcommit core
**0e2bc55bac5ebfafd9d30014e34e2afb3e9776bbe6b5d83a53cc710c0d99c492**,
assembly 0.11.0. All 568 production source hashes match the commit; see
[source identity](source-identity.json). The source subsequently completed
[Windows/Ubuntu, both targets, original stress and installed Release consumers](delivery-ci.json).
The original identities accurately record that CI was pending at measurement time.

Six existing CSV/text jobs complete in **164.735 seconds**. Four independent roles
per job, five-second warmups and seven rotated 150 ms rounds, ordinary GC/tiering.
All identity, full output, ownership, quiet, join and archive checks pass.
[Raw evidence](observations.tgz), [medians/CPU/IQR/all pairings](summary.json),
[terminal receipt](terminal-status.json), [preparation and frozen core](preparation.tar.xz).
Worker `fec79afd`, protocol and optional adapter 0.4 are unchanged.

| Complete workload / phase | Lython | CPython | Lython / CPython | Status; control / max IQR |
| --- | ---: | ---: | ---: | --- |
| CSV validation/helpers / compile | 71.1–71.3 µs | 65.7–66.2 µs | 1.07–1.09× | Variable; L0.25%, P0.81% / 4.73% |
| CSV validation/helpers / warm | 4.526–4.666 ms | 4.342–4.355 ms | 1.04–1.07× | Variable; L3.11%, P0.29% / 1.45% |
| CSV validation/helpers / compile-run | 4.665–4.728 ms | 4.525–4.550 ms | 1.03–1.04× | Diagnostic; L1.34%, P0.55% / 0.52% |
| Text summary/helpers / compile | 44.7–45.3 µs | 41.2–41.3 µs | 1.08–1.10× | Diagnostic; L1.30%, P0.13% / 2.14% |
| Text summary/helpers / warm | 1.636–1.729 ms | 1.176–1.199 ms | 1.36–1.47× | Variable; L5.42%, P1.97% / 1.21% |
| Text summary/helpers / compile-run | 1.847–1.874 ms | 1.324–1.340 ms | 1.38–1.42× | Diagnostic; L1.43%, P1.25% / 2.44% |

CSV input: 5,120 records / 68,625 B; text: 3,072 lines / 35,840 B. Ranges span
two process medians per engine; ratios include all four pairings, not confidence
intervals. Warm CSV Lython control spread 3.11% and warm text 5.42% limit precision.
Text remains roughly 1.4× CPython. Compilation is about 1.07–1.10× for these scripts.
This is a useful state measurement, not attribution to one optimization, proof of
CPU equivalence or resolution of the original tuple-construction adversity.

Warm and compile-to-result include mediated RAM-file I/O, full per-invocation
stdout/file checks and prior-result cleanup. Compile-to-result adds compilation
inside an already-running host; compile measures construction/disposal. Fixture
staging/reset, separate controller verification requests, protocol, startup and
final cache teardown are excluded. External CPU includes additional reset/protocol
work and coarse ticks. Independent phase medians need not add. Pending-I/O latency,
RSS and embedding tail latency remain unmeasured.

Preparation initially refused before deletion, root creation, staging or any job:
verified disk headroom was short by 24,576 B. The correction retired one additional
exact canonical raw duplicate under the idle lease, retaining all timing settings
and the fixed 240 MiB floor / 12 MiB setup headroom. Both launch conditions survive
in preparation. The inherited controller print says “nine-job”; actual payload,
raw records and terminal receipt consistently contain six jobs. No timing was rerun.

All users joined; RAM fixtures removed; archive verified under the owned lease and
again locally (SHA 1a1f33f2eccc8df3936dbf5b250016a4d0db137ecc0cd723842a6d2aaa9758ce).
Terminal state is cached; no remaining VM work. The preparation's boundary note
also corrects earlier ambiguous R207 prose: full timed validation was included;
separate controller verification was excluded. No raw clocks or ratios changed.
The [prior guide](prior-BENCHMARKS.md) preserves the replaced source-specific rows.
Publication remains postponed; the broader performance and independent regex tracks continue.
