# Frontend flow-consolidation source gate

R158,2026-11-09, assessed7c10322c. **No new timing, capture or runtime prototype.**
Reuse the [R128 compilation traces](../2026-11-09-compile-phase/README.md), measured
8472935f. The current name-binding visitor and abstract statement analyzer are
unchanged from that revision; current source hashes are recorded. Other frontend
changes mean these are not current whole-compilation CPU shares.

| Old compile case | Name diagnostics | Abstract interpretation |
| --- | ---: | ---: |
| Config |5/1172 samples,0.43%|224,19.11%|
| CSV helpers |21/1199 samples,1.75%|350,29.19%|

The two visitors retain different facts: maybe-assigned locals versus resolvable
abstract values. Parameters are bound but abstractly unknown; directive-sensitive
deferred bodies skip abstract evaluation while still requiring name diagnostics.
A merger would need additional shared flow state, not remove a proven duplicate.
No merger is selected at this checkpoint; this is not a permanent design ban or
a fixed-percentage veto.

`AbstractState` starts with empty value/length maps, not duplicated builtin
inventories. The current CPython reference compiles source directly, without an
AST rewrite or second parser stage. Neither suspected mechanism needs a new cache
or correction. The next material gap is current short config editing across
compile/warm/compile-to-result, before selecting any runtime initialization change.

[Source comparison, historical phase counts and decision](source-gate.json).
