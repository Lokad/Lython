# B1954 Quiet shipment and report comparison after loop consolidation

Committed Lython `2806d7d75` and stock CPython 3.13.16 ran on November 28,
2026 UTC on the four-vCPU Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11
RC3. Both quiet gates and all 168 timed batches pass. The 32-file Lython
image and five independent output and file goldens verify locally and on
the VM.

| Warm workload | Lython API | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Ordinary helper and tuple-generator reduction | `Run` | 2.56272–2.58353 ms | 1.97420–1.98738 ms | 1.289–1.309× | 1.275–1.323× |
| Ordinary helper and tuple-generator reduction | `RunAsync` | 2.86833–2.91178 ms | 1.97384–1.97935 ms | 1.449–1.475× | 1.427–1.475× |
| Iterable shipment | `RunAsync` | 2.17587–2.18321 ms | 1.92753–1.94365 ms | 1.119–1.133× | 1.129–1.144× |
| Repeated explicit tuple loop | `RunAsync` | 1.09815–1.12434 ms | 1.58768–1.59349 ms | 0.689–0.708× | 0.705–0.720× |
| Markdown report from JSON | `RunAsync` | 2.68877–2.71743 ms | 2.33718–2.59956 ms | 1.034–1.163× | 1.066–1.199× |
| File-fed CSV quantity map | `RunAsync` | 0.42935–0.45132 ms | 1.36488–1.36769 ms | 0.314–0.331× | 0.345–0.360× |

Ratios divide Lython by CPython. Each range spans all pairings of the
two worker medians per engine. The report uses 3,072 records in a
189,441 B JSON file. All other scripts use 2,048 rows from a 27,350 B CSV.
The explicit-loop variant keeps the ordinary reducer's input and report
and replaces each two-key generator reduction with a direct loop.

Warm elapsed includes applicable RAM-backed I/O, output and file checks,
and previous-result disposal. Process CPU adds fixture reset and framing.
Compilation and startup are outside both clocks. Lython uses the mediated
host with shared admission. CPython uses native file access and its stock
interpreter with `-I -S -X utf8`.

Ordinary async generator reduction has the largest stable warm gap in
these lanes. The explicit-loop script is faster than CPython on the same
input, making the generator lifecycle and reduction transport candidates
for closer inspection. The report's CPython elapsed medians differ by
11.23%. Its precise gap is inconclusive. The file-fed map has 5.12%
Lython elapsed spread. The earlier matched control remains a separate
measurement. [B1943](../2026-11-28-cpython-sequence-iterator-checkpoint/README.md) ·
[Matched control](../2026-11-28-sum-source-selection/baseline/README.md).

The first preflight found 17,534,976 B of free disk against the required
17,821,672 B. Packing a completed reference core freed space and preserved
its original hash and the observation archive. The first collection then
stopped before timing because a manifest entry was missing. Its failed
archive is retained. The retry keeps the same image, catalog and lanes.
It reuses all 32 frozen image files under the lease. Workers join and
guest fixtures are removed before archive verification.

[Timing](summary.json) · [Image](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures and first attempt](procedures.tar.xz).
