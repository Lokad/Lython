# B1208 Captured factory delivery against CPython

Measures production `ef171a35b` with the source-matched `7f583edfa`
core against stock CPython 3.13.16. The quiet Azure F4as_v7 ran
Ubuntu 24.04.4 and .NET 11 RC3 on 2026-11-20 UTC. Each workload used two
processes per engine and five rotated rounds targeting 0.4-second batches.
Ranges cover the four pairings of process medians. The ratio is Lython
elapsed divided by CPython elapsed. IQR values describe the widest
within-process spread relative to its median.

| Workload | Boundary | Lython | CPython | Ratio | Measurement controls |
| --- | --- | ---: | ---: | ---: | --- |
| Nested annotated factories | Warm execution | 1.386–1.498 ms | 0.595–0.607 ms | 2.28–2.52× | Controls L/C 8.04 / 2.04%. IQR ≤2.57%. |
| Nested annotated factories | Compilation only | 0.02403–0.02431 ms | 0.04001–0.04029 ms | 0.596–0.608× | Controls L/C 1.16 / 0.71%. IQR ≤1.45%. |
| Nested annotated factories | Compile-to-result | 1.410–1.530 ms | 0.638–0.642 ms | 2.20–2.40× | Controls L/C 8.50 / 0.57%. IQR ≤1.38%. |
| CSV invoice | Warm execution | 6.693–6.709 ms | 6.385–6.406 ms | 1.04–1.05× | Controls L/C 0.24 / 0.33%. IQR ≤1.31%. |

Nested factories define 4,096 functions across 2,048 inputs. The
invoice processes 4,096 data records from 127,031 input bytes. Warm
timing includes mediated RAM-backed I/O where applicable, golden checks
and prior-result cleanup. Compilation includes code-owner cleanup.
Compile-to-result includes compilation and one execution. Process
startup, fixture resetting and final teardown are separate.

Nested warm and compile-to-result remain more than twice CPython's
elapsed. The two Lython processes differ by 8.04% warm and 8.50%
compile-to-result, so the ratio is a diagnostic range. The nested warm
range is lower than the 2.99–3.05× observed at the prior checkpoint.
Use the independent [B1207 source-pair measurement](../2026-11-20-inline-captured-factories/refinement/README.md)
to attribute gains to the implementation. The invoice remains near
1.05× with closely matched controls.

Both image hashes and exact output/file goldens were verified. All
80 timed batches passed in 144 seconds of collection. The workers
joined, the persistent archive was verified under the VM lease, and
the guest fixtures were removed. The local copy matches its remote
archive hash.

[Timings and controls](summary.json) · [Raw observations](observations.tgz) ·
[Source and procedures](procedures.tar.xz) ·
[Earlier external checkpoint](../2026-11-20-cpython-inline-lexical-definitions/README.md).
