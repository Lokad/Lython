# Remaining value transport and whole-script relevance

R446, 2026-11-16 UTC, source `cc794368`. Bounded source/evidence assessment
after the retained generator consolidation. No new runtime prototype, benchmark
framework, timing comparison or qualification replay.

A concrete remaining boundary exists: executable Yield calls `TakeValue`, which
publishes a fresh machine integer before storing the object in `YieldValue` and
the existing readonly `PyIterationResult`. Native sum then reads that object even
though its numeric prefix already retains an unboxed accumulator. `TakeSlotValue`
and `ExecutableSlot` already provide managed scalar transport elsewhere. This is
a source hypothesis for generated numeric aggregates, not proof of dominance.

Replacing the result payload with an existing slot could remove fresh integer
publication through a complete producer/consumer chain. It also enlarges and
copies every result, including reference-only text/JSON items. Repeated object
observation must retain one identity; readonly struct copies cannot each publish
an independent box. Existing local aliases and tuple borrows must retain their
canonical owners. A per-item identity owner would replace a box with another
allocation; a second typed iterator API would retain duplicate protocols. Neither
is selected. Real suspension, delegation, injected exceptions and ownership
transfer must remain correct without borrowed storage escaping its lifetime.

The representative normalization pipeline carries strings across yield; its
remaining 2.2× CPython gap would not lose those boxes. JSON/report values are
largely already published by parsing or retained records. Current profiles do not
establish fresh scalar publication as their dominant removable stage. R436's
final-slot range transport improves numeric loops and diagnostic generator drain,
but its complete CSV control is inconclusive. Do not extrapolate or open a new
global tagged-value migration from those diagnostics.

**Decision:** close this source assessment without a prototype. Reconsider when
a complete numeric record-processing script and allocation/CPU evidence establish
the affected work. A bounded comparison then needs warm and compile-to-result,
compilation cost, ordinary text/call controls and explicit repeated-observation/
alias/default/delegation/cancellation/refusal/cleanup acceptance. Replace the
whole selected transport, rather than adding another carrier or special-case
generator backend. This is an evidence gate, not a permanent architecture ban.

The same pass confirms that async startup already shares the bounded reusable
execution workers; incomplete I/O releases the worker. Immutable builtin functions,
constructor signatures and core types are already shared while mutable namespaces
remain execution-owned. Recreating these mechanisms would duplicate delivered
work. Ordinary join already uses admitted final UTF-8 storage after fully consuming
its input; retaining that consumption preserves Python error/effect precedence.
Removing only its wrapper was measured slower under R438. No setup, scalar or
materialization speedup is claimed here.

[Exact source hashes and decision](assessment.json),
[existing normalized-script samples](../2026-11-16-subtext-attribution/README.md),
[range-transport evidence](../2026-11-16-iteration-slot-transport/refined/README.md),
[call/frame assessment](../2026-11-16-complete-call-design/README.md).
The performance objective remains open; publication and regex qualification are
independent. Preserve all earlier slower designs and unknowns.
