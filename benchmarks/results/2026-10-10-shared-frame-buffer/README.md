# Rejected shared locals and operand-stack buffer

The candidate stays isolated. It reduces complete-call managed allocation by
**4.18–4.29%**, but slows positional calls by
+2.64% / +1.99%, keyword calls by
+3.55% / +1.46% and the integer loop by
+3.74% / +1.35%. The loop saves only 24 allocated bytes per
job. This tradeoff does not support integrating the change for this performance
round. Production, tests and the benchmark harness retain delivered `0720325f`.

The experiment combines local and initial operand-stack storage in one array.
Its local prefix stays stable; growth moves the stack to a separate array and
clears its old tail. The combined change also adds offsets and lower-bound
guards. These micros do not separately attribute the timing regression to either.

Fourteen paired micros stop in **8.04–8.06 seconds**,
including cleanup. The two separate allocation groups stop in
**4.03 / 4.04 seconds** after normal timing finishes.
Each has a **30-second external process-group cap**. **No full lanes run**;
longer qualification is deferred to an infrequent, declared milestone.

## Frozen design and tradeoff

Baseline `a39daf4d919e557bf785348ad5286bd316f22217`, production `7f10d59e8ef989e4d90fff1a31303910bd7638c2`, is identical
in production to delivered green head `0720325f`. Its prepared Release producer
is rehashed and reused. Candidate `957885366a020fc7f61d71abea20fc7c798433f5` has production tree
`947379b9526b33b411a825a4f3962fa75504949a` and tests `c1c0faffd45fb6b850c0a98b50a4cca46906815c`. The whole
benchmark project remains `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`. Six canonical cases are unchanged;
one small, prospective stack-growth control is added to this private catalog.

Initial stack capacity remains max(8, local-count). Frames without local slots
retain an independent stack. The local prefix alone is initialized with the
unbound sentinel; the stack tail starts clear. Captured-cell arrays and generator
seeding still use the logical local count. Metadata verifies the added offset
and unchanged frame fields; it does not predict physical object sizes.

On first growth, only active stack entries move to the independent doubled stack.
The old tail is cleared without disposing transferred values. Local slots keep
the original buffer, including that cleared tail, until the frame is released.
This retained capacity is a tradeoff and matters for suspended generators.
Existing generator storage funding covers the retained initial capacity and
resize overlap; logical charges are unchanged. No pooling is introduced.
Negative stack indices still fail explicitly rather than reading the local prefix.
Temporary disposal, closure/namespace lifetimes, checkpoints, limits, cancellation
and host mediation retain their contracts.

## Complete-job execution timings

Microseconds per invocation; parentheses are IQR divided by median. Negative
change means faster than baseline. Both replicas are retained.

| Case / replica | Baseline µs (IQR) | Candidate µs (IQR) | CPython µs (IQR) | Change |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.664 (5.8%) | 23.555 (1.1%) | 1.493 (0.6%) | -0.46% |
| Integer loop / 1 | 1292.901 (1.1%) | 1341.290 (0.8%) | 591.757 (0.2%) | +3.74% |
| Positional calls / 1 | 600.617 (1.1%) | 616.465 (1.0%) | 119.353 (1.1%) | +2.64% |
| Keyword calls / 1 | 672.768 (1.4%) | 696.674 (1.8%) | 137.195 (0.9%) | +3.55% |
| Full stable sort + output / 1 | 2699.632 (7.0%) | 2683.914 (13.8%) | 304.110 (0.3%) | -0.58% |
| ASCII pipeline / 1 | 139.447 (2.9%) | 137.093 (1.9%) | 41.839 (0.6%) | -1.69% |
| Stack growth / 1 | 121.552 (1.1%) | 121.192 (1.5%) | 11.099 (2.8%) | -0.30% |
| Empty / 2 | 23.739 (2.6%) | 23.356 (3.5%) | 1.530 (1.1%) | -1.61% |
| Integer loop / 2 | 1302.690 (0.5%) | 1320.265 (0.9%) | 596.880 (1.5%) | +1.35% |
| Positional calls / 2 | 591.788 (0.8%) | 603.543 (0.5%) | 124.998 (0.6%) | +1.99% |
| Keyword calls / 2 | 675.209 (0.3%) | 685.075 (1.4%) | 138.563 (0.4%) | +1.46% |
| Full stable sort + output / 2 | 2843.081 (3.8%) | 2825.923 (9.5%) | 308.109 (0.5%) | -0.60% |
| ASCII pipeline / 2 | 140.055 (1.1%) | 141.127 (2.3%) | 41.287 (0.4%) | +0.77% |
| Stack growth / 2 | 120.824 (2.0%) | 120.368 (1.6%) | 11.050 (3.4%) | -0.38% |

Positional calls: +2.64% / +1.99%; keyword calls:
+3.55% / +1.46%; growth control:
-0.30% / -0.38%. These short observations are diagnostic,
not independent-session qualification or a general language speed ratio.

All producers use the same source, fixture and complete golden output, including
the full stable-sort output. Compilation precedes timing; invocation state is
fresh through the existing comparison worker. The growth control performs 32
calls, each building a 41-element tuple while retaining a local value, and prints
the complete checksum `4896`. Both frozen producers are verified against CPython
before timing. CPython 3.13.16 is pinned PGO/LTO, -I -S, ordinary GIL/GC.
Lython uses SDK 10.0.401, CLR 10.0.12, workstation GC and Interactive latency,
ordinary worker tiering/PGO, with no worker overrides. Host/API/materialization
and containment costs remain in these complete jobs.

## Separate allocation diagnostic

The unchanged helper is rehashed and reused without rebuilding. It invokes the
exact comparison Compile/Invoke methods and checks complete output on every call.
`GC.GetTotalAllocatedBytes(precise:true)` brackets 100 invocations after 16 warmups
per case/pass. Two passes share each producer process: 2,800 measured and 448
warmup invocations, 28 rows. This measures process-wide managed allocation,
not exact bytes by guest type. No forced GC or diagnostic worker recollection.

| Case / pass | Baseline bytes/job | Candidate bytes/job | Bytes saved | Saved |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 29,400.00 | 29,376.00 | +24.00 | +0.082% |
| Integer loop / 1 | 1,079,424.64 | 1,079,400.64 | +24.00 | +0.002% |
| Positional calls / 1 | 1,146,354.48 | 1,097,171.04 | +49,183.44 | +4.290% |
| Keyword calls / 1 | 1,162,981.20 | 1,114,311.04 | +48,670.16 | +4.185% |
| Full stable sort + output / 1 | 3,537,364.96 | 3,535,783.04 | +1,581.92 | +0.045% |
| ASCII pipeline / 1 | 461,808.00 | 461,784.00 | +24.00 | +0.005% |
| Stack growth / 1 | 303,957.76 | 303,275.36 | +682.40 | +0.225% |
| Empty / 2 | 29,339.92 | 29,208.00 | +131.92 | +0.450% |
| Integer loop / 2 | 1,079,210.64 | 1,079,186.64 | +24.00 | +0.002% |
| Positional calls / 2 | 1,146,122.64 | 1,096,973.28 | +49,149.36 | +4.288% |
| Keyword calls / 2 | 1,162,770.64 | 1,113,615.68 | +49,154.96 | +4.227% |
| Full stable sort + output / 2 | 3,437,129.20 | 3,437,087.92 | +41.28 | +0.001% |
| ASCII pipeline / 2 | 461,808.00 | 461,784.00 | +24.00 | +0.005% |
| Stack growth / 2 | 303,943.92 | 303,279.84 | +664.08 | +0.218% |

Positional savings: +4.290% (+49,183.44 bytes) / +4.288% (+49,149.36 bytes).
Keyword savings: +4.185% (+48,670.16 bytes) / +4.227% (+49,154.96 bytes).
Growth control: +0.225% (+682.40 bytes) / +0.218% (+664.08 bytes).
All increases and decreases remain visible; these shared-process passes do not
establish independent-session qualification.

## Verification and cleanup

The matching Debug probe and frozen full Debug suite pass **9,169 checks**
(1,408 white / 7,761 public) before the timing declaration. Matching VM Release
probe, **115 selected white / 290 selected public checks**, and all seven CPython
cases pass first. Four added checks cover ordinary functions and generators,
locals snapshots before/after growth, escaped closures, resume and finally/close.
Their complete golden outputs also match isolated local CPython 3.13.2.

The initial focused run had four failures caused by mistaken live-dictionary
expectations in the new tests. Those receipts are retained. The tests were
corrected to verified [Python 3.13 locals() snapshot semantics](https://docs.python.org/3.13/builtins/functions.html#locals)
before the frozen full-suite gate and before any timing declaration.

Audit independently recalculates all **595 normal responses**,
medians/IQRs, complete outputs, request counts, frozen identities/defaults and
all 28 allocation rows. Journals verify prospective starts and separate timing
and diagnostics. Both sources and producers, plus the reused helper, are
rehashed. All **18 owned services** are terminal, **62 recorded PIDs** absent
and the shared VM lease freshly free.

Raw declarations, test receipts, journals and cleanup proof remain private under
`.git/agent-notes/shared-frame-buffer-20261010/`. The maintained
[evidence](evidence.json) retains all observations and supporting hashes.
Further call/binding/dispatch costs, original CSV/pipeline causes, external
Utf8Regex integration and milestone/secondary qualification stay pending.
