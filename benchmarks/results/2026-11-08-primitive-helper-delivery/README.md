# Primitive helper proof and ordinary forwarding

2026-11-08. Retained combined R113/R114/R115 source over9b61264b, frozen core
93e926fe; reference fully qualified05790317/core13107e6b. Focused1526public/104
internal Release pass peractual.NET11/.NET10. Full delivery qualification pending.
Public API/Python behavior unchanged; publication postponed.

One pure compiler/runtime fact proves stable BigInteger/double/bool arithmetic,
comparisons/bitwise operations and existing string addition; execution uses the
canonical operators. Permanently unsupported native binary proofs are rejected
during preparation; their supported Python behavior stays ordinary. Existing
native proof nodes own immutable opcode descriptors and member/return spans,
removing runtime source/return/member offsets and the native/operand-forwarding
mutual exclusion. Ordinary code forwards loads even when a dynamic guard fails.
No new evaluator/backend/value representation/owner/cache/arena/call-control flag.

Independent CPython goldens cover mixed primitives, negative floor/modulo,
promotion, defaults/order, zero division/error context, Decimal/complex and custom
protocols. Actual suspended multiply/reflected protocols cancel and release funded
owners. Integer result refusal/after-admission cancellation preserve origin/cleanup.
Intermediate assertion/build-identity/measurement corrections remain linked below.

Same quiet F4as_v7/Ubuntu24.04.4/four EPYC9V45vCPUs/16GiB/no swap, .NET11RC3,
frozen worker7cf/optional0.4, normal GC/tiering. Two isolated roles/core,5swarmup,
7rotated150msrounds,8jobs224.458s. Warm includes invocation, mediated RAM-file I/O,
complete stdout/file validation and prior-result cleanup; excludes compilation,
startup/protocol/staging/final cache teardown. New accounting variations have4096
records and full CSV reports, independently checked with CPython3.13. They are
not a refreshed external performance comparison or proof of the historical gap.

| Script / API | Elapsed change across all pairings | Process CPU |
| --- | ---: | ---: |
| Integer CSV / sync |−5.17% to−3.80%|−3.45%|
| Integer JSON / sync |−4.93% to−3.47%|−9.24% to−3.57%|
| Float CSV / sync |−3.45% to−1.76%|−6.25% to−3.85%|
| Float CSV / async |−6.16% to−2.23%|−8% to0%|
| Custom operator CSV / sync |−0.87% to+2.57%|0% to+0.39%|
| Custom operator CSV / async |+1.28% to+3.36%|+6.67%|
| Unchanged text / sync |−2.69% to+1.69%|−2.41% to+2.44%|
| Unchanged report / sync |−0.74% to+0.54%|0% to+6.25%|

Negative is faster. Text old-role spread3.54% and custom-sync3.19% are inconclusive.
Report elapsed is nearbaseline but coarse CPU remains possible debt. Integer and
float gains are supported; custom async has measured regression debt. Rejected
proof attempts are source-visible, not isolated attribution of all CPU. Retain
the coherent simplification with that debt explicit; current whole-script CPU
assessment must include custom operators and text, without reviving slower
call-control/arena carriers. No universal CPU/speedup claim.

Incremental warm allocations are neutral. Direct qualified-reference compilation
allocation rises1.40–1.48%accounting/0.59%text, report unchanged; desktop elapsed is
uncertain. Proof nodes grow32→72B, ordinary instructions remain32B and forwarded
loads disappear. The admitted source-based compilation estimate remains coarse;
no claim of exact tiny-budget thresholds or free retained metadata.

All Ready/core/aux/golden/baseline/quiet/Closed/join/fixture/archive/local gates pass.
Live owner captured270s at startup; transient terminal unit reports infinity after
completion. No users or owned RAM remain. Exact-byte hardlinks under fresh idle
lease recovered11.17MB, every path/alias/content/archive retained, guards unchanged.

Preserved stages: [integer-only R113](../2026-11-08-integer-helper-prototype/README.md),
[independent proof R114](../2026-11-08-independent-native-proof/README.md), including
its adverse async-float+4.14–6.29% and limit metadata errors. These are different
source pairs; percentages are not additive and original debts are not erased.

[Status](status.json), [all roles](assessment.json), [identity](identity.json),
[incremental allocation](incremental-allocation.json), [direct compilation](compilation.json),
[receipt](archive-receipt.json), [live unit](live-unit.txt),
[all source/binaries/corrections/raw](observations.tar.xz).
