# B157 native producer/stream call source gate

No prototype implemented. Current production0f05d7c0, docs-only successor10d909ae;
all source snapshots/measurements are joined. Keep current qualification independent.

PyTextStream.Write and WriteAsync resolve "write" and build a CallArgumentValue[]
before InvokeCallableTarget on every chunk. TextFileHandleMembers.TryGetMember
constructs BoundCallable for "write"; named binding builds final values. There is
no executable member cache on this helper route. JSON dump uses it per iterator
chunk. Compiler ordinary file.write already routes to InvokeFileWrite in Calls.cs.
That operation enters/leaves the managed interpreter frame, checks execution,
translates errors, and calls the same TextFileHandleMembers.Write/BinaryFile.Write.
Native writing stages governed data synchronously; actual asynchronous publication
and pending ownership remain in existing flush/close/cleanup. No new dispatcher.

Exact TextFileHandle is sealed, has no instance dictionary (PyMemberAccess
HasInstanceDict returns false), and builtin member mutation reports read-only.
Native write selection therefore needs no dynamic lookup; PyInstance/custom
writers keep ordinary per-call resolution, including rebinding and descriptor
side effects. Ordinary native attribute reads still materialize Python method
objects. The generic implementation must remain for those actual callers.

Selected implementation direction: share/widen the existing guarded native write
operation internally; exact native handles bypass member/argument/binding packets
inside PyTextStream only. Preserve current arity/type/error precedence by reusing
the same typed operation (one value always supplied). No new table, interface,
cached bound receiver, options, builtin registry or alternate writer framework.

Before code, verify ambient source spans/frame admission are equivalent. Text
type validation precedes EnsureOpen in both current paths; preserve this when a
closed file receives a non-string. Preserve custom encoder arbitrary chunks,
binary/text distinctions, ignored writer return values and native/callback errors.
Native output preflights capacity/overlap before mutation; removing temporary
packets must not weaken first terminal admission, cancellation or cleanup.
Custom asynchronous callbacks still await actual work; native staging remains
synchronous while real flush/close remains awaited. No user operation is abandoned.

Existing coverage includes JsonTextStreamTests, JsonSuspendingCallbackTests,
JsonOptionUnwindTests, separator/object-graph ownership and command admission.
Add only independently missing custom-rebind/error/ownership regressions. Avoid
architecture-only argument-array counts, exact checkpoint tallies or timeout changes.

Measure complete config/JSON output and one representative native CSV output if
existing coverage lacks the affected record-write shape; reuse sources/fixtures.
Use frozen0f baseline, changed image and unchanged controls, ordinary profiles.
Choose/disclose filesystem condition; RAM mitigates observed ftruncate latency
without changing APIs, and ordinary-filesystem results must remain visible.
Report allocations and CPU/elapsed separately. B154 CPU member4/174/binder5/174
leaves are evidence of work, not an additive speedup prediction. Bounded refinement
or measurement correction is allowed; all adverse results persist. Decide
retain/debt/withdraw at completion; no dormant implementation. Platform/stress/
package qualification batches at the next retained delivery boundary.

Coverage refinement: JsonTextStreamTests.DumpDispatchesIterencodeAndResolvesWriteForEveryChunk
already verifies write rebinding after the first yield and ignored writer return.
Do not duplicate it. BinaryFileCompatibilityTests binary-closed-validation preserves
its specific closed/type precedence. Native shared operation calls the same typed
members, so text and binary precedence must each remain unchanged.
