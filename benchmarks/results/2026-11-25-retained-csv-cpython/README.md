# B1705 Retained CSV runtime against CPython

This checkpoint measures `96d54066a` against stock CPython 3.13.16 on
November 25, 2026 UTC. The quiet machine is an Azure F4as_v7 running Ubuntu
24.04.4 with .NET 11 RC3. The source patch matches the frozen runtime.
The identity records all 32 measured image files and five complete CPython
goldens. Both quiet gates passed. All 280 batches verified their source,
input and expected outputs. Workers joined and the archive was verified
while the comparison held the machine lease.

Two processes per engine each contribute seven rotated batches per phase.
Each phase warms all four processes for five seconds each and targets
0.46 s per batch. The ranges divide Lython by CPython across all four
pairings of process medians. Async rows use Lython's `RunAsync` and the
same guest source and file APIs on CPython.

Warm and compile-to-result clocks include applicable RAM-backed I/O,
output and file verification, and result cleanup. Lython uses its mediated
host with ranged reads, append and shared memory admission. CPython uses
native file APIs. Compile-only includes disposal of compiled owners.
Fixture reset and protocol overhead sit outside the elapsed execution
clock and inside the wider process CPU clock. Startup and final process
teardown sit outside both.

| Workload | Phase | Lython | CPython | Elapsed ratio | Wider CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| File-fed CSV quantity map | Warm async | 0.66773–0.67879 ms | 1.36584–1.36624 ms | 0.489–0.497× | 0.507–0.517× |
| Ordinary generated definitions | Warm sync | 0.31710–0.31826 ms | 0.14767–0.14861 ms | 2.134–2.155× | 2.125–2.186× |
| Shipment callback report | Warm async | 2.62524–2.63519 ms | 1.61938–1.63009 ms | 1.610–1.627× | 1.602–1.634× |
| Defaulted rule pairs | Compilation | 0.02210–0.02213 ms | 0.04138–0.04167 ms | 0.530–0.535× | 0.529–0.546× |
| Quote-heavy invoice | Warm sync | 6.19140–6.21811 ms | 6.24889–6.35058 ms | 0.975–0.995× | 0.973–0.986× |
| Shipment callback report | Warm sync | 2.39440–2.39926 ms | 1.61504–1.64348 ms | 1.457–1.486× | 1.439–1.465× |
| Ordinary generated definitions | Compilation | 0.57164–0.57350 ms | 0.94477–0.94915 ms | 0.602–0.607× | 0.610–0.617× |
| Defaulted rule pairs | Compile to result | 0.91998–0.95816 ms | 0.73180–0.74067 ms | 1.242–1.309× | 1.262–1.308× |
| Shipment callback report | Compile to result | 2.55341–2.55451 ms | 1.90798–1.95218 ms | 1.308–1.339× | 1.298–1.335× |
| Ordinary generated definitions | Compile to result | 0.89955–0.91139 ms | 1.11883–1.12655 ms | 0.798–0.815× | 0.805–0.832× |

The 194-byte file-fed map reads 2,048 records from a 27,350-byte CSV and
sums one integer field through a guest helper. Shipment reporting uses the
same input fixture and runs more framing, binding and formatting work.
Ordinary definitions remain over twice as slow warm, yet reach the
first result sooner. Defaulted rules and shipment reporting remain slower
through result. Defaulted rules show a 4.15% Lython worker spread through
result. Invoice elapsed is within 2.6% of parity. These ratios are
diagnostic and describe independent phases on the recorded source.

[Identity](identity.json) · [Timings and spreads](summary.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz) ·
[Previous checkpoint](../2026-11-25-retained-async-cpython/README.md).
