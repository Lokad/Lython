# Basic-loop investigation, 2026-10-09

The focused comparison supports a substantial execution gap on a basic integer
loop. Per-call startup does not explain most of the large-loop cost. A separate
CPU profile identifies interpreter dispatch as the leading design investigation.
**No comparative multiplier qualified:** the empty control failed spread, and
the large loop failed the cross-session stability rule. These are diagnostic
findings, with their exclusions preserved, rather than a certified baseline.

The warm lane completed in **2 minutes 13 seconds**, within its ten-minute hard
limit. Measured source was the clean Release checkout
`56d4346d960b58f037da7d4590eb66c87ffec29e`, policy/eligibility **5/5**. The VM and
toolchains were the same as the [earlier four-lane run](../2026-10-09-quick/README.md):
four AMD EPYC 9V45 vCPUs, Ubuntu 24.04.4, .NET 10.0.12/SDK 10.0.401 and
PGO/LTO CPython 3.13.16. Both workers retained normal GC, tiering and public
defaults; optional Lython instruction fuel was unset.

## Comparison boundary and observations

Both engines compile this identical module before timing and reuse the resulting
code. Each timed invocation creates fresh state, executes the complete program,
captures output and checks it against the same independent golden:

```python
N = 16384  # also 256 and 2048
total = 0
for i in range(N):
    total += i
print(total)
```

Output is one integer and a newline: 6, 8 or 10 UTF-8 bytes. This avoids the large
formatted-container output of some broader jobs. All inputs, loop values and
sums fit within signed 32-bit integers; arbitrary-precision limb growth is not
needed by this workload. CPython still has no equivalent of Lython's governor,
and the comparison includes each engine's ordinary public invocation overhead.
It does not isolate a bare arithmetic instruction or cover function-local loops.

The [generated report](warm.md) contains every retained session and exclusion.
Ranges below summarize its session medians, including unqualified observations:

| Job | Lython | CPython |
| --- | ---: | ---: |
| Empty control | 83–90 µs | 1.52–1.64 µs |
| Tiny control | 82–85 µs | 2.14–2.16 µs |
| 256 iterations | 144–154 µs | 10.2–11.8 µs |
| 2,048 iterations | 529–584 µs | 75.8–80.9 µs |
| 16,384 iterations | 3.58–3.72 ms | 0.578–0.666 ms |

The middle job retained two complete sessions and part of a third before an idle
CPU gate failed. Its third median summarizes incomplete evidence. The smallest
job is also below the ten-times-control floor. The largest job completed all
three sessions; each passed its individual sampling checks, but their median
ratios differed by more than ten percent. The empty control's second session had
Lython IQR/median above ten percent. None of these exclusions was waived.

As a diagnostic calculation, the additional time from 256 to 16,384 iterations
corresponds to approximately **213–221 ns per additional iteration** in Lython
and **35–41 ns** in CPython across the three session indices. These differences
use separate jobs, do not constitute paired slope estimates or qualified
speedups, and have no claimed confidence interval. No invocation control was
subtracted from the published timing table. Together with the absolute medians,
they indicate that a fixed entry cost cannot account for the large-loop result.

## Separate CPU profile

After timing had stopped, a separate bounded diagnostic exercised only the
same precompiled large loop through the existing public comparison worker.
It verified both invocations and every measured batch's complete output hash,
warmed for 640 invocations, then issued 3,840 invocations in 30 batches while a
15-second trace ran. Worker and tracer exited successfully. Its service had a
75-second hard limit; these instrumented timings are not benchmark samples.

Collection used `dotnet-trace` **10.0.750501**, `collect-linux`, explicit worker
PID, `cpu-sampling,dotnet-common` and a 128 MiB collector limit. This captures
on-CPU kernel samples; the different EventPipe sampled-thread-time profile can
also sample waiting threads. See the [official tracing documentation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace).
The initial attachment failed with `No such process`; its logs remain preserved.
A separate attempt attaching while the warmed worker was idle succeeded.

The collector's bundled report reader could not parse a new label kind, so the
trace was read offline with **Microsoft.Diagnostics.Tracing.TraceEvent 3.2.8**.
Only `Universal.Events/cpu` events for the recorded worker PID enter the sample
distribution. There were **10,833 CPU samples**, no samples without stacks and
zero events reported lost. **12.2%** of leaves lacked resolved method names,
mostly in CoreCLR. The [complete sample summary](cpu-samples.json) retains that
limitation, event counts and both exclusive and inclusive rankings.

| Location | Exclusive share of CPU samples |
| --- | ---: |
| `ExecutableFrameInterpreter.<ExecuteCoreAsync>.MoveNext` | 39.38% |
| `ExecutionGuards.CheckExecution` | 5.09% |
| `LythonRuntime.ResolveName` | 4.57% |
| `ExecutableFrameInterpreter.ExecuteValueOperation` | 4.42% |
| `LythonRuntime.<AssignLoopTargetCoreAsync>.MoveNext` | 4.29% |
| `LythonRuntime.EvaluateAdd` | 3.04% |
| `ExecutableFrameInterpreter.ExecuteStackTransfer` | 2.57% |
| `LythonRuntime.OwnFreshInteger` | 1.80% |

These are shares of sampled CPU, not shares of elapsed wall time or promised
savings. JIT inlining can place work in its caller. In particular, the 39.38%
dispatch share includes executing the loop body and dispatch machinery; it
does not mean async overhead alone costs 39.38%. Inclusive figures overlap and
must not be added: the interpreter state machine appears in 90.87% of stacks,
and generalized augmented assignment in 10.89%.

Source review agrees with these locations:

- [Synchronous execution](../../../src/Lokad.Lython/Runtime/LythonRuntime.Executable.Interpreter.cs)
  enters `ExecuteCoreAsync(false).GetAwaiter().GetResult()`. Its shared state
  machine performs per-opcode dispatch, checkpoints and exception handling.
- [Loop-target assignment](../../../src/Lokad.Lython/Runtime/LythonRuntime.Resolution.cs)
  also enters an async state machine in the synchronous path on each iteration.
- [Integer addition](../../../src/Lokad.Lython/Runtime/LythonRuntime.Evaluation.Operators.cs)
  follows generalized type/protocol dispatch. Augmented assignment also handles
  in-place protocols and enters a structural ambient scope before addition.
- [Public entry](../../../src/Lokad.Lython/Runtime/LythonRuntime.cs)
  creates and joins a dedicated thread per invocation. The trace records 3,431
  thread-created events and 3,354 ordinary `ReturnSignal` exception events.
  Their presence identifies separate per-call costs; it does not establish that
  they dominate this large loop.

## Next design experiments

The first candidate is a dedicated synchronous interpreter loop and synchronous
simple-name loop assignment, retaining current operation semantics, exception
and generator behavior, instruction accounting and cancellation checks. Compare
one change at a time against the frozen source before accepting it. The profile
does not establish how much this restructuring will save.

Then investigate exact built-in numeric fast paths ahead of generalized
augmentation/addition dispatch, and measured namespace/stack/iterator costs.
Special-method precedence, exact integer semantics and governor ownership must
remain correct. Treat thread lifecycle as a separate small-job investigation;
retaining contained stack behavior and safe nested/concurrent calls matters.

A qualified baseline and candidate result remain pending. The current evidence
justifies investigating execution design without claiming a universal Python
performance ranking or bypassing Lython's containment guarantees.

## Evidence identity

Raw receipts, manifests, toolchain/binary hashes, test results, diagnostic scripts
and traces remain in ignored task evidence storage. Source and input hashes
were unchanged before and after the uninstrumented lane. All 347 comparison
checks passed locally in Debug and Release and on the VM in Release. The exact
measured commit's [both-platform CI](https://github.com/Lokad/Lython/actions/runs/37929620913)
passed on its first attempt, including the Release package consumers.

| Artifact | SHA-256 |
| --- | --- |
| Warm receipt | `d480b5283434bf8a09de67bd51011720859f0b468c86400df3f7491cb2e95ec7` |
| Core-loop catalog | `6f2a5f018909e56a1d7fc62cceec75f4803687a0413c86f7a17ffc9727cd2092` |
| CPU trace | `55c9a2790f110ec5214010ed8408a4ee4e1dcb3082d437e0a7bffbc92af0b498` |
| Diagnostic receipt | `2da5ce9ff8f9a4f2beef0dd1608e762500a084e17b8865f793986a0a8e0871c8` |
| Diagnostic script | `fe55c29bd63a7354c16540da1c467000ae89ef0fd56df614e8f4e2e50c210530` |
| Offline reader source | `c505ea21f5923ea8736d5228a247bdd1c600546c3ea83ce64a00b5c5ddf1d9fa` |
