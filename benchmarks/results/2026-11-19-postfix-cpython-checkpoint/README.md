# B1130 shared postfix parsing checkpoint against CPython

Measured Lython `a472eb2a4` with core SHA-256 beginning `e18ee460`
against stock CPython 3.13.16 on the quiet Azure F4as_v7 with Ubuntu
24.04.4 and .NET 11 RC3 on November 19, 2026 UTC. The full 32-file
Lython image and both unchanged CPython workload goldens passed local
verification. Ratios divide Lython elapsed by CPython elapsed across all
four pairings of process medians. Above one means slower.

| Workload | Phase | Lython | CPython | Ratio | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Generated validator | Warm execution | 0.102–0.103 ms | 0.043–0.044 ms | 2.33–2.38× | B1130 diagnostic. Controls L/C 0.57 / 1.63%. IQR ≤1.57%. |
| Generated validator | Compile-to-result | 2.141–2.159 ms | 1.953–2.001 ms | 1.07–1.11× | B1130 diagnostic. Controls L/C 0.84 / 2.50%. IQR ≤4.30%. |
| Generated validator | Compilation only | 1.98704–2.01548 ms | 1.82218–1.98945 ms | 1.00–1.11× | B1130 diagnostic. Controls L/C 1.43 / 9.18%. IQR ≤9.24%. Inconclusive near parity. |
| Configuration manifest | Warm execution | 0.199–0.200 ms | 0.165 ms | 1.21× | B1130 diagnostic. Controls L/C 0.17 / 0.06%. IQR ≤0.99%. |
| Configuration manifest | Compile-to-result | 0.463–0.464 ms | 0.493–0.498 ms | 0.93–0.94× | B1130 diagnostic. Controls L/C 0.30 / 0.99%. IQR ≤2.07%. |
| Configuration manifest | Compilation only | 0.15576–0.15686 ms | 0.16836–0.17140 ms | 0.91–0.93× | B1130 diagnostic. Controls L/C 0.70 / 1.80%. IQR ≤0.80%. |

The generated validator reads 1,714 bytes of JSON and checks 96 fields.
The configuration manifest reads 833 bytes of JSON and requirements and
writes a manifest. Validator compilation varies across CPython workers.
Both warm controls remain close on the validator and manifest, and the
manifest finishes ahead on the complete source-to-result boundary.

Warm includes mediated RAM-backed I/O, golden checks and previous-result
cleanup. Compilation includes compiled-owner disposal. Compile-to-result
adds compilation and compiled-owner disposal to the execution boundary.
CPython uses native file APIs. Lython has shared accounting enabled and
checks allowance restoration. Guest clocks exclude process startup,
fixture reset, request framing and final cache teardown. Wider CPU and
all control pairings are available in the summary.

The two workloads passed all 120 timed responses in 143.04 seconds.
Both quiet gates passed. All workers joined, guest fixtures were removed,
and the service verified the persistent archive under its lease. These
measurements have their own source revision and are separate from the
earlier validation and parser source-pair timing.

[Measurements](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).
