# Governed memory

Lython admits conservative storage estimates against a host-supplied
`ILythonMemoryBudget`. Executions, compiled programs and returned results use
explicit owners sharing that allowance. The standalone `LythonMemoryBudget`
provides the same contract without a host adapter.

```csharp
var budget = new LythonMemoryBudget(16 * 1024 * 1024);
using var compiled = new LythonEngine().Compile(source, budget);
foreach (ILythonHost batchHost in batchHosts)
{
    using var result = await compiled.RunAsync(batchHost);
    // Consume the result and relinquish its aliases before the iteration ends.
    Consume(result);
}
```

`Compile(source, budget)` reserves a rough estimate before compilation and keeps
the compiled representation and its bounded diagnostic cache charged until the
script is disposed. Refusal produces an invalid script with diagnostic `LA0008`.
Local-module compilation also admits a coarse estimate before parsing/lowering.
Once prepared code can be exposed to guest callbacks, its estimate stays charged
for the batch even if import execution fails; parsing/lowering failures return
their unused admission. This preserves escaping code aliases without tracing them.
Runs use that borrowed budget unless `LythonRunOptions.MemoryBudget` supplies
another. One-shot `LythonEngine.Run`/`RunAsync` disposes its temporary compilation
after the invocation and its cleanup finish.

Each invocation owns its working storage. Projection allocates under a result
owner established before execution; this owner is a sibling of the invocation
owner and survives execution cleanup. Disposing `LythonExecutionResult` clears
its payload references and releases its estimate. Results, including failure
diagnostics, retain their admitted estimates. A small resource-refusal outcome
can be returned without payload admission when no allowance remains; its wrapper,
fixed diagnostic and borrowed original source are part of host/control headroom.
Refused compilation still preserves the borrowed budget association. Payload accessors reject use
after disposal.

Extracting `ReturnValue`, stdout or diagnostics does not create another owner.
Keep the result alive until **all extracted aliases and asynchronous users have
finished**. Likewise, await all runs before disposing a compiled script, and
relinquish extracted diagnostic aliases first. Disposal does not cancel work.
Serialize disposal against every user of the owner; concurrent admission does
not authorize concurrent disposal of live storage.

The supplied budget is borrowed. Lython disposes only scopes it creates. A host
may provide `ResultMemoryBudget` when an invocation's parent will end before
its output; that longer-lived owner must be chosen before running. Ancestor
disposal invalidates descendants and refunds their reservations, so descendants
and their storage must never outlive an ancestor. There is no automatic ownership
transfer or refund triggered by CLR collection.

Executable starred-tuple expansion uses an owned mutable working list before
constructing its immutable result. Working and result storage are funded together
during conversion. Their estimates may remain charged until the execution/result
owner ends, so narrow allowances can reject earlier than with the former lowered
temporary-list path. Empty results reuse the existing immutable empty tuple;
no new cache or identity tracking is introduced.
The same finite-owner policy applies to incremental set/dictionary display
construction: unpublished partial storage can remain charged after a caught
construction error until its execution/result owner ends. Admission can therefore
refuse earlier than a lowered path that retires private constructor storage.
Allocation preflights and deterministic owner cleanup remain; retaining a result
or pending asynchronous user keeps the owner funded.
Executable f-strings keep their formatted parts funded until the final join,
admitting output capacity while those parts still exist. Intermediate estimates
can remain with the finite execution/result owner after a caught formatting or
join failure. Earlier denial than the incremental lowered builder is possible;
pending protocol callbacks and escaping text retain the same ownership protection.
Executable lambdas retain shared closure cells rather than forcing complete local
namespace mirroring. Cell-array capacity and newly retained cells are admitted
before allocating the captured array; denied capture publishes no funding marks.
Lowered bodies and dynamic namespace consumers retain their existing dictionary
ownership. Lambda metadata now uses ordinary function storage, so small allowances
can reject at different thresholds; pending users and escaping aliases stay funded.
Deleting function metadata removes the reference without refunding table capacity
or escaped data. Reinsertions conservatively admit another slot. Reserved-field
resets that need a new metadata entry must admit it before publication.
Executable eager comprehensions reuse the existing binding lifetime in the caller
frame. Scope end restores lookup/capture state and does not refund collection or
element storage retained by a result. Failed/cancelled nested scopes unwind in
reverse order; unpublished partial storage can stay with the finite owner.
Native enumeration working storage is disposed on loop/comprehension failure or
early exit. This releases its temporary allowance without closing retained Python
iterator/generator values or refunding their surviving storage.

Fixed-arity unpacking admits capacity for at most one excess value beyond its
target count, using the existing temporary lease and capacity-growth preflights.
The input's full length does not determine that scratch storage. Starred unpacking
and general materialization keep their complete-drain behavior. A next callback
can run before subsequent buffer growth is refused; target stores wait for arity
validation. Lease cleanup releases scratch capacity while values retained by
guest containers, iterators or public results keep their owners and funding.

Executable member/subscript stores retain RHS/receiver and optional key under the existing
execution owner until the awaited mutation ends, then clear them even on failure.
They introduce no per-store read dictionary, scratch owner or reservation fee.
Stored aliases retain their existing funding; clearing operands does not refund
data still retained by containers or results.

Host command requests carry the active budget in
`LythonSubprocessRequest.MemoryBudget`. A cooperating dispatcher borrows it,
creates any working scopes it needs, forwards it to nested Lython executions,
and awaits command users and cleanup. Output buffers that survive the callback
need an owner that also survives it, such as the borrowed parent. Cancellation
is a request to stop; allowance stays charged until outstanding work has ended.
Request payloads are borrowed until callback completion. A host retaining them
must create owned copies under a surviving owner, rather than retaining the
borrowed guest buffers after the invocation ends.

Scopes start empty, share one root limit and introduce no independent cap.
`TryReserve` and `TryResize` refuse immediately without changing accounting;
zero is valid and negative estimates throw. Failed resize preserves the previous
charge. Disposing a reservation refunds it; disposing a scope refunds its
subtree and invalidates it. Disposal is idempotent and nonthrowing. Admission and
resize through disposed owners throw `ObjectDisposedException`.

Major backing capacity, copies and replacement overlap are conservatively
charged. Internal execution/projection limits remain separate policy limits.
Exact immutable-byte sums with builtin integer starts keep a machine subtotal
and preflight one final exact-integer result, using existing temporary ownership
for major magnitudes. This avoids per-byte numeric storage while preserving
input/result overlap, empty-start aliases and native-operation ownership. The
conservative final preflight can refuse earlier at narrow allowances; identical
tiny-budget thresholds are not a contract.
Function parameter-name maps/layout arrays and live default containers own
capacity charges. Definition-time default maps use temporary admission during
construction, returning it once the Python containers own their storage; failed
construction refunds only new, unpublished metadata. Clearing/replacing published
defaults does not refund storage that can still have aliases or an active lookup.
Parameter-heavy definition batches can therefore refuse earlier than older
revisions that omitted layout capacity. Use bounded execution batches; immutable
layout sharing remains a performance opportunity, not an accounting exemption.
Approximation deliberately leaves roughly 20% of minor objects, metadata and
short-lived temporaries uninstrumented; this is a design allowance, not a
measured coverage percentage. Keep additional host headroom for CLR/runtime
memory, native library buffers and delayed physical collection. Estimate counters
are neither live CLR heap size nor process RSS.

Guest values can share storage and escape through closures or containers.
Eligible compiled functions use captured cells under both `Run` and `RunAsync`;
selecting the asynchronous API does not by itself require retaining a mirrored
whole local namespace. Supported lowered fallbacks can still require mirroring.
Numeric container slots can each retain a conservative payload estimate even
when they refer to the same integer. Python identity is preserved; identity-based
charge deduplication is not promised. Alias-heavy collections can therefore be
refused earlier than their physical CLR size would suggest.
Lython therefore retains their estimates conservatively within a finite
invocation instead of tracing reference graphs. For long CSV/text/JSON/file
work, use bounded host-driven batches with persistent results under longer-lived
owners. Repeated batches return temporary accounting to baseline immediately.
One uninterrupted script is not promised a row-count-independent allowance;
arbitrary reference withdrawal does not promise immediate accounting recovery.

Admission failure is an ordinary resource-limit outcome. It occurs before
tracked allocation or mutation; failed construction refunds unused admission.
Earlier output and side effects remain. Refusal implies neither transaction
rollback nor permission to replay a command. Lython does not force collection,
wait for allowance, or retry after collection.
