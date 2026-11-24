# B1656 Representative scripts against CPython

This comparison measures Lython `6b41db7b3` and stock CPython 3.13.16 on
November 24, 2026 UTC. The Azure F4as_v7 ran Ubuntu 24.04.4 and .NET 11 RC3.
Each lane uses two warmed workers per engine with seven rotated batches per
worker. Ranges span the four pairings of worker medians. Ratios divide
Lython by CPython elapsed or wider process CPU time.

| Script | Boundary | Lython | CPython | Elapsed ratio | Wider CPU ratio | Elapsed worker spread Lython / CPython |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| Generated rules with defaults | Warm | 0.90361–0.95646 ms | 0.68706–0.68754 ms | 1.314–1.392× | 1.314–1.402× | 5.85 / 0.07% |
| Configuration manifest | Compile-to-result | 0.37001–0.37281 ms | 0.47042–0.47427 ms | 0.780–0.793× | 0.765–0.804× | 0.76 / 0.82% |
| Generated classifier | Compilation only | 0.32944–0.33057 ms | 0.81943–0.82016 ms | 0.402–0.403× | 0.404–0.410× | 0.34 / 0.09% |
| Short Unicode word-count report with a nonlocal helper | Warm | 0.92332–0.95067 ms | 0.94036–0.94581 ms | 0.976–1.011× | 0.969–0.996× | 2.96 / 0.58% |
| Quote-heavy CSV policy invoice | Warm | 6.31467–6.37929 ms | 6.27986–6.30539 ms | 1.001–1.016× | 0.977–1.036× | 1.02 / 0.41% |
| Generated rules with defaults | Compilation only | 0.02260–0.02262 ms | 0.04123–0.04160 ms | 0.543–0.549× | 0.544–0.553× | 0.08 / 0.89% |
| Short Unicode word-count report with a nonlocal helper | Compile-to-result | 0.97879–0.98058 ms | 1.07920–1.08718 ms | 0.900–0.909× | 0.895–0.898× | 0.18 / 0.74% |
| Configuration manifest | Warm | 0.18420–0.18750 ms | 0.15496–0.15530 ms | 1.186–1.210× | 1.032–1.046× | 1.79 / 0.22% |
| Generated rules with defaults | Compile-to-result | 0.89763–0.91158 ms | 0.73299–0.73394 ms | 1.223–1.244× | 1.232–1.247× | 1.55 / 0.13% |

Warm elapsed covers applicable RAM-backed I/O, verification of output and
files, and disposal of previous results. Lython uses a mediated file host
with efficient ranges and appends plus an active shared allowance. CPython
uses its file APIs. Compilation includes compiled-owner disposal. Time to
result measures both phases inside one request. Wider process CPU includes
fixture reset and request framing. Process startup and final teardown sit
outside these clocks.

Defaulted rules take 1.314–1.392× CPython warm and 1.223–1.244× through
result. Their two Lython warm workers differ by 5.85%. The configuration
manifest takes 1.186–1.210× warm and 0.780–0.793× through result. The short
report and quote-heavy invoice are close to warm parity. Compilation
of the generated classifier takes 0.402–0.403× CPython.

The collection uses the exact candidate image from the
[paired comparison](../2026-11-24-inline-positional-default/README.md).
Its committed production source matches the measured patch and reference.
Both machine gates and all 252 batches pass. Complete goldens, worker
identity, CPython identity, worker joins, fixture cleanup and persistent
archive all verify. These figures are diagnostic. The prior broad
[CPython comparison](../2026-11-24-frontend-cpython/README.md) remains dated
and separate.

[Summary](summary.json) · [Identity](identity.json) ·
[Observations](observations.tar.xz) · [Terminal receipt](terminal-status.json) ·
[Procedures](procedures.tar.xz).
