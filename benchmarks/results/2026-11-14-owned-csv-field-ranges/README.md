# Owned CSV field ranges: runtime withdrawn, regressions retained

2026-11-14, R355. Withdraw frozen510a3f86 after the corrected B728comparison.
All581production inputs are restored exactly to qualifiedf5817a15/frozen9cfb.
Remove the new field-end state, CopyWithinWritten primitive and borrowed-line
pre-copy/raw-syntax retention contract. Keep the18public independent CPython/
I/O/lifetime cases and stronger actual-prefix/capacity/funding refusal assertions.
Forced nonincremental actual .NET11/10 builds and70focused restored checks pass
(17internal/18public per target). Runtime qualification43583650094 is reused;
new test qualification is focused, with full test/platform batching deferred to a
coherent checkpoint. No new runtime/API/package change or publication.

The proposal kept ordinary bytes in owned raw input and rewrote only escapes
before making independent fields. It removed a real copy stage, but introduced
more position state and retained raw syntax until each record ended, increasing
scratch/earlier-denial exposure. Whole invoice/fulfillment/text allocation is flat.
The demonstrated useful benefit does not justify that cost. This is a contextual
tradeoff decision, not a fixed-percentage veto or proof of inherent CPU regression.
New owning evidence or a less costly representation can justify reconsideration.

| Complete script / phase | Elapsed change | Wider CPU change | Reference/candidate process spread |
| --- | ---: | ---: | ---: |
|diagnostic.csv-fulfillment.borrowed.large /warm|-0.36..+0.01%|-0.99..+0.00%|0.23/0.15%|
|diagnostic.csv-fulfillment.escaped.large /warm|-1.02..-0.54%|-0.93..+0.06%|0.47/0.02%|
|scripts.csv-fulfillment.large /warm|-2.39..+0.71%|-2.48..+1.71%|0.47/2.63%|
|scripts.csv-fulfillment.large /compile-run|-2.57..-1.39%|-2.54..-1.71%|0.35/0.85%|
|scripts.text-summary-helpers.nonlocal-counter.large /warm|+0.50..+3.09%|+1.19..+3.05%|0.08/2.44%|

Negative means lower/faster; all independent process-median pairings, not confidence
intervals. Borrowed CSV is effectively flat; escaped CSV has~0.5–1%elapsed direction
and uncertain CPU. Normal warm is mixed, fresh modestly improves; unchanged text
is adverse and candidate processes vary. No large/universal/neutrality claim.
Do not add percentages or attribute the text control to a particular source line.

B728uses exactly the original510a/9cfb images/data/settings, one catalog and
diagnostic cases first. Five jobs finish375.423s under480sownership, all image/
auxiliary/golden/budget/quiet/join/archival gates pass. Four fresh processes/job,
12sfull-request warmups, five rotating1s batches, ordinary GC/tiering, fixed workers
and optional nonpackable0.4adapter. Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3.
Warm clocks include mediated RAM-file I/O, stdout/file validation and prior-result
cleanup; exclude compile/startup, fixture reset/protocol and final cache teardown.
Fresh adds compilation/compiled-owner disposal; wider CPU includes reset/protocol.
No external CPython timing, startup/RSS/tail/shared-contention/pending-I/O claim.

Both new control cases were independently verified by isolated CPython3.13.2 and
actual frozen workers before measurement. Borrowed DictReader(source.readlines())
materializes input lines first; escaped-SKU fixture uses quoted fields containing
literal quotes/commas/emoji. Their algorithms/data differ from normal fulfillment,
but each source pair is identical. They are sensitivity controls, not algorithm gains.

The[original collection](initial/README.md) remains TERMINAL FAILED: copied
diagnostic routing referenced undefined control_catalog after two normal phases.
Their partial clocks lack final gates and remain diagnostic, with every adverse
point and all setup corrections preserved. Remove only stale routing and change
case order for the separately owned correction; no old-owner restart. A local
confirmation PLAN-anchor error was repaired without remote work or source/data
changes. Both collections' users are joined and RAM fixtures closed; every byte
is archived/verified persistently. Current9cfb/510a images, auxiliaries/catalogs/
toolchains and254/252MiBfloors remain; correction uploaded no new image/catalog
and retired nothing. Successful rawSHA256:
`916d31336aeeeb424737c0a3b11a09ee5ef5ef879a8e7ff2a669cf8bddeb6fe5`.

[Decision](decision.json),[all timings/wider CPU](timing-summary.json),
[successful raw](comparison/observations.tgz),[confirmation/restoration checks](confirmation-and-restoration.tar.xz),
[restored checks](restored-checks.json),[original source/core/failures](initial/README.md).
This closes the bounded CSV migration by withdrawal, while preserving useful
coverage and moving to the next substantive execution/operand question.
The performance objective remains active; publication/tagging remain postponed.
