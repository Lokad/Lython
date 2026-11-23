# B1483 CPython checkpoint after direct factory construction

The November 23, 2026 UTC checkpoint compares Lython `9cea1fc0a`
with stock CPython 3.13.16. The measured `f2ac9fa89` image has identical
production and test sources. All 32 image files and the CPython
executable verify on the quiet Azure F4as_v7, Ubuntu 24.04.4 and .NET 11 RC3.

Ratios divide Lython elapsed by CPython elapsed. Ranges span all four
pairings of medians from two workers per engine. Each worker warms
for eight seconds and supplies seven rotated batches. Warm elapsed
includes applicable RAM-backed I/O, output and file verification
and prior-result disposal. Compilation includes code-owner disposal.
Compile to result includes both phases in running processes. Wider
process CPU includes reset and transport. Startup has its own checkpoint.

| Workload | Phase | Lython | CPython | Lython / CPython | Worker spread, Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Short nonlocal word report | Warm | 1.42788–1.45270 ms | 0.93998–0.94459 ms | 1.512–1.545× | 1.74 / 0.49% |
| Postponed nested factories | Warm | 0.86537–0.88860 ms | 0.53891–0.53896 ms | 1.606–1.649× | 2.68 / 0.01% |
| Configuration edit | Compilation | 0.01907–0.01922 ms | 0.03404–0.03434 ms | 0.555–0.564× | 0.76 / 0.86% |
| Quote-heavy CSV invoice | Warm | 6.89736–6.95100 ms | 6.31325–6.33021 ms | 1.090–1.101× | 0.78 / 0.27% |
| Postponed nested factories | Compile to result | 0.91711–0.92592 ms | 0.62028–0.62389 ms | 1.470–1.493× | 0.96 / 0.58% |

Postponed factories improve compared with B1480, where the warm ratio
was 1.731–1.811×. The controlled B1482 source pair supports the gain.
Configuration compilation and the invoice remain close to earlier
quiet runs. The unchanged nonlocal text report is slower than in
B1478 and its CPython elapsed overlaps. This diagnostic checkpoint
leaves the cause of that change unresolved.

The four independent stdout/file goldens and 140 timed batches verify
in 228 seconds. Both quiet gates pass. Workers join,
guest fixtures are removed, and the archive verifies under the lease.

[Summary](summary.json) · [Identity](identity.json) ·
[Raw observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz) ·
[Controlled source pair](../2026-11-23-closed-definition-return/README.md) ·
[Earlier phase comparison](../2026-11-23-factory-phase-cpython/README.md).
