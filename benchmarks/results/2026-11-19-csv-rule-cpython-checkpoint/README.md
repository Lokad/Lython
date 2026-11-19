# B1143 CSV and annotated rules against CPython

Lython `353ec074c` was rebuilt from the retained source and frozen as
`5e40c428`. The complete 32-file image and stock CPython 3.13.16 ran on
the quiet Azure F4as_v7 with Ubuntu 24.04.4 and .NET 11 RC3 on November 19,
2026 UTC. Both used the same scripts and inputs as the earlier measurements
for these workloads. Invoice uses 4,096 data records and 127,031 input bytes.
Annotated rules create 2,048 definitions for 2,048 inputs.

| Workload | Phase | Lython | CPython | Lython / CPython |
| --- | --- | ---: | ---: | ---: |
| CSV invoice | Warm | 6.439–6.606 ms | 6.169–6.276 ms | 1.03–1.07× |
| CSV invoice | Compilation | 0.07780–0.07865 ms | 0.09355–0.09493 ms | 0.82–0.84× |
| CSV invoice | Compile-to-result | 6.625–6.737 ms | 6.367–6.465 ms | 1.02–1.06× |
| Annotated rules | Warm | 1.656–1.708 ms | 0.416–0.423 ms | 3.92–4.11× |
| Annotated rules | Compilation | 0.02233–0.02275 ms | 0.03110–0.03140 ms | 0.71–0.73× |
| Annotated rules | Compile-to-result | 1.695–1.769 ms | 0.443–0.455 ms | 3.73–3.99× |

The ratio divides Lython elapsed by CPython elapsed. Ranges cover all
pairs of duplicate process medians. The Lython annotated-rule controls
differ by 3.15% warm and 4.35% compile-to-result, leaving the size of those
gaps uncertain. The invoice is close to parity in this collection. The
individual source-pair trials document the CSV gain and binding CPU debt
separately. [CSV searches](../2026-11-19-prepared-csv-search-sets/README.md) ·
[Binding state](../2026-11-19-inline-function-binding-state/README.md).

Warm includes mediated RAM-backed I/O, output and file verification, and
previous-result cleanup. The compiler lane includes compiled-owner
disposal. Compile-to-result measures compilation, one execution and
compiled-owner disposal together.
The clocks exclude process startup, fixture reset, framing and final
teardown. Wider process CPU includes reset and framing and is preserved
in the observations. CPython uses native file I/O. Lython uses the host's
filesystem and an accounted shared memory allowance.

All 120 batches passed the goldens in 142.42 seconds. Both quiet gates
passed, workers joined, and the VM verified the persistent archive before
ending the lease. Staging retired the two redundant B1142 VM images after
verifying their canonical archives and aliases.

[Summary](summary.json) · [Observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).
