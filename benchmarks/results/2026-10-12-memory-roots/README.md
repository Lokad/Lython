# Guest memory roots: bounded replacement assessment

A namespace-only collector cannot safely replace weak-owner reclamation. Live
storage also belongs to operands, dynamic callers, closures, suspended generators,
exceptions and pending host operations. CLR reachability currently covers these
references; a logical replacement must cover them explicitly before refunding.

This assessment uses production tree `32b91f2209a861070f2d5aeccbc61c2fdf32adf2`
at `f4299c66`, on 2026-10-12. It records a rejected partial collection rule and
adds regression gates. Runtime memory, forced-GC and managed-stack policies stay
unchanged. Local focused checks are distinguished from delivery CI below.

## Required root families

| Root family | Current representation | Replacement requirement |
| --- | --- | --- |
| Namespaces and persistent state | ExecutionFrame variables/lexical parents; ExecutionState modules, builtins, arguments and caches | Follow guest values and intentionally retained cache storage; distinguish host capabilities from guest storage. |
| Locals and closure cells | CurrentExecutableFrame locals/cells; function closure contexts | Include every active invocation. Lexical parents do not describe the dynamic caller stack. |
| Operands and pending control flow | Interpreter stack, return/yield/delegation state, abrupt signals and saved exceptions | Include values without namespace bindings, including values already popped into helpers. |
| Builtin helpers and async evaluation | Rented argument arrays, C# receiver/index/left/right locals, constructor transactions and async state machines | Keep values funded throughout admission, callbacks, awaits and cancellation. |
| Suspended iteration | PyGenerator frame/interpreter/exception state; PyIteration.Cursor source/enumerators; CSV source/parser state | Preserve suspended values and escaping aliases through closure/abandonment. |
| Exceptions and cleanup | ExecutionServices.CurrentException, handler saves, arguments/attributes/cause/context and pending returns | Follow payloads even without an `as` binding and through cleanup. |
| I/O and projection | Pending operation state, acquired handles/windows and the return value being projected | Preserve funds until release or transfer to separately bounded projection. |
| Finalizable storage | IPyFinalizableOwnership and registered snapshots | Logical unreachability does not prove resources have been released; account through release/finalization. |

These are required families, rather than a complete visitor for every runtime
type. `IPyOwnershipSnapshot` reports an owned charge; it does not enumerate child
references or active execution roots.

## Why the partial rule is rejected

The candidate rule walks current namespaces, lexical parents and executable
locals, then refunds unmarked owners. Existing source exposes concrete gaps:

- [Executable invocation](../../../src/Lokad.Lython/Runtime/LythonRuntime.Executable.cs)
  publishes CurrentExecutableFrame only when locals/cells/closures require it.
  Other invocations can still have live operands and pending returns. A dynamic
  caller's active context need not be a lexical parent.
- [Async executable operations](../../../src/Lokad.Lython/Runtime/LythonRuntime.Executable.Interpreter.Async.cs)
  remove argument values from the stack before invoking a target. Binary,
  subscript and slice handlers likewise pop into C# locals before helpers and
  allocation checks. Walking the remaining stack would miss live storage.
- [Lowered async expressions](../../../src/Lokad.Lython/Runtime/LythonRuntime.LoweredAsync.Expressions.cs)
  retain a left operand while evaluating/awaiting the right. Partial literals,
  comprehensions and argument evaluation have analogous pending values. C# locals
  must not be assumed dead or enumerable from a guest namespace.
- [Generators](../../../src/Lokad.Lython/Runtime/LythonRuntime.Generators.cs),
  [cursors](../../../src/Lokad.Lython/Runtime/PyIteration.cs) and
  [exception services](../../../src/Lokad.Lython/Runtime/ExecutionServices.cs)
  retain additional references beyond a value's accounting snapshot.
- [Finalizable ownership](../../../src/Lokad.Lython/Runtime/IPyOwnershipSnapshot.cs)
  deliberately retains charges through finalization. Reference coverage alone
  cannot establish resource release.

A replacement needs explicit temporary/caller roots and owned-value child
traversal, or a broader execution representation that supplies those roots.
Neither redesign is adopted here. No scalar coupons/refcounts, reclamation tiers,
reflection heap scan, native telemetry or policy switch is introduced. Forced GC
remains necessary under the current invariant.

## Fixed containment gates

[RootLifetimeContainmentTests](../../../tests/Lokad.Lython.PublicApi.Tests/RootLifetimeContainmentTests.cs)
holds a 16,000-byte payload in five roots: a partly evaluated call argument,
dynamic caller local, closure, suspended generator and active exception without
an `as` binding. A second 16,000-byte allocation follows an actual one-byte
mediated binary read and source closure.

Each root rejects that allocation at a 28,000-byte cap with typed
ExecutionMemoryLimit; a 120,000-byte cap completes with an independently known
integer result. The `gate` stdout checkpoint proves denial happens after I/O and
closure rather than initial setup. Identical scripts run through Run and genuinely
delayed RunAsync. All **20 focused Release cases pass** in an owned **2.844-second**
group, without timeout and with accounted peaks within the cap.

These gates protect concrete lifetimes. They do not prove complete root coverage,
a GC-independent collector or embedding latency. Delivered representative
CSV/JSON/text/file alias, retention, cancellation and lease gates remain required.
Exact-head full Release/probe/package-consumer CI is a separate delivery gate.

The bounded assessment stops at the named missing-root/resource requirements.
No partial collector or premature refund is implemented.
