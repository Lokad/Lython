# One source-backed transport correction

Both original independent blocks show affected CPU regressions: text12-13%, search16-25%, captured-generator4-8%, with controls generally stable; lower allocations alone are not an acceptance. Full raw results retained; original service186.932s terminal success/inactive/lease free.

The implementation currently copies a large value activation through a return temporary, by-value installer argument and pop temporary, beyond the state transfer itself. Correct the representation mechanically: save directly into a ref continuation slot, install through an in argument, restore from the backing slot before clearing it. Keep all fields, lexical views, arrays, ownership/admission and generator semantics unchanged. This is one bounded correction to inadvertent operand transport, not a new driver or claim that it removes the whole regression.

Rerun affected semantic/cancellation/refusal checks, then the same frozen five complete jobs/two counterbalanced blocks with current runtime baseline and controls. No additional measurement if controls are sound and regression remains: remove this prototype and continue toward a different demonstrated dominant-work target; preserve the allocation finding and reusable-driver design lessons without permanently vetoing future materially different architectures.
