# Loop-local heap-cell prototype: withdrawn

2026-11-14UTC, R358. Frozen97f5ecc1 vs fully qualified47df7e19/frozencbb254be,
ten changed production inputs. **Runtime withdrawn in full** after one usable
comparison; all581production input bytes are restored to the qualified baseline.
Twelve independent public cases remain. Forced nonincremental actual .NET11/10
builds and41focused restored checks per target pass. No publication or tag.

The prototype reused existing cells for private loop-carried integers and shared
primitive reads/updates/comparisons. It retained final object[]slots/direct calls,
exact promotion, major admission, cancellation and native/frame ownership.
Native working reservations survived through cleanup; destination frame funding
preceded source refunds. This avoided many boxes, but per-invocation cell/grant
and shared-reader overhead made ordinary short calls materially worse.

Quiet AzureF4as_v7, Ubuntu24.04.4, .NET11RC3, unchanged workers/auxiliaries and
normal GC/tiering. Four fresh processes/job,5sfull-request warmups (12scaptured
text), five0.5srotating batches. B733all8jobs complete304.959s with every
identity/output/file/budget/quiet/join/archive gate. Warm clocks include mediated
RAMI/O, validation and result cleanup; exclude compile/startup/reset/protocol/
final cache disposal. Compile-to-result adds compilation/disposal. Wider CPU
includes reset/protocol. No CPython/startup/tail-latency or cross-algorithm gain.

| Workload | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
|100,000-add counter, warm |−13.87..−11.01%|−14.79..−10.95%|
|4,096-row CSV while validation, warm |−14.04..−10.64%|−14.89..−10.49%|
|Same CSV, compile-to-result |−11.37..−9.69%|−11.01..−8.89%|
|4,096-row fulfillment, warm |+3.78..+6.00%|+3.51..+7.33%|
|3,072-line captured text, warm |−2.03..+1.13%|−4.00..+0.80%|
|JSON helper summary, warm |+1.25..+2.19%|+1.40..+4.15%|
|Repeated short/zero helpers |+39.94..+43.14%|+39.35..+43.23%|
|Helpers exposing every intermediate |+4.47..+9.65%|+4.85..+12.17%|

Counter allocation falls57.23%(7,518,770→3,216,018B/run); CSVwhile falls13.41%
(5,131,710.625→4,443,554.375–4,443,557.375B/run). Four fresh allocation
processes/reversed role order retain normalGC and budget checks; no CPU inference.
CSVwhile is a diagnostic variant of the maintained text-digit validator: only
the helper algorithm changes, both runtimes execute the identical variant, and
isolated CPython/output-file goldens independently match. It is not a runtime
speedup relative to the different algorithm.

2,170focused actual11/10checks and20independent CPython3.13.2controls pass.
Counter/CSVgains are useful, but ordinary-helper overhead and unrelated fulfillment
regression do not justify retaining heap cells or adding pools/adaptive thresholds
around them. This decision uses complete tradeoffs, **not a rigid percentage veto**
or a claim that deferred integers cannot work. Text remains inconclusive; every
adverse sample survives. No unchanged retiming is needed to make this decision.

Original638ee71e stores-only form cut counter allocation57.23%but increased
CSVwhile allocation4.47%. Completing primitive arithmetic reads removed that
materialization cost; both sources/results survive. A direct balance test also
caught an initial erroneous working-reservation Commit, which accumulated96B
per helper. It was fixed without weakening the assertion;20,000helpers fit512KiB.
An unconditional-unbound fixture correctly failed frontendLA3146; conditional
binding covers runtime UnboundLocalError. An inspected chain-decline PC hazard
was corrected/tested in the prototype, not claimed as an existing baseline bug.

On withdrawal, remove cell preparation/charges/per-call reservations and
cell-aware reader additions. Delete the white-box test enforcing that discarded
per-call admission point. Keep public aliases/types/errors/locals snapshots,
wide helpers, exact overflow chains and bounded repeated-helper coverage.
Established cancellation/refusal/async cleanup tests remain. Qualification of
restored47df/cbb2is[already cached](../2026-11-14-shared-machine-integer-kernel/delivery/README.md);
new public tests will join the next full checkpoint.

Next assess primitive working storage alongside existing final object slots:
eliminate private heap cells/grants, preserve stable materialization on alias/
call/return, fund substantial frame storage/overlap, and retain managed stack
safety. Avoid cell pools, a universal guest-type rewrite or argument repackaging.
The broader representative whole-script objective remains active.

[Decision](withdrawal.json),[timing](timing-summary.json),[allocation](allocation-summary.json),
[restored checks](restored-checks.json),[all sources/binaries/checks/failures/raw](sources-checks-and-observations.tar.xz).
