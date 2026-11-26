# B1805 Current script comparison against CPython

Lython `cb258fe7f` and stock CPython 3.13.16 ran on November 26,
2026 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per engine warm for five seconds and supply seven rotated
batches targeting 460 ms. Ranges span all pairings of worker medians.
Ratios divide Lython elapsed by CPython elapsed. Above 1× means Lython
takes longer. [Worker spreads and CPU](summary.json).

| Workload | Input or source | Phase | Lython | CPython | Lython / CPython | Status |
| --- | --- | --- | ---: | ---: | ---: | --- |
| JSON Lines summary with record helper | 5,120 lines / 110,592 B | Warm sync | 3.12574–3.13816 ms | 4.15785–4.16446 ms | 0.751–0.755× | Diagnostic. |
| JSON Lines summary with record helper | 5,120 lines / 110,592 B | Warm `RunAsync` | 3.21453–3.23051 ms | 4.13075–4.14701 ms | 0.775–0.782× | Diagnostic. |
| Five-item `starmap` shipment report | 2,048 records / 27,350 B | Warm `RunAsync` | 2.60249–2.80150 ms | 1.81483–1.84158 ms | 1.413–1.544× | Diagnostic. Lython worker spread is 7.65%. |
| Unicode word-count report with a nonlocal helper | 3,072 lines / 35,840 B | Warm sync | 0.83407–0.84667 ms | 0.94136–0.96632 ms | 0.863–0.899× | Diagnostic. |
| Generated ordinary definitions | 8,641 B of source | Compilation only | 0.56925–0.57157 ms | 1.10194–1.10238 ms | 0.516–0.519× | Diagnostic. |
| Generated ordinary definitions | 96 definitions, built 16 times | Compile-to-result | 0.75729–0.76925 ms | 1.11806–1.12177 ms | 0.675–0.688× | Diagnostic. |

Warm execution reuses compiled code in a running process. Elapsed includes
applicable RAM-backed guest I/O, verified output and files, and disposal of
the previous result. Lython uses its mediated comparison host with shared
memory admission. CPython uses native file I/O and ordinary cyclic GC.
Async rows compare Lython `RunAsync` with the same synchronous CPython script.
Compile-only includes compiled code disposal. Compile-to-result adds a
synchronous execution and result cleanup. Startup, request framing and
fixture reset sit outside elapsed. The wider process CPU clock includes
fixture reset and request framing.

The JSON Lines and five-item shipment cases come from the verified
[B1802](../2026-11-26-trim-truth-without-result/README.md) and
[B1804](../2026-11-26-starmap-frame-inputs/quiet/README.md) experiments.
The text and ordinary definitions cases retain the same sources and
fixtures as [B1791](../2026-11-26-post-ascii-cpython/README.md). Separate
checkpoints use independent workers. The paired Lython experiments
attribute source changes more directly than the external checkpoint.

All four independent goldens, the pinned CPython executable and all
32 Lython image files verify. All 168 timed batches and both machine
checks pass. Workers join, guest fixtures are removed, and the archived
observations verify. Staging reuses the verified B1804 candidate image.

[Identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Verification and procedures](procedures.tar.xz).
