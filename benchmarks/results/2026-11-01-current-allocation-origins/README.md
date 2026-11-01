# Current complete-script allocation origins (B34)

Source **ae22392d**, 2026-11-01. Two fixed EventPipe captures, 128 complete warm
jobs per case, **2.065 seconds total capture**. Dedicated Ubuntu 24.04.4 Azure
F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, pinned .NET 11 RC3. Ordinary GC/tiering;
both quiet-machine gates and all complete stdout/file goldens pass. Workers and
collectors close/join; the owning service is inactive and VM lease available.
No runtime change, retained-memory tracing, recapture or speedup claim.

## Boundary and method

The existing checked comparison worker executes maintained text/CSV helper
scripts. Compilation/startup excluded; file work, checks and declared cleanup
included in the batch. The actual recorded Lython execution thread selects guest
events; max-CPU selection is removed. Every thread's sampled allocation totals
and CPU deltas remain visible. The collector's ready signal follows successful
EventPipe session creation; it is not a guessed readiness delay.

The existing external collector records CLR allocation ticks, method/type
metadata and stacks. The offline TraceEvent reader groups the capture-window
guest events by type and complete recorded Lython caller chain. It preserves
all origins without top-N truncation, alongside unresolved types/stacks. These
are **sampled approximate allocation bytes**, not exact per-type allocations,
retained storage, heap/RSS or CPU fractions. Inlining/missing frames still limit
causal attribution even when a stack resolves.

| Complete workload | Capture | Guest allocation ticks | Approximate guest bytes | Missing Lython stacks |
| --- | ---: | ---: | ---: | ---: |
| Text helpers | 0.915 s | 6,619 | 705,628,968 | 0 |
| CSV helpers | 1.150 s | 11,454 | 1,221,567,016 | 0 |

Sampled totals divided by 128 are about 5.513 / 9.543 MB per guest job. The
separate uninstrumented [B33 baseline](../2026-11-01-bulk-whitespace-rejected/README.md)
records 5.519 / 9.542 MB per complete process job at the same runtime source and
inputs. Those boundaries differ: process diagnostics include fixture/validation
threads and common reflection overhead; guest allocation ticks are approximate.
Close totals support plausibility, not exact attribution or a ratio guarantee.

## Findings

| Sampled type | Text guest bytes | CSV guest bytes |
| --- | ---: | ---: |
| ExecutableFrameInterpreter | 16.51% | 11.98% |
| ExecutionContext | 9.62% | 6.87% |
| System.Object[] | 9.70% | 9.08% |
| BigInteger | 6.92% | 10.94% |
| TemporaryMemoryReservation | 8.57% | 9.70% |

Interpreter-frame plus execution-context objects account for **26.13% text /
18.85% CSV** sampled guest bytes. Origins repeatedly pass through
CreateExecutableInterpreter/BeginPendingCall. Object arrays include both operand
stacks and required list/container storage; they cannot all be called frame
overhead. Integer boxes and temporary reservations are visible distributed costs,
not a universal explanation of the CPython gap.

This evidence complements the small named frame-helper CPU self fractions in
B29. It identifies meaningful object traffic; it does **not** prove that removing
objects removes the same CPU fraction. Required live state remains real storage:
context/interpreter fusion merely combines fields, and pooling introduces
retention/reset concerns already rejected. Any next frame-layout proposal must
identify state or construction that can disappear while preserving lexical
identity, generators, genuine suspension, cleanup, cancellation and ownership.

Background tiered-JIT CPU is **0.53 / 0.61 seconds**, while guest CPU is
0.70 / 0.91 seconds. The captures include ongoing tiering and trace overhead;
they are not steady-state or embedding-latency qualification. No new CPU/CPython
comparison is inferred from this diagnostic.

## Evidence and stopping point

[summary.json](summary.json) records identities, gates, all thread CPU deltas and
archive hash. [Text origins](text-warm-allocation-origins.json) and
[CSV origins](csv-warm-allocation-origins.json) retain all sampled categories and
paths. [raw-capture.tar.gz](raw-capture.tar.gz) preserves nettrace files, metadata,
complete records/identities/goldens, machine gates, catalog and collector logs.
Readable collector-driver and corrected offline-reader sources accompany it.
All diagnostics remain outside production; no dependency edge enters core.

Stop B34. Use this fixed evidence for one bounded source/lifetime decision before
selecting code. No automatic activation pool, context fusion, universal tagged
values, JIT or another local reservation tweak. A new redesign needs a concrete
deletion/lifetime tradeoff and fixed whole-job controls before implementation.

## Bounded source/lifetime decision

The follow-up source read selects no runtime prototype. These are concrete
constraints on the observed categories, rather than a claim that the current
architecture is optimal:

| Storage or proposal | Current source / lifetime constraint | Decision |
| --- | --- | --- |
| Function context | `PyFunctionBinding.EnterInvocationFrame` creates the lexical context used by names, nonlocals, class cells, diagnostics and callable guards. Captured contexts can outlive the call. `ExecutionFrame` already creates the empty namespace lazily. | No demonstrated duplicate namespace/context to delete. Reusing one mutable context would need an escaping-identity design. |
| Interpreter and operands | `BeginPendingCall` retains caller operands until child completion; `CreateExecutableInterpreter` already reuses bound argument storage for locals. `CloseInvocation` releases it after cleanup, including suspension. | A second local copy has already disappeared. Eliminating the interpreter requires moving real suspended state, rather than deleting a redundant wrapper. |
| Exceptional and generator state | Handler saves are lazy, absent-region depth arrays are shared, and generator send/delegation state survives suspension. | Moving infrequent fields to another object alone is not a demonstrated material complete-script improvement. |
| Context/interpreter fusion or activation pool | Combining live fields saves headers without removing their state; pooling retains storage and needs correct reset/escape rules. | Do not reopen the earlier rejected designs from an allocation percentage alone. |
| Indexed frame arena | Could change allocation and argument transport together, but callers use stable context/frame references and async methods retain receivers across real I/O. | A larger redesign requiring an explicit stable-identity and awaited-disposal model. This diagnostic does not establish its implementation or latency benefit. |

The relevant implementation is in
[calls](../../../src/Lokad.Lython/Runtime/LythonRuntime.Executable.Calls.cs),
[frame construction](../../../src/Lokad.Lython/Runtime/LythonRuntime.Executable.cs),
[binding](../../../src/Lokad.Lython/Runtime/PyFunctionBinding.cs) and
[lexical namespaces](../../../src/Lokad.Lython/Runtime/ExecutionFrame.cs).
No fallback engine, alternative value representation, frame pool, ownership
abstraction or configuration switch is introduced. This closes this particular
allocation investigation; independent runtime correctness work and a future
source-justified redesign remain possible without a regex producer release.
