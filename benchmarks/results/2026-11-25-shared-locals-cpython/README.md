# B1663 Production scripts against CPython

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 24,
2026 UTC. It compared Lython `788be8f35` to stock CPython 3.13.16. The
fresh Release build includes the committed capture rollback and the two
direct-return changes. The unchanged worker and adapter match the
frozen B1662 versions. Both engines verify complete output and file goldens.

Ranges span four pairings of process medians. Each of two workers per
engine warms for at least five seconds and runs seven rotated batches.
Ratios divide Lython time by CPython time.

| Script | Boundary | Lython | CPython | Elapsed ratio | Wider CPU ratio | Elapsed worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Shipment callback report | Warm | 2.64784–2.70685 ms | 1.63242–1.64563 ms | 1.609–1.658× | 1.556–1.616× | 2.23 / 0.81% |
| Generated rules with defaults | Compilation only | 0.02192–0.02232 ms | 0.04156–0.04162 ms | 0.527–0.537× | 0.529–0.536× | 1.82 / 0.13% |
| Quote-heavy invoice | Warm | 6.21792–6.22559 ms | 6.29574–6.41623 ms | 0.969–0.989× | 0.951–1.009× | 0.12 / 1.91% |
| Configuration manifest | Warm | 0.18483–0.18905 ms | 0.15317–0.15646 ms | 1.181–1.234× | 1.034–1.063× | 2.28 / 2.15% |
| Generated rules with defaults | Warm | 0.88223–0.91955 ms | 0.68946–0.69164 ms | 1.276–1.334× | 1.251–1.345× | 4.23 / 0.32% |
| Shipment callback report | Compile-to-result | 2.81043–2.81311 ms | 1.91096–1.91609 ms | 1.467–1.472× | 1.432–1.450× | 0.10 / 0.27% |
| Configuration manifest | Compile-to-result | 0.37256–0.37438 ms | 0.45948–0.46091 ms | 0.808–0.815× | 0.793–0.808× | 0.49 / 0.31% |
| Generated rules with defaults | Compile-to-result | 0.97829–1.00012 ms | 0.72680–0.73392 ms | 1.333–1.376× | 1.321–1.375× | 2.23 / 0.98% |

Warm elapsed covers applicable RAM-backed I/O, output and file
verification, and disposal of previous results. Lython uses a mediated
host with efficient ranges, appends and an active shared allowance.
CPython uses its file APIs. Compilation includes compiled-owner disposal.
Time to result measures both phases in one request. Wider process CPU
includes fixture reset and request framing. The clocks exclude startup
and final process teardown.

The shipment report's 2,048 rows expose the largest gap in this set. The
defaulted rules still leave a warm and through-result gap. The earlier
B1656 checkpoint was faster through result, and the source of that
difference remains unresolved.
Invoice warm elapsed is near parity and CPU spans parity. The manifest is
slower warm and faster from compilation through result.

Both quiet gates and all 224 timed batches pass. The exact 32-file worker
image and Python executable verify before and after collection. Workers
join, fixture scratch is removed and the persistent archive verifies.
These comparisons are diagnostic. [Previous checkpoint](../2026-11-24-inline-defaults-cpython/README.md).

[Summary](summary.json) · [Identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal receipt](terminal-status.json) · [Procedures](procedures.tar.xz).
