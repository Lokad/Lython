# Managed call-descriptor transport — R71 / B426–B427

2026-11-08. Retained correction on f88191f7. Measured cores bc30d424 (.NET 11) /
051865c7 (.NET 10); exact inventories/patch/binaries preserved. Public APIs and
Python semantics unchanged. Full delivery of this correction remains pending.

## Concrete source correction, bounded attribution

R71 consolidated nonmirrored keyword/variadic calls onto the linear frame runner
and reduced returned-record allocation about 13%, with adverse ordinary-CSV elapsed
and coarse CPU observations. B424/B425 sources and every original result remain
[separately preserved](../2026-11-08-linear-keyword-calls/README.md).

One unchanged-source baseline/current full-CSV attribution, B426: 1,124 guest
samples per role, 5.65/5.66 guest CPU seconds, unknown leaves 78/60, no malformed
maps or empty/single-frame chains. Call-preparation leaf samples are 36 in the old
method versus 15+13 in the split methods; inclusive stacks overlap and include
useful execution. This is not evidence of a dominant new call cost or a causal
timing explanation. Normal tiering/GC, diagnostic PerfMap/JIT overrides, perf
199 Hz/frame pointers; missing/inlined/native frames and sample skid remain.

Windows x64 Tier1 already elides the suspected positional descriptor copy. Actual
Linux Tier1 does not: all three new BeginBoundCall call sites copy the 48-byte
BoundCallArguments onto outgoing stack storage using reference stores/movsq.
The corrected synchronous entry borrows its existing readonly local with managed
`in`. Values/owners still transfer into existing work/frame fields; the reference
never survives this synchronous method. Actual async binding awaits first, then
enters the same method. No unsafe mechanism, new allocation/field/engine/policy,
removed cancellation check or duplicate positional path. A separate Linux code
acceptance run confirms **all three optimized call sites use the readonly reference
and the outgoing descriptor copy disappears**, with the full golden preserved.

## Focused checks and allocations

1,366 internal / 244 targeted public checks per actual .NET 11/10 pass with matching
probes. Function/default/method/native/deferred/argument-order/namespace/generator/
recursion/suspension/cancellation/refusal/lifetime tests remain. Five new sequence
identity/lifetime cases are included; production sequence construction is unchanged
in this source pair. All 16 deferred-context cases first pass on unchanged R71.
Initial fixture errors (tuple public projection is an array, then a misplaced test
edit causing compile failure) are retained; neither was a runtime failure or a
weakened assertion. Corrected tests verify withdrawn aliases through real pending
I/O/cancellation, escaping results and shared-budget baseline return.

Existing five-script diagnostic, both APIs, normal GC/tiering and independent
before/current/control assemblies: full goldens/balances/isolation/cleanup pass.
Returned-record allocations remain **−13.51–13.53% sync / −12.62–12.64% async**
versus R70, ordinary allocations essentially flat. Desktop clocks remain variable:
sync affected −1.90/−10.68% with 9.82% old drift; async ordinary CSV +10.65/−8.43%
with 20.84% old drift, async report +2.82/+9.49%. Preserve these adverse/contradictory
observations; no desktop CPU or causal improvement claim.

## Controlled corrected source pair — B427

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, same fully qualified R70 baseline d6445fc5,
unchanged 7cf37ca5 manifest worker and normal optional 0.4 adapter. Two independent
baseline/two current processes, five-second warmup per role/job and seven rotated
150 ms rounds. Four complete jobs take **114.398 seconds**. Negative means faster.

| Complete job | Elapsed change, all process pairs | Baseline / current spread |
| --- | ---: | ---: |
| csv-validated-records.large / warm | -1.00 to +1.17% | 2.03% / 0.16% |
| csv-validated-records.large / warm-async | -6.31 to -1.21% | 2.61% / 2.77% |
| csv-summary-functions.large / warm | -1.74 to +0.43% | 1.43% / 0.73% |
| text-summary-helpers.large / warm | -0.70 to +2.10% | 2.31% / 0.50% |

Ordinary CSV now stays near baseline, with coarse process CPU flat across all four
pairs. The previous B425 +6.25–11.70% CPU/+0.07–5.09% elapsed is not reproduced
after the concrete transport correction. Retain the correction; this bounded
evidence disposes of the immediate large ordinary-CSV regression concern without
claiming its full cause is proven. Original adverse data stays visible. Returned-
record sync remains flat; async elapsed is favorable but between-process spread
limits precision. Text +2.10% and affected sync +1.17% remain adverse observations.
No universal CPU gain, exact causal percentage, debt repayment or CPython claim.
Remaining regression debt must still be considered at the combined checkpoint.

Warm includes public invocation, mediated RAM I/O, full stdout/file validation and
prior-result cleanup; compile/startup/reset/staging/protocol/final cached teardown
excluded. Async timing uses completed I/O; real suspension is correctness-tested.
Desktop includes reset/result disposal and absolute clocks cannot be compared.
No pending-I/O, RSS, startup or embedding-tail claim.

All source/actualReady/auxiliary/runtime/golden/quiet/Closed/exit/join/empty-fixture
gates pass. Persistent records remain outside owned 128 MiB RAM fixtures, users
join and mounts disappear before archive verification under lease and locally.
B426/B427 are terminal/archived/verified; never query again. B427 staging
274,960,384 B passes the 256 MiB floor without reclaim. 150 s owner + 30 s stop +
30 s margin; no measured timeout/restart. The first local launcher mistakenly
reused an old staging filename and refused before any collection setup; its
original condition is preserved and only the unique filename was corrected.

[Controlled assessment](assessment.json), [allocation/adverse desktop](local-assessment.json),
[paired diagnostic chains](paired-chains.json), [Linux acceptance](linux-code.json),
[focused checks](binding-focused.json), [identities](identity.json), [VM identities](vm-identity.json),
[pre-code gate](pre-code-plan.md), [VM archive](observations.tgz),
[attribution archive](attribution-observations.tgz),
[local raw/source/cores/tests/code](local-observations.tar.xz).
