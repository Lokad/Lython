# Shared instruction-prefix prototype: adverse CPU result

This is an active isolated R180 prototype above `ee448715`, **not promoted to the
main runtime**. It replaces native expression preparation, `ExecutableNativeBody`
and `NativeReturnValue`, argument-source metadata/mutation and recursive native
evaluation with the finished executable IR, a type proof and one instruction
prefix. Managed bounded span storage preserves ownership/cancellation; ordinary
frame state retains capacity, caches and awaited cleanup. No unsafe code, new
budget, tracing, GC policy or execution quota is introduced.

The first ordinary five-script desktop diagnostic has flat warm allocation and
compilation allocation down 0.01–0.65%; elapsed is variable/adverse. That catalog
does not exercise the newly closed record-conversion path: its validator performs
later mutable reads. A second useful CSV source adds `parse_amount(record)` to
the maintained validator, preserving the same input/stdout/file goldens. Its new
source hash distinguishes it from the published CPython workload. Independent
isolated CPython 3.13.2 confirms both files and stdout.

That helper variant initially allocated 38.72% less. Review found that a shared
record lookup allocated a frame cache inside the stack-only path. Reentrant
admission could change a later global callable after the entry proof. The original
one-off-dictionary fixture did not exercise the cache; using shared record keys
reproduced the failure. Cache storage now requires persistent frame backing;
closed lookups use the existing direct native path with no cache admission.
This is not recovery after refused admission. The fixed variant allocates 42.95%
less in that complete CSV script; original images/data/failing diagnostics remain.

Quiet B526 compares qualified `ee448715`-equivalent core `b49e50a6` with the corrected
prototype. Five jobs/four processes/two per source, five-second role warmups and
seven rotated 150 ms rounds finish in 141.401 seconds:

| Workload | Elapsed change | Wider process CPU change |
| --- | --- | --- |
| CSV validation with conversion helper | +1.29–2.23% | Flat at the available tick resolution |
| Original text helper script, synchronous | +38.00–52.22% | +36.36–48.15% |
| Original text helper script, asynchronous | +38.01–43.72% | +37.25–41.18% |
| JSON-to-Markdown report | −2.52% to +3.83% | −3.88% to +3.69% |
| Unicode index control | −3.59% to +2.27% | −4.44% to +8.01% |

Text reference/candidate process spreads are 6.68%/3.29% synchronous and
1.05%/2.97% asynchronous. They do not explain the regression's magnitude. The
allocation benefit is not a CPU benefit; flat coarse ticks do not prove CSV CPU
equivalence. Report/Unicode controls remain inconclusive. Desktop elapsed differed
substantially and remains diagnostic, never a replacement for this adverse result.

Includes mediated RAM I/O, full output validation and prior-result cleanup;
excludes startup, fixture reset, protocol and final caches. Process CPU is wider;
compilation is a separate desktop lane. This is not a CPython timing or a released
package qualification. Source/binary/auxiliary identities are explicit in
[the quiet summary](quiet/summary.json) and [frozen identity](identity.json).

All output/balance/isolation/quiet/join/unmount/archive/local-SHA gates pass.
No VM workers or fixtures remain, and terminal status is cached. Archive SHA-256:
`201aa4e4a5dd74483dcb1549194d3515d0bc9238a1285687cba5f6d192a4ea33`.
Three exact redundant compressed copies were removed only after proving canonical
tracked copies, including archive members; raw trees and frozen binaries survive.

Focused checks include 3,625 broad public cases (.NET 11 after cache correction,
.NET 10 before it), 39 final boundary cases on each target, and original managed
stack/admission checks (69 internal/60 public per target before the cache change).
The delivered runtime remains qualified by cached GREEN42727417102; that run does
not qualify this prototype. Two independent record semantic cases are retained on
the delivered runtime. Publication remains postponed.

Next: attribute the adverse text cost to proof replay versus operand/execution
transport, then refine the coherent design or explicitly decide its remaining
debt. A slow measured prototype is evidence, not a permanent design veto. No
promotion or speedup claim follows from removing code alone. [Development archive](development.tar.xz)
retains original/refined sources, cores, commands, validation and all observations.
