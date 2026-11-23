# B1527 function-scoped arithmetic against CPython

Retained `be20f34fc` and CPython 3.13.16 run the same function-scoped
report with 2,048 fully supplied calls. The quiet Azure F4as_v7 ran
Ubuntu 24.04.4 with .NET 11 RC3 on November 23, 2026 UTC.

| Phase | Lython | CPython | Lython / CPython |
| --- | ---: | ---: | ---: |
| Warm | 0.16576–0.16663 ms | 0.13036–0.13117 ms | 1.264–1.278× |
| Compile to result | 0.18063–0.18096 ms | 0.14512–0.15200 ms | 1.188–1.247× |

Ratios span four pairings of two process medians from each engine.
Warm worker spreads are 0.53% and 0.62%. First-use spreads are 0.18% and
4.74%. Warm execution uses compiled code and includes result verification
and prior-result disposal. Time to result adds compilation and code
disposal. The report has no file I/O. Startup and request framing sit
outside both elapsed clocks. The phases use independent workers.

All workers warm for eight seconds and contribute seven timed batches.
Goldens, all 56 batches, both quiet gates, full image hashes, worker joins,
fixture cleanup and the persistent archive verify.
[B1526](../2026-11-23-supplied-lexical-call/README.md) attributes the runtime
improvement against the immediate Lython reference. The module-scope
script has different clocks for both engines.
[Module comparison](../2026-11-23-supplied-operand-window-cpython/README.md).

[Identity](identity.json) · [Summary](summary.json) · [Observations](observations.tar.xz) ·
[Procedures](procedures.tar.xz) · [Terminal receipt](terminal-status.json).
