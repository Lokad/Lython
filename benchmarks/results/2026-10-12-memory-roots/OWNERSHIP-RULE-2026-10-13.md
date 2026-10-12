# Ownership rule and native execution boundaries

Assessment on 2026-10-13, starting from `86b7432e` (production tree
`e115fc393823b1912a18f6e48cffa33f44ddaf77`). This is an architectural proposal
and compatibility assessment, not a performance measurement or a shipped
GC-independent collector. The earlier [namespace assessment](README.md) remains
historical evidence.

## Proposed invariant

Every governed storage allocation has one funded owner from preflight through
publication. At any pressure point, that owner stays charged whenever it is
reachable from a published execution root through explicit runtime child edges.
An owner can be refunded only after complete traversal proves it unreachable
and any resource/storage release needed for the refund has completed.

Roots include persistent run state, every active invocation and its operands,
suspended generators, native helper arguments **and current helper state**,
pending exceptions/cleanup/results, asynchronous operations and public projection.
Root publication precedes admission, guest callbacks and suspension. Replacement
and return transfer publish the new value before withdrawing the old root.
Completion withdraws only that invocation's roots, preserving escaped values.

Child traversal must follow owned and unowned bridge objects: functions/closures,
bound methods, types, cursors, containers and exception state can retain storage
without owning that storage themselves. It must read runtime fields rather than
execute guest attribute getters, iterators or callbacks. Traversal and cycle
handling need funded, bounded workspace available when the budget is exhausted.
Neither that workspace nor complete child coverage exists as a finished design.

This would replace weak owner handles, collection-dependent refunds, forced
generation escalation and allocation-progress retry logic. Existing preflights,
per-container charges, managed stack admission, cancellation and projection
remain. No scalar coupons/refcounts, reclamation tier, uncharged allowance,
native probing or optional protection mode is proposed.

## Why interpreter-frame publication alone is insufficient

[Sum/SumAsync](../../../src/Lokad.Lython/Runtime/LythonRuntime.BuiltinIterables.Aggregates.cs)
hold `total` while the next generator item executes guest code and performs host
I/O. `__radd__` can replace it with a new instance retaining a 16,000-byte string;
that accumulator is neither the original argument nor a guest namespace binding.
It remains a C# local or async state-machine field. A collection at a re-entered
generator's opcode boundary must include it.

The new native-accumulator cap/control cases in
[RootLifetimeContainmentTests](../../../tests/Lokad.Lython.PublicApi.Tests/RootLifetimeContainmentTests.cs)
cover this lifetime through Run and genuinely delayed RunAsync. A second root
case retains a BytesIO buffer solely through a bound method. Existing bound
wrappers already attach receivers; a replacement must traverse those receivers
and audit any additional captured guest values without invoking `__self__`.
These eight cases extend the original twenty root cases.

Deferring all reclamation until native-call return is also insufficient.
[NativeConsumerStreamingTests](../../../tests/Lokad.Lython.PublicApi.Tests/NativeConsumerStreamingTests.cs)
run a complete CSV-to-report job: `sum()` consumes a generator over 5,000 rows
inside one native call at a 256 KiB cap. Dropped rows complete with exact stdout
and report bytes; retaining the rows rejects before publishing the report. The
row registration fees alone would accumulate to more than the cap if every
allocation stayed pinned for that entire call. All four sync/delayed-async cases
pass under current live accounting.

The bounded frame-only/defer-native candidate stops here. A replacement needs
explicit native-helper state and transfer lifetimes in addition to guest frames,
then complete child/resource coverage and funded traversal. No partial collector
or premature refund is implemented. The tests protect representative cases;
they do not prove exhaustive coverage.

## Independent bounded fix: binary file creation scopes

The assessment also found an unnecessary strong edge. BinaryFileHandle retained
its creating ExecutionContext for operations that use only execution services.
An escaped handle, including an already closed one, consequently retained
unrelated creation locals. Six white-box read/write/append and open/closed cases
reproduced the retained frame; both public 28,000-byte cases initially rejected.

The bounded implementation stores the existing ExecutionServices instead and
consolidates binary range/write/append mediation there. Actual file storage and
writer registration remain funded. Caller context still handles Python argument
coercion. Path containment, capability checks, cancellation and exception
translation retain their existing host boundary.

The initial async public case also retained a transient completed-call chain.
The final case adds a genuinely delayed read after function return, giving that
chain an opportunity to unwind before testing retention by the escaped file.
The cap stays 28,000 bytes. This distinguishes unnecessary file ownership from
transient CLR liveness; it does not claim those transient roots were eliminated.
Six owner checks and both public cases then pass. Existing binary funding tests
and root/native-consumer controls remain required.

Other text/CSV/gzip creation-context edges still need separate assessment. This
binary fix neither completes the proposed invariant nor removes forced GC.
