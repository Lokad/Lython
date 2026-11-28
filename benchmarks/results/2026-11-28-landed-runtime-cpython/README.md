# B1994 CPython comparison for the landed runtime

Lython `8633debf8` and stock CPython 3.13.16 ran on November 28, 2026 UTC
on a four-vCPU Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3. The
frozen worker includes the optional adapter and signed PythonRe 0.5.0.

| Workload | Input | Boundary | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | --- | ---: | ---: | ---: | ---: |
| Iterable-row shipment | 2,048 records / 27,350 B | Warm `RunAsync` | 2.04289–2.07681 ms | 1.93651–1.96239 ms | 1.041–1.072× | 1.041–1.081× |
| Compiled-pattern regex log search | 3,072 lines / 86,954 B | Warm `Run` | 2.67861–2.70232 ms | 0.95203–0.98192 ms | 2.728–2.838× | 2.634–2.688× |
| Module-level regex log search | 3,072 lines / 86,954 B | Warm `Run` | 2.88147–2.94182 ms | 1.58949–1.60682 ms | 1.793–1.851× | 1.757–1.816× |
| Configuration edit | 357 B source / 181 B input | Sync compile to result | 0.09458–0.09531 ms | 0.12166–0.12338 ms | 0.767–0.783× | 0.724–0.753× |

Ratios divide Lython by CPython. Two workers per engine warm for at least
five seconds and contribute seven rotated batches each. Ranges span all
pairings of worker medians. Elapsed includes applicable RAM-backed I/O,
result and file verification, and previous-result disposal. Process CPU
also includes fixture reset and request framing. The configuration lane
includes compilation and compiled-owner disposal. Both clocks start after
process launch. Lython uses mediated I/O and borrowed memory admission.
CPython uses its native file APIs and runs with `-I -S -X utf8`.

| Workload | Lython elapsed worker spread | CPython elapsed worker spread |
| --- | ---: | ---: |
| Iterable-row shipment | 1.66% | 1.34% |
| Compiled-pattern regex log search | 0.88% | 3.14% |
| Module-level regex log search | 2.09% | 1.09% |
| Configuration edit | 0.77% | 1.42% |

The shipment ratio increased from 1.024–1.028× at B1981. B1993's paired
shipment comparison spans zero for CPU and elapsed. The separate CPython
collections leave the cause of the ratio change uncertain. The two regex
gaps remain the largest on this set. Fresh-process results use a separate
clock. [B1981](../2026-11-28-current-iterable-cpython/README.md) ·
[B1993](../2026-11-28-generator-construction-transport/README.md) ·
[Fresh-process comparison](../2026-11-28-current-fresh-command-cpython/README.md).

All four independent CPython output and file goldens verify locally and on
the VM. Staging reuses the 32 verified B1993 worker files. The collector
completes all 112 timed batches and both quiet gates. The workers join,
the RAM-backed fixtures are removed and the persistent archive verifies.

[Timing summary](summary.json) · [Worker identity](identity.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal status](terminal-status.json).
