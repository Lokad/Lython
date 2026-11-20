# B1168 CPython comparison for dataclasses and factories

Measured Lython `ab2aa9554` and stock CPython 3.13.16 on 2026-11-20 UTC.
The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3. All four
script goldens passed on both engines. The full 32-file Lython image
was verified before collection.

| Script | Input | Compilation | Warm | Compile-to-result |
| --- | --- | ---: | ---: | ---: |
| Inherited dataclasses | 1 base and 48 subclasses, 48 instances | 0.45–0.50× | 0.039–0.040× | 0.074–0.078× |
| Eager annotated rules | 2,048 inputs and definitions | 0.67–0.68× | 3.58–3.72× | 3.42–3.49× |
| Nested annotated factories | 2,048 inputs and 4,096 definitions | 0.65–0.67× | 3.35–3.47× | 3.23–3.28× |
| CSV invoice | 4,096 data records, 127,031 B | — | 1.07–1.10× | — |

Ratios divide Lython elapsed by CPython elapsed. Ranges cover the four
pairings of two process medians per engine. Compilation includes compiled-owner
disposal. Warm includes mediated I/O where applicable, output and file
checks, and prior-result cleanup. Compile-to-result adds compilation and
code-owner disposal. Process startup, fixture reset and final teardown are
outside these timers. Process CPU covers a wider boundary.

The dataclass workload constructs and decorates all 49 classes on each
execution. The warm comparison therefore includes generated method setup.
A separate local CPython 3.13.2 profile with `dataclasses` already imported
shows most of the request under `dataclasses._process_class`, with 49
method-generation calls. This identifies a workload-specific cost.
The quiet Lython dataclass compile-only duplicates differ by 9.61%,
limiting the precision of that ratio. The active performance gap in
the factory scripts is in warm execution, where both remain over 3×.

All 200 timed batches and both quiet gates passed. Workers joined,
fixture storage was removed and the persistent archive verified under
the lease. These timings describe the measured revision.

[Elapsed and process CPU](summary.json) · [Raw observations](observations.tgz) ·
[Catalog, images and procedure](procedures.tar.xz) ·
[Archival receipt](archive-receipt.json).
