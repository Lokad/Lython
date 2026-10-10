# Direct synchronous plain-jump dispatch

The candidate remains isolated. Basic-loop times decrease 0.76% / 2.38%, but the first effect is within the observed spread. Positional calls reverse from 5.21% faster to 5.04% slower; keyword calls decrease 4.44% / 0.39%. Loop/positional-call allocation is unchanged. The larger generated interpreter does not earn integration on these inconclusive observations. Delivered production, tests and harness retain green 4f51cd2c; all controls and native bodies remain visible.

Twelve paired micros stop in **8.04–8.05 seconds**, including
cleanup. Separate allocation groups stop in **4.03 / 4.03 seconds**;
separate native-code groups in **4.05 / 4.03 seconds**.
Every collection has a **30-second external process-group cap**. **No full lanes
run**. Qualification remains deferred to an infrequent, declared milestone.

## Frozen change and comparison

Baseline `45937811043adb6a8d49976a9bc594a8c035d088` / production `c9892ea6e03dc0ea04bf65d039e6d182f83d4eae` is identical
in production to delivered green `4f51cd2c`. Its prepared Release producer is
rehashed and reused. Candidate `a20d1e1ced2906035b4b75876359cf5d29622244` has production
`690b43bf64bd9d6e621e21f6f5a8e1288696155b`. Tests `7d3c9cfcf80a35125e723997141db47234052f6b` and the whole
benchmark project `c194ce88a7e4d52c576f3929ef82bce60bf37e2d` remain unchanged.

The synchronous main opcode switch handles plain Jump by assigning its original
target block and setting jumped=true, bypassing the second control-flow switch.
The common instruction-index reset and block-entry/stack-depth handling remain.
Its checkpoint, injected-exception and catch-routing boundary stay in place.
Conditional/abrupt/finally paths, async/shared Jump, instruction count, fuel,
spans, value-stack/local-cell operations, temporary funding, arbitrary precision,
generators and host mediation retain their original operations. No opcode fusion,
iterator/arithmetic change, pooling or unsafe access is introduced.

The preceding accepted local-transfer round and unchanged compiler loop shape
motivate this separate boundary. The canonical header and body each contain a
plain Jump; static shape does not predict dynamic frequency or savings. The
preceding native dump retains a control-flow handler call in main Tier1 code.
See the [preceding evidence](../2026-10-10-direct-local-dispatch/README.md).

## Complete-job timing

Microseconds per invocation; parentheses are IQR divided by median. Negative
change means faster than baseline. Both replicas and every control remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.431 (3.1%) | 22.922 (4.0%) | 1.466 (0.2%) | -2.17% |
| Integer loop / 1 | 1219.243 (0.5%) | 1209.978 (0.9%) | 638.051 (0.1%) | -0.76% |
| Positional calls / 1 | 605.182 (2.7%) | 573.628 (1.1%) | 124.518 (0.5%) | -5.21% |
| Keyword calls / 1 | 650.965 (1.8%) | 622.067 (1.3%) | 135.793 (0.4%) | -4.44% |
| Full stable sort + output / 1 | 2946.153 (17.2%) | 2764.854 (12.3%) | 302.794 (0.2%) | -6.15% |
| ASCII pipeline / 1 | 139.129 (1.8%) | 139.017 (3.1%) | 41.253 (0.3%) | -0.08% |
| Empty / 2 | 23.318 (3.8%) | 23.261 (3.5%) | 1.515 (0.7%) | -0.25% |
| Integer loop / 2 | 1216.273 (0.6%) | 1187.369 (1.3%) | 596.120 (3.6%) | -2.38% |
| Positional calls / 2 | 608.914 (0.8%) | 639.626 (6.0%) | 122.042 (0.7%) | +5.04% |
| Keyword calls / 2 | 647.573 (0.5%) | 645.034 (0.6%) | 137.733 (0.6%) | -0.39% |
| Full stable sort + output / 2 | 2900.469 (10.7%) | 2671.908 (7.0%) | 302.149 (0.6%) | -7.88% |
| ASCII pipeline / 2 | 136.431 (1.6%) | 136.842 (2.2%) | 41.123 (0.6%) | +0.30% |

Sources, fixtures and complete goldens are identical, including full stable-sort
output. Compilation precedes timing; each invocation gets fresh guest state.
CPython 3.13.16 uses pinned PGO/LTO, -I -S and ordinary GIL/GC. Lython uses
SDK 10.0.401 / CLR 10.0.12, workstation GC, Interactive latency and ordinary
worker tiering/PGO with no worker overrides. API/materialization/containment costs
remain in complete jobs. These short diagnostics provide neither independent
session qualification nor a general language speed ratio.

## Separate allocation diagnostic

The frozen helper is rehashed and reused without rebuilding. Exact comparison
Compile/Invoke checks complete output on every invocation. Process-wide
GC.GetTotalAllocatedBytes(precise:true) brackets 100 invocations after 16 warmups
for each case/pass; two passes share each role's process. All 24 rows, 2,400
measured and 384 warmup invocations are retained. No forced GC. These are managed
allocation observations, not exact bytes by guest type or independent sessions.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved |
| --- | ---: | ---: | ---: |
| Empty / 1 | 29,400.00 | 29,400.00 | +0.00 |
| Integer loop / 1 | 1,079,424.40 | 1,079,424.40 | +0.00 |
| Positional calls / 1 | 1,146,354.72 | 1,146,354.72 | +0.00 |
| Keyword calls / 1 | 1,162,981.20 | 1,163,473.20 | -492.00 |
| Full stable sort + output / 1 | 3,540,020.32 | 3,510,647.12 | +29,373.20 |
| ASCII pipeline / 1 | 461,808.00 | 461,808.00 | +0.00 |
| Empty / 2 | 29,232.00 | 29,232.00 | +0.00 |
| Integer loop / 2 | 1,079,211.68 | 1,079,211.68 | +0.00 |
| Positional calls / 2 | 1,146,151.36 | 1,146,151.36 | +0.00 |
| Keyword calls / 2 | 1,162,813.20 | 1,162,813.20 | +0.00 |
| Full stable sort + output / 2 | 3,437,140.08 | 3,437,115.04 | +25.04 |
| ASCII pipeline / 2 | 461,808.00 | 461,808.00 | +0.00 |

## Separate native-code review

Two additional groups run after all normal timing and ordinary allocation groups.
Only JIT disassembly flags change; ordinary tiering/GC remain. All 24 helper rows
check complete outputs. Their allocation values are retained separately and are
not used as ordinary allocation estimates. Every emitted native body and tier is
retained below; missing tiers are not inferred. Native bytes include generated
method code, rather than a prediction of instruction cost or speed.

| Producer | Method | Emitted tier | Native bytes |
| --- | --- | --- | ---: |
| baseline | WorkItem.Execute | Tier0 | 365 |
| baseline | Frame.Execute | Instrumented Tier0 | 3,099 |
| baseline | Frame.ExecuteControlFlow | Tier0 | 2,869 |
| baseline | Frame.ExecuteStackTransfer | Tier0 | 2,876 |
| baseline | Frame.Execute | Tier1-OSR | 6,939 |
| baseline | Frame.ExecuteControlFlow | Instrumented Tier0 | 3,397 |
| baseline | Frame.ExecuteControlFlow | Tier1 | 1,966 |
| baseline | Frame.Execute | Tier1-OSR | 3,406 |
| baseline | Frame.ExecuteStackTransfer | Instrumented Tier0 | 3,481 |
| baseline | WorkItem.Execute | Instrumented Tier0 | 379 |
| baseline | Frame.Execute | Instrumented Tier0 | 3,098 |
| baseline | Frame.Execute | Tier1-OSR | 2,796 |
| baseline | Frame.Execute | Tier1-OSR | 2,813 |
| baseline | Frame.ExecuteStackTransfer | Tier1 | 2,580 |
| baseline | Frame.Execute | Tier1 | 2,675 |
| baseline | WorkItem.Execute | Tier1 | 225 |
| candidate | WorkItem.Execute | Tier0 | 365 |
| candidate | Frame.Execute | Instrumented Tier0 | 3,146 |
| candidate | Frame.ExecuteControlFlow | Tier0 | 2,869 |
| candidate | Frame.ExecuteStackTransfer | Tier0 | 2,876 |
| candidate | Frame.Execute | Tier1-OSR | 7,462 |
| candidate | Frame.Execute | Tier1-OSR | 3,891 |
| candidate | Frame.ExecuteStackTransfer | Instrumented Tier0 | 3,481 |
| candidate | Frame.ExecuteControlFlow | Instrumented Tier0 | 3,395 |
| candidate | WorkItem.Execute | Instrumented Tier0 | 379 |
| candidate | Frame.Execute | Instrumented Tier0 | 3,145 |
| candidate | Frame.Execute | Tier1-OSR | 2,851 |
| candidate | Frame.Execute | Tier1-OSR | 2,862 |
| candidate | Frame.ExecuteStackTransfer | Tier1 | 2,577 |
| candidate | Frame.ExecuteControlFlow | Tier1 | 2,447 |
| candidate | Frame.Execute | Tier1 | 2,860 |
| candidate | WorkItem.Execute | Tier1 | 225 |

The final emitted Tier1 Frame.Execute body grows from 2,675 to 2,860 bytes (+185 / 6.92%); inline summaries change from 19 PGO / 55 single-block / 5 other inlinees to 20 / 61 / 5. The unchanged shared control-flow handler has different profiles: emitted Tier1 grows from 1,966 to 2,447 bytes, with inline summaries 12 / 68 / 5 versus 8 / 85 / 3. Early candidate OSR bodies include 7,462 and 3,891 bytes, versus baseline 6,939 and 3,406. Every body/tier is retained. These profile-dependent sizes and call targets are not total code-cache costs, dynamic call frequencies or causal performance estimates.

## Fresh accepted-runtime CPU leads

After normal timing, allocation and native-code groups stop, one prospectively
declared ten-second capture uses accepted `45937811`, production `c9892ea6`,
identical to delivered green `4f51cd2c`. Its owned group stops in 14.08 seconds
under a 30-second cap. The 1,530 samples have zero reported losses/missing
stacks and 4.97% unresolved leaves. Dispatch appears in 37.65% of exclusive
samples; value operations 7.39%, range iteration/control-flow/fresh-integer
ownership 3.53% each, checked array stores 3.46%. Value-estimation core and outer
wrapper appear separately at 2.48% / 1.24%; integer addition 2.16%, augmented
evaluation 2.09%. Frame execution is inclusive in 86.34% of stacks.

Inlining moves work between leaf names; earlier profiles cannot be directly
compared as CPU savings. Inclusive stacks overlap, missing inlined names are not
zero cost, and kernel/profiler/background-JIT overhead remains. BigInteger leads
sampled allocation ticks, rather than exact per-type bytes. Incomplete GC event
pairs prevent complete counts/pauses. No qualified old/new CPU or speed claim.
All 274 responses / 8,676 invocations check complete goldens;
source/input/helper hashes hold. One service is terminal, three recorded
controller/worker/tracer PIDs absent and the shared lease freshly free. Raw proof:
`.git/agent-notes/loop-cost-after-local-20261010/`.

This supports assessing the iteration or scalar-value/observation boundary next,
preserving generic/rich fallback, exact budget checks/charges and original
checkpoints/spans. More main-switch expansion has no clear gain from this round.

## Verification and cleanup

Matching Debug probe and frozen full Debug suite pass **9,165 checks**
(1,408 white / 7,757 public) before declarations. Matching VM Release checks
pass **128 white / 286 public**, followed by all six complete-output CPython cases.
Existing suites cover functions/generators, closure/local mirrors, argument errors,
exceptions, cancellation, budgets and sync/async host execution. No new test
mirrors the two existing helper calls.

Independent audit recalculates all **513 normal responses**, medians,
IQRs, output hashes, request IDs/counts, frozen identities/defaults and both
diagnostic groups. Journals verify prospective declarations and scheduling.
Both producers and the reused helper rehash; **18 owned services** are terminal,
**58 recorded PIDs** absent and the shared VM lease freshly free. Every observation
is retained without recollection.

Raw declarations, tests, journals, native listings and cleanup proof remain private
in `.git/agent-notes/direct-jump-dispatch-20261010/`. Maintained
[evidence](evidence.json) contains audited observations and supporting hashes.
Further runtime costs, original CSV/pipeline causes, external Utf8Regex integration
and milestone/secondary qualification remain pending.
