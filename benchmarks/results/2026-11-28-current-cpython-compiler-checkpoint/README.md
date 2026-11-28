# B1926 Compare the compiler checkpoint with CPython

The Azure F4as_v7 ran Ubuntu 24.04.4, .NET 11 RC3 and stock CPython 3.13.16
on November 28, 2026 UTC. The 32-file Lython image matches committed
`486b6bb60`. All five independent output and file goldens, 168 timed
batches and both quiet gates passed. The workers joined and the persistent
archive verified after fixture removal.

Two workers per engine warm for at least five seconds and supply seven
rotated batches. Ratios span all pairings of worker medians. Warm elapsed
includes applicable RAM-backed I/O, output and file verification, and
previous-result disposal. Compile-only uses resident source and includes
prior compiled-owner disposal. Compile-to-result adds execution, mediated
I/O and result cleanup. Process CPU also includes fixture reset and framing.
Startup sits outside these lanes. CPython uses its synchronous file APIs and
runs with `-I -S -X utf8`.

| Workload | Boundary | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Iterable-row shipment | Warm RunAsync | 2.19451–2.24686 ms | 1.95501–1.96270 ms | 1.118–1.149× | 1.112–1.135× |
| Generated ordinary definitions | Compile only | 0.49597–0.50051 ms | 1.09063–1.09859 ms | 0.451–0.459× | 0.452–0.462× |
| Iterable-row shipment | Warm | 1.98382–1.98854 ms | 1.94462–1.96143 ms | 1.011–1.023× | 1.017–1.034× |
| Nested JSON document summary | Warm RunAsync | 0.64055–0.64199 ms | 0.67123–0.67802 ms | 0.945–0.956× | 0.978–0.995× |
| File search, first line | Warm | 0.06664–0.06685 ms | 0.05021–0.05086 ms | 1.310–1.331× | 1.016–1.033× |
| Configuration edit | Compile to result | 0.09525–0.09653 ms | 0.12228–0.12304 ms | 0.774–0.789× | 0.730–0.747× |

## Matched compiler repeat

The earlier [B1925](../2026-11-28-direct-lexical-binding-slots/README.md)
comparison saw higher CPU on the function-free control. The repeat uses
the identical B1925 reference and candidate images in Ubuntu WSL, reversing
placement and measuring three lanes. Both images match their four CPython
goldens and all 84 batches pass. The VM's available persistent disk covers
the external comparison. This matched repeat runs separately in WSL.

| Workload | Boundary | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Function-free configuration | Compile only | −1.43% to −0.03% | −2.12% to +0.47% |
| Ordinary definitions | Compile only | −1.03% to +2.15% | −4.08% to +2.08% |
| Escaping definitions | Compile to result | −3.66% to +3.93% | −3.16% to +1.29% |

The earlier control CPU increase spans zero on repeat. The two definition
workloads also span zero on repeat. Retain the confirmed allocation
savings and both timing runs. One additional local launch-to-drain smoke
verifies the configuration edit through the existing fresh worker on
both engines. It establishes correctness for one launch per engine.

[Quiet timing](summary.json) · [Matched timing](matched-compiler/summary.json) ·
[Quiet image](identity.json) · [Matched images](matched-compiler/identity.json) ·
[Quiet observations](observations.tar.xz) · [Matched observations](matched-compiler/observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz).
