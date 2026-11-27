# B1815 Protected file search against CPython

Lython `c10f06884` and stock CPython 3.13.16 ran on November 27,
2026 UTC on the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3.
Two workers per engine warm for five seconds and supply seven rotated
batches targeting 460 ms. Ranges span all pairings of worker medians.
Ratios divide Lython elapsed by CPython elapsed. Above 1× means Lython
takes longer. [Worker spreads and CPU](summary.json).

| Workload | Input or source | Phase | Lython | CPython | Lython / CPython | Status |
| --- | --- | --- | ---: | ---: | ---: | --- |
| File search, match at line 1,025 | 42,010 B file | Warm sync | 0.08292–0.08345 ms | 0.13705–0.13762 ms | 0.603–0.609× | Diagnostic. |
| File search, match at line 1,025 | 42,010 B file | Warm `RunAsync` | 0.08385–0.08518 ms | 0.13787–0.13931 ms | 0.602–0.618× | Diagnostic. |
| File search, match at line 1,025 | 319 B of source | Compilation only | 0.01677–0.01698 ms | 0.03059–0.03078 ms | 0.545–0.555× | Diagnostic. |
| File search, match at line 1,025 | 42,010 B file | Compile-to-result | 0.11875–0.12018 ms | 0.20888–0.21198 ms | 0.560–0.575× | Diagnostic. |
| File search, match at line 1 | 42,010 B file | Warm sync | 0.07438–0.07471 ms | 0.05127–0.05131 ms | 1.450–1.457× | Diagnostic. |
| Unicode word-count report with a nonlocal helper | 3,072 lines / 35,840 B | Warm sync | 0.85825–0.92594 ms | 0.94030–0.94431 ms | 0.909–0.985× | Diagnostic. Lython worker spread is 7.89%. |

Warm execution reuses compiled code in a running process. Elapsed includes
applicable RAM-backed guest I/O, verified output and files, and disposal of
the previous result. Lython uses its mediated comparison host with shared
memory admission. CPython uses native file I/O and ordinary cyclic GC.
Async rows compare Lython `RunAsync` with the same synchronous CPython script.
Compile-only includes compiled code disposal. Compile-to-result adds a
synchronous execution and result cleanup. Startup, request framing and
fixture reset sit outside elapsed. The wider process CPU clock includes
fixture reset and request framing.

The file-search script, file size and output file are identical in the
first-line and late-match cases. The input places the matching line at
position 1 or 1,025 and standard output reports the scanned count. The
Unicode word-count report is unchanged from B1805. This checkpoint measures
CPython parity and elapsed gaps on the delivered source. The B1814 paired
Lython repeat directly investigates the change, and its early-match elapsed
increase remains open. [B1805](../2026-11-26-post-starmap-cpython/README.md) ·
[B1814](../2026-11-26-compiled-prefix-search/correction/README.md).

All three independent goldens, the pinned CPython executable and all 32
Lython image files verify. All 168 timed batches and both machine checks
pass. Workers join, guest fixtures are removed and the archived observations
verify. The VM retired two duplicate cores after verifying complete local
images, separate compressed core backups and published observation archives.
Only the delivered core needed upload. The frozen B1804 auxiliary files
matched and supplied the rest of the image.

[Identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Verification and procedures](procedures.tar.xz).
