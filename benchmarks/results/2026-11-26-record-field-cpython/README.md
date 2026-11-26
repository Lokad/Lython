# B1760 Shipment and CSV conversion against CPython

Lython `bc93d4d74` and stock CPython 3.13.16 ran on the quiet Azure F4as_v7
with Ubuntu 24.04.4 and .NET 11 RC3 on November 26, 2026 UTC. Four workers
per phase supply seven rotated batches each, with two workers per engine.
Ratios divide Lython elapsed by CPython elapsed and span the four pairings
of worker medians. Values above 1× mean Lython took longer.

| Workload | Input or source | Phase | Lython | CPython | Elapsed ratio |
| --- | ---: | --- | ---: | ---: | ---: |
| Shipment callback report | 2,048 rows, 27,350 B | Warm sync | 2.12759–2.23394 ms | 1.62084–1.63259 ms | 1.303–1.378× |
| Shipment callback report | 2,048 rows, 27,350 B | Warm `RunAsync` | 2.22429–2.25398 ms | 1.62762–1.63293 ms | 1.362–1.385× |
| Shipment callback report | 1,376 B of source | Compilation only | 0.06267–0.06401 ms | 0.11348–0.11376 ms | 0.551–0.564× |
| Shipment callback report | 1,376 B of source, 2,048 rows | Compile-to-result | 2.27511–2.33693 ms | 1.89620–1.92228 ms | 1.184–1.232× |
| File-fed integer quantity map | 2,048 rows, 27,350 B | Warm `RunAsync` | 0.45152–0.48065 ms | 1.36416–1.36826 ms | 0.330–0.352× |
| CSV meter with a float field helper | 4,096 rows, 139,299 B | Warm sync | 4.25497–4.31230 ms | 5.27953–5.28402 ms | 0.805–0.817× |

Elapsed includes applicable RAM-backed reads and writes, output and file
checks, guest cleanup and result disposal. Warm phases reuse compiled code.
Compile-to-result uses the synchronous execution API and disposes the new
compiled owner on every invocation. Compilation only includes replacement
and prior-owner cleanup, with the final code owner retained outside the
clock. Startup, fixture reset and request framing are outside the elapsed
clock. Wider process CPU includes fixture reset and framing. Lython uses a
mediated file host and shared memory accounting. CPython uses native file I/O
with ordinary cyclic GC and its GIL enabled.

Shipment's warm execution is slower even though its standalone compilation
is faster. Its wider process CPU ratios are 1.268–1.360× warm sync,
1.364–1.390× warm async and 1.187–1.215× through result. The file-fed and
float-helper workloads are faster on Lython in this diagnostic. Same-source
Lython workers differ by 6.45% on file-fed map and 5.00% on warm sync shipment.
Use the separately paired source comparison for change attribution.
[B1759 native field returns](../2026-11-26-direct-record-field-returns/README.md) ·
[Earlier external checkpoint](../2026-11-26-file-processing-cpython/README.md).

The initial launch stopped before fixture setup because the run manifest
omitted the frozen-image path. The retry checked the collector's required
manifest keys, the full 32-file image, the pinned CPython executable, and the
actual catalog against the comparison workers. It added warm sync and
compile-only shipment phases. Both quiet gates, all 168 timed batches,
independent output and file goldens, worker joins and persistent archive
verification passed. The first launch and both manifests are preserved.

[Summary](summary.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Procedures](procedures.tar.xz).
