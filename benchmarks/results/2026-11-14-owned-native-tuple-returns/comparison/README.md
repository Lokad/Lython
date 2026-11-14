# Owned tuple-return source comparison

2026-11-14UTC, B725. Exact committed `f5817a15` / frozen `9cfb7d5b`,
581production inputs matching the commit, versus qualified7e2f7cac/frozen72c9.
No external CPython timing or cross-collection attribution. Full delivery pending.

| Complete script / phase | Reference ms | Candidate ms | Elapsed change | Wider process CPU change | Reference/candidate process spread |
| --- | ---: | ---: | ---: | ---: | ---: |
|csv-fulfillment.large /warm|8.582121–8.659915|8.388540–8.474721|-3.13..-1.25%|-3.51..-1.82%|0.91/1.03%|
|csv-fulfillment.large /compile-run|8.909096–9.003641|8.584666–8.809049|-4.65..-1.12%|-5.25..-0.78%|1.06/2.55%|
|csv-invoice.large /warm|7.523676–7.597346|7.416334–7.429696|-2.38..-1.25%|-2.48..-1.73%|0.97/0.18%|
|text-summary-helpers.nonlocal-counter.large /warm|1.239444–1.277842|1.246001–1.286458|-2.49..+3.79%|-2.67..+2.57%|3.00/3.25%|

Negative means lower/faster. Ranges pair all independent process medians, not
confidence intervals. Complete fulfillment allocation separately falls6.77%;
invoice/text allocation are effectively flat. Invoice also improves in this
comparison, so do not attribute all CPU change specifically to tuple elimination.
Captured-text process spreads~3% and adverse medians remain uncertainty. Retain
the shared convention and eliminated construction; CPU direction is modest.
No large CPU or universal-neutrality claim; broader performance work continues.

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering, same fixed
B685workers, optional nonpackable0.4adapter and representative catalog/goldens.
Each job uses four fresh processes,12sfull-request warmups and five rotating1s
batches. Warm clocks include mediated RAM I/O, stdout/file validation and prior
result cleanup; exclude compile/startup, fixture reset/protocol and final cache
shutdown. Compile-to-result includes compilation/compiled-owner disposal. Wider
process CPU includes reset/protocol. No RSS/heap, genuinely pending I/O, shared-
contention or embedding-tail claim. Independent phases are not additive attribution.

Four jobs complete300.216s under one360sowner/300sjob-startguard.
All image/auxiliary/output/budget/quiet/join/archive gates pass. No VM users or RAM
fixtures remain. Raw SHA256: `fc2467d96a8a2b3f53b1cbeef30f019c83ac28b309eff0407f822c77f8cb6b2c`.
Initial preflight rejected legacy unmounted fixture directories before staging,
deletion/mount/service. Fresh inspection proved mount absence; corrected guard
started the first measurement owner. Original script/log/failed condition remain.
Only superseded71ba/8ae working core aliases were retired after canonical Git/SHA/
path/UID/all-alias proof; current72reference/new9cfb, all auxiliaries/catalogs/
toolchains and254/252MiBfloors remain. No measurement restart or floor relaxation.

[Decision](decision.json),[summary/wider CPU](summary.json),[raw](observations.tgz),
[collection/staging evidence](collection-tools.tar.xz),[581input proof](source-proof.json).
Exact source/core and focused checks remain in the[parent archive](../sources-checks-and-allocation.tar.xz).
Publication/tagging/adapter promotion remain held. Full qualification is independent
of timing uncertainty; this checkpoint does not complete the performance objective.

Measured source subsequently [passes full43583650094qualification](../delivery/README.md).
Collection-time pending metadata/adverse evidence remain unchanged; later CSV work
is separate.
