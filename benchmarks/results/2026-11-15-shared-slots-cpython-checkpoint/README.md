# Shared-slot complete-script CPython checkpoint

B764, 2026-11-15 UTC, committed `cf754dc7` / frozen `95798eb9` versus stock
CPython 3.13.16. All 585 inputs match the frozen image with explicit newline
normalization; independent rebuilt binary identity is not inferred. Full delivery
qualification is pending independently in CI 43661215938. Same unchanged
worker, optional 0.4 adapter, scripts and data. Core remains decoupled.

| Complete script / phase | Lython | CPython | Lython / CPython | Status |
| --- | ---: | ---: | ---: | --- |
| scripts.csv-fulfillment.large / warm | 8.017–8.121 ms | 6.721–6.777 ms | 1.18–1.21× | Diagnostic cf754; spreads 1.27/0.82%; max IQR 1.02% |
| scripts.csv-fulfillment.large / compile | 129.351–130.314 µs | 115.500–115.824 µs | 1.12–1.13× | Diagnostic cf754; spreads 0.74/0.28%; max IQR 1.52% |
| scripts.csv-fulfillment.large / compile-run | 8.338–8.439 ms | 6.953–7.055 ms | 1.18–1.21× | Diagnostic cf754; spreads 1.19/1.46%; max IQR 1.58% |
| scripts.csv-invoice.large / warm | 7.358–7.414 ms | 6.087–6.100 ms | 1.21–1.22× | Diagnostic cf754; spreads 0.76/0.23%; max IQR 1.96% |
| scripts.text-summary-helpers.nonlocal-counter.large / warm | 1.259–1.272 ms | 0.885–0.888 ms | 1.42–1.44× | Diagnostic cf754; spreads 1.04/0.33%; max IQR 0.60% |

Above one means Lython slower. Ranges span independent process medians and all
four engine pairings, not confidence intervals. Keep every process, IQR and
wider-CPU observation. These are diagnostic measurements of complete scripts;
they do not attribute changes across older collections or erase the observed-
value and possible fulfillment regression in the R373 source pair.

Five jobs/199.457s on quiet Azure F4as_v7, Ubuntu 24.04.4, four EPYC 9V45 vCPUs,
16 GiB/no swap, .NET 11 RC3, ordinary workstation GC/tiering and Python GIL/GC.
Two fresh processes per engine/job, 5s full-request warmup (text 12s), rotating
five 0.5s batches. Warm includes mediated RAM-file I/O, full stdout/file validation
and prior-result cleanup; excludes startup, compilation, fixture reset/protocol
and final cache teardown. Fresh adds compilation/compiled-owner disposal; compile
alone excludes execution. Wider process CPU includes reset/protocol. Lython
compiles/validates its supported subset; CPython uses its normal compiler.
Startup, contention, pending-I/O latency, physical heap/RSS and embedding tails
remain unmeasured. Fulfillment compilation ~0.130ms versus ~8ms execution does
not establish frontend dominance.

Every image/catalog/source/output/file/budget/stock-Python/quiet/join/archive gate
passes. No new image/catalog upload, retirement or deduplication. Existing
254/252MiB floors remain; final free 266,305,536 B. Raw is verified persistently
under lease before RAM teardown, every worker joined, no VM users/fixtures remain.
Archive SHA256:
`a30d5a34db5bf698fcb0a92a66eefc6828ed3916133d62cd7afc3e05fd50ba5d`.

[All raw timings/controls/CPU](summary.json), [image identity](identity.json),
[raw archive](observations.tgz), [collection/custody](collection-and-custody.tar.xz),
[previous guide](prior-BENCHMARKS.md). Publication remains held; the whole-script
performance objective and independent regex work continue.
