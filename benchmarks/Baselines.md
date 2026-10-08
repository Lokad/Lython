# Benchmark baselines

Release scan baselines recorded **2026-10-08 at 0104bbd1** cover all 39
CSV, text/XML and regex cases. BenchmarkDotNet 0.15.6 ran out of process on
Windows 11 (10.0.26200.9448), an Intel Core i7-14700KF (20 physical/28 logical
cores), .NET 10.0.12 x64 and SDK 10.0.300-preview.0.26177.108. Each case used
two launches, six warmups and fifteen measured iterations per launch: 1,170
actual measurements overall. Statistical processing retained 24–30 samples
per case. The largest 99.9% confidence margin is 8.99% of the mean.

Reproduce from a short-path checkout to avoid the observed Windows generated
runner copy failures in deeply nested worktrees:

```powershell
dotnet build benchmarks/Lokad.Lython.Benchmarks/Lokad.Lython.Benchmarks.csproj -c Release
dotnet benchmarks/Lokad.Lython.Benchmarks/bin/Release/net10.0/Lokad.Lython.Benchmarks.dll --filter '*CsvReaderBenchmarks*' '*TextScanBenchmarks*' '*RegexScanBenchmarks*' --job short --launchCount 2 --warmupCount 6 --iterationCount 15 --exporters json
```

All 39 independent invocation controls returned the expected work counts.
Each timed operation executes a script through RunAsync with an immediately
completing host, at the then-current 1 GiB memory/50 million step defaults. Guest
script compilation and host fixture seeding occur outside timing. Regex subject
construction and initial pattern compilation occur inside the timed script;
the fresh-compilation case also purges and recompiles on every loop iteration.
No other benchmark ran concurrently, though lightweight inspection and two
short tool builds occurred during measurement. These results are machine/SDK/GC
dependent; comparisons with older environments do not establish a regression.

Means and 99.9% confidence margins below are milliseconds. Allocated/op is
cumulative managed allocation in MiB (1,048,576 bytes), measured separately
from governor-accounted live memory and RSS. It does not establish peak heap
usage or a leak. Raw BenchmarkDotNet JSON retains measurement and outlier data.

CSV uses six-column UTF-8 input of 48 bytes per row plus an 18-byte header.
The historical 20K workload remains alongside 1K/10K/100K cases.

| Operation | Rows/iterations | Mean ± margin (ms) | Allocated/op (MiB) |
| --- | ---: | ---: | ---: |
| DictReader retain | 1,000 | 4.920 ± 0.113 | 3.39 |
| DictReader early break | 1,000 | 0.116 ± 0.002 | 0.10 |
| DictReader discard | 1,000 | 4.426 ± 0.288 | 3.13 |
| DictReader retain | 10,000 | 85.956 ± 4.082 | 33.83 |
| DictReader early break | 10,000 | 0.150 ± 0.013 | 0.10 |
| DictReader discard | 10,000 | 51.787 ± 1.964 | 29.95 |
| DictReader retain | 20,000 | 136.011 ± 10.890 | 67.41 |
| DictReader early break | 20,000 | 0.115 ± 0.002 | 0.10 |
| DictReader discard | 20,000 | 90.825 ± 4.031 | 58.79 |
| DictReader retain | 100,000 | 1362.934 ± 36.329 | 326.46 |
| DictReader early break | 100,000 | 0.114 ± 0.002 | 0.10 |
| DictReader discard | 100,000 | 492.089 ± 9.668 | 287.07 |

Early break allocates about 0.10 MiB at every fixture size. The 10K timing
is higher than the other early-break cases; these samples do not establish
strictly size-independent latency. Full-scan allocations grow approximately
with row count, with retention and collection also affecting elapsed time.

Text fixtures contain twelve-byte lines. XML input contains one 24-byte element
per row plus a 13-byte root wrapper. Long-record iteration reads the entire XML
as one line; ElementTree retains its children.

| Operation | Rows/iterations | Mean ± margin (ms) | Allocated/op (MiB) |
| --- | ---: | ---: | ---: |
| Text discard | 1,000 | 0.907 ± 0.015 | 0.79 |
| Text retain | 1,000 | 0.488 ± 0.017 | 0.31 |
| Long XML record | 1,000 | 0.139 ± 0.002 | 0.23 |
| ElementTree retain | 1,000 | 5.459 ± 0.159 | 3.22 |
| Text discard | 10,000 | 9.936 ± 0.265 | 7.53 |
| Text retain | 10,000 | 6.903 ± 0.200 | 2.93 |
| Long XML record | 10,000 | 0.759 ± 0.032 | 1.89 |
| ElementTree retain | 10,000 | 82.826 ± 3.814 | 31.76 |
| Text discard | 100,000 | 95.672 ± 2.501 | 73.12 |
| Text retain | 100,000 | 76.708 ± 4.357 | 28.21 |
| Long XML record | 100,000 | 6.992 ± 0.147 | 18.45 |
| ElementTree retain | 100,000 | 1038.660 ± 21.459 | 295.92 |

Regex uses `(a)(b)` and PythonRe 0.2.0. Bulk scans use `ab ` repeated
N times (3N ASCII bytes). Compilation comparisons perform N searches over
`ab`, separating one compiled pattern, per-run cache hits and repeated actual
compilation. Unnamed captures keep this cost investigation independent of the
pending capture-numbering correction.

| Operation | Rows/iterations | Mean ± margin (ms) | Allocated/op (MiB) |
| --- | ---: | ---: | ---: |
| finditer discard | 1,000 | 3.599 ± 0.175 | 7.97 |
| findall retain tuples | 1,000 | 4.305 ± 0.172 | 18.79 |
| Compiled pattern reuse | 1,000 | 3.893 ± 0.083 | 3.70 |
| Compile cache hits | 1,000 | 4.295 ± 0.042 | 4.04 |
| Purge and compile each iteration | 1,000 | 239.188 ± 6.091 | 173.32 |
| finditer discard | 5,000 | 28.303 ± 0.402 | 153.39 |
| findall retain tuples | 5,000 | 44.101 ± 1.076 | 436.41 |
| Compiled pattern reuse | 5,000 | 20.105 ± 0.654 | 17.59 |
| Compile cache hits | 5,000 | 24.293 ± 0.436 | 19.34 |
| Purge and compile each iteration | 5,000 | 1112.905 ± 30.099 | 866.42 |
| finditer discard | 10,000 | 83.097 ± 3.213 | 592.66 |
| findall retain tuples | 10,000 | 165.793 ± 3.110 | 1730.91 |
| Compiled pattern reuse | 10,000 | 40.377 ± 1.768 | 34.75 |
| Compile cache hits | 10,000 | 41.031 ± 0.894 | 38.26 |
| Purge and compile each iteration | 10,000 | 2269.215 ± 47.189 | 1732.80 |

Bulk regex allocation grows near quadratically in this finite series:
finditer reaches 592.66 MiB and findall 1,730.91 MiB at 10K matches. The published
backend discovers an eager detailed-match array, so discarded guest matches
do not demonstrate lazy discovery. Cache hits reduce compilation cost but
leave this bulk-scan cost unresolved.

Separate public-boundary cancellation diagnostics cover both modes, the first
finditer match and full findall materialization at 10K matches. Four positive
controls return the expected counts. Four cancellations before the call allocate
under 6 KiB after the setup checkpoint; four cancellations triggered after at
least 16 MiB of bulk allocation still incur at least 99.6% of their matching
positive-control allocation before failing explicitly. These are process-wide
diagnostics with a monitoring task and a finite watchdog, not throughput or
latency acceptance tests. Current backend scratch allocation, work budgeting,
lazy discovery and live cancellation require a backend/API repair. Existing
ownership, catchable budget-failure and bounded-read controls remain separate.

ZIP archive scaling benchmarks live in `ZipArchiveBenchmarks.cs` (200 mixed
STORED/DEFLATED entries; append adds 20). Release results below (full
BenchmarkDotNet runs, .NET 10, Windows 10.0.26200 x64, 2026-09-09, 9d5b827);
re-record here on release runs before changing archive hot paths.

| Benchmark | Mean | Allocated/op |
| --- | --- | --- |
| Write 200 mixed STORED/DEFLATED entries | 1,143.4 us | 669.56 KB |
| List and read 200 mixed entries | 294.1 us | 493.23 KB |
| Append 20 entries to a 200-entry archive | 120.9 us | 554.71 KB |
| Extract 200 mixed entries | 311.8 us | 918.43 KB |

Notes: 6 outliers removed on the write case, 3 on list/read, 6 on append,
1 on extract. Read and extract allocate far less than the 2026-09-07 figures
after the staged-storage and reader accounting work.

Gzip flush scaling (`GzipFlushBenchmarks.cs`, fixed 100 KiB payload) confirms
the R16 recompression concern with confidence intervals:

| Benchmark | Mean | Allocated/op |
| --- | --- | --- |
| Write 100 KiB gzip without flushes | 1.815 ms | 2.65 MB |
| Write 100 KiB gzip with 10 flushes | 3.047 ms | 2.67 MB |
| Write 100 KiB gzip with 100 flushes | 15.642 ms | 2.86 MB |

OpenPyXL persistence (`OpenPyxlPersistenceBenchmarks.cs`, 500-row worksheet)
and SequenceMatcher scaling (`SequenceMatcherBenchmarks.cs`, near-identical
inputs) from full BenchmarkDotNet runs:

| Benchmark | Mean | Allocated/op |
| --- | --- | --- |
| Create and save a 500-row workbook | 1.654 ms | 2.56 MB |
| Load a workbook and read cells | 1.851 ms | 1.8 MB |
| SequenceMatcher ratio over 1K inputs | 57.97 us | 208.45 KB |
| SequenceMatcher ratio over 8K inputs | 362.50 us | 1452.75 KB |
| SequenceMatcher opcodes over 8K inputs | 360.69 us | 1454.28 KB |

Async materialization (AsyncMaterializationBenchmarks.cs, RunAsync path)
from a full BenchmarkDotNet run on the same machine shape:

| Benchmark | Mean | Allocated/op |
| --- | --- | --- |
| Sort 20K integers (async) | 12.411 ms | 2.75 MB |
| Sum a materialized 50K list (async) | 4.632 ms | 5.6 MB |
| Sum a 20K generator (async) | 6.291 ms | 10.95 MB |

Notes: the sort run reported a bimodal-distribution warning and the
materialized-sum run dropped 2 outliers; generator consumption allocates
more than the materialized list on the async path. Re-record on release
runs before changing async sequencing or materialization buffers.

Entry-count scaling (ZipEntryScalingBenchmarks.cs, fixed 256-byte DEFLATED
entries at 50/200/800 entries; the read benchmark returns total expanded
bytes: 12,800 / 51,200 / 204,800) from a full BenchmarkDotNet run (.NET 10,
Windows 10.0.26200 x64, 2026-09-09, 9d5b827):

| Benchmark | EntryCount | Mean | Allocated/op |
| --- | --- | --- | --- |
| Write N fixed-size DEFLATED entries | 50 | 270.47 us | 215.41 KB |
| Read N entries and sum expanded bytes | 50 | 94.16 us | 150.42 KB |
| Write N fixed-size DEFLATED entries | 200 | 1,051.85 us | 778.73 KB |
| Read N entries and sum expanded bytes | 200 | 339.70 us | 519.7 KB |
| Write N fixed-size DEFLATED entries | 800 | 4,314.14 us | 3049.51 KB |
| Read N entries and sum expanded bytes | 800 | 1,485.98 us | 1995.56 KB |

Notes: 16/1/1 outliers removed on the 50/200/800 write cases, 2/0/1 on the
read cases (same order). Write cost and allocation scale near-linearly with
entry count (3.9x/3.6x time/allocation from 50 to 200; 4.1x/3.9x from 200
to 800). Read allocation scales about linearly too (3.5x/3.8x); only read
runtime grows faster than entry count (3.6x then 4.4x). Re-record on release
runs before changing archive write/read hot paths.

Compression ratio (CompressionRatioBenchmarks.cs, single 1,000,000-byte
DEFLATED entry; compressible is a repeated byte, incompressible is
seeded PRNG bytes) from a full BenchmarkDotNet run (.NET 10, Windows
10.0.26200 x64, 2026-09-09, 9d5b827):

| Benchmark | Mean | Allocated/op |
| --- | --- | --- |
| Write 1MB maximally compressible entry | 5.211 ms | 3.86 MB |
| Write 1MB incompressible entry | 18.911 ms | 9.66 MB |

Notes: 3 outliers removed on the compressible case, 2 on the incompressible
case. Incompressible input costs about 3.6x time and 2.5x allocation versus
fully compressible input; identical allocation to the 2026-09-07 figures shows
the corrected serializer did not move compression costs.

Member lookup scaling (`ZipLookupBenchmarks.cs`; each op opens the archive
and runs 200 indexed getinfo/read pairs against the last entry) from a full
BenchmarkDotNet run (.NET 10, Windows 10.0.26200 x64, 2026-09-09, 9d5b827):

| Benchmark | EntryCount | Mean | Allocated/op |
| --- | --- | --- | --- |
| Lookup last entry by name 200 times | 50 | 286.7 us | 340.31 KB |
| Lookup last entry by name 200 times | 200 | 318.9 us | 549.14 KB |
| Lookup last entry by name 200 times | 800 | 775.5 us | 1383.3 KB |

Notes: 8 outliers removed on the 50-entry case, 6 on the 800-entry case.
The 50 to 200 step barely moves (287 to 319 us) while the 800-entry directory
parse dominates its row; per-op cost includes opening the archive.

Integer magnitude sizing (`IntegerSizingBenchmarks.cs`) from the same run shape:

| Benchmark | Mean | Allocated/op |
| --- | --- | --- |
| Shift a million-bit integer | 89.74 us | 146.42 KB |
| Power to a million-bit integer | 35,310.72 us | 146.6 KB |
| Shift a thousand-bit integer | 10.77 us | 24.41 KB |

Notes: 1 outlier removed on the power and thousand-bit cases. Shift and power
at a million bits allocate nearly identically, showing the magnitude guard
sizes without proportional scratch. Re-record on release runs before changing
member lookup or integer guard paths.

Six-column DictReader (`CsvReaderBenchmarks.cs`, 20,001-line seeded host file,
960,018 bytes, default execution budget) from a full BenchmarkDotNet run (.NET 10,
Windows 10.0.26200 x64, 2026-09-13, 4cb64e5):

| Benchmark | Mean | Allocated/op |
| --- | --- | --- |
| DictReader retain 20K six-column dicts | 150,890.1 us | 70733.44 KB |
| DictReader break after first of 20K rows | 127.4 us | 109.6 KB |

Notes: 1 outlier removed on retain, 4 on break-first; both distributions read
bimodal this run. Retaining every dictionary still costs about 1100x time and 650x allocation
versus stopping after the first row, all under the same default
1 GiB execution budget in an isolated process. Versus the 2026-09-10 figures,
retain costs about 2.4x time and 1.76x managed allocation because chunked file
reads stream through the benchmark host fallback ranges (each
16 KiB window re-reads the whole file host-side; native ranged hosts skip that
traffic), plus per-line reclamation tracking; governor-accounted peaks are
unchanged or lower (see the milestone bound pins). Break-first is about 2.6x
faster (only the first window decodes) at about 2.9x managed allocation
(window plus carry infrastructure replacing one shared whole-text array).
Re-record on release runs before changing CSV record, dictionary, or file-line
hot paths.

Host traffic (deterministic single-run counts from a counting host; host
call counts and byte totals do not depend on build configuration; asserted
repeatably by `ZipHostTrafficTests`):

| Benchmark case | Host calls | Bytes in | Bytes out |
| --- | --- | --- | --- |
| ZIP write 200 mixed entries | write x1 | 0 | 19,366 |
| ZIP list+read 200 entries (20,100 expanded) | read x1, stat x1 | 19,366 | 0 |
| ZIP append 20 to 200-entry archive | read x1, stat x1, write x1 | 19,366 | 21,206 |
| ZIP extract 200 entries | mkdir x1, read x1, stat x402, write x200 | 19,366 | 20,100 |

ZIP compressed byte totals are measurements for this environment. Native
DEFLATE implementations can differ slightly across OS/runtime versions; the
traffic regressions assert exact transfer counts and fixture-relative bytes,
with exact expanded sizes and STORED append overhead.
| ZIP write 50/200/800 fixed entries | write x1 | 0 | 4,802 / 19,402 / 78,202 |
| ZIP read 50/200/800 entries | read x1, stat x1 | 4,802 / 19,402 / 78,202 | 0 |
| ZIP write 1MB compressible entry | write x1 | 0 | 1,101 |
| ZIP write 1MB incompressible entry | write x1 | 0 | 1,000,428 |
| gzip write 100 KiB, 0/10/100 flushes | write x1/x10/x100 | 0 | 251 / 1,621 / 15,297 |
| openpyxl save 500-row workbook | write x1 | 0 | 19,487 |
| openpyxl load and read cells | read x1, stat x1 | 19,487 | 0 |

Notes: flush count maps 1:1 onto host publication writes, and 100 flushes
of a 100 KiB payload push 15,297 bytes (61x the single-write 251 bytes)
through the host, confirming the R16 recompression cost in host traffic
as well as time. Pure-compute benchmarks (async materialization,
SequenceMatcher, hot paths) make no host calls.

Governor paths (`GovernorBenchmarks.cs`: bounded scalar/file scans and slice/set
temporary loops at a 3 MiB execution budget) from a ShortRun BenchmarkDotNet run
(.NET 10, Windows 11 10.0.26200 x64, 2026-09-14, 743bbd5):

| Benchmark | Mean | Allocated/op |
| --- | --- | --- |
| Bounded scalar CSV scan, 20K rows | 86.96 ms | 42.54 MB |
| Bounded scalar CSV scan, 20K rows (async) | 76.32 ms | 48.76 MB |
| Bounded file line scan, 20K rows (delayed async) | 18.57 ms | 16.32 MB |
| Bounded scalar CSV scan, 200K rows | 764.17 ms | 422.66 MB |
| Dropped string slices, 10K x 1K chars | 11.29 ms | 12.24 MB |
| Dropped empty sets, 100K | 75.54 ms | 36.27 MB |

Notes: ShortRun (3 iterations, wide intervals: shape, not thresholds). The 200K
scan costs about 8.8x time and 9.9x allocation versus 20K (linear in rows);
Gen-2 counts ride exhaustion relief (about 9/90 collections per scan operation
at 20K/200K rows, i.e. 9K/90K per 1,000 operations in BDN normalized units). File line
scans skip CSV parsing. Scripts precompile once per class lifetime, so figures
exclude cold compilation; accounted peaks ride the deterministic
`BoundedScanPeakStaysFlatAcrossSizes` pin. Re-record on release runs before
changing CSV record, slice, set, or exhaustion-relief paths.

Repeated-constant-pool compilation (`RuntimeHotPathBenchmarks.cs`) from the same
ShortRun shape:

| Benchmark | Mean | Allocated/op |
| --- | --- | --- |
| CompileRepeatedConstantPool | 24.620 ms | 25.24 MB |
| Reject an invalid repeated-constant pool | 8.537 ms | 10.53 MB |

Notes: the invalid pool (5,000 assignments plus an unbalanced tail) fails about
3x faster than the valid pool compiles. Re-record on release runs before
changing frontend pooling or validation paths.

Call overhead (`CallOverheadBenchmarks.cs`: 10K calls plus the 100K-step integer
loop) from a ShortRun BenchmarkDotNet run (.NET 10.0.12, Windows 11 10.0.26200 x64,
2026-09-15, cae84bb):

| Benchmark | Mean | Allocated/op |
| --- | --- | --- |
| 10K positional identity calls | 17.943 ms | 12.47 MB |
| 10K positional identity calls (async) | 5.225 ms | 9.26 MB |
| 10K keyword identity calls | 19.899 ms | 13.53 MB |
| 10K variadic identity calls | 38.981 ms | 19.08 MB |
| 10K closure calls | 19.584 ms | 13.15 MB |
| 10K method calls | 6.494 ms | 12.62 MB |
| 100K-step integer loop | 15.529 ms | 16.05 MB |

Notes: ShortRun (wide intervals: shape, not thresholds). Every script result is
validated once at construction, outside timed sections, with exact CLR
representation (identity 9999, keyword 10001, variadic 1, closure/method 10000,
integer loop 100000, all BigInteger; async identity matches sync). First-chance
exceptions measured separately with an observer (Release): 10,001 per 10K sync
calls (one frame-exit throw each; closure 10,002 including its outer() setup
call), 0 async, 1 per integer loop. Deterministic
process allocation counters attribute about 1,351 B fixed per positional call
plus 30-40 B per additional parameter after the plan-layout binder change.
Scripts precompile once per class lifetime, so figures exclude cold compilation.
Re-record on release runs before changing call binding, return delivery, or
value boxing.
