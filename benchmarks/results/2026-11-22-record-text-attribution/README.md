# B1382 CPU attribution for complete CSV and Unicode reports

On November 22, 2026 UTC, four fresh processes ran the frozen
`a4cb961be` source image from B1381 on the quiet Azure F4as_v7 with
Ubuntu 24.04.4 and .NET 11 RC3. The production revision `aef642701`
also has the independently qualified regex key protocol correction.
These four scripts do not call regex. All 32 files of the measured
image verified. Local CPython 3.13.2 independently verified stdout
and file bytes for each complete script.

Each worker warmed for at least eight seconds and ran for about
5.5 seconds while perf sampled all threads at 199 Hz. These are
single-image default-tier CPU profiles with the perf map enabled.
The whole-request samples include interpreter execution, mediated
RAM-backed I/O, output verification, disposal of the previous
result and worker protocol. Host compilation and startup are
separate. Counts use the nearest resolved Lython frame. Inlining,
unresolved frames and sampling uncertainty limit attribution.

| Script | Input | Samples | Nearest instruction dispatcher | Nearest CSV | Selected additional frames |
| --- | --- | ---: | ---: | ---: | --- |
| Explicit `float()` meter | 4,096 records / 139,299 B | 1,122 | 283 | 126 | 74 in `ParseFloatText`, 46 in `PyTuple.FromSlots` |
| Quote-heavy policy invoice | 4,096 records / 174,147 B | 1,133 | 162 | 130 | 155 in native `RunCore`, 35 in instance function lookup |
| Typed Unicode meter | 4,096 records / 223,267 B | 1,131 | 205 | 281 | 93 in `TryParseAsciiFloat`, 65 in `TryParseNormalizedFloat` |
| Unicode word-count report with a helper | 3,072 lines / 35,840 B | 1,101 | 104 | 0 | 171 in `TryExecuteDictionaryLoop`, 113 in `PrepareCasedWhitespaceIteration` |

File reading and decoder methods account for only 6–7 nearest frames
in each CSV profile. Some line handling can be inlined. The evidence
does not identify the physical line copy as a dominant cost. The
Unicode numeric scanner and conversion together account for 14.0%
of nearest frames in the typed meter. This justifies a bounded source
pair that joins the short-token classification and normalization
steps and uses one managed decimal value lookup for BMP digits.
Keep wide-token preflight and the existing final decimal conversion.
An earlier change to the final parser slowed the Unicode meter.
[B1338](../2026-11-22-complete-decimal-token/README.md).

All 106 captured batches verified. Both quiet gates passed and all
processes joined. The archive was verified under the lease before
guest fixtures were removed. Staging reclaimed one byte-identical
replica of an already pushed archive after verifying both copies.
The separate Windows allocation diagnostic restored the host
allowance on all four scripts. This checkpoint has no before-and-after
timing or new CPython ratios.

[Attribution](attribution.json) · [Raw observations](observations.tgz) ·
[Local allocation and allowance results](allocation.json) ·
[Image, verification and procedures](procedures.tar.xz) ·
[Archive receipt](archive-receipt.json).
