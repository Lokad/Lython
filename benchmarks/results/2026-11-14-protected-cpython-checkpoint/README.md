# Protected-script CPython checkpoint

B757, 2026-11-14 UTC. Exact committed `3994dfda` / frozen `b5a6426e`,
582-input committed-source proof attached. Full
[43640155007](https://github.com/Lokad/Lython/actions/runs/43640155007) is active;
qualification is not inferred. Stock CPython3.13.16, ordinary GIL/GC (not debug or
free-threaded), quiet Ubuntu24.04.4/F4as_v7/four EPYC9V45/16GiB/no swap/.NET11RC3,
normal workstation GC/tiering. Existing worker/catalog/adapter0.4, no new framework.

| Complete workload /phase | Lython | CPython | Lython/CPython | Status |
| --- | ---: | ---: | ---: | --- |
|scripts.csv-invoice.large.protected /warm|7.545–7.590 ms|5.936–6.086 ms|1.24–1.28×|Diagnostic 3994; spreads 0.59/2.46%; max IQR 1.35%|
|scripts.csv-invoice.large.protected /compile|103.845–105.523 µs|98.430–98.738 µs|1.05–1.07×|Diagnostic 3994; spreads 1.62/0.31%; max IQR 2.78%|
|scripts.csv-invoice.large.protected /compile-run|7.729–7.743 ms|6.218–6.237 ms|1.24–1.25×|Diagnostic 3994; spreads 0.19/0.31%; max IQR 1.30%|
|scripts.file-search.large.protected /warm|266.372–266.982 µs|160.576–162.048 µs|1.64–1.66×|Diagnostic 3994; spreads 0.23/0.91%; max IQR 2.60%|

Ratio above one means Lython is slower. Ranges use all independent process-median
pairings, not confidence intervals. CPython warm invoice processes spread2.46%;
Lython compile maxIQR2.78%, search2.60%. Keep uncertainty and every observation.
The native protected-path penalty is removed in R368's separate source pair;
these external numbers still show general invoice and file-search gaps. Do not
infer an embedding-latency, cumulative or universal improvement.

One four-job137.673s collection, two fresh processes/engine/job, five-second
full-request warmups and five rotating half-second batches. Warm includes mediated
RAM-file I/O, full stdout/file validation and prior-result cleanup; excludes
startup/compile/fixture reset/protocol/final cache teardown. Fresh adds compilation
and compiled-owner disposal; compile excludes guest execution. Wider process CPU
includes reset/protocol and is preserved separately. No forced GC or runtime policy
override. Pending-I/O latency, shared contention, heap/RSS and startup unmeasured.

Same source and every input/output file under both engines: invoice wraps the
pricing helper with try/except ValueError; search wraps its keyword-default helper
with try/except TypeError. Valid data and outputs match the earlier plain forms,
but exceptional behavior intentionally differs. These are representative defensive
forms, not a cross-collection speedup claim. Existing B747 plain-text/JSON/report/
fulfillment/for-CSV rows retain their own fa40/73a revision. Prior configuration
rows retain cdd9/f4c. No unchanged nine-job rerun or relabeling as current HEAD.

All image/aux/catalog/golden/file/budget/quiet/join/archive gates pass. Raw SHA256
`72d110eb225ad83d8f8c7b5e8121771b95fc1f3f70611b010f0a66889fed22a0`.
Evidence verifies under the lease before RAM teardown; no users/fixtures remain.
Only verified persistent image/catalog/worker hardlinks, no image/catalog upload
or retirement. Free267,259,904B;254/252MiBfloors/current images/catalog/toolchains
unchanged. Full CI remains independent; publication/tagging held.

[Summary and widerCPU](summary.json), [raw](observations.tgz),
[source proof](source-proof.json), [collector/analysis](collection-and-analysis.tar.xz),
[prior guide](prior-BENCHMARKS.md). Detailed prototype debt remains in
[R368](../2026-11-14-protected-helper-activation/README.md).

Exact measured3994/b5subsequently
[passes full43640155007](../2026-11-14-protected-helper-activation/delivery/README.md).
Original pending metadata/raw remains historical; later R369is excluded.
