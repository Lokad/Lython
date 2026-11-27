# B1878 Check the current checkpoint against CPython on Windows

The November 27 short diagnostic compared `18c82c45f` and `6e59cd41e`
with CPython 3.13.2 on Windows 11 build 26300 and an Intel i7-14700KF.
Lython used .NET 11 RC3. Both complete 32-file images and four independent
CPython goldens verified. The quiet Azure VM had 4.4 MB of working space,
so this check ran locally.

Warm elapsed includes applicable file I/O through the comparison host,
output and file verification, and prior-result disposal. These Windows
fixtures reside on the local filesystem. Compile-only includes compiled
owner disposal. Compile to result adds one run, output verification and
cleanup. Startup lies outside these lanes. The short tool warms each worker
for one second and collects seven batches per engine.

| Workload | Lane | Earlier Lython median | Current Lython median | CPython median | Current Lython IQR / median |
| --- | --- | ---: | ---: | ---: | ---: |
| CSV `DictWriter` re-export | Warm | 31.321 ms | 20.633 ms | 21.547 ms | 9.6% |
| Default JSON Lines export | Warm `RunAsync` | 33.771 ms | 22.005 ms | 16.998 ms | 81.8% |
| Generated ordinary definitions | Compile | 1.563 ms | 1.499 ms | 2.503 ms | 34.6% |
| Unicode word-count report | Warm | 10.461 ms | 14.905 ms | 8.959 ms | 52.4% |
| Default JSON Lines export | Warm | 27.466 ms | 18.783 ms | 15.391 ms | 37.1% |
| Generated ordinary definitions | Compile to result | 4.857 ms | 10.495 ms | 2.476 ms | 14.7% |

All 126 measured batches verified. The spread leaves the Windows
comparison inconclusive. The earlier Lython re-export has 109.7% IQR
relative to its median. Preserve the adverse definition and text readings
and keep the primary CPython table anchored to its original quiet runs.

## Exact-image WSL follow-up

The same Lython images ran under Ubuntu WSL, pinned to one logical CPU.
Two workers per image warmed for five seconds and each supplied seven
rotated batches. Ranges span all worker median pairings. Elapsed uses the
same lane boundaries. Wider process CPU also covers reset and framing.
Guest file fixtures use `/dev/shm`. The follow-up compares the two Lython revisions.

| Workload | Lane | Elapsed change | Process CPU change |
| --- | --- | ---: | ---: |
| Generated ordinary definitions | Compile to result | −10.75% to −6.05% | −6.10% to −3.16% |
| Unicode word-count report | Warm | −9.31% to +8.38% | −8.91% to +5.95% |
| Generated ordinary definitions | Compile | −4.16% to −2.29% | −5.24% to −3.29% |
| Generated ordinary definitions | Warm | −5.49% to +2.78% | −3.46% to +3.07% |

WSL shows lower compile and compile-to-result CPU for the current image.
Warm definitions and text span zero. Windows timing remains unresolved.
All 112 batches verify, workers join and the persistent archive verifies.

[Windows timing](windows-summary.json) · [WSL timing](wsl-summary.json) ·
[Image identity](identity.json) · [WSL observations](wsl-observations.tar.xz) ·
[Receipts and procedures](procedures.tar.xz).
