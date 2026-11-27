# B1869 Compare the CSV dictionary re-export with CPython

The comparison measures Lython `6e59cd41e` and stock CPython 3.13.16 on
November 27, 2026 UTC. The machine is an Azure F4as_v7 running Ubuntu
24.04.4 and .NET 11 RC3. The frozen worker uses comparison host `05c1059de`.
The frozen source patch and all 32 image files match the delivered change.

The report reads 2,048 shipment records, reorders the columns with
`csv.DictWriter` and writes the 27,350-byte result. Both engines match
stdout and the full output file. Two workers per engine contribute seven
rotated batches per lane. Ranges span pairings of process medians. The ratio
divides Lython elapsed by CPython elapsed.

| Workload | Phase | Lython | CPython | Ratio |
| --- | --- | ---: | ---: | ---: |
| CSV dictionary re-export | Warm `Run` | 1.74944–1.76297 ms | 2.74569–2.75434 ms | 0.635–0.642× |
| CSV dictionary re-export | Warm `RunAsync` | 2.12564–2.14952 ms | 2.73113–2.75321 ms | 0.772–0.787× |
| CSV dictionary re-export | Compilation only | 0.01653–0.01674 ms | 0.03124–0.03132 ms | 0.528–0.536× |
| CSV dictionary re-export | Compile-to-result | 1.84274–1.87090 ms | 2.81251–2.86722 ms | 0.643–0.665× |
| File-fed quantity map | Warm `RunAsync` | 0.40361–0.45127 ms | 1.37066–1.37078 ms | 0.294–0.329× |

Warm uses compiled code. It includes mediated RAM-backed I/O, output and
file verification and previous-result cleanup. Compilation only includes
fresh compilation and disposal of the previous artifact. Compile-to-result
includes compilation, execution, verification and cleanup. Fixture
reset, framing and process startup sit outside the elapsed clock. The async
lane uses Lython's `RunAsync` and the same synchronous CPython script.

All 140 batches and both quiet checks pass. The file-fed map has 11.81%
Lython worker spread, so its change relative to [B1865] remains uncertain.
Re-export worker spreads stay below 2% on elapsed. The local before/after
Lython comparison and its adverse plain-writer control are separate. [B1868].

Staging reuses 31 immutable files and replaces one core DLL. It frees a
withdrawn B1852 worker replica after verifying its 32-file backup and original
archive. All workers join and the archived observations verify after removing
the guest fixtures. [Storage record](stage-custody.json).

[Timing](summary.json) · [Image identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal status](terminal-status.json) ·
[Verification and procedures](procedures.tar.xz).

[B1865]: ../2026-11-27-iterable-starmap-cpython/README.md
[B1868]: ../2026-11-27-csv-dictwriter-native-validation/README.md
