# B1720 CSV admission checkpoint against CPython

The frozen image matches the production source of `7120ee55e`, including
direct completed-row and dictionary-shell admission. The Azure F4as_v7
running Ubuntu 24.04.4 measured .NET 11 RC3 and stock CPython 3.13.16 on November 25, 2026 UTC.
All 32 measured image files and seven complete output/file goldens
were verified. Both quiet checks and all 308 timed batches passed.

Each phase warms two processes per engine for five full-request seconds
apiece. Each process contributes seven rotated batches targeting 0.46
request seconds. Ratios divide Lython by CPython and span the four
pairings of worker medians. Async rows use Lython's `RunAsync` and
the same Python script and file API on CPython.

Warm and compile-to-result elapsed include mediated RAM-backed I/O when
applicable, result/file verification and cleanup. Lython uses its efficient
ranged-read and append host with an explicitly supplied shared, unbounded
memory allowance. CPython uses native I/O. Compilation includes disposal
of preceding compiled owners in each batch.
Fixture reset and framing are included in the wider CPU clock. Startup and
final process teardown are outside these measurements.

| Workload | Phase | Lython | CPython | Elapsed ratio | Wider CPU ratio | Elapsed worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| File-fed CSV quantity map, Lython `RunAsync` | Warm async | 0.59234–0.62370 ms | 1.36880–1.37651 ms | 0.430–0.456× | 0.454–0.486× | 5.30% / 0.56% |
| Short Unicode word-count report with a nonlocal helper | Warm sync | 0.92415–0.99757 ms | 0.95457–0.96076 ms | 0.962–1.045× | 0.943–1.018× | 7.94% / 0.65% |
| Generated ordinary definitions | Warm sync | 0.31621–0.31811 ms | 0.14656–0.14897 ms | 2.123–2.171× | 2.157–2.215× | 0.60% / 1.65% |
| Shipment callback report, Lython `RunAsync` | Warm async | 2.54592–2.55642 ms | 1.63596–1.64217 ms | 1.550–1.563× | 1.545–1.576× | 0.41% / 0.38% |
| JSON Lines export with default options | Warm sync | 4.27718–4.35394 ms | 5.06345–5.15320 ms | 0.830–0.860× | 0.855–0.862× | 1.79% / 1.77% |
| Quote-heavy CSV policy invoice | Warm sync | 6.19443–6.33377 ms | 6.30969–6.36204 ms | 0.974–1.004× | 0.966–1.008× | 2.25% / 0.83% |
| Shipment callback report | Warm sync | 2.32632–2.43430 ms | 1.62235–1.63046 ms | 1.427–1.500× | 1.420–1.457× | 4.64% / 0.50% |
| Generated ordinary definitions, 8,641 B of source | Compilation only | 0.56978–0.57063 ms | 0.94974–0.95028 ms | 0.600–0.601× | 0.600–0.609× | 0.15% / 0.06% |
| Generated rules with defaults | Compile-to-result | 0.91836–0.95004 ms | 0.73616–0.75183 ms | 1.221–1.291× | 1.222–1.293× | 3.45% / 2.13% |
| Shipment callback report | Compile-to-result | 2.49544–2.51408 ms | 1.90542–1.92300 ms | 1.298–1.319× | 1.261–1.292× | 0.75% / 0.92% |
| Generated ordinary definitions | Compile-to-result | 0.90039–0.90285 ms | 1.11963–1.13414 ms | 0.794–0.806× | 0.799–0.814× | 0.27% / 1.30% |

This checkpoint measures the retained source. Separate paired Lython
runs record the CSV allocation savings and the adverse text-control result.
The external rows are diagnostic. The text report and quoted invoice span
parity, with 7.94% Lython worker spread on the text report. Use the paired
comparison to assess the CSV admission patch.

[Delivery checks](https://github.com/Lokad/Lython/actions/runs/45604807176) ·
[Identity](identity.json) · [Timings](summary.json) · [Observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz) ·
[Paired CSV evidence](../2026-11-25-csv-direct-record-admission/README.md) ·
[Previous checkpoint](../2026-11-25-retained-workspace-cpython/README.md).
