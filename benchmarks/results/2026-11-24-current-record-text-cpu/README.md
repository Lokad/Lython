# B1637 CSV and text attribution after callable consolidation

Three serial warm profiles use the sealed `3d8f68aaf` image. The Azure
F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24, 2026 UTC.
Each worker verifies its whole-script CPython golden, warms for at least
eight seconds of guest execution, and samples at 199 Hz for about six
seconds. The samples cover worker requests, including RAM-backed file
operations, result verification, disposal, fixture reset and protocol.
Compilation and process startup are outside sampling.

| Workload | Invocations | All samples | Resolved runtime ancestry | Unmapped leaf |
| --- | ---: | ---: | ---: | ---: |
| Typed Unicode CSV meter, 4,096 records | 2,048 | 1,214 | 1,151 | 125 |
| Explicit-float CSV meter, 4,096 records | 2,048 | 1,247 | 1,194 | 122 |
| Short Unicode word-count report, 3,072 lines | 6,912 | 1,226 | 1,113 | 181 |

`ExecuteInstructionPrefix` is the nearest resolved runtime method in
263 typed and 338 explicit samples. `TryDescribeLiteralRecord` is
nearest in 222 typed and 71 explicit samples. `ReadCsvLine` appears
nearest in 53 and 43, with the line-end scan nearest in eight for each.
Unpacking is nearest in 44 and 45 and tuple construction in 26 and 19.
The normalized-float helper no longer appears as a separately dominant
method. Inlined conversion can still be attributed to its caller.
[Prior meter attribution](../2026-11-24-meter-tuple-cpu/README.md) ·
[Direct decimal conversion](../2026-11-24-exact-unicode-float/README.md).

The short report has 219 nearest samples in the dictionary loop, 162
in the direct cased helper return and 128 in cased word preparation.
The word buffer already supports borrowing UTF-8 keys in the dictionary
loop and materializing escaping values. Admission still precedes the
first guest loop effect. The private empty-line shortcut previously
made the target slower. [Earlier text attribution](../2026-11-24-short-text-helper-cpu/README.md) ·
[Empty-line trial](../2026-11-24-cased-empty-iteration/README.md).

Both CSV entry paths already share literal field description and
keep the already scanned prefix when falling back to transformed fields.
One remaining CSV-specific stage is the asynchronous `ReadCsvLine`
wrapper on completed buffer reads. It covers a smaller share than the
parser or the instruction loop. The profiles and source review did not
identify a common construction or conversion stage that can disappear
without extra storage layout or altered admission and error order.
These stacks locate CPU and give no paired elapsed comparison.

The complete image and three goldens verify before staging. Both quiet
gates pass, workers join and fixtures are removed. The persistent archive
verifies under the lease.

[Attribution](attribution.json) · [Image identity](identity.json) ·
[Raw profiles](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
