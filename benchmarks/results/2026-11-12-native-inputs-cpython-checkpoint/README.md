# Retained native-input runtime versus CPython

2026-11-12 (collection began 2026-11-11 UTC). Runtime
`2ec9d67f84a0b89361abb83b178abd344961ce8c`, exact frozen core `c9829194`, stock
CPython 3.13.16. The core has precommit assembly metadata; all production source
hashes match. [Identity](identity.json), [source proof](source-identity.json).
The normal optional adapter remains 0.4; no regex development producer is involved.

Eighteen jobs finish in **487.070 seconds** on the existing quiet Azure F4as_v7:
Ubuntu 24.04.4, four AMD EPYC 9V45 vCPUs, 16 GiB/no swap, .NET 11 RC3. Identical
sources and fixtures, two independent processes per engine, five-second warmups
per role/job and seven rotated 150 ms rounds. Identity, full stdout/file goldens,
allowance balances, quiet-before/after, joined users, RAM cleanup and persistent
archive verification pass. [Summary](summary.json), [receipt](archive-receipt.json).

| Complete script | Compile ratio | Warm ratio | Compile + one execution ratio |
| --- | ---: | ---: | ---: |
| Text / captured helper and nonlocal counter | 0.83–0.84×, variable | 1.74× | 1.57–1.63×, variable |
| Ordinary text summary/helpers | 1.05–1.06× | 0.99–1.06×, variable | 1.00–1.03× |
| CSV validation/helpers | 1.07–1.08× | 1.08–1.09× | 1.05–1.06× |
| JSON-lines/helpers | 1.00–1.03× | 0.85–0.89×, variable | 0.85–0.86× |
| JSON-to-Markdown report | 1.08–1.09× | 1.26–1.31×, variable | 1.26–1.29×, variable |
| File search / keyword-default helper | 0.91–0.94×, variable | 1.52–1.60×, variable | 1.51–1.55×, variable |

Ratio is Lython elapsed / CPython elapsed. Ranges contain all four independent
process-median pairings, not confidence intervals. Variable rows retain observed
control spread/IQR; the [current guide](../../../docs/BENCHMARKS.md) gives times,
input sizes and both-engine controls. Full details remain in the summary/raw data.

Warm and complete execution include mediated RAM-file I/O, full validation and
prior-result cleanup. Compilation includes code construction and disposal. Startup,
fixture staging/reset, protocol and final caches are excluded; wider process CPU
also includes reset/protocol. All external rows use synchronous Run. Independent
phase medians need not add up. Small file fixtures do not qualify large-file
throughput, pending-I/O latency, RSS or embedding tail latency.

The [combined source pair](../2026-11-12-native-inputs/README.md) is a separate
comparison with qualified `8f8628a4`. Its additional reportAsync CPU uncertainty
remains accepted; these synchronous external rows do not settle it. Neither gains
nor prior debt may be multiplied into the new CPython ratios. The [prior guide](prior-BENCHMARKS.md)
preserves every replaced row and older source-pair details, including adverse data.

Exact source has now passed full Windows/Ubuntu actual .NET 11/10, original stress
and installed Release package consumers in [43119174279](https://github.com/Lokad/Lython/actions/runs/43119174279).
[Terminal receipt](delivery-ci-terminal.json) supplements the original pending-at-
collection identities without editing them. Publication remains postponed.

[Raw observations and catalog](observations.tgz), [preparation](preparation.tar.xz),
[terminal owner](terminal-status.json) and [archive identity](archive-identity.json)
preserve the completed checkpoint. No worker or RAM fixture remains owned.
