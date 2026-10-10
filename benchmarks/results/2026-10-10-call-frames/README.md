# Function-call frames and a held namespace experiment

Two bounded CPU captures point toward general function-frame and interpreter
work rather than keyword binding alone. They profile the existing positional
and keyword medium jobs on the current runtime, with scalar checksums and
ordinary public limits. Each capture takes about **14 seconds total**, including
two seconds of warmup, ten seconds of tracing and shutdown, under a **30-second
whole-process-group cap**. No timing microbenchmark or full lane runs here.

| Profile | Samples | Unresolved leaves | Lost events / missing stacks | Verified public invocations |
| --- | --- | --- | --- | --- |
| Positional calls | 1,088 | 5.70% | 0 / 0 | 14,084 |
| Keyword calls | 1,173 | 6.14% | 0 / 0 | 12,644 |

The independent protocol/provenance audit checks **841 responses and 26,728
complete invocations**, including pre/post output verification. The same two
cases passed independent isolated CPython verification before collection.
The instrumented call stacks retain these leading locations:

| Location | Positional | Keyword | Interpretation |
| --- | --- | --- | --- |
| Interpreter `Execute` | 13.97% | 16.88% | Exclusive samples |
| `CheckExecution` | 4.87% | 4.43% | Exclusive; containment stays enabled |
| `BindInto` | 0.64% | 2.64% | Exclusive argument-binding work |
| `PyFunctionBase.Invoke` | 33.73% | 35.98% | Inclusive function invocation |
| `ExecuteExecutableCodeObject` | 70.68% | 75.11% | Inclusive, includes module and called frames |

Inclusive rows overlap. Profile percentages identify work to investigate;
they are not normal latency, a decomposition by subtraction, or predicted
savings. The positional allocation ticks include interpreter frames, contexts,
arrays and namespace dictionaries. Keyword allocation payloads have blank types
and zero byte fields under this analyzer, so their allocation quantities are
withheld. Sampling ticks are not exact bytes per public invocation.

Source confirms that every function context creates an empty namespace
dictionary, even when compiled locals stay in slots. Candidate `8d20133d` on
local branch `perf/function-frame-namespace` defers that dictionary until first
access and checks class-cell shadowing without materializing an empty namespace.
Explicitly supplied namespaces preserve aliases; first access publishes one
stable dictionary. A fixed 10,000-object constructor diagnostic records
**256.6344 to 176.0152 bytes per context plus frame**, approximately 80 bytes
less. The baseline has a 0.62-byte-per-object residual above the nominal 256-byte
layout; those original counts remain visible. This measures constructor storage
only, using a parent shell, and does not predict retained guest memory or latency.

The candidate stays **isolated and unmerged**. Its full local Debug run completes
**9,130 checks: 9,129 pass and one fails**. The failure is
`HostReadPrereservationTests.DroppedLinesCompleteWhileStreaming`, at the first
synchronous success assertion, after a denial under the unchanged **65,536-byte**
execution budget. The script totals 100,000 bytes from 100 host-mediated lines
and has no guest function calls. No causal link to the namespace change or the
original CSV failure is established.

Both baseline and candidate pass that streaming test in isolation. One
predefined 20-attempt check in each independent process also passes; each attempt
executes the original test's sync and async assertions. These negative controls
do not override the failed full suite or prove its cause. The failed suite is
not rerun. Candidate timing and integration remain withheld pending diagnosis.

Other candidate checks pass: **296 public / 62 white-box focused Release checks**
locally, and **1,375 full white-box / 296 focused public Release checks** on the VM.
Six new checks cover namespace identity across concurrent first reads, scope
isolation, recursive mutable closures and class-cell/implicit-super shadowing
through sync/async public calls. Both new guest scripts match isolated CPython.
Those checks support individual invariants, while the broader failure keeps
overall candidate acceptance unproven.

The delivered change enriches the existing streaming test's failure message with
phase, peak accounted bytes, denied reservation size, ranged-read count, returned
bytes and maximum window. Formatting runs only on failure. Assertions, 64 KiB
limit, file fixture, host mediation and production code remain unchanged.
All **five host-read reservation checks** pass after that diagnostic edit.

Profiles use frozen producer `39952af6e4c645c0733e6eda771cc513ecea16f0`, matching
production delivery `0759ec81`, library tree
`b4c953ccb98d78b0ca56be1510dae243ec38f76c`. The isolated candidate library tree is
`46c6cfb5e6bc37e4de5a89a8eafcd8c0d40fb972`. Both benchmark trees stay
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`; the entire exported canonical catalog
is byte-identical. VM preparation uses SDK 10.0.401/runtime 10.0.12 and pinned
CPython 3.13.16. Local tests use SDK 10.0.300-preview.0.26177.108/runtime 10.0.12.
Native tracing uses the existing pinned 10.0.750501 tool and runtime symbols.
No worker tuning overrides or forced collections are added by the profiler.

[Evidence](evidence.json) retains trace/receipt hashes, all CPU statistics,
the failed full-suite receipt, fixed-count controls and cleanup/input proof.
Raw traces, scripts, the unmerged candidate bundle and all test logs remain under
ignored `.git/agent-notes/call-binding-profile-20261010/`. Final physical rehashing
passes, both roots are clean, all four owned services are terminal, worker/tracer
PIDs are absent and the VM lease is free. The next action is to diagnose the
streaming failure before evaluating the namespace candidate with short micros.
