# Benchmark baselines

ZIP archive scaling benchmarks live in `ZipArchiveBenchmarks.cs` (200 mixed
STORED/DEFLATED entries; append adds 20). Release results below (full
BenchmarkDotNet runs, .NET 10, 2026-09-07); re-record here on release runs
before changing archive hot paths.

| Benchmark | Mean | Allocated/op |
| --- | --- | --- |
| Write 200 mixed STORED/DEFLATED entries | 1,024.19 us | 657.81 KB |
| List and read 200 mixed entries | 409.47 us | 2178.97 KB |
| Append 20 entries to a 200-entry archive | 88.16 us | 553.59 KB |
| Extract 200 mixed entries | 371.98 us | 2617.53 KB |

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
bytes: 12,800 / 51,200 / 204,800) from a full BenchmarkDotNet run on the
same machine shape:

| Benchmark | EntryCount | Mean | Allocated/op |
| --- | --- | --- | --- |
| Write N fixed-size DEFLATED entries | 50 | 283.0 us | 213.21 KB |
| Read N entries and sum expanded bytes | 50 | 103.1 us | 578.59 KB |
| Write N fixed-size DEFLATED entries | 200 | 1,098.6 us | 773.09 KB |
| Read N entries and sum expanded bytes | 200 | 452.8 us | 2235.17 KB |
| Write N fixed-size DEFLATED entries | 800 | 4,545.1 us | 3028.91 KB |
| Read N entries and sum expanded bytes | 800 | 2,678.1 us | 8861.31 KB |

Notes: 2 outliers removed on one read case, 1 on one write case. Write
cost scales near-linearly with entry count. Read allocation scales about
linearly too (3.96x for 4x entries from 200 to 800); only read runtime
grows faster than entry count there (about 5.9x). Re-record on release
runs before changing archive write/read hot paths.

Compression ratio (CompressionRatioBenchmarks.cs, single 1,000,000-byte
DEFLATED entry; compressible is a repeated byte, incompressible is
seeded PRNG bytes) from a full BenchmarkDotNet run on the same machine
shape:

| Benchmark | Mean | Allocated/op |
| --- | --- | --- |
| Write 1MB maximally compressible entry | 6.347 ms | 3.86 MB |
| Write 1MB incompressible entry | 18.675 ms | 9.66 MB |

Notes: 1 outlier removed on the compressible case. Incompressible input
costs about 3x time and 2.5x allocation versus fully compressible input.
All write figures above predate the corrected DEFLATE serializer and staged
storage accounting; re-record them on release runs before validating
write-path changes against these numbers.

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
| --- | --- | --- | --- |
| Shift a million-bit integer | 89.74 us | 146.42 KB |
| Power to a million-bit integer | 35,310.72 us | 146.6 KB |
| Shift a thousand-bit integer | 10.77 us | 24.41 KB |

Notes: 1 outlier removed on the power and thousand-bit cases. Shift and power
at a million bits allocate nearly identically, showing the magnitude guard
sizes without proportional scratch. Re-record on release runs before changing
member lookup or integer guard paths.
at the same expanded size. Re-record on release runs before changing
archive compression or staging paths.

Host traffic (deterministic single-run counts from a counting host; host
call counts and byte totals do not depend on build configuration):

| Benchmark case | Host calls | Bytes in | Bytes out |
| --- | --- | --- | --- |
| ZIP write 200 mixed entries | write x1 | 0 | 20,197 |
| ZIP list+read 200 entries (20,100 expanded) | read x1, stat x2 | 20,197 | 0 |
| ZIP append 20 to 200-entry archive | read x1, stat x2, write x1 | 20,197 | 22,037 |
| ZIP extract 200 entries | mkdir x1, read x1, stat x403, write x200 | 20,197 | 20,100 |
| ZIP write 50/200/800 fixed entries | write x1 | 0 | 5,002 / 20,202 / 81,402 |
| ZIP read 50/200/800 entries | read x1, stat x2 | 5,002 / 20,202 / 81,402 | 0 |
| ZIP write 1MB compressible entry | write x1 | 0 | 1,106 |
| ZIP write 1MB incompressible entry | write x1 | 0 | 1,000,433 |
| gzip write 100 KiB, 0/10/100 flushes | write x1/x10/x100 | 0 | 251 / 1,621 / 15,297 |
| openpyxl save 500-row workbook | write x1 | 0 | 19,487 |
| openpyxl load and read cells | read x1, stat x1 | 19,487 | 0 |

Notes: flush count maps 1:1 onto host publication writes, and 100 flushes
of a 100 KiB payload push 15,297 bytes (61x the single-write 251 bytes)
through the host, confirming the R16 recompression cost in host traffic
as well as time. Pure-compute benchmarks (async materialization,
SequenceMatcher, hot paths) make no host calls.
