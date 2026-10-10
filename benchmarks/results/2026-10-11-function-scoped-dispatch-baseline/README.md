# Current method CPU leads and scoped dispatch baseline

Two short CPU captures on the accepted default-slot runtime retain shared async
lowered dispatch as a design lead: statement dispatch appears in **35.58% / 33.53%**
of inclusive sampled stacks, expression dispatch in **11.29% / 11.20%**.
These stacks overlap. They do not establish removable shares or a before/after
CPU improvement. Attribute lookup and assignment remain visible costs.

The next prototype is **isolated**, with **no measured gain or integration decision**.
Baseline and candidate each pass **9,395 Debug tests**, **181 selected boundaries**
and **58 CPython comparisons**. Fresh Release gates and short paired micros remain
pending. **No full comparison lane or normal candidate timing ran.**

## Current CPU evidence

Prospective captures reuse Release source `122384e3099cf25fd162694d9273abbce1f2fed5`,
production `d5165bbb6cb51aac155234a30e4df91ff8011b96`, identical in production to accepted
delivery `5b3fd2e05b19876387ad0cabeb2b837a5e6d9efa`. They follow all completed normal timing,
allocation and native-code groups from the [accepted attribute round](../2026-10-10-default-object-attribute-slots/README.md).
No source changes, builds, tests or transfers run on the VM during captures.
Producer/adapter/tool/helper hashes remain unchanged, with ordinary tiering/PGO,
SDK 10.0.401 / CLR 10.0.12, workstation GC and Interactive latency without worker
overrides. The frozen offline parser is reused without rebuilding.

Each requested trace lasts ten seconds; complete owned groups take **14.07s /
16.08s**, under **30-second external caps including cleanup**. All **501 protocol
responses** and **15,848 complete invocations** check full canonical outputs,
including before/after verification. The retained traces contain **1,231 / 1,205
samples**, with **zero lost events and missing stacks**. Unresolved leaves are
**4.96% / 5.73%**. Both services are terminal, six recorded controller/worker/tracer
PIDs absent, inputs rehashed and the shared VM lease available.

| Capture | Stack coverage | Method | Samples | Share |
| --- | --- | --- | ---: | ---: |
| method1 | leaf | `PyAttributeLookup::TryResolveInstanceMember` | 126 | 10.24% |
| method1 | leaf | `LythonRuntime+ExecutableFrameInterpreter::Execute` | 80 | 6.50% |
| method1 | leaf | `PyMemberAccess::TryAssign` | 79 | 6.42% |
| method1 | leaf | `LythonRuntime::EvaluateLoweredExpression` | 52 | 4.22% |
| method1 | leaf | `PyFunctionBase::Invoke` | 52 | 4.22% |
| method1 | inclusive | `PyFunctionBase::Invoke` | 680 | 55.24% |
| method1 | inclusive | `LythonRuntime+<DispatchLoweredStatementAsync>d__1015::MoveNext` | 438 | 35.58% |
| method1 | inclusive | `LythonRuntime+<DispatchLoweredExpressionAsync>d__1016::MoveNext` | 139 | 11.29% |
| method1 | inclusive | `PyAttributeLookup::TryResolveInstanceMember` | 130 | 10.56% |
| method1 | inclusive | `PyMemberAccess::TryAssign` | 86 | 6.99% |
| method2 | leaf | `PyAttributeLookup::TryResolveInstanceMember` | 94 | 7.80% |
| method2 | leaf | `LythonRuntime+ExecutableFrameInterpreter::Execute` | 93 | 7.72% |
| method2 | leaf | `PyMemberAccess::TryAssign` | 88 | 7.30% |
| method2 | leaf | `PyFunctionBase::Invoke` | 63 | 5.23% |
| method2 | leaf | `LythonRuntime::EvaluateLoweredExpression` | 40 | 3.32% |
| method2 | leaf | `LythonRuntime+ExecutableFrameInterpreter::ExecuteStackTransfer` | 21 | 1.74% |
| method2 | inclusive | `PyFunctionBase::Invoke` | 618 | 51.29% |
| method2 | inclusive | `LythonRuntime+<DispatchLoweredStatementAsync>d__1015::MoveNext` | 404 | 33.53% |
| method2 | inclusive | `LythonRuntime+<DispatchLoweredExpressionAsync>d__1016::MoveNext` | 135 | 11.20% |
| method2 | inclusive | `PyAttributeLookup::TryResolveInstanceMember` | 112 | 9.29% |
| method2 | inclusive | `PyMemberAccess::TryAssign` | 90 | 7.47% |

The table selects relevant boundaries; full summaries and raw traces remain
private with audited hashes. Sampling includes profiler/kernel and background
JIT overhead. Inclusive rows overlap and must not be summed as exclusive costs.
Allocation ticks are sampled observations, with unresolved type labels; they do
not provide exact per-type counts/bytes. Incomplete GC event pairs do not establish
complete GC counts or pauses. No qualified before/after CPU comparison or speed
prediction is made, and no capture is recollected to chase favorable samples.

## Independent invocation boundaries

Commit `11d1173c` adds **27 checks on original production before the prototype**:
entry/body fuel boundaries and spans for three argument widths in both modes,
fuel-before-cancellation ordering, admission denial without stranded funding,
and actual frame/parent/services/source/exception/argument identity. A recording
callable compares in-flight logical funding and checkpoint counts in both modes.
Every exit unwinds depth, current executable-frame state and reservations.

The initial fixture incorrectly expected body source annotation on a pre-body
entry failure: **21 pass / 6 fail**. The corrected fixture expects the original
internal boundary contract and passes **all 27**. Original receipt, test input
and binary hashes remain retained; this is a fixture correction, with **no runtime
compatibility fix inferred**. The public engine annotates errors at its own boundary.

Both full Debug suites pass **9,395 tests** (1,574 white / 7,821 public) with matching
probes built. Both pass **181 selected checks** (90 white / 91 public), retaining
descriptor/default-slot, binding/funding, live class-cell/super, closure/generator,
evaluation-order and genuine delayed-host/cancellation controls. Twenty-nine pure
snippets match CPython in both public modes for each source. These gates establish
the tested contracts; they do not establish a performance gain.

## Isolated function-body strategy

Baseline `11d1173ca0b3fdf9db13dcd6dd1bbfcc1b9182e7` has production
`d5165bbb6cb51aac155234a30e4df91ff8011b96`. Candidate
`0445ca571417aa2ae20bc833e8839a845e6dac52` has production
`1bee4c9234fb1d56821d7b864b6339aa48380df2`. Both share tests
`07cd1ebf8666e2a4abe6fcf402d37fbd404edc78` and whole benchmark project
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`. The candidate remains on
`perf/function-scoped-dispatch`; delivered production remains the accepted baseline.

Only an active synchronous `PyFunction` body enables direct statement/expression
dispatch in its actual frame. A finally block restores prior strategy on all
exits. Child frames initialize their own strategy; async bodies preserve the
existing awaited route. Direct dispatch maps every supported action to the existing
operation, retaining entry checkpoints and exception/source handling. There is no
function-factory, argument-mirroring, closure, namespace, binder, funding or object-
slot change, and no synthetic context. Executable fallback dispatch remains on its
existing route. Successful frozen binaries now persist outside build directories.

The earlier [global direct-dispatch experiment](../2026-10-10-lowered-sync-dispatch/README.md)
remains rejected because its loop repeatedly regressed. This scoped strategy is
a separate candidate; it does not erase that decision. Switching the class-method
factory to executable functions remains an additional design question, requiring
actual context/cell/funding/checkpoint and genuine host-suspension proofs.

Policy and byte-identical decision code are frozen before candidate timing:
method gains must exceed max(2%, retained spread) in both passes, without repeated
control regressions beyond that bound or repeated complete-job allocation growth
above 0.5%. There is no allocation-saving floor or gain estimate. Fresh matching
Release inputs and seven full canonical CPython checks must precede fourteen short
method/loop/module/empty/sort/output/pipeline micros. Separate ordinary allocation
and native-code groups retain every observation/emitted tier/inline summary/call
target under 30-second whole-group caps. No routine full lane or recollection.

Original CSV/pipeline/C05 causes, milestone/secondary qualification, additional
call-result assignment/property-constructor findings and Utf8Regex integration
remain open. Supporting [evidence](evidence.json) ties raw receipts, summaries,
source identities, frozen policy and durable binary hashes to these claims.
