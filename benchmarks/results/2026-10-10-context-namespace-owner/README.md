# Shared context and namespace-frame storage

Each execution context now owns its namespace-frame storage through its base
class. This removes a separate wrapper and its context-to-frame reference.
Complete call jobs use **4.06–4.12% less managed
allocation** across both fixed-count passes. The change is integrated as a
storage improvement. Call timings are small or mixed; no reliable throughput
gain is established. All observations remain below, and these short collections
do not establish milestone-qualified speed ratios.

Twelve paired micros stop in **8.04–8.05 seconds**, including
owned-group cleanup. Separate allocation groups stop in **4.04 /
4.03 seconds**, after normal micros finish. Each collection has a
**30-second external process-group cap**. **No full lanes run.** Longer
qualification stays deferred to an infrequent, declared milestone.

## Design and frozen sources

Baseline `e0a4ea775171ea113ef813a8d99da5d9e583e632`, production `eb8857ec378778a91831b6ea5549fe9389d4a956`, is
identical in production to delivered green head `06700b77`. Its prepared Release
producer is rehashed and reused. Candidate `a39daf4d919e557bf785348ad5286bd316f22217` has production
tree `7f10d59e8ef989e4d90fff1a31303910bd7638c2` and tests `7d3c9cfcf80a35125e723997141db47234052f6b`.
The whole benchmark project remains `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`; workloads and harness
are unchanged. One focused concurrent namespace regression is added.

The context derives from `ExecutionFrame`, with its existing Frame property
projecting the same owner. The inherited parent/dictionary fields retain their
operations. Compiled metadata from both frozen libraries confirms the base
owner and removal of the former Frame backing field; no physical byte saving
is predicted from field counts.

All context constructors preserve their parent and dictionary choices. Modules
initialize namespaces eagerly after preparing services; executable functions
create them lazily. Ordinary child contexts remain independent and class
comprehensions retain their existing shared dictionary. Nested class contexts
choose the same lexical parent. Dictionary publication still uses
`Interlocked.CompareExchange`, preserving stable aliases on concurrent first
reads. Context and namespace parent views retain their relationships.

Captured/mirrored locals, globals/nonlocals, class cells and zero-argument super,
generator/coroutine lifetimes, exception state, annotations, stack arrays,
temporary disposal, checkpoints, logical charges, limits and host mediation
retain their contracts. No pooling or runtime-setting changes are introduced.

## Complete-job execution timings

Microseconds per invocation; parentheses are IQR divided by median. Negative
change means faster than baseline. Both replicas remain visible.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.928 (5.1%) | 23.177 (2.6%) | 1.473 (1.2%) | -3.14% |
| Integer loop / 1 | 1274.502 (0.7%) | 1295.529 (0.6%) | 577.392 (5.3%) | +1.65% |
| Positional calls / 1 | 623.038 (1.3%) | 633.192 (2.2%) | 119.978 (0.2%) | +1.63% |
| Keyword calls / 1 | 673.661 (0.6%) | 672.880 (1.5%) | 149.429 (0.3%) | -0.12% |
| Full stable sort + output / 1 | 2709.698 (12.1%) | 2829.744 (4.7%) | 301.239 (0.5%) | +4.43% |
| ASCII pipeline / 1 | 148.285 (2.8%) | 137.963 (2.2%) | 41.543 (0.8%) | -6.96% |
| Empty / 2 | 23.556 (2.9%) | 23.588 (4.1%) | 1.464 (0.6%) | +0.13% |
| Integer loop / 2 | 1308.690 (0.7%) | 1294.773 (0.7%) | 626.467 (0.1%) | -1.06% |
| Positional calls / 2 | 627.098 (0.6%) | 618.903 (1.2%) | 128.362 (0.2%) | -1.31% |
| Keyword calls / 2 | 680.397 (1.5%) | 669.339 (1.2%) | 139.883 (0.5%) | -1.63% |
| Full stable sort + output / 2 | 2930.998 (5.9%) | 2642.291 (6.8%) | 301.465 (0.6%) | -9.85% |
| ASCII pipeline / 2 | 140.912 (2.8%) | 137.861 (1.7%) | 41.215 (0.1%) | -2.17% |

Positional call time changes +1.63% / -1.31%; keyword
changes -0.12% / -1.63%. Interpretation must account for
the two replicas and their spreads. These are diagnostic complete-job observations,
not independent-session or general language qualification.

Both producers execute identical canonical source, fixture and complete golden
output, including the full stable-sort output. Compilation precedes timing;
guest state is fresh for each invocation through the existing comparison worker.
CPython 3.13.16 uses its pinned PGO/LTO build, -I -S and ordinary GIL/GC.
Lython uses SDK 10.0.401, CLR 10.0.12, workstation GC, Interactive latency and
ordinary tiering/PGO. No worker overrides. API/object-model/containment costs
remain part of the complete jobs; empty-control timings are not general ratios.

## Separate managed-allocation diagnostic

The previously frozen allocation helper is rehashed and reused without rebuilding.
It invokes the exact comparison worker's Compile/Invoke methods and checks full
output on every invocation. It measures `GC.GetTotalAllocatedBytes(precise:true)`
around 100 fixed invocations after 16 warmups per case/pass. There are two passes
per producer process: 2,400 measured and 384 warmup invocations, 24 rows.
Allocation is process-wide, including worker/materialization/runtime costs,
not exact accounting by guest type. No forced collections. The two passes share
their producer process and are not independent-session qualification.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved | Saved |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 29,424.00 | 29,400.00 | +24.00 | +0.082% |
| Integer loop / 1 | 1,079,448.40 | 1,079,424.40 | +24.00 | +0.002% |
| Positional calls / 1 | 1,195,524.32 | 1,146,354.48 | +49,169.84 | +4.113% |
| Keyword calls / 1 | 1,212,157.20 | 1,162,981.44 | +49,175.76 | +4.057% |
| Full stable sort + output / 1 | 3,606,894.16 | 3,496,352.56 | +110,541.60 | +3.065% |
| ASCII pipeline / 1 | 461,832.00 | 461,808.00 | +24.00 | +0.005% |
| Empty / 2 | 29,300.32 | 29,232.00 | +68.32 | +0.233% |
| Integer loop / 2 | 1,079,221.84 | 1,079,211.68 | +10.16 | +0.001% |
| Positional calls / 2 | 1,195,341.20 | 1,146,151.36 | +49,189.84 | +4.115% |
| Keyword calls / 2 | 1,211,975.36 | 1,162,813.20 | +49,162.16 | +4.056% |
| Full stable sort + output / 2 | 3,486,336.88 | 3,437,128.88 | +49,208.00 | +1.411% |
| ASCII pipeline / 2 | 461,832.00 | 461,808.00 | +24.00 | +0.005% |

Positional allocation savings: +4.113% (+49,169.84 bytes) / +4.115% (+49,189.84 bytes).
Keyword allocation savings: +4.057% (+49,175.76 bytes) / +4.056% (+49,162.16 bytes).
Empty-control savings: +0.082% (+24.00 bytes) / +0.233% (+68.32 bytes).
Full-sort savings: +3.065% (+110,541.60 bytes) / +1.411% (+49,208.00 bytes).
Every increase and decrease stays visible; process-wide variation has no exact
per-type attribution. No observation is discarded or recollected.

## Verification and cleanup

Matching Debug probe and full frozen Debug suite pass **9,165 checks**
(1,408 white / 7,757 public) before collection is declared. Matching VM Release
probe, **115 selected white / 286 selected public checks**, and six canonical
cases against CPython pass first. The added test reads aliases concurrently
through both context and frame views and checks stable, independent, ordinal
namespaces. Existing suites cover closures/local mirrors, classes/super,
generators, reentry, exceptions, cancellation, budgets, async execution and streaming.

Audit checks **517 normal responses**, complete output hashes,
request IDs/counts, frozen versions/defaults and all 24 allocation rows; medians
and IQRs are independently recalculated. Journals prove prospective starts,
separate timing/diagnostic schedules and whole-group caps. Both producers and
the reused helper are rehashed; source trees are clean. All **16 owned services**
are terminal, **54 recorded PIDs** are absent and the shared lease is freshly free.

Raw declarations, TRX, journals, hashes and cleanup proof remain private under
`.git/agent-notes/context-namespace-owner-20261010/`. Maintained
[evidence](evidence.json) preserves all observations and supporting hashes.
Remaining call/local-array/binding/dispatch costs, original CSV/pipeline causes,
external Utf8Regex integration and milestone/secondary qualification stay pending.
