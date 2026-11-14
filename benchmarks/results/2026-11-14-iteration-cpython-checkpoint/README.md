# Iteration delivery CPython checkpoint

2026-11-14 UTC, R347/B717: committed `009d236a` / frozen `8ae1ae5f`,
581 production inputs, stock CPython 3.13.16. Full Windows/Ubuntu .NET 11/10,
original stress and installed-package CI43563853239 was pending at collection
and now [passes](../2026-11-14-indexed-sequence-cursors/delivery/README.md).
This includes shared integer chains, native builtin-for prefixes, indexed cursors
and the explicit-LF fixture correction; it is not the earlier qualified1778 source.
Publication/tagging remain held.

| Complete script / phase | Lython ms | CPython ms | Lython / CPython | Lython/CPython process spread | Max IQR |
| --- | ---: | ---: | ---: | ---: | ---: |
|csv-invoice.large /warm|7.599619–7.872715|6.020321–6.047259|1.26–1.31×|3.47/0.45%|1.82%|
|csv-invoice.large /compile|0.101315–0.102321|0.093146–0.093415|1.08–1.10×|0.99/0.29%|1.11%|
|csv-invoice.large /compile-run|7.714700–7.778332|6.267073–6.380068|1.21–1.24×|0.82/1.80%|1.58%|
|text-summary-helpers.nonlocal-counter.large /warm|1.327342–1.420706|0.891018–0.896567|1.48–1.59×|6.57/0.62%|1.70%|
|json-summary-helpers.large /warm|3.406603–3.416350|4.103179–4.128542|0.83–0.83×|0.29/0.62%|1.86%|
|file-search.large /warm|0.269828–0.280750|0.159672–0.162647|1.66–1.76×|4.05/1.83%|1.67%|
|report-render.large /warm|2.782314–2.799973|2.321051–2.336665|1.19–1.21×|0.63/0.67%|4.81%|
|csv-fulfillment.large /warm|8.840486–8.878284|6.763770–6.813428|1.30–1.31×|0.43/0.73%|1.77%|
|csv-check-digits.for-text.large /warm|5.366891–5.390009|4.181989–4.196480|1.28–1.29×|0.43/0.35%|1.44%|

Ratio = Lython elapsed / CPython elapsed, above one means slower. All pairings of
two independent process medians produce the ranges, not confidence intervals.
Warm invoice/search process spreads3.47/4.05% and captured text6.57% remain
inconclusive stability. The earlier catalog and all adverse controls survive.
No gain or regression is attributed by subtracting these from older collections.

The quiet Azure F4as_v7 VM runs Ubuntu24.04.4, four EPYC9V45 vCPUs,
16GiB/no swap, .NET11RC3 ordinary workstation GC/tiering and stock Python GIL/GC.
Unchanged B685.NET/Python workers and optional0.4adapter verify all exact images,
stdout/files and shared-budget balances. Eight prior jobs retain their sources,
fixtures and independent goldens; the ninth is the identical4,096-row CSV script
used in R345/R346, independently checked under isolated CPython3.13.2.

Each job has two fresh processes per engine, five rotating0.5s batches sized by
complete-request wall time, five-second warmup (12s captured text/report).
Warm guest clocks include mediated RAM-file I/O, full stdout/file validation and
prior-result cleanup; they exclude compilation/startup, fixture reset/protocol
and final cache shutdown. Compilation alone includes compiled-owner disposal;
compile-to-result adds compilation/disposal to execution. Wider process CPU
includes reset/protocol. Independently warmed phases cannot be added/subtracted
as attribution. No startup/RSS/heap/shared-contention/pending-I/O/tail claim.

Nine jobs finish360.55s inside a480s owner (420s work guard). All source/image/
golden/balance/quiet/join/archive checks pass; no workers or RAM fixtures remain.
The frozen existing VM image is hardlinked, with every auxiliary checked and no
new core copy or evidence retirement. Disk floors remain unchanged.
Persistent raw SHA256:
`a3f6b61efc7cc751eb3659e002ca54a818301aabfcd0d0d5678b07727e832e79`.

[Raw observations](observations.tgz), [summary/identities](summary.json),
[tools/catalog](collection-tools.tar.xz), [previous guide](prior-BENCHMARKS.md).
The exact production source/core remains in the
[R346 archive](../2026-11-14-indexed-sequence-cursors/sources-checks-and-observations.tar.xz).
This checkpoint informs further complete-script redesign; it does not complete
the performance objective or claim a uniformly improved runtime.
