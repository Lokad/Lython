# B1102 retained defaults and compiler checkpoint against CPython

This diagnostic measures `80cbf4988` using the verified source-equivalent
`bc65698dd` image with core SHA-256 `f26c8272…`. It includes the retained
closure charge, declaration preparation and final positional-default tuple
changes. The quiet Azure F4as_v7 ran Ubuntu 24.04.4, .NET 11 RC3 and stock
CPython 3.13.16 on November 19, 2026 UTC. Complete worker images and all seven
goldens passed before collection.

Each ratio is Lython elapsed divided by CPython elapsed, across the four
combinations of duplicate process medians. Compilation includes compiled-owner
disposal. Warm includes mediated RAM-backed I/O, output and file goldens, and
prior-result cleanup. Compile-to-result adds compilation and compiled-owner
disposal to the execution boundary. Fixture reset, framing, cold process
startup and final cache disposal are outside guest elapsed. Process CPU covers
a wider boundary and is recorded separately.

| Workload | Input | Compilation | Warm | Compile-to-result |
| --- | --- | ---: | ---: | ---: |
| Ordinary rule closures | 2,048 inputs | 0.74–0.75× | 3.58–3.68× | 3.43–3.56× |
| Annotated rule closures | 2,048 inputs | 0.76–0.78× | 4.10–4.18× | 3.93–4.02× |
| Captured rule closures | 2,048 inputs | 0.74–0.75× | 3.78–3.86× | 3.64–3.70× |
| Inline forwarding with dynamic keys | 1,024 records | 0.87–0.89× | 1.63–1.68× | 1.54–1.57× |
| Expanded forwarding | 1,024 records | 0.80–0.82× | 1.71–1.73× | 1.60–1.62× |
| CSV invoice | 4,096 data records | 0.93–0.94× | 1.07–1.13× | 1.07–1.11× |
| Generated validator | 23,531 source characters | 1.35–1.37× | — | 1.34× |

The three rule scripts still have the largest warm gaps. The generated
validator took 2.487–2.511 ms to compile versus 1.831–1.838 ms for CPython.
Its compile-to-result time was 2.635–2.642 ms versus 1.967–1.968 ms. Ordinary
warm and validator compilation have maximum within-process IQR just over 5%.
The validator has no timed warm row in this collection. The six other
workloads are unchanged from [B1091](../2026-11-19-inline-forwarding-cpython/README.md).
That earlier collection describes its own revision. The retained changes'
asynchronous and allocation diagnostics remain in [B1101](../2026-11-19-positional-default-slots/refinement/README.md).

All 400 batches verified in 459.75 seconds. Both quiet gates passed, workers
joined, fixtures were removed and the persistent archive verified under the
lease. Absolute times, controls and wider CPU are in the summary.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Lease receipt](archive-receipt.json) · [Procedures](procedures.tar.xz).
