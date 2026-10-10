# Exact integer sort keys

Exact integer sort keys now compare directly, avoiding general rich-comparison
dispatch and a boxed boolean. Two short replicas show **28.0% less time** for the
scalar-output sort variant and **21.9% / 26.4% less time** for the canonical full
sort/output job. The change is integrated with correctness checks passing.

All ten micros complete in **6.43–7.26 seconds**, with a **30-second external
process-group cap**. No full benchmark lanes run. These observations have no
milestone qualification, confidence interval or certified CPython multiplier.
All replicas, control changes and spread remain visible below.

## Current runtime CPU leads

Two prospectively declared 10-second CPU captures use the previously accepted
tuple-owned-registration build `f4faf1925e66c8cba302250eb8009432f2f2dd5b`, whose
production tree `fc9af4d384b7333afdf32b8d5510a1f9c6973174` is identical to then-delivered
head `2931acb76c00266579b3bc166b7a005a2592717e`. The frozen, already prepared Release
producer is reused after hash/source revalidation. Profiles are collected separately
from timing on the dedicated VM, with ordinary worker settings and no forced GC.

| Current-build capture | Whole seconds | CPU samples | Unresolved leaves | Checked responses | Invocations |
|---|---:|---:|---:|---:|---:|
| Canonical sort + complete output | 13.88 | 1,315 | 7.76% | 92 | 2,884 |
| Tuple construction + scalar length | 14.22 | 1,393 | 7.25% | 321 | 10,212 |

TraceEvent 3.2.8 reports zero lost events and missing stacks. Both captures have
unbalanced GC/collection-suspension event pairs, so no complete collection counts
or pause-duration claim is made. Sampling and tracer overhead can affect execution;
these are current-build design leads, not a qualified comparison with the earlier
baseline trace or an additive breakdown of normal runtime cost.

For the sort capture, `EvaluateRichComparison` has 60 exclusive samples (4.56%) and
140 inclusive (10.65%); `SortItems` is present in 31.56% of stacks, rendering in
24.33%, allocation in 24.64% and registration in 13.69%. For construction, tuple
literal evaluation is present in 66.62%, scalar adoption in 20.10%, allocation in
24.55% and registration in 10.84%. Inclusive percentages overlap. Construction
still uses the shared expression-evaluation path and creates scalar adoption
scratch; rendering and those construction costs remain future investigations.

## Guarded change and correctness

Candidate `afe6bbc48164a1c02efd6b357a5f27c6c2eb6b82` has production tree
`baee33a49691abc7af5623f5d9b61011741bdefc`, test tree
`d27d8415373ea7cd4419d8a657ec994ceb0c6542`, and unchanged comparison-harness tree
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

The 13-line production change guards both synchronous and asynchronous sort
comparers on **both operands being exact `BigInteger` values**. Arbitrary precision
is preserved. Exact integers cannot carry guest comparison slots. Mixed integers,
booleans, floats, custom keys and `cmp_to_key` continue through the existing Python
comparison/truthiness dispatch. Key invocation order/count, stable merging, reverse
stability, temporary reservations and cooperative execution checks remain intact.

Two new public tests exercise both modes: 130-bit keys, stable equal-key ordering
in both directions, one key call per element, and mixed boolean/float/integer order
including the 2^53 precision boundary. Existing selected tests cover rich reflected
slots, raw truth-tested results, delayed async callbacks, cancellation, lifetime and
sorting error behavior. Local focused public Release passes **106 checks**.

On the VM, the frozen candidate passes all **1,387 white Release** and **426 selected
public Release** checks after the matching Release probe build. The candidate's full
canonical catalog is byte-identical to the accepted baseline's. All five micro
cases pass explicit isolated CPython output verification. Timings are declared
after those correctness checks, while local full Debug is still running. Acceptance
waits for that run: all **9,140 Debug checks pass** (1,387 white / 7,753 public),
with the matching Debug probe and frozen candidate binaries. No failed test or
timing attempt is retried.

## Short old/new observations

The baseline is the same accepted tuple-registration build used for the profiles.
Both producers use SDK 10.0.401 / CLR 10.0.12. Context uses isolated release CPython
3.13.16. Timed workers retain ordinary runtime/governor/instruction settings; only
the separate controller disables tiering. No tracing, builds, tests or downloads
run on the VM during timing. The local Debug suite runs on a separate machine.

Each engine gets one second of warmup, roughly 25 ms calibration and seven measured
rounds, with rotating engine order. Identical precompiled source creates fresh
per-invocation inputs. Construction, guest key calls, sorting and output stay inside
the clock; compilation, transport and output verification are outside it. Full
canonical output verifies every tuple against an independent stable group-order
golden. The scalar variant verifies length and boundary components separately;
subtracting variants does not isolate additive phase costs.

Cells show median **µs/invocation**, followed by **IQR/median**. Replica 1 finishes
before replica 2.

| Case / replica | Before | After | CPython |
|---|---:|---:|---:|
| Empty / 1 | 24.171 (5.7%) | 23.292 (2.8%) | 1.496 (0.4%) |
| Integer loop / 1 | 1651.167 (1.1%) | 1673.511 (1.4%) | 589.877 (0.1%) |
| Build tuples + length / 1 | 1073.555 (3.0%) | 1105.375 (3.4%) | 50.455 (0.1%) |
| Build + sort + scalar output / 1 | 2559.053 (7.2%) | 1843.493 (2.6%) | 119.178 (0.2%) |
| Canonical full sort + output / 1 | 4025.358 (11.3%) | 3143.145 (7.5%) | 303.699 (0.6%) |
| Empty / 2 | 23.496 (6.2%) | 24.187 (3.3%) | 1.498 (0.4%) |
| Integer loop / 2 | 1654.374 (1.2%) | 1647.173 (0.9%) | 578.642 (0.1%) |
| Build tuples + length / 2 | 1102.517 (4.3%) | 1070.234 (7.0%) | 49.285 (0.2%) |
| Build + sort + scalar output / 2 | 2696.191 (5.6%) | 1940.458 (5.7%) | 128.151 (0.3%) |
| Canonical full sort + output / 2 | 4463.209 (14.6%) | 3285.861 (13.8%) | 331.612 (0.7%) |

The scalar sort improvement repeats at 28.0% with 2.6–5.7% candidate spread. Complete
sort/output improves in both replicas but retains 7.5–13.8% candidate spread. The
empty, loop and construction guards have mixed small changes; there is no material
repeatable regression. The remaining absolute gap to CPython is substantial, and
the control spread/limited sampling do not certify a comparative multiplier.

## Audit and delivery

Independent audits validate **413 profile responses / 13,096 invocations** and
**420 micro responses**, including every request ID, requested/completed count,
source/fixture/output hash and pre/post output check. All medians and IQR fractions
are recomputed from the seven raw rounds. Loaded versions, SDKs, runtime defaults,
canonical catalogs, declarations and original TRX receipts are checked.

Final VM audit confirms both producer sources clean, input manifests rehashed,
all **13 owned services terminal**, the 34 recorded profile/micro PIDs absent and
a fresh available shared lease. Initial profile cleanup receipts remain separately
preserved. Full old/new qualification and compile/startup lanes stay deferred to
an explicitly declared consolidated milestone. Original CSV/streaming failure
causes and the held namespace candidate remain separate pending investigations.

Maintained [evidence](evidence.json) retains all observations and raw receipt hashes.
Private declarations, raw responses/TRX, trace summaries, traces, scripts and
cleanup proofs remain under `.git/agent-notes/sort-current-profile-20261010/`.
