# D50 nominal executable temporary lifetime

2026-11-05 before code, baseline2183bf18/frozen3fa1389d; final D48 CI running.
D49 offline caller-assessment.json reuses immutable D43 capture only: text53/783
interface leaves, ordinary42/1022; first Lython caller RetireTo27/32 and target
instruction24/10. RetireTo contains the internal temporary-interface predicate;
target attribution can contain inlining/other code, not a precise removable share.
Preserve old failed gates/unknown5.11/8.51percent/truncated maps and caller paths.

Whole activation gate: context/frame merging preserves their actual metadata and
mutable fields, saves an object header rather than26percent of sampled bytes,
and requires detaching transient registers/control state after lexical escape.
Binding and operand owners also have different completion responsibilities.
No coherent removal selected from object counts alone; no field movement/arena.

Selected complete temporary contract has four sealed reference-class implementers:
PyIteration.Cursor, ExecutionContext.InlinedComprehensionScope, CallArgumentAccumulator,
GovernedByteBuilder. None has another class base. Guests cannot implement the
internal contract. Ordinary Python operands never implement it and must never be
disposed for exposing a Python close method. Replace existing internal interface
with an empty abstract IDisposable base and abstract Dispose; existing Dispose
implementations become overrides. This replaces one type, not an added lifecycle
framework, resource policy or ownership layer. No instance fields/arrays/modes.

RetireTo keeps reverse order, defer-async cursor selection before nominal check,
pending cursor chain/detachment before possible later failure, same clearing and
LiveExtent update. Explicit comprehension completion retains its original trim/
restore order. All other owners/direct calls remain; await pending users before
release, no retry/fallback/early refund. Cancellation/first refusal/stack unchanged.

Hypothesis: remove repeated interface-implementation tests for every retired
ordinary operand across complete scripts. No predicted multiplier or allocation/
layout claim; actual class checks/disposal paths must measure. Focus existing
Release call/expansion/comprehension/generator/formatting/cursor/ownership/cancellation
families both actual targets, preserve semantic/non-replay/refusal assertions.
One<=180s frozen source pair: textwarm/textasync/ordinaryCSV/JSON/report, same
normalGC/tiering/quiet/identities/goldens/joins/finalzero, independent same-current
role and CPython reference. Existing tools only; semantic actual-I/O suspension
separate from completed-host async timing. Retain/refine/withdraw with explicit
regression debt; no fixed percentage veto or automatic retiming campaign.
