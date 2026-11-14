# Typed builtin scalar publication

R370/B759, 2026-11-14 UTC. Retain frozen `bed6e57e` against qualified
`3994dfda` / `b5a6426e`, pending full platform/stress/installed-consumer checks.
All582production inputs and the complete frozen worker/adapter image are recorded.
No public API, regex contract or package dependency change; publication held.

Shared int-text and length kernels return BigInteger directly through the existing
managed invocation/exception/cleanup wrapper. Closed text calls use the same
admitted parser directly. Ordinary observations still box; native arithmetic can
publish into its existing integer bank without first allocating an object result.
No extra bank for nonnumeric helpers, second parser/binder/selector, value framework,
pool or analysis pass. Exact wider promotion, stable aliases, materialization before
calls/returns, async protocols, stack protection and cancellation remain. Major
parse estimates still conservatively fund box headroom even for a private result.

Separate complete warm allocation diagnostics: fulfillment5,762,044.25→4,713,427.75B
(−18.20%); invoice5,245,458.125→4,983,303.25B(−5.00%); text and short controls flat.
Both roles measured with identical existing allocation tool/catalog. Process-wide
CLR allocation, no forced GC; not CPU, RSS or embedding-latency evidence. Includes
reset/mediated I/O/full output-file checks/result cleanup, excludes compilation,
startup/final cache teardown. Every invocation returns the shared budget to baseline.

| Complete script /phase | Elapsed change | Wider CPU change | Reference/candidate process spread |
| --- | ---: | ---: | ---: |
|scripts.csv-fulfillment.large /warm|-3.95%..-3.02%|-4.84%..-4.84%|0.27/0.69%|
|scripts.csv-fulfillment.large /compile-run|-5.59%..-2.64%|-5.28%..-1.46%|1.26/1.82%|
|scripts.csv-invoice.large /warm|+0.94%..+1.87%|+2.00%..+4.00%|0.59/0.32%|
|scripts.text-summary-helpers.nonlocal-counter.large /warm|+0.53%..+4.28%|-1.43%..+5.98%|1.75/1.95%|
|diagnostic.loop-helper.short-zero.large /warm|-3.57%..+4.22%|-3.24%..+4.51%|7.08/0.42%|
|diagnostic.loop-helper.observed.large /warm|-1.37%..+6.83%|-1.23%..+8.51%|2.18/5.62%|
|scripts.json-summary-helpers.large /warm|+0.22%..+1.38%|-1.27%..+1.42%|0.93/0.23%|

Negative means lower/faster. All independent process-median pairings, not
confidence intervals. Fulfillment has a modest useful CPU improvement. Invoice
is adverse with stable processes: retain its measured0.94–1.87%elapsed/2–4%CPU
regression as explicit0.11debt, to correct or decide before release. Text remains
adverse/uncertain; short and observed controls move substantially. JSON CPU is
mixed. No neutrality, universal/cumulative speedup or attributed layout cause.
The allocation benefit and shared typed publication justify this bounded stage;
they do not close the substantial whole-script performance objective.

One seven-job269.972s quiet collection, AzureF4as_v7/Ubuntu24.04.4/fourEPYC9V45/
.NET11RC3, normal GC/tiering and fixed worker/nonpackable adapter0.4. Four fresh
processes/job, five-second full-request warmups/twelve for text, five rotating
half-second batches. Warm includes mediated RAM I/O, full output/file validation
and prior-result cleanup; excludes startup/compile/reset/protocol/final cache
teardown. Compile-run adds per-invocation compilation/disposal. Wider CPU includes
reset/protocol. CPython external timings are not refreshed by this source pair;
the guide keeps exact earlier revisions. Startup/heap/tail latency remain unmeasured.

Focused actual11/10 each62internal+561public pass,136isolatedCPythoncontrols and
16source/phasegoldens match. New cases preserve boundaries/Unicode/bytes/errors,
rebinding/protocol order, aliases, wide working storage, retained results across
executions, genuine I/O/cancellation/join and terminal parse refusal. The first
internal run found a reflection delegate still expecting the old object result;
change its return type only, preserving every cancellation/admission assertion.
The original four failed cases/logs remain archived; no runtime recovery path added.

All image/catalog/golden/file/budget/quiet/join/archive gates pass. Exact withdrawn
C3twoaliases match committed evidence and retire under idle lease only after all
path/UID/inode/link/hash checks; all current images/catalogs/toolchains survive.
Actual planned staging bytes, unchanged254MiB staging/252MiB archive floors.
After archive265,588,736Bfree. Raw bytes verify under lease before RAM teardown;
no VM users/fixtures remain. SHA256:
`20160450963b2396dcbfabbb98c962f8caeb6bd4f86a2f25e50e3068b96b84ed`.

[Decision and completion debt](decision.json),[all timing/control/IQR](timing-summary.json),
[allocation](allocation-summary.json),[raw](observations.tgz),
[source/image/local checks/first failure/custody](sources-and-local-checks.tar.xz).
