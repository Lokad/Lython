# Stable-sort decomposition and tuple registration

Lython's stable-sort gap includes substantial work before sorting. The tuple-building
variant alone takes about 1.5 ms on the original runtime, versus about 50 µs under
CPython. A separate CPU profile points to CLR handle scanning during allocation and
collection. Moving tuple reclamation metadata from the global conditional weak table
to the tuple itself gives repeatable short-run improvement on the construction case.
The change is integrated; the rest of the stable-sort gap remains open.

This round runs **no full benchmark lanes**. Twenty prospectively declared micros
each complete in **6.41–7.45 seconds**, with an external **30-second process-group
cap**, plus one separate 10-second CPU capture and two fixed-count allocation
diagnostics. All replicas and high-variance results are retained. These are diagnostic
observations, without milestone qualification or confidence intervals.

## Inputs and method

Clean baseline `036f3522f3e4ad02e0287890c38390b753f9a059` has production tree
`4174256cc6da53c0f2f5cd7d163c2bb47fb0fcf3`. Clean candidate
`f4faf1925e66c8cba302250eb8009432f2f2dd5b` has production tree
`fc9af4d384b7333afdf32b8d5510a1f9c6973174`. Both use comparison-harness tree
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

The dedicated Ubuntu 24.04.4 VM uses SDK 10.0.401, CLR 10.0.12 and isolated release
CPython 3.13.16. Timed Lython workers retain ordinary runtime settings, governor and
instruction defaults; no forced GC. Only the separate controller disables tiering.
Worker assembly hashes, source versions and effective settings are checked.

Each micro warms each engine for one second, calibrates a roughly 25 ms batch, then
collects seven rounds. The public API invokes the same precompiled source on fresh
per-invocation inputs. Tuple/list construction, key evaluation and output remain
inside the timed work. CPython context uses the same source and complete expected
output. Compilation and process startup are outside these warm measurements.

The canonical case creates 2,048 tuples in descending index order, sorts by the first
component through a guest lambda, and prints every tuple. Variants preserve that
construction prefix while changing the subsequent work. Arithmetic and stable
group-order enumeration supply independent goldens. The sparse scalar-output
variant verifies length and boundary components; the canonical variant separately
verifies the entire sequence. Variants are end-to-end observations: subtracting them
would not yield reliable additive phase costs.

## Baseline decomposition

Both Lython columns here run **the identical baseline binaries**. Cells show median
**µs/invocation**, followed by **IQR/median**. Replica 1 completes before replica 2.

| Case / replica | Lython A | Lython B | CPython |
|---|---:|---:|---:|
| Empty / 1 | 23.461 (5.3%) | 22.974 (2.7%) | 1.485 (0.7%) |
| Build tuples + length / 1 | 1539.331 (14.7%) | 1521.295 (9.3%) | 49.976 (0.2%) |
| Build + inline keys + sum / 1 | 2343.054 (9.3%) | 2837.476 (40.7%) | 70.203 (0.5%) |
| Build + guest key calls + sum / 1 | 3377.595 (20.4%) | 5619.705 (73.1%) | 138.961 (1.4%) |
| Build + sort + scalar output / 1 | 2856.344 (4.9%) | 2754.745 (1.7%) | 118.376 (0.4%) |
| Canonical full sort + output / 1 | 4119.302 (13.3%) | 4403.141 (6.5%) | 303.769 (0.4%) |
| Empty / 2 | 23.446 (3.7%) | 23.665 (2.7%) | 1.495 (0.2%) |
| Build tuples + length / 2 | 1514.363 (7.1%) | 1464.846 (8.6%) | 49.537 (0.2%) |
| Build + inline keys + sum / 2 | 2405.757 (22.7%) | 2153.341 (17.1%) | 69.990 (0.2%) |
| Build + guest key calls + sum / 2 | 3284.394 (21.2%) | 3260.828 (21.1%) | 142.627 (1.0%) |
| Build + sort + scalar output / 2 | 2831.664 (5.1%) | 2985.300 (1.9%) | 117.715 (0.3%) |
| Canonical full sort + output / 2 | 4272.335 (13.8%) | 4499.279 (20.0%) | 304.338 (0.6%) |

Construction is already expensive without sorting. Guest key calls and full tuple
representation add work, but several rows have large spread (up to 73.1%). This
evidence does not isolate either key binding or sorting as the dominant cause, and
does not support a certified Lython/CPython ratio.

## Separate CPU capture

The original canonical job's whole capture completes in 14.37 seconds under its
30-second cap. TraceEvent 3.2.8 resolves 3,236 worker CPU samples, with zero lost
events or missing stacks and 92 unresolved leaves (2.84%). Pre/post full-output
checks pass; 52 checked responses cover 1,604 invocations including warmup.

`ScanConsecutiveHandlesWithUserData` accounts for 1,502 exclusive samples (46.42%).
Dependent-handle promotion appears in 24.47% of inclusive stacks; allocation in
75.12%, full-output rendering in 31.24%, sorting in 30.28% and tuple literal evaluation
in 24.85%. Inclusive stacks overlap and must not be added. Instrumentation can alter
GC behavior and latency. This profile supplies a design lead, not a predicted gain
or proof that the candidate reduces native handle CPU by a particular amount.

`PyString` and `PyList` already keep registration metadata directly on their owners.
Tuples still used a global `ConditionalWeakTable`, which entails dependent handles.
The candidate applies the existing direct-storage pattern to `PyTuple`, leaving the
fallback for other value types. The entry still refers weakly to the owner.

## Candidate comparison

Eight new micros were declared after correctness checks and before collection,
using the baseline and candidate side by side. No tracing runs during these timings.
Cells have the same units and spread definition as above.

| Case / replica | Lython before | Lython after | CPython |
|---|---:|---:|---:|
| Empty / 1 | 24.025 (4.0%) | 23.505 (4.2%) | 1.467 (0.8%) |
| Integer loop / 1 | 1647.007 (0.9%) | 1653.003 (0.3%) | 597.299 (0.3%) |
| Build tuples + length / 1 | 1492.300 (3.2%) | 1116.288 (7.6%) | 49.116 (0.2%) |
| Canonical full sort + output / 1 | 4298.032 (19.4%) | 3883.787 (11.8%) | 309.466 (0.4%) |
| Empty / 2 | 23.034 (3.6%) | 23.168 (4.7%) | 1.488 (0.7%) |
| Integer loop / 2 | 1663.083 (0.6%) | 1630.966 (0.6%) | 613.119 (1.2%) |
| Build tuples + length / 2 | 1510.354 (4.2%) | 1124.104 (5.2%) | 48.943 (0.1%) |
| Canonical full sort + output / 2 | 4692.501 (16.5%) | 3902.516 (7.7%) | 302.119 (3.9%) |

Tuple construction takes **25.2% / 25.6% less time** across the two replicas, enough
to accept this round alongside correctness coverage. Integer-loop observations
change by +0.4% / −1.9%; empty controls are mixed and show no material repeatable
regression. Canonical sort medians take 9.6% / 16.8% less time, but their spread remains
large: these figures are diagnostic and do not establish the size of a sort gain.
The change does not remove the substantial remaining gap to CPython.

A separate constructor-plus-registration helper retains 512 warmup tuples and
10,000 measured tuples, without forced GC. Current-thread managed allocation is
**2,969,624 → 2,414,224 bytes** (**296.96 → 241.42 bytes/tuple**, 18.7% less).
Both runs register 10,512 owners and commit exactly 2,149,376 logical bytes. This
measures helper construction/registration only, not public per-invocation allocation,
native handle storage, resident memory or latency. The existing logical accounting
model, 128-byte entry fee, rollback, aliases and host mediation remain unchanged.

## Correctness, provenance and cleanup

Four new white-box checks cover alias identity across pools, distinct equal tuples,
registration denial/retry, unpublished refunds/re-registration, and weak-owner
collection with exact fee recovery. Focused Debug/Release each pass 60 checks;
331 selected public Release checks pass. Matching probe builds precede dependent
tests. One frozen-candidate full Debug run passes all **9,138 checks** (1,387 white,
7,751 public). On the VM, all 1,387 white Release and 673 selected public Release
checks pass. Full canonical catalogs are byte-identical across producers.

Initial baseline preparation had **414 passes / one failure**: the probe-dependent
Unicode comparison test could not launch the default `python` command. An exact
command reproduction records the absent executable and empty stdout. A task-local
alias to the pinned CPython fixes the environment; only that failed test is rerun
and passes. The original failure and the correction remain in the evidence; no
timing began before the correction. Untimed verification receipts include two-call
compile smoke checks; these are not full compile benchmark lanes.

The independent audit recomputes every median/spread from raw counters and validates
all 839 micro responses, invocation counts, source/fixture/output hashes and
pre/post output checks. Final VM audit rehashes both producer input manifests,
confirms both source trees clean, all **26 owned services terminal**, recorded worker,
tracer and allocation-helper PIDs absent, and a fresh available shared lease. No
collection is repeated to chase variance.

Maintained [evidence](evidence.json) records all rows and raw receipt hashes. Raw
receipts, declarations, TRX files, scripts, trace and audit remain private under
`.git/agent-notes/stable-sort-decomposition-20261010/`. Original CSV/streaming failure
causes and the held namespace experiment remain separate pending investigations.
Matched old/new milestone and secondary lanes stay deferred to an explicitly
declared consolidated milestone.
