# Guarded bulk-yield refinement

Base82e45812, production unchanged. Retain the refined existing-engine borrowed
buffer, but check the destination at its three callers before entering the cold
TryBufferYield helper. Single-item Advance/AdvanceAsync remain original. This
removes an unnecessary helper dispatch from ordinary yields; it does not claim
the entire B864 numeric regression is caused by that dispatch. No new owner,
ledger, execution backend, quota, option or test-only checkpoint counter.

Focused actual-target semantics/ownership/cancellation checks first. Then freeze
actual tested images and compare whole normalization warm/fresh, ordinary generator,
numeric and CSV controls on the quiet VM. B861/B862/failedB863/B864 evidence is
committed and immutable; no unchanged retiming. No promotion/full qualification
until a retained coherent design and explicit decision on remaining CPU debt.
