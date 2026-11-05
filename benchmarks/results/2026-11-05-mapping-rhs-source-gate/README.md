# Mapping lookup/arithmetic source gate

2026-11-05, committed source cf3dc9cd. Read-only assessment; no prototype,
timing, allocation reduction or new qualification is claimed.

The maintained CSV, JSON and text aggregation scripts execute
`totals[kind] = totals.get(kind, 0) + amount` or
`counts[word] = counts.get(word, 0) + 1`. Their ordinary executable route already
keeps receiver and arguments in existing operand storage. `LoadCallReceiver`
selects the immutable direct dictionary-get marker, and `ExecuteOrdinaryCall`
passes the evaluated key/default directly to `InvokeDictionaryGet`. There is no
argument array or bound-method object on this exact builtin path. The ordinary
arithmetic funnel owns exact integer promotion and admission. Assignment evaluates
its receiver/key only after the complete RHS.

A combined RHS would principally remove instruction dispatch, temporary-slot
publication/retirement and a marker transfer. It would not remove a collection,
call frame, argument array or result box. Supporting the same Python behavior
requires preserving failable leaf spans, receiver-before-argument evaluation,
default-before-lookup and delta-after-lookup evaluation, protocol-key callbacks,
numeric protocols, real suspension, and the later LHS reads. A pure native guard
would also need to distinguish dictionary protocol keys and result types without
repeating a potentially effectful lookup. The generic executable path remains
necessary. New guarded metadata or conditional instructions would add retained
code and an alternative RHS route rather than displace that implementation.

The current prefix/call/mutation samples include useful and overlapping work;
they do not isolate this sequence as dominant removable CPU. Earlier
[checked-read variants](../2026-11-05-operand-read-assessment/README.md) already
preserve adverse whole-script results despite fewer instructions. This gate
therefore selects **no dictionary-specific opcode or expression evaluator**.
This is a priority decision, not a permanent veto or a measured design failure.
Reopening needs evidence or a design that removes complete work/storage while
retaining the effect/lifetime proof above. No timing is needed to establish the
absence of the proposed argument-array removal.

Source anchors: `Frontend/ExecutableScript.Expressions.cs` (call/binary lowering),
`Runtime/LythonRuntime.Executable.Interpreter.cs` (direct receiver),
`Runtime/LythonRuntime.Executable.Calls.Invoke.cs` (borrowed operands),
`Runtime/LythonRuntime.Calls.cs` (dictionary invocation), and
`Runtime/LythonRuntime.Executable.PreparedTargets.cs` (later target evaluation).

Continue at a real bulk boundary: JSON string token reading and output encoding
still inspect ordinary bytes individually. Existing complete report attribution
has **ReadJsonString** at 6.13% leaf / JSON at 31.5% inclusive, without predicting
removable CPU. The initial encoder-only plan misread that as emission: preserve
its original plan and control collection with this correction. The report does
not encode JSON; its timing cannot establish an encoder gain. A bounded native
byte-run comparison of the completed reader/encoder algorithms preserves strict
decoding, escape semantics, bounded cancellation, preflights and existing owners.
