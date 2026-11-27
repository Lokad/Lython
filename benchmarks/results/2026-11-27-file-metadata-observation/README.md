# B1828 Read one set of metadata per file path component

The trial changes the comparison host on `b289fbd9a`.
The reference checks existence and attributes separately. The trial reads
a fresh `FileInfo.Attributes` value for each part, classifies missing
entries and reparse points, and passes the final observation to callers
that already need its kind or length. The missing-path sentinel is part
of the managed API contract. [FileSystemInfo.Attributes](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesysteminfo.attributes?view=netframework-4.8.1#remarks)

The normal path issues one managed metadata read per intermediate component
and one for the final entry. For a single-component existing file, this
replaces four managed existence and attribute calls with two. Exceptional
metadata errors preserve the original existence behavior. These counts
describe managed API calls. The benchmark compares complete commands that
use the host.

Both 32-file images use the same B1815 Lython core at
`c10f06884`. Only the benchmark assembly changes.
Three complete scripts match independent CPython 3.13.2 output and file
goldens through both workers. Windows .NET 11 and Ubuntu WSL .NET 10 each
pass 137 focused Release checks, including literal component order, missing
parents, trailing separators, refused acquisition and cleanup. Ubuntu also
checks rejected symbolic links.

The paired run uses Ubuntu WSL and .NET 11 RC3 under one logical CPU.
Two workers per source warm for five seconds and contribute seven rotated
batches. Changes span all four pairings of worker medians. Warm elapsed
includes mediated I/O, verification and prior result disposal. Process CPU
also covers fixture reset and request framing. Compilation and startup
sit outside those clocks.

| Complete workload, warm sync | Elapsed change | Process CPU change |
| --- | ---: | ---: |
| File search, match at line 1 | −4.51% to −0.14% | −2.80% to +0.65% |
| Configuration edit | −7.66% to −7.46% | −7.89% to −4.56% |
| File search, match at line 1,025 | −7.63% to −4.86% | −3.12% to −2.02% |

Keep the host simplification. The configuration edit uses 7.46–7.66% less
warm elapsed time and 4.56–7.89% less process CPU. Late search also uses less
elapsed time and process CPU. The first-line elapsed range includes a pair
under 0.15% and its process CPU spans zero. These observations leave the
size of the first-line benefit uncertain. The experiment attributes the
measured improvements to the comparison host. The next representative
CPython checkpoint should record the new worker image.

All 84 timed batches verify, workers join, temporary fixtures are removed
and the persistent archive verifies. This diagnostic compares the host
implementations under an identical runtime. [B1827 command boundary](../2026-11-27-short-command-boundary/README.md).

[Frozen images](identity.json) · [Medians and spreads](summary.json) ·
[Raw observations](observations.tar.xz) · [Archive receipt](archive-receipt.json) ·
[Exact patches, checks and procedures](procedures.tar.xz).
