# B1888 Compare nested JSON with CPython

The November 27 local comparison uses the frozen `126d551c2` image, .NET
11 RC3 and verified CPython 3.13.16 under Ubuntu WSL on an Intel
i7-14700KF. All 32 Lython files and the three independent output and file
goldens verify. The original nested document contains 2,048 records
(103,331 bytes). The Lines report reads 4,096 nested records (159,744
bytes), aggregates them and writes a JSON report. File-fed CSV reads
2,048 records (27,350 bytes).

| Workload | Lython API | Lython elapsed | CPython elapsed | Elapsed ratio | Process CPU ratio |
| --- | --- | ---: | ---: | ---: | ---: |
| Nested document, confirmation | `Run` | 1.01837–1.11356 ms | 0.76955–0.79924 ms | 1.274–1.447× | 1.154–1.241× |
| Nested document, confirmation | `RunAsync` | 1.03631–1.11165 ms | 0.79008–0.79924 ms | 1.297–1.407× | 1.174–1.257× |
| Nested Lines | `Run` | 3.55033–3.67051 ms | 4.66297–4.69670 ms | 0.756–0.787× | 0.752–0.784× |
| Nested Lines | `RunAsync` | 3.67599–3.70718 ms | 4.65803–4.73711 ms | 0.776–0.796× | 0.792–0.811× |
| File-fed CSV map | `RunAsync` | 0.50207–0.51600 ms | 1.37309–1.40471 ms | 0.357–0.376× | 0.374–0.386× |

Ratios divide Lython by CPython. Ranges span all pairings of worker
medians. Two workers per engine run on the same logical CPU. Each warms
for five seconds per lane, then contributes seven rotated batches.
The two hosting modes share workers and interleave in the JSON cases.
Warm elapsed includes mediated RAM-backed I/O, verification and prior
result cleanup. Process CPU also includes fixture reset and framing.
Compilation and startup are outside these clocks.

The initial nested-document elapsed ratios are 0.718–1.777× sync and
1.042–1.891× async. Identical Lython workers differ by 59–66%. The
confirmation uses the same images and reverses worker placement. Both
document modes then take more elapsed and CPU than CPython. Local
same-image spread and per-batch variation remain visible. The earlier
quiet B1849 result retains its original machine and revision.

All 140 initial and 56 confirmation batches verify. Workers join and
RAM fixtures are removed before persistent archives verify. The quiet
VM had 4,124 KiB available during a read-only check. These are local
diagnostics.

[Initial timing](summary.json) · [Confirmation timing](repeat/summary.json) ·
[Image identity](identity.json) · [Initial observations](observations.tar.xz) ·
[Confirmation observations](repeat/observations.tar.xz) ·
[Initial procedures](procedures.tar.xz) · [Confirmation procedures](repeat/procedures.tar.xz).
