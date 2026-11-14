# Current complete-script CPython checkpoint

R372/B761, 2026-11-14 UTC. Committed source `4beb9ecf` / frozen `bed6e57e`
against stockCPython3.13.16. [Full43649993181passes](../2026-11-14-builtin-scalar-publication/delivery/README.md):
Windows/Ubuntu actual11/10, originalstress/installedRelease consumers;582-input
source proof. The image was frozen before commit at0ceb with the five production
changes; all source bytes map to4beb with explicit newline normalization. Independent
rebuilt binary identity is not inferred. Same fixed worker/adapter0.4/catalog and
trusted Python scripts/data. Core remains decoupled; publication held.

| Complete script /phase | Lython | CPython | Lython /CPython | Status |
| --- | ---: | ---: | ---: | --- |
|scripts.csv-fulfillment.large /warm|7.895–8.100 ms|6.804–6.867 ms|1.15–1.19×|Variable 4beb; spreads 2.53/0.93%; max IQR 2.15%|
|scripts.csv-fulfillment.large /compile|128.043–128.679 µs|115.542–115.561 µs|1.11×|Diagnostic 4beb; spreads 0.49/0.02%; max IQR 1.12%|
|scripts.csv-fulfillment.large /compile-run|8.268–8.355 ms|6.936–6.968 ms|1.19–1.20×|Diagnostic 4beb; spreads 1.04/0.47%; max IQR 1.80%|
|scripts.csv-invoice.large /warm|7.554–7.555 ms|6.141–6.147 ms|1.23×|Diagnostic 4beb; spreads 0.01/0.10%; max IQR 1.70%|
|scripts.text-summary-helpers.nonlocal-counter.large /warm|1.305–1.365 ms|0.895–0.896 ms|1.46–1.53×|Variable 4beb; spreads 4.46/0.06%; max IQR 1.22%|

Above one means Lython slower. Ranges span independent process medians and ratios
all four engine pairings; they are not confidence intervals. Fulfillment's Lython
processes vary2.53%; text4.46%while CPython varies0.06%. Retain those uncertain
rows and all IQRs, wider CPU and warmups. Invoice is stable within this collection.
This refresh does not attribute changes across older collections to R370, erase
its invoice regression debt, or predict embedding latency. Fulfillment compilation
is about.13ms versus~8ms execution; analysis is not a demonstrated dominant cost.

One five-job200.366s collection, quiet AzureF4as_v7/Ubuntu24.04.4/fourEPYC9V45/
16GiB/no swap/.NET11RC3, normal workstationGC/tiering and PythonGIL/GC. Two fresh
processes per engine/job,5sfull-request warmups/12stext,5rotating.5sbatches.
Warm clocks include mediated RAM-file I/O, full stdout/file validation and prior-
result cleanup. Exclude startup/compile/fixture staging/reset/protocol and final
cache teardown. Fresh adds per-invocation compilation and compiled-owner disposal.
Compile alone excludes execution. Wider process CPU also includes reset/protocol.
Lython compiles/validates its supported subset; CPython uses its normal compiler.
Startup, pending-I/O latency, contention, heap/RSS peaks and embedding tails remain
unmeasured. Diagnostic profiling overrides were absent from this collection.

Every library/catalog/source/golden/output/file/budget/stockPython/quiet/join/archive
gate passes. No new core/catalog uploaded; current images/auxiliaries/toolchains
are hardlinked from the already-verified image. One exact duplicate B758rawJSON
is removed only after committed/local/remote archive member SHA and path/UID/link
proof under idle lease. Original compressed archive/localGit evidence survives.
254/252MiB floors unchanged; final free265,969,664B. Raw persists and verifies
under lease before RAM teardown; no users/fixtures remain. SHA256:
`0f2a451f35b07799a1fc29d6b8b6c842c9670f0ec6b76442d33a6cd0e86c3690`.

The primary guide updates these changed-source rows; protected search and the
other historical workloads retain their exact earlier images. No unchanged
nine-job rerun. The remaining whole-script objective and explicit debt decisions
continue independently of final regex adapter producer publication.

[All timings/controls/CPU](summary.json),[exact worker/image identity](identity.json),
[raw](observations.tgz),[collection/custody](collection-and-custody.tar.xz),
[previous guide](prior-BENCHMARKS.md).
