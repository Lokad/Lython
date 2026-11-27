# B1906 Compare `a159f24f2` with CPython on the quiet VM

On November 27, 2026 UTC, the Azure F4as_v7 ran Ubuntu 24.04.4, .NET
11 RC3 and stock CPython 3.13.16. The frozen 32-file image is the
delivered B1905 candidate. Staging reuses all 32 files and uploads
384,256 bytes of catalog and tooling. The core source matches
`a159f24f2`. The protocol, fixtures and independent output and file
goldens retain their recorded hashes.

Two workers per engine warm independently for at least five seconds
and supply seven rotated batches per lane. Ratios divide Lython by
CPython and span every pair of worker medians. Warm execution includes
applicable mediated RAM-backed I/O, output and file verification, and
prior-result disposal. Compilation uses resident source and includes
retiring previous compiled storage. Compile-to-result adds script
execution and its I/O and result cleanup. Process CPU also includes
fixture reset and framing. CPython uses ordinary synchronous guest file
APIs with `-I -S -X utf8`. Marked async rows use Lython `RunAsync`.

| Workload | Phase | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Iterable shipment | Warm `RunAsync` | 2.40065–2.40647 ms | 1.96262–1.97993 ms | 1.212–1.226× | 1.223–1.237× |
| Nested JSON document | Warm | 0.77133–0.78416 ms | 0.67826–0.68660 ms | 1.123–1.156× | 1.076–1.119× |
| File-fed CSV quantity map | Warm | 0.39554–0.40183 ms | 1.36802–1.37413 ms | 0.288–0.294× | 0.302–0.315× |
| File search, first line | Warm | 0.06670–0.06676 ms | 0.05033–0.05209 ms | 1.280–1.326× | 0.987–1.037× |
| Generated ordinary definitions | Compile only | 0.50878–0.51710 ms | 0.94729–0.95570 ms | 0.532–0.546× | 0.536–0.549× |
| Iterable shipment | Warm | 2.11862–2.16291 ms | 1.97177–1.97204 ms | 1.074–1.097× | 1.076–1.110× |
| File search, line 1,025 | Warm | 0.07624–0.07629 ms | 0.13581–0.13637 ms | 0.559–0.562× | 0.636–0.638× |
| Configuration edit | Warm | 0.06962–0.07004 ms | 0.06173–0.06242 ms | 1.115–1.135× | 0.927–0.948× |
| Generated ordinary definitions | Compile to result | 0.70738–0.71531 ms | 1.12081–1.12491 ms | 0.629–0.638× | 0.636–0.651× |
| Configuration edit | Compile to result | 0.09658–0.09868 ms | 0.12451–0.12738 ms | 0.758–0.793× | 0.715–0.750× |

The first-line search remains about 15–16 µs behind in elapsed and spans
CPU parity. The later match includes the same one-line output write.
Generated definitions contain 96 definitions in 8,641 source bytes.
Configuration edit reads and rewrites a 181-byte JSON fixture.
The B1896 async shipment ratio was 1.315–1.373× on its earlier
`36ed51e1b` image. Several runtime paths changed between the two images.
Dense JSON elapsed and CPU are still higher than CPython.
[Earlier checkpoint](../2026-11-27-current-quiet-cpython/README.md) ·
[Matched B1905 comparison](../2026-11-27-generator-row-workspace/README.md).

## Fresh launch diagnostic

Seven configuration-edit pairs use the existing fresh-process runner.
Median launch-to-drain times are 259.0 ms for Lython and 32.06 ms for
CPython, a ratio of 8.08×. Lython launches first within each pair, and OS
caches stay warm. The clock includes process and worker initialization,
compilation, one correct run, I/O, in-process cleanup, process exit and
complete pipe drain. The supervisor validates the decoded replies after
the clock stops. The median describes the process boundary.

All seven workload goldens, 280 warmed or compile batches, 14 fresh
launches and both quiet gates pass. The measured procedure takes about
414 seconds. Workers join, the guest fixtures are removed and the
persistent archive verifies under the machine lease.

[Timing and CPU](summary.json) · [Fresh launch samples](fresh-summary.json) ·
[Images](identity.json) · [Observations](observations.tar.xz) ·
[Staging](stage-custody.json) · [Terminal receipt](terminal-status.json) ·
[Procedures](procedures.tar.xz).
