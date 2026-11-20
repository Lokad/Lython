# Representative CPython checkpoint B1162

Measured Lython `e6e1f964c59cc39051e7c29001effbc75f6fa7dc` against stock
CPython 3.13.16 on 2026-11-20 UTC. The quiet Azure F4as_v7 ran Ubuntu
24.04.4 and .NET 11 RC3. Both engines matched the four script goldens.
The full 32-file Lython image was verified before collection.

| Script | Input | Compilation | Warm | Compile-to-result |
| --- | --- | ---: | ---: | ---: |
| Eager annotated rules | 2,048 inputs and definitions | 0.67–0.68× | 3.69–3.75× | 3.53–3.55× |
| Expanded forwarding report | 1,024 records, 26,112 B | 0.71–0.72× | 1.55× | 1.45–1.47× |
| Shipment report with format selectors | 3,072 records, 189,441 B | 0.77–0.78× | 1.21× | 1.16–1.20× |
| CSV invoice | 4,096 data records, 127,031 B | — | 1.05–1.09× | — |

Ratios divide Lython elapsed by CPython elapsed, using the extrema across
two independent process medians per engine. Values above one mean Lython
was slower. Compilation includes disposal of the compiled owner. Warm
includes execution, mediated file I/O on Lython, output and file checks,
and prior-result cleanup. Compile-to-result adds compilation and code-owner
disposal. Process startup, fixture reset and final teardown are outside
these timers. Wider process CPU includes protocol and reset work.

All 200 timed batches completed. Lython's duplicate-process spread was
3.31% for shipment compile-to-result, with a worst within-process IQR of
5.04%. The Lython invoice warm duplicates differ by 2.60%. The measurements
describe the tested scripts and revision. They do not isolate the effect of
individual runtime changes.

[Elapsed and process CPU](summary.json) · [Raw observations](observations.tgz) ·
[Catalog, image hashes, goldens and procedure](procedures.tar.xz) ·
[Exact measured core](measured-core.dll.gz) · [Archival receipt](archive-receipt.json) ·
[Terminal state and cleanup](terminal-status.json).
