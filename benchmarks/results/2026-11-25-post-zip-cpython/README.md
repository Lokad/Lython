# B1669 Production scripts against CPython after direct zip unpacks

The quiet Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3 on November 25,
2026 UTC. It compared Lython `a45b6da7f` with stock CPython 3.13.16. The fresh
Release build includes the committed zip unpack and single-comparison parser
changes. Both engines verify complete output and file goldens. Ranges span
the four pairings of process medians, using two warmed workers per engine
and seven rotated batches per worker. Ratios divide Lython time by CPython
time.

| Script | Boundary | Lython | CPython | Elapsed ratio | Wider CPU ratio | Elapsed worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Shipment callback report | Warm | 2.39100–2.48841 ms | 1.62448–1.63355 ms | 1.464–1.532× | 1.436–1.543× | 4.07 / 0.56% |
| Generated rules with defaults | Compilation only | 0.02190–0.02229 ms | 0.04149–0.04185 ms | 0.523–0.537× | 0.526–0.557× | 1.78 / 0.89% |
| Quote-heavy CSV policy invoice | Warm | 6.21554–6.23104 ms | 6.36763–6.39008 ms | 0.973–0.979× | 0.958–0.983× | 0.25 / 0.35% |
| Configuration manifest | Warm | 0.18510–0.18554 ms | 0.15478–0.15563 ms | 1.189–1.199× | 1.035–1.042× | 0.23 / 0.55% |
| Generated rules with defaults | Warm | 0.90184–0.90818 ms | 0.68792–0.69404 ms | 1.299–1.320× | 1.324–1.330× | 0.70 / 0.89% |
| Shipment callback report | Compile-to-result | 2.52584–2.64295 ms | 1.91063–1.91196 ms | 1.321–1.383× | 1.310–1.357× | 4.64 / 0.07% |
| Configuration manifest | Compile-to-result | 0.36811–0.37172 ms | 0.45703–0.46407 ms | 0.793–0.813× | 0.798–0.810× | 0.98 / 1.54% |
| Generated rules with defaults | Compile-to-result | 0.95198–0.96078 ms | 0.72858–0.74569 ms | 1.277–1.319× | 1.304–1.344× | 0.92 / 2.35% |

Warm elapsed includes applicable mediated RAM-backed I/O for Lython or
native I/O for CPython, complete output and file verification, and disposal
of previous results. Lython uses efficient ranged reads and appends and an
active shared allowance. Compilation includes compiled-owner disposal.
Time to result measures compilation, execution and cleanup in one request.
Wider process CPU includes fixture reset and request framing. Startup and
final teardown are outside these clocks.

Shipment takes 1.464–1.532× warm and 1.321–1.383× through result. Both ratios
are lower than in B1663. B1665 provides the separate paired measurement of
the zip change. Defaulted rules take 1.299–1.320× warm and 1.277–1.319× through
result, with similar compile-only ratios to B1663. The invoice is within 3%
of parity. The manifest uses less time through result and more time warm.

Both quiet gates, all 224 timed batches, exact worker and CPython identities,
worker joins, guest-scratch cleanup and persistent archival verify. These
comparisons are diagnostic.
[Previous checkpoint](../2026-11-25-shared-locals-cpython/README.md).

[Summary](summary.json) · [Identity](identity.json) · [Observations](observations.tar.xz) ·
[Terminal status](terminal-status.json) · [Procedures](procedures.tar.xz).
