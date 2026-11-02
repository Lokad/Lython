# Native single-operation function returns

2026-11-02, source **fec967a5 + [prototype.patch](prototype.patch)**.
**Prospective retention criteria pass.** Allocation/construction improvement;
elapsed observations remain diagnostic, without a CPython ratio or latency claim.

## Construction and contracts

The existing guarded simple-return operation now handles a single native
operation rather than only exact-integer addition. Common `return int(value)`
and `return record['amount']` helpers retain real function contexts, binding,
entry/recursion/managed-stack checks and instruction spans. They omit the
resumable interpreter and operand/cache storage. The actual native conversion,
lookup and addition operations are reused. Global lookup is shared with the
ordinary engine and observes current rebinding; no callable is cached.

Runtime guards decline before operations for protocol-bearing dictionaries,
custom operands, captured/mirrored/observable state or possible suspension.
Those cases retain the full executable path. Implicit nested/class name rules
also keep ordinary resolution. Native failures retain exception context and
operation attribution; refused admission is terminal. Borrowed inputs, returned
aliases and actual asynchronous users remain funded through cleanup. No arena,
pool, new backend/owner layer, quota, dependency or public API was introduced.

The [compile-only inspection](ir-proof.json) confirms four-instruction CSV/JSON
helper bodies with no captures/mirroring/regions. The text helper retains two
method calls and remains on its normal executable route. This differs from the
closed call-arena prototype: lexical contexts and general suspended state are
not relocated or reused.

## One complete-workload comparison

Dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4, four EPYC9V45 vCPUs / 16 GiB,
.NET11RC3, ordinary GC/tiering. Existing complete-job driver, original/candidate/
identical-original isolated core images sharing CoreLib and actual key/word hashes.
Every auxiliary host/adapter DLL matches. An initial freeze detected rebuilt
auxiliary differences before measurement; unchanged sources were verified and
both images use the same frozen auxiliaries. Exact identities remain below.

128 interleaved warmups per image/case, two-second settling, seven rotating
32-job batches. Public warm execution includes fixture reset, efficient mediated
I/O, complete stdout/file checks and result disposal. Startup and compilation
are excluded. Process-wide CLR allocation counters surround each batch outside
its elapsed clock; allocations are cumulative, not retained memory or RSS.
Shared process GC/JIT and instrumentation limit elapsed interpretation.

| Complete warm job | Original / candidate / original control allocation | Reduction vs originals | Elapsed ms, original / candidate / control |
| --- | --- | --- | --- |
| CSV helpers, 4,096 records | 9,543,699 / 7,642,786 / 9,543,675 B | 19.92% / 19.92% | 8.481 / 7.759 / 8.268 |
| JSON helpers, 4,096 records | 8,148,067 / 6,574,762 / 8,148,067 B | 19.31% / 19.31% | 8.675 / 8.305 / 8.758 |
| Text helpers, 3,072 lines | 5,519,095 / 5,519,095 / 5,519,095 B | Unchanged | 6.599 / 6.756 / 6.663 |

Both affected jobs exceed the pre-code 10% allocation criterion against both
originals; unaffected allocation is unchanged. Elapsed point changes are
-8.51/-6.15% CSV, -4.27/-5.17% JSON and **+2.38/+1.39% text**. All series IQRs
are 0.78-3.35%. No case meets the prospective stable >5% regression rejection
condition. Preserve the adverse text points; low within-run dispersion does not
establish repeatability, a CPU explanation or a general speedup.

One **35.955-second** owned service completes under its fixed 45-second limit.
Both quiet gates, every golden, per-execution baseline and final allowance-zero
check pass. All users finish; fixtures are removed normally. Service exits0,
MainPID0, inactive/dead, empty control group; shared VM lease verified free.
No repetition, tuning, longer lane or changed retention criteria.

## Semantic validation

Focused Release: **183 internal / 85 public per actual .NET11/10**, including
existing simple-function/argument/frame/default/ownership checks. Twelve new
independent CPython3.13.2 goldens exercise conversions, native/custom dictionary
reads, aliases, native errors/context, rebinding, callable parameters, closures
and class/global behavior through both execution APIs. Two gated cases prove
real integer/index protocol suspension, cancellation, awaited cleanup and budget
reuse. One native refusal case covers all four direct/general sync/async drivers,
funding a borrowed input separately while shared host pressure denies the result;
the active exception, first refusal and actual conversion span survive.

The 14 new public cases and refusal case also pass against the unchanged frozen
original core. No tests enforce the optimizer's layout and no assertions/timeouts
were relaxed. Full platform/stress/installed Release-consumer validation is recorded
at delivery. No package publication or tag.

[Pre-code design/criteria](pre-code-plan.md), [all samples](comparison.json),
[assessment](assessment.json), [baseline](baseline-identity.json)/[candidate](candidate-identity.json),
[full selected catalog](catalog.json), [public Python goldens](native-return-goldens.json),
[VM Python validation](reference-validation.json), [focused proof](validation.json),
[build/test logs and TRX](validation-logs.tar.gz),
and [exact driver/collection/cleanup evidence](evidence.tar.gz) preserve the result.
