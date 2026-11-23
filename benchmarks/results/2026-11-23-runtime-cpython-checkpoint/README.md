# B1451 Retained CSV and text runtime against CPython

Measured `dfabddb88` against stock CPython 3.13.16 on November 23,
2026 UTC. The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3.
The core includes borrowed native CSV line windows and reusable
private word admission. The CPython executable, protocol and 31
auxiliary image files match B1443.

| Workload | Boundary | Lython | CPython | Lython / CPython | Worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: |
| Postponed factories | Warm | 0.9783–0.9953 ms | 0.5353–0.5515 ms | 1.774–1.859× | 1.74 / 3.02% |
| Generated validator with read-only branches | Compile | 0.4757–0.4924 ms | 0.9257–0.9317 ms | 0.511–0.532× | 3.53 / 0.65% |
| Native typed ASCII CSV meter | Warm | 2.970–2.996 ms | 2.536–2.590 ms | 1.147–1.181× | 0.86 / 2.13% |
| Nonlocal Unicode word count | Warm | 1.326–1.364 ms | 0.9371–0.9486 ms | 1.398–1.455× | 2.87 / 1.23% |
| Sparse generated validator | Compile to result | 1.535–1.565 ms | 1.9741–1.9742 ms | 0.777–0.793× | 1.97 / <0.01% |
| Quote-heavy CSV policy invoice | Warm | 7.113–7.119 ms | 6.318–6.343 ms | 1.121–1.127× | 0.09 / 0.40% |
| Postponed factories | Compile to result | 1.059–1.061 ms | 0.5852–0.5907 ms | 1.793–1.814× | 0.21 / 0.95% |
| Expanded JSON Lines export | Warm | 5.376–5.567 ms | 6.556–6.558 ms | 0.820–0.849× | 3.56 / 0.04% |

Two fresh workers per engine supply medians after eight seconds of
warmup and seven rotated batches. Ratios divide Lython elapsed by
CPython elapsed across all four pairings. Warm execution includes
applicable RAM-backed file operations, output verification and
previous-result disposal. Lython uses efficient host-mediated file
operations with shared admission active. CPython uses native file
operations. Compilation includes code-owner disposal. Compile to result
adds one execution and applicable cleanup. Wider process CPU includes
fixture resets and protocol. Startup and final teardown are measured
separately.

The factory remains the largest warm and first-result gap among these
cases. The nonlocal report's Lython spread falls from 13.77% at B1443
to 2.87% here. The native meter's elapsed ratio is lower and the quoted
invoice's ratio is higher than at B1443. These cross-checkpoint
observations include worker and reference variation. The matched
Lython source-pair tests establish the individual CSV and text effects.
[B1443](../2026-11-23-branch-cpython-checkpoint/README.md) ·
[CSV source pair](../2026-11-23-borrowed-native-csv-window/README.md) ·
[Text source pair](../2026-11-23-reusable-private-word-admission/README.md).

Eight cases verify against independent CPython goldens on the frozen
local image. All eight quiet lanes and 224 timed batches verify in
423 seconds after the first quiet gate. All workers join, both quiet
gates pass and the persistent archive verifies under the lease after
guest fixture cleanup. These are diagnostic observations.

## Fresh process

| Workload | Lython median | CPython median | Lython / CPython |
| --- | ---: | ---: | ---: |
| Empty script | 122.1 ms | 29.6 ms | 4.12× |
| Tiny arithmetic | 150.7 ms | 29.9 ms | 5.03× |
| Configuration edit / 181 B JSON | 278.0 ms | 32.2 ms | 8.62× |

The existing fresh-process smoke measures parent-observed launch through
pipe drain and exit. The clock includes worker initialization, compilation,
one execution, applicable RAM-backed I/O and in-worker cleanup. Validation
of the response by the parent comes afterward. The OS file caches stay warm.
Ratios divide the medians of seven separate starts per engine. Case order
rotates, and the existing smoke runs Lython first within each pair. The
controls expose launch overhead. Their timings are included as observed.
These diagnostic numbers measure the entire comparison worker path.
Independent attribution would be needed for .NET startup, harness work,
library initialization and first execution.

All 42 starts match stdout and file goldens on the same core, protocol and
CPython executable as the warm and compile lanes. Both quiet checks pass
and the fixture mount is removed. The configuration edit ranges from
276.0 to 287.0 ms in Lython and 31.9 to 32.6 ms in CPython.
[Fresh-process summary](startup-summary.json) ·
[Raw startup observations](startup-observations.tar.xz) ·
[Startup procedures](startup-procedures.tar.xz) ·
[Startup terminal evidence](startup-terminal.json).

[Source and images](source.json) · [Timing and CPU](summary.json) ·
[Raw observations](observations.tgz) · [Procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json) ·
[Release qualification](https://github.com/Lokad/Lython/actions/runs/45122987541).
