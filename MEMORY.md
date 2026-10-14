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
Approximation deliberately leaves roughly 20% of minor objects, metadata and
short-lived temporaries uninstrumented; this is a design allowance, not a
measured coverage percentage. Keep additional host headroom for CLR/runtime
memory, native library buffers and delayed physical collection. Estimate counters
are neither live CLR heap size nor process RSS.

Guest values can share storage and escape through closures or containers.
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
