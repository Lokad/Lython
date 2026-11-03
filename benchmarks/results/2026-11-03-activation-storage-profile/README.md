# B170: current activation and storage allocation gate

2026-11-03. Retained source **2323ccebc8d526e5a5e0214d85d2de9feba5bff2**,
core **3bed411efcd6e008fc04bca8634d32be64bafc71742509ff0192e8eee86ab8bc**.
No runtime prototype, CPython ratio or timing speedup. One bounded attribution
using existing workers/EventPipe collector and offline allocation reader.

| Complete script | Important sampled allocation shares |
| --- | --- |
| Text summary with helpers | Python strings 26.93%; byte arrays 15.54%; boxed BigInteger 12.08%; temporary reservations 11.90%; loop cursors 8.78%; list enumerators 3.56%; object arrays 7.74% |
| Report rendering | Byte arrays 38.67%; executable interpreter 11.12%; execution context 6.71%; Python strings 7.65%; temporary reservations 6.19%; object arrays 4.52%; boxed BigInteger 3.90% |

Types are disjoint sampled allocation groups, not retained memory or CPU shares.
Array origins distinguish list/split output from executable operand storage and
JSON collection growth. Report contexts originate in complete ordinary-call
activation; text cursors originate in the executable loop's generic iteration path.
Full origins and all smaller groups remain in the raw archive. Neither capture has
unresolved Lython allocation stacks under this reader; sampled byte attribution
still is approximate and instrumentation changes execution.

## Concrete next design decision

Select **private exact-builtin sequence iteration transport in the existing operand
workspace**. Repeated native list/tuple loops currently allocate a general Cursor
and CLR enumerator despite requiring only a source and position. The text capture
attributes about 12.33% of allocation to those two types. Native sequence source
can remain in its private operand slot with separately admitted per-slot positions;
no guest iterator escapes from this representation. Replace the per-entry cursor/
enumerator acquisition and generic next dispatch for this path. Retain general
cursors for exposed iterators, user protocols, resource-bearing iteration and real
asynchronous I/O. Do not create a cursor pool, lifetime graph, new backend or option.

Acceptance before retention: actual list mutation/index semantics, tuple order,
nested loops/comprehensions/generators, break/continue/return/protected cleanup,
custom iterator acquisition/effects, genuinely pending iteration/disposal, terminal
admission, non-allocating-loop cancellation, retained aliases and storage refund at
frame completion. Workspace capacity/metadata must be admitted before allocation;
unstarted/suspended generators stay funded. Use both APIs and controlled complete
text/CSV/report scripts plus a tuple/list mutation control. This allocation evidence
selects a bounded coherent prototype; it does not predict CPU gain.

Complete function activation remains a separate substantial target: interpreter
and context objects account for about 17.84% of report allocation, with operand
arrays additional. Existing lazy namespaces, shared metadata and native helper
returns stay. Do not infer dominant binder CPU from these allocation shares or
revive the measured-slower call-arena/call-control designs. Further activation
work must eliminate whole storage/transport paths and preserve lexical/async users.

## Conditions and stopping point

Two existing complete text/report scripts, same quiet Standard_F4as_v7/Ubuntu
24.04.4/.NET 11 RC3. Actual RAM-file host, full stdout/file verification before/
after, public Run, normal GC/tiering; compilation/startup outside capture. Each
records 512 invocations after at least four seconds of warmup and two-second settle.
EventPipe session readiness uses the existing collector's explicit ready signal,
then stop/rundown and worker close are awaited. All actual named active guest TIDs
are retained; the reader uses that set instead of guessing the maximum-CPU thread.

Text 15,912 allocation ticks /1.932 instrumented seconds; report 31,944 /3.190 s.
Instrumented elapsed is not a benchmark. Session, worker, fixtures and native users
join; each worker verifies output, closes and exits zero. Before/after quiet gates
pass. Owned service **20.582 s**, exit zero, shared lease released. Raw traces and
offline intermediates were staged in an owned RAM directory to avoid the VM's
nearly full disk; the archive was retrieved and verified locally, and the RAM staging directory
was subsequently confirmed absent.
The original immutable archive remains available. No unchanged recapture selected.

[Assessment](assessment.json), [complete traces/origins/records](observations.tgz),
[identity](identity.json), [reader source](allocation-reader.cs.gz).
Existing collector owns the diagnostic session only; no allocation tracing,
reclamation machinery or unsafe code is added to Lython. Core delivery qualification
is independently cached under the B166 milestone. Publication postponed.
