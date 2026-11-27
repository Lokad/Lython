# B1823 Shipment generator handoff against CPython

Lython `489fde50e` and stock CPython 3.13.16 ran on November 27, 2026 UTC
on the quiet Azure F4as_v7 running Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per engine warm for five seconds and contribute seven
rotated batches per lane. Ranges span all pairings of worker medians.
Ratios divide Lython elapsed by CPython elapsed. Above 1× means longer
elapsed. These are diagnostic comparisons. [Medians and CPU](summary.json).

| Workload | Input | Phase | Lython | CPython | Lython / CPython |
| --- | --- | --- | ---: | ---: | ---: |
| Five-field shipment | 2,048 records / 27,350 B | Warm sync | 2.16876–2.22905 ms | 1.83627–1.84352 ms | 1.176–1.214× |
| Three-field shipment | 2,048 records / 27,350 B | Warm RunAsync | 2.45376–2.46541 ms | 1.77269–1.80375 ms | 1.360–1.391× |
| Bound zip shipment | 2,048 records / 27,350 B | Warm RunAsync | 2.02734–2.02978 ms | 1.67217–1.67259 ms | 1.212–1.214× |
| Five-field shipment | 2,048 records / 27,350 B | Warm RunAsync | 2.46637–2.46954 ms | 1.80589–1.81313 ms | 1.360–1.367× |
| File-fed quantity map | 2,048 records / 27,350 B | Warm RunAsync | 0.44933–0.45386 ms | 1.36115–1.36441 ms | 0.329–0.333× |
| Five-field shipment | 2,048 records / 27,350 B | Compile-to-result | 2.39930–2.44152 ms | 2.14403–2.14737 ms | 1.117–1.139× |

Warm execution reuses compiled code in a running worker. Elapsed includes
mediated RAM-backed file reads, verified output and files, and prior-result
disposal. CPython uses native file I/O and its usual cyclic GC. Async rows
use the same synchronous guest script, with Lython's `RunAsync` API. The
compile-to-result lane adds compilation and compiled-owner disposal.
Process startup, fixture reset and request framing sit outside elapsed.
The wider process CPU clock includes fixture reset and framing.

The five-field shipment takes 1.360–1.367× CPython elapsed in the
warm async lane and 1.117–1.139× through result. The file-fed map takes
0.329–0.333×. The bound zip script uses a different row source from the two
list-yield scripts. These comparisons describe the delivered source and
case mix. [The paired Lython experiment](../2026-11-27-starmap-slot-handoff/README.md)
measures the source change directly.

All four independent goldens and the complete frozen image verify on
both workers. All 168 batches and both quiet gates pass. Workers join,
guest fixtures are removed and the persistent archive verifies under the
lease. Staging streamed the new core and metadata directly to their
persistent paths. It recovered space from one inactive duplicate core
after verifying the complete local image, compressed backup and published
observation archive. The unchanged 31 auxiliary files matched the
existing frozen image.

[Image identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Verification and custody](procedures.tar.xz).
