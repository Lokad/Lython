# Shared memory ownership

Lython admits conservative substantial-storage estimates through a borrowed
[`ILythonMemoryBudget`](../src/Lokad.Lython/Public/ILythonMemoryBudget.cs).
Executions, scratch, caches, compiled programs and retained results share its
allowance. [`LythonMemoryBudget`](../src/Lokad.Lython/Public/LythonMemoryBudget.cs)
is the standalone implementation; a null limit is unbounded. Without a supplied
budget, Lython introduces no hidden allowance.

```csharp
var budget = new LythonMemoryBudget(16 * 1024 * 1024);
using var compiled = new LythonEngine().Compile(source, budget);
foreach (ILythonHost batchHost in batchHosts)
{
    using var result = await compiled.RunAsync(batchHost);
    Consume(result); // Finish using values and aliases inside this scope.
}
```

Invocations inherit the compilation-supplied parent unless `MemoryBudget`
overrides it. Multiple executions can draw from one root. A separate
`ResultMemoryBudget` can fund results that outlive the invocation parent;
choose that owner before projection.

## Admission

`CreateScope()` reserves nothing and adds no cap. `TryReserve` admits nonnegative
bytes immediately; `TryResize` adjusts a live reservation. Refusal preserves the
old balance. Admission covers capacity, metadata, major working storage and
old/new replacement overlap before allocation or observable mutation. Failed
construction releases unused reservations.

Any refused admission fails the command, including cache admission. Guest code
cannot catch, suppress or return over it. There is no retry, wait, algorithm
fallback or optional-storage recovery. Guest-raised `MemoryError` remains
catchable. Compilation refusal produces diagnostic `LA0008` on an invalid script.
Native cleanup/refunds remain available; later commands can reuse the allowance.

There are no independent execution, projection, string, collection, host-read
or captured-output size policies. Checked arithmetic, representation constraints,
managed stack protection and bounded I/O chunks still apply.

## Ownership lifetimes

| Owner | When its storage can be released |
| --- | --- |
| Compiled program | After all runs/diagnostic aliases end, on disposal; includes code, metadata and retained compilation caches |
| Execution/batch | After execution, pending host/protocol users and cleanup end |
| Scratch | When construction/operation relinquishes it, including failure/cancellation |
| Retained result | After consumers relinquish projected values/aliases, on result disposal |
| Cache or pool | At its established owner's end or ordinary funded eviction, after users finish |

Scope disposal refunds and invalidates its subtree. It neither cancels work nor
disposes runtime objects; await users and serialize disposal with subtree
operations. Lython disposes its own scopes, never a borrowed root. Retained
aliases, borrowed buffers and suspended work keep their funding. Cancellation
alone does not end ownership.

Refund is independent of CLR collection. Guest reference deletion, collection
clearing or GC does not promise immediate accounting recovery. Lython uses no
guest-heap tracing, per-object identity ledger, weak reclamation or forced GC.

## Streaming and approximation

Long CSV, text, JSON and file workloads use bounded host-driven executions.
Reuse compiled code; finish a batch, consume/dispose its result, then continue.
Scratch can end during an operation; retained guest data can stay funded for the
batch. Arbitrary uninterrupted scans are not promised row-count-independent
accounting. This is not cumulative allocation counting across completed batches.

Estimates cover major capacities and construction peaks, not exact object sizes
or process memory. Allow headroom for small uninstrumented storage, framework
buffers, pools, JIT and host work. Conservative retention can reject earlier;
identical tiny-budget thresholds are not promised. Hosts/providers must admit
substantial storage through supplied owners and respect their lifetimes.
A memory allowance does not control CPU time.

The standalone root's `ReservedBytes`/`PeakBytes` report aggregate estimates.
Result execution/projection peaks and denied-reservation reporting are diagnostics,
not separate allowances. See [public memory controls](../tests/Lokad.Lython.PublicApi.Tests/GovernedMemoryContractTests.cs)
and [provider lifetime controls](../tests/Lokad.Lython.PublicApi.Tests/RegexProviderContractTests.cs).
