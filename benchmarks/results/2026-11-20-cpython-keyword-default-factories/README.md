# B1184 annotated factories against CPython

The quiet Azure F4as_v7 ran stock CPython 3.13.16 and Lython at `605b3cd50`
with .NET 11 RC3 on Ubuntu 24.04.4 on November 20, 2026 UTC. The complete
frozen `848cbf783` image matches the measured revision in core, adapter,
tests and worker sources. Both quiet gates and all 140 timed batches passed.

| Workload | Boundary | Lython | CPython | Lython / CPython |
| --- | --- | ---: | ---: | ---: |
| Eager annotated rules | Warm | 1.434–1.491 ms | 0.421–0.429 ms | 3.34–3.54× |
| Eager annotated rules | Compile-to-result | 1.468–1.515 ms | 0.448–0.478 ms | 3.07–3.38× |
| Eager annotated rules | Compilation | 0.02141–0.02142 ms | 0.03213–0.03220 ms | 0.66–0.67× |
| Nested annotated factories | Warm | 2.054–2.063 ms | 0.588–0.595 ms | 3.45–3.51× |
| Nested annotated factories | Compile-to-result | 2.100–2.111 ms | 0.625–0.644 ms | 3.26–3.38× |
| Nested annotated factories | Compilation | 0.02587–0.02632 ms | 0.03905–0.03949 ms | 0.65–0.67× |
| File-backed CSV invoice | Warm | 6.793–6.815 ms | 6.441–6.457 ms | 1.05–1.06× |

Ratios cover all combinations of the two process medians per engine. The
eager compile-to-result CPython medians differ by 6.75%. The large factory
gap persists after including compilation. These external ratios cannot
isolate the CPU effect of the binder. The paired source comparison finds
materially less managed allocation and mixed CPU and elapsed timing.
[B1183 source comparison](../2026-11-20-prepared-keyword-default-binding/README.md).

Each factory script uses 2,048 inputs. The eager script creates 2,048
functions and the nested script creates 4,096. Invoice reads 127,031 bytes
and processes 4,096 data records. Warm timing includes execution, mediated
RAM-backed I/O where used, output checks and prior-result cleanup. Compilation
includes code-owner disposal. Compile-to-result includes compilation, one run
and code-owner disposal. Process startup, fixture reset and final teardown
are outside these clocks. Wider process CPU is recorded separately.

The original local capacity preflight stopped before staging. The successful
run kept the 168 MiB floor and retired only verified remote copies of an old
core already recorded in Git. Both engines verified every timed workload.
Workers joined, fixtures were removed and the archive verified under the lease.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Procedures and capacity checks](procedures.tar.xz).
