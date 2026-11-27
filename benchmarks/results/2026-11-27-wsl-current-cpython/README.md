# B1880 Compare the current compiler and file workloads with CPython

The November 27 diagnostic uses `ab883268b`, .NET 11 RC3 and CPython
3.13.16 on a pinned Intel i7-14700KF logical CPU under Ubuntu WSL. The
CPython executable matches the previous quiet Azure baseline by checksum.
The relocated standard-library and native-module files match their
captured archive. Four independent goldens and the complete 32-file Lython
image verify. The VM has insufficient persistent working space for a new
quiet campaign.

Each worker contributes seven batches, with elapsed normalized by completed
commands before taking its median. The ranges below span worker medians.
Warm execution uses compiled scripts
inside running processes and includes applicable RAM-backed I/O, output
and file verification, and previous-result disposal. Compile-only includes
compiled-owner disposal. Compile to result adds one execution, verification
and cleanup. Process CPU includes worker framing and fixture reset. Startup
lies outside these lanes. The CPython worker executes the same synchronous
script for both Lython hosting modes.

| Workload | Phase | Lython | CPython | Lython / CPython |
| --- | --- | ---: | ---: | ---: |
| CSV `DictWriter` re-export, 2,048 records | Warm | 1.23204–1.32887 ms | 2.66358–2.79263 ms | 0.441–0.499× |
| Default JSON Lines export, 768 records | Warm `RunAsync` | 1.18873–1.18971 ms | 1.38734–1.46039 ms | 0.814–0.858× |
| Generated ordinary definitions, 96 declarations | Compile | 0.63372–0.69575 ms | 1.04639–1.13268 ms | 0.559–0.665× |
| Unicode word count, 3,072 lines | Warm | 0.81928–0.85916 ms | 1.00724–1.03890 ms | 0.789–0.853× |
| Default JSON Lines export, 768 records | Warm | 1.28677–1.40359 ms | 1.86282–2.04698 ms | 0.629–0.753× |
| Generated ordinary definitions, 96 declarations | Compile to result | 0.82209–0.84757 ms | 1.14554–1.22822 ms | 0.669–0.740× |

These are local diagnostics. Lython worker spread reaches 9.79% on
compilation. In the first run, CPython's two unchanged JSON execution lanes
vary across their separate worker groups. The paired follow-up shares
processes and interleaves the APIs to examine that drift.

## Paired JSON follow-up

The exact Lython and CPython images share one group of workers. Each
worker warms both lanes. Timed batches rotate the engine and hosting
lane. CPython's median ranges now overlap between the two labels.

| Lython API | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| `Run` | 1.19990–1.30230 ms | 1.47476–1.54651 ms | 0.776–0.883× |
| `RunAsync` | 1.25198–1.32638 ms | 1.49664–1.55189 ms | 0.807–0.886× |

Both API ranges are below parity for this export. The Lython ranges overlap
between APIs. All 168 initial and 56 paired timed batches verify. Workers
join, RAM fixtures are removed and persistent archives verify.

[Initial timing](summary.json) · [Image identity](identity.json) ·
[Initial observations](observations.tar.xz) · [Verification and procedures](procedures.tar.xz) ·
[Paired timing](paired-summary.json) · [Paired observations](paired-observations.tar.xz) ·
[Paired verification](paired-procedures.tar.xz).
