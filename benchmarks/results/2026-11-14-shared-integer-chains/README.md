# Shared closed integer-chain execution

2026-11-14UTC, R341frozen53becdb6 over fully qualified1778c30d/frozen38c0562f.
Decision: **retain**. One shared checked kernel removes the native-only arithmetic distinction and reduces complete inline-fulfillment allocation by10.45%. Affected warm elapsed improves2.98–4.01% and compile-to-result1.40–2.67%, with supporting wider CPU. This is a useful allocation/simplification milestone with modest CPU improvement, not completion of the substantial performance objective.

Move the native-only arithmetic scanner/evaluator into one checked kernel shared
by native and ordinary executable input operations. Both callers publish through
their existing lifetime machinery. Intermediate exact machine integers stay in a
local accumulator; only the final value is boxed. Overflow, unsupported operands/
operations and Python protocols keep exact ordinary arithmetic. Cancellation uses
the active token through the sequence. No tagged VM, shadow slots, pool, owner,
public API or new execution route. The displaced native-only file is removed.

Ordinary execution declines combined publication if pending disposal or owned
temporaries require per-op retirement. Published local/namespace stores, identity,
handler state, admission-before-effects and first refusal remain observable.
927internal/1871public focused Release cases pass each actual.NET11/10. Twelve new
public cases cover module/class/closure/loop/generator/handler arithmetic, exact
signs/overflow, aliases, error order, actual asynchronous numeric protocols without
replay, funding through awaited cleanup, terminal refusal despite suppression/
finally returns, CPU-loop cancellation and shared-allowance reuse. Six isolated
CPython3.13.2 controls supply independent expectations. Original two missing warm
file fixtures failed; corrected fixtures are seeded identically, with original
source/log/TRX retained and no production fix or assertion relaxation.

The complete4096-row fulfillment validation/pricing/aggregation/report script keeps
arithmetic inside an ordinary loop body. Source/fixtures/outputs are identical
across revisions and independently checked by CPython. Frozen inspection shows
the same37slots/170instructions, native=False before/after. Native helper and
captured-text controls keep their exact sources. Each frozen worker independently
verifies goldens and shared-budget reuse before timing.

Four desktop allocation processes show7,425,525 B versus6,649,560 B per affected
invocation, **−10.45%**; native-helper7,107,982 B and captured-text546,078 B controls
are flat. This includes reset/checks/cleanup; no forced GC or local timing claim.
Allocation reduction is separate from CPU and elapsed observations.

| Complete script / phase | Elapsed change | Wider CPU change | Process spread candidate/reference | Max IQR |
| --- | ---: | ---: | ---: | ---: |
|csv-fulfillment.inline.large /warm|-4.01..-2.98%|-5.18..-3.28%|0.07/1.00%|1.97%|
|csv-fulfillment.inline.large /compile-run|-2.67..-1.40%|-3.59..-1.69%|0.55/0.75%|1.49%|
|csv-fulfillment.large /warm|-2.96..+3.00%|-3.68..+1.54%|1.92/4.10%|1.67%|
|text-summary-helpers.nonlocal-counter.large /warm|+0.15..+2.30%|-1.39..+1.23%|1.38/0.73%|1.25%|

B711four jobs finish188.273s under one300s owner on the quiet
AzureF4as_v7/Ubuntu24.04.4/.NET11RC3. Ordinary workstation GC/tiering; unchanged
B685worker/optional0.4adapter, two fresh processes per revision/job, eight-second
full-request warmups and five rotating0.5s wall batches. Warm clocks include
mediated RAM-file I/O, complete stdout/file validation and prior-result cleanup;
compilation/startup, reset/protocol and final cache teardown are excluded.
Compile-to-result adds compilation/compiled-owner disposal. Wider CPU includes
reset/protocol. Process-median ranges are not confidence intervals. No CPython
timing, RSS/heap, embedding-tail or universal speedup is inferred. All adverse and
inconclusive controls survive; see the explicit[debt decision](decision.json).

All image/source/golden/allowance/quiet/join/archive gates pass; no workers or RAM
fixtures remain. RawSHAd0bf8d7a0c9fb4d8e6791038ba8c8b2e4bc41b717f49ea49031109e92605e87d. No VM floor relaxation, evidence
retirement or producer change. Full retained platform/stress/installed-consumer
delivery[passes43548640898](delivery/README.md):581inputs/16summaries cached. Publication held; this milestone does not complete the
whole-script performance objective.

[Timing/wider CPU](summary.json),[allocation](allocation-summary.json),
[exact frozen sources/bits, initial failure and focused checks](source-checks.tar.xz),
[raw](observations.tgz).
