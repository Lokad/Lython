# B1534 quoted CSV invoice against CPython

Retained `0112138c1` and stock CPython 3.13.16 run the same CSV
policy invoice with 4,096 records and 174,147 input bytes. The quiet
Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 23, 2026 UTC.

| Phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Warm | 6.5665–6.6281 ms | 6.5816–6.6234 ms | 0.991–1.007× |
| Compile to result | 6.5390–6.6442 ms | 6.7990–6.8250 ms | 0.958–0.977× |
| Compilation only, 1,293 B source | 0.05936–0.06033 ms | 0.09800–0.09855 ms | 0.602–0.616× |

Ranges span four process-median pairings. The warm elapsed result
spans parity, while wider process CPU is 0.976–0.992×. Warm includes
RAM-backed reads and writes, golden verification and prior-result
disposal inside an already-running worker. Lython uses mediated file
access and active shared-budget admission. CPython uses its native file
APIs. The time-to-result clock also includes compilation and disposal
of the compiled owner. Compilation alone includes prior-code disposal.
Reset, framing and final process exit sit outside the elapsed clocks.
The phases sample independently.

Each worker warms eight seconds before seven rotated batches. All 84
batches, both file and stdout goldens, full image hashes, quiet gates,
joined processes and persistent archive verify. [The matched Lython
source pair](../2026-11-23-owned-class-module-slots/README.md) measures
the implementation change with the same invoice.

[Identity](identity.json) · [Summary](summary.json) ·
[Observations](observations.tar.xz) · [Procedures](procedures.tar.xz) ·
[Terminal receipt](terminal-status.json).
