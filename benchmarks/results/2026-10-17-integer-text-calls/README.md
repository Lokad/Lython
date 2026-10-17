# Native integer-text call transport — 2026-10-17 UTC

Ordinary one-positional-input `int(text)` and `int(bytes)` calls consume existing
executable operands. Both hosting APIs reuse the existing single-input guarded
entry and integer conversion kernel. Eligible calls avoid call-argument transport
and bound-argument arrays; `RunAsync` also avoids awaiting the general builtin
entry for these synchronous operations. Per-run mutable constructors stay private.
Dynamic hooks, explicit bases, keywords and expanded calls keep their general route.

| Execution API, 128 calls | Earlier Lython allocated bytes | Candidate allocated bytes |
| --- | ---: | ---: |
| Synchronous, text or bytes | 20,792 | 11,576 |
| Asynchronous hosting, text or bytes | 32,056 | 11,576 |

Every one of eight samples per case has that value, after four warmups. There are
four cases: text/bytes through both APIs. This is **allocation evidence for native
call transport**, not an elapsed-time, complete-script or CPython comparison.
The complete CSV workload uses integer field conversion, but its timing benefit
is unmeasured. Historical CSV measurements retain their original revisions.

Baseline production source: `8505c40d0f1123d7659480d49a02f5cb2a1e60e7`.
Candidate production source: `dada7c9cd067259f7bda3cfcfd1a7d8c8dbe3162`.
Both measurements compile production into white-box test assemblies, with an
identical temporary diagnostic. The candidate was measured before its commit;
informational metadata alone therefore does not identify its code. Three changed
production files match the committed candidate, with normalized Git-source and
raw measured hashes, DLL hashes and process receipts in [allocation.json](allocation.json).
A later strengthening of the permanent owner assertion is outside the diagnostic.

Boundary: execute 128 unrolled `int(value)` expressions followed by `return 0`,
using one preconstructed, governed text or bytes value containing `7`. Compilation,
context/input construction and final owner disposal are outside the allocation
counter. Frame entry/cleanup, parse scratch, boxing, result admission and ordinary
execution checks remain inside. Native async execution completes inline, keeping
`GC.GetAllocatedBytesForCurrentThread` on one thread; actual host I/O is separate
semantic coverage. Each fresh scope shares a 4 MiB root; after use its accounting
returns to zero. No forced collection, pooling override or protection change.

Machine: shared Windows 11 x64 development workstation, Intel i7-14700KF,
28 logical processors, 64 GiB RAM; SDK 10.0.300-preview.0.26177.108 / .NET 10.0.13,
ordinary GC and tiering. Owned diagnostic groups finish below 1.3 seconds,
including cleanup, under their existing 28-second process bounds. These durations
are process-budget receipts, not benchmark times. No VM capture or long lane.

Eight new fuel/span matrices match exactly, alongside 72 prior traces. Fourteen
white-box controls cover ordinary/general fuel and paused text/bytes inputs through
success, cancellation and terminal refusal, including another owner sharing the
allowance. Fourteen public controls preserve independent CPython outputs, aliases,
rebinding during argument evaluation, dynamic callables/protocols, general argument
forms, actual host reads and cancellation. Existing integer preallocation, retained
result and managed stack controls remain. Qualification belongs to the delivery
revision; source-level acceptance is not a complete performance qualification.

The [temporary diagnostic source](native-allocation-probe.txt) is archived outside
live builds and removed from the test project. It is evidence, not another
benchmark framework. Allocation totals establish neither CLR physical heap nor
collection pauses or embedding latency. CPython remains the primary external
performance baseline in [BENCHMARKS.md](../../../BENCHMARKS.md).
