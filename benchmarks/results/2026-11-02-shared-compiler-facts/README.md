# Shared compiler facts: exponential compilation correction

2026-11-02, source **6418de81 + [runtime.patch](runtime.patch)**.
One bounded local diagnostic; no CPython ratio, steady-state claim or update to
the representative warm-execution comparisons.
The numerical comparison below covers the **initial implementation, 96b78e6d**.
Delivery review subsequently extends the same traversal to callback inspection;
these numbers are not measurements of that expanded source.

Repeated `x = (x, x)` statements create a small shared graph of inferred values.
The old dictionary/mutability queries expanded it as a tree. Defining a function
then cloning its captured facts revisited both branches exponentially, allocating
recursive LINQ closures and temporary collections. Two recursive queries and the
captured-state helper are replaced with one iterative query. Reference identity
deduplicates shared payloads and captured states. This is temporary compiler
analysis, with no guest-heap tracing or persistent cache. Scalars allocate no
traversal state. The redundant dictionary query before the broader mutability
query is also removed.

Current-token checks cover both pending facts and child enumeration, with prior
admission refusal taking precedence. Mutable alias inference, diagnostics and
existing compilation admission/owner cleanup remain. No public API changes,
policy ceilings, new dependencies, runtime backend or memory-owner infrastructure.

## One controlled diagnostic

Local Windows 11 Business 10.0.26200, Intel i7-14700KF, 20 cores / 28 logical
processors; .NET 11 RC3, ordinary GC/tiering. Three isolated assemblies share
CoreLib in one process: original, candidate, identical original. Exact loaded
core hashes are checked. Rotating role order; 16 trivial-compilation warmups per
image. This is not the quiet Azure reference machine.

Reflection invokes public `Compile(source, sharedBudget)` under a finite 16 MiB
allowance. Compilation and deterministic script disposal have separate clocks.
Startup, execution and I/O are excluded. Every script is valid, remains charged
while retained, then returns the shared allowance to zero. Allocations use the
current-thread CLR counter; they are cumulative allocation, **not retained memory**.

| Compilation / median | Original | Candidate | Identical original |
| --- | ---: | ---: | ---: |
| 8 shared-tuple steps | 109.4 us | 78.8 us | 130.6 us |
| 24 shared-tuple steps | 2,319,059 us | 244.3 us | 2,269,158 us |
| Unchanged CSV helper source | 184.0 us | 164.6 us | 174.8 us |
| Allocation, 24 steps | 13,958,788,912 B | 168,352 B | 13,958,788,912 B |
| Allocation, CSV helper | 196,288 B | 189,560 B | 196,288 B |

The 24-step case has three individual observations per role. All old observations
exceed 2.09 seconds; all candidate observations are below 0.36 ms. The source and
allocation evidence establish removal of an exponential cliff. The ordinary CSV
control has five batches of 128 compilations per role; the eight-step control has
three batches of 32. Every observation, including large first-batch/JIT effects,
is retained. CSV allocates 6,728 fewer bytes at the median. Its noisy clocks do
not qualify an ordinary-workload speedup. These compile-only results make no claim
about warm execution, host I/O, embedding-process latency or the CPython gap.

The entire comparison joins normally in **15.697 seconds** under its external
45-second process deadline. A candidate-only 128-step scale check compiles in
1.385 ms and cleans up normally; it is not an old/new ratio. The earlier fresh
probe includes startup/compile/run and is kept separately in [before.json](before.json).
Do not subtract its clock from these compilation clocks.

## Semantic and ownership checks

Focused Release checks pass on actual .NET 11/10: **53 internal / 226 public per
runtime**. The four new public cases compile shared tuple graphs, execute both
APIs, preserve aliases/mutable siblings and return result/compiled ownership to
baseline. Two new internal cases cancel during real fact enumeration and preserve
first refusal, with cleanup and budget reuse. Existing inference, nested-sequence
diagnostic, deferred-definition, memory-contract and parser-cancellation checks
remain. Public goldens independently match isolated CPython 3.13.2; no Python
timing is used here. No assertions or timeouts were relaxed.

Full Windows/Ubuntu, original stress and installed Release package consumers are
recorded at the coherent delivery checkpoint. No publication or tag.

## Delivery review correction

The initial source still has a third recursive query, `IsCallbackFreeValue`, in
call analysis, and one mutability-query caller omits its compilation context.
A separate [28-step reproduction](callback-before.json) on the initial image takes
2.12 seconds including startup/compile/run, with full stdout matching CPython.
It is not another before/after comparison or a phase attribution.

The completed correction folds callback inspection into the same temporary
iterative query, including dictionary keys/values, and requires explicit context
forwarding. It removes the remaining recursive callback checker. New complete
tuple/dictionary argument cases and active callback-inspection cancellation/refusal
checks cover this branch. The [correction patch](corrected-runtime.patch) applies
to 96b78e6d; original measured images, source and observations remain unchanged.
The comparison is not repeated. Current-source qualification supersedes the
initial focused checks; see [validation.json](validation.json).
Corrected Release checks pass **55 internal / 230 public per actual .NET 11/10**,
including eight public shared-graph cases and four active query cancellation/
refusal cases per runtime. All four complete Python source shapes match CPython.

[Prospective design/acceptance](pre-implementation-plan.md),
[all comparison observations and loaded hashes](comparison.json),
[assessment](assessment.json), [machine](machine.json),
[complete public goldens](public-goldens.json), [validation](validation.json),
[build/test logs and TRX](validation-logs.tar.gz),
[original source and exact diagnostic driver](diagnostic-source.tar.gz),
and [joined process/deadline](terminal.json) preserve the complete evidence.

Final expanded source **61237f58** passes [full delivery CI](delivery-ci.json):
Windows/Ubuntu ordinary .NET11/10, original stress on both runtimes and installed
Release package consumers on both runtimes. Superseded96b78e6d run41395804027 is
terminal cancelled and is not this proof. No package publication or tag.
