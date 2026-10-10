# Direct access to plan-owned binding arrays

The candidate remains isolated. Positional-call time changes +0.86% / +7.36%;
keyword calls +2.32% / -0.86%. The smaller compiled binder does not provide a
repeatable complete-call speed improvement. Call allocation is effectively
unchanged. Production, tests and the harness retain delivered `aa15844a`. Every
observation remains below, including the integer-loop decreases and their
limitation as untargeted short diagnostics.

Twelve paired micros stop in **8.04–8.06 seconds**,
including cleanup. Separate allocation groups stop in
**4.04 / 4.03 seconds** after normal timing finishes.
Every collection has a **30-second external process-group cap**. **No full lanes
run**; qualification remains deferred to an infrequent, declared milestone.

## Frozen design

Baseline `a39daf4d919e557bf785348ad5286bd316f22217`, production `7f10d59e8ef989e4d90fff1a31303910bd7638c2`, is identical
in production to delivered green `aa15844a`. Its prepared Release producer is
rehashed and reused. Candidate `92685427716af93bd93df0c41ae6ed47159efc37` has production
`ca45d4ba20d47f49d42639b2cb84fcc9e22a96b3`. Tests remain `7d3c9cfcf80a35125e723997141db47234052f6b` and the
whole benchmark project remains `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`; all six canonical workloads
and complete golden outputs are unchanged.

FunctionBindingPlan already creates positional, keyword-only and layout-name
arrays. Its existing read-only properties now project those same constructor-owned
arrays. The binder borrows direct array views for length/index access. Compiled
Debug metadata confirms three field replacements, without additional reference
fields, and Debug IL uses array lengths instead of former plan-list count/index calls.
BindFunctionArguments shrinks from 77 to 70 IL bytes; BindInto from 1,206 to 1,177.
This is compiled structure evidence, not a prediction of CPU savings or physical
object sizes. The existing read-only views retain their identities and contents.

Argument ordering, presence tracking, positional-only/keyword-only/default/variadic
rules, keyword dictionaries and error paths retain the same operations. Scoped
bound-value reuse and clearing, local-slot maps, frame/closure/generator lifetimes,
temporary disposal, logical charges, checkpoints, cancellation and mediated host
access are unchanged. No additional pooling or array copy is introduced.

## Complete-job execution timings

Microseconds per invocation; parentheses are IQR divided by median. Negative
change means faster than baseline. Both replicas remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 24.910 (4.2%) | 24.462 (4.8%) | 1.583 (0.1%) | -1.80% |
| Integer loop / 1 | 1307.957 (0.4%) | 1291.155 (1.2%) | 646.065 (0.1%) | -1.28% |
| Positional calls / 1 | 648.366 (2.2%) | 653.936 (0.4%) | 125.731 (0.8%) | +0.86% |
| Keyword calls / 1 | 667.514 (1.3%) | 682.976 (4.9%) | 139.931 (3.6%) | +2.32% |
| Full stable sort + output / 1 | 2712.570 (3.6%) | 2691.113 (5.8%) | 302.783 (0.5%) | -0.79% |
| ASCII pipeline / 1 | 137.772 (1.1%) | 139.169 (1.8%) | 42.055 (0.7%) | +1.01% |
| Empty / 2 | 22.962 (3.1%) | 23.160 (2.1%) | 1.475 (0.7%) | +0.86% |
| Integer loop / 2 | 1313.205 (1.0%) | 1281.030 (0.9%) | 618.261 (0.1%) | -2.45% |
| Positional calls / 2 | 600.545 (1.0%) | 644.740 (0.9%) | 120.585 (0.4%) | +7.36% |
| Keyword calls / 2 | 667.795 (1.0%) | 662.059 (1.5%) | 138.928 (0.8%) | -0.86% |
| Full stable sort + output / 2 | 2663.822 (1.6%) | 2624.558 (6.5%) | 310.101 (0.4%) | -1.47% |
| ASCII pipeline / 2 | 139.992 (2.5%) | 139.469 (3.0%) | 41.641 (0.8%) | -0.37% |

Positional calls: +0.86% / +7.36%; keyword calls:
+2.32% / -0.86%; integer loop: -1.28% / -2.45%.
Interpretation includes both replicas and their spreads. Short micros are
diagnostic, not independent-session qualification or general language ratios.

Both producers use identical source, fixture and complete output, including the
full stable-sort output. Compilation precedes timing; each invocation has fresh
guest state through the existing comparison worker. CPython 3.13.16 is pinned
PGO/LTO, -I -S, ordinary GIL/GC. Lython uses SDK 10.0.401, CLR 10.0.12,
workstation GC, Interactive latency and ordinary worker tiering/PGO, with no
worker overrides. API/materialization/containment costs remain in complete jobs.

## Separate allocation diagnostic

The frozen helper is rehashed and reused without rebuilding. Exact comparison
Compile/Invoke methods check full output on every invocation.
`GC.GetTotalAllocatedBytes(precise:true)` brackets 100 invocations after 16 warmups
per case/pass. Two passes share each producer process: 2,400 measured and 384
warmup invocations, 24 rows. Allocation is process-wide managed storage,
not exact accounting by guest type. No forced GC or worker recollection.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved | Saved |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 29,400.00 | 29,400.00 | +0.00 | +0.000% |
| Integer loop / 1 | 1,079,424.40 | 1,079,424.40 | +0.00 | +0.000% |
| Positional calls / 1 | 1,146,354.72 | 1,146,354.48 | +0.24 | +0.000% |
| Keyword calls / 1 | 1,162,981.20 | 1,162,981.44 | -0.24 | -0.000% |
| Full stable sort + output / 1 | 3,538,217.36 | 3,491,676.00 | +46,541.36 | +1.315% |
| ASCII pipeline / 1 | 461,808.00 | 461,808.00 | +0.00 | +0.000% |
| Empty / 2 | 29,232.00 | 29,232.00 | +0.00 | +0.000% |
| Integer loop / 2 | 1,079,197.84 | 1,079,211.68 | -13.84 | -0.001% |
| Positional calls / 2 | 1,146,165.20 | 1,146,165.20 | +0.00 | +0.000% |
| Keyword calls / 2 | 1,162,799.36 | 1,162,799.36 | +0.00 | +0.000% |
| Full stable sort + output / 2 | 3,437,131.76 | 3,437,620.88 | -489.12 | -0.014% |
| ASCII pipeline / 2 | 461,808.00 | 461,808.00 | +0.00 | +0.000% |

Every increase/decrease remains visible. No allocation reduction is predicted by
the array-view change, which retains the same owned arrays and reference fields.
The two shared-process passes are not independent-session qualification.

## Next design boundary: current basic-loop CPU leads

After all normal micros and allocation groups finish, one separately declared
ten-second capture uses the unchanged delivered runtime, frozen `a39daf4d`.
Its owned group stops in 14.08 seconds under a 30-second cap. The 1,574 samples
have zero reported losses/missing stacks and 4.57% unresolved leaves. Dispatch
has 29.86% of exclusive samples, stack transfer 9.15%, value operations 6.80%,
fresh-integer ownership 3.62% and range iteration 3.37%. Full frame execution
appears in 86.28% of inclusive stacks. These overlapping stacks are leads,
not predicted savings. Inlining/summary limits prevent treating absent names
as zero cost; profiler/kernel/background-JIT overhead remains visible.

BigInteger dominates allocation ticks, which are sampling evidence rather than
exact bytes by type. Incomplete GC event pairs prevent complete counts or pause
claims. No old/new CPU distribution or speed ratio is qualified. All 254 protocol
responses and 8,036 completed invocations check complete outputs. Source and
producer hashes are stable; the single service is terminal, three recorded
controller/worker/tracer PIDs absent and the shared lease freshly free. Raw
proof remains in `.git/agent-notes/loop-cost-current-20261010/`.

The next experiment should assess dispatch/value transfer or integer iteration
on this current evidence. Preserve logical per-instruction fuel/checkpoints,
source spans, arbitrary precision, rich-operator fallbacks, captured/local
mirrors, temporary ownership and host mediation. Further interface-view or
shared-stack offset changes have no proven complete-call benefit from these
rounds. Longer qualification remains a declared milestone.

## Verification and cleanup

The matching Debug probe and frozen full Debug suite pass **9,165 checks**
(1,408 white / 7,757 public) before declaration. Matching VM Release probe,
**128 selected white / 286 selected public checks**, and six complete-output
CPython cases pass first. Existing checks cover argument errors/defaults/variadics,
ordinary functions, generators, recursion, closure/local mirrors, sync/async host
execution, classes/super, exceptions, cancellation, budgets and streaming.
No new implementation-mirroring test is added for the array representation.

Audit independently recalculates all **514 normal responses**,
medians/IQRs, complete output hashes, request IDs/counts, frozen identities/defaults
and all 24 allocation rows. Journals verify prospective declarations and separate
timing/diagnostic schedules. Both sources/producers and the reused helper rehash.
All **16 owned services** are terminal, **54 recorded PIDs** absent and the VM
lease freshly free. Every observation is retained without recollection.

Raw declarations, test receipts, journals, compiled evidence and cleanup proof
remain private in `.git/agent-notes/binding-plan-arrays-20261010/`.
Maintained [evidence](evidence.json) retains all observations and supporting hashes.
Further runtime costs, original CSV/pipeline causes, external Utf8Regex integration
and milestone/secondary qualification remain pending.
