# B1261 expanded JSON Lines export against CPython

Measured Lython `3ab6f6c81` against stock CPython 3.13.16 on November 21,
2026 UTC. The Azure F4as_v7 runs Ubuntu 24.04.4 and .NET 11 RC3.
The runtime change is retained at `8785193bb`. [Source pair](../2026-11-21-expanded-builtin-span/README.md).

The script reads 3,072 shipment records from a 189,441-byte JSON file,
normalizes names and states and forwards two encoding options through
`**options` on each row. It writes 189,440 bytes of JSON Lines and prints
the row count and total amount. Both engines validate stdout and file
bytes against the same goldens.

| Boundary | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Warm execution | 6.503–6.598 ms | 6.534–6.571 ms | 0.990–1.010× |
| Compilation alone | 0.02975–0.02978 ms | 0.04436–0.04453 ms | 0.668–0.671× |
| Compile-to-result | 6.619–6.621 ms | 6.603–6.686 ms | 0.990–1.003× |

Ranges cover all pairings of two independently warmed process medians
per engine and phase. Warm and compile-to-result span parity. Warm
control spreads are 1.47% for Lython and 0.57% for CPython. The maximum
elapsed IQR across all phases is 1.78%.

Warm elapsed includes guest execution, mediated RAM-backed file I/O,
golden observation and prior-result cleanup. Compile-to-result adds
compilation and code-owner disposal. Compilation alone excludes guest
execution. Process startup, fixture reset and final teardown sit outside
the guest clocks.

All 84 timed batches and both quiet checks pass. Complete worker images,
the CPython executable and protocol hashes, goldens and raw archives
verify. All workers join and guest fixtures are removed. The original
32 MiB staging margin refused before collection. The successful run
keeps the persistent floor and uses the existing 16 MiB margin with a
hard-linked immutable image. Both launch conditions are in the procedures.

[Summary](summary.json) · [Raw observations](observations.tgz) ·
[Image verification and procedures](procedures.tar.xz).
