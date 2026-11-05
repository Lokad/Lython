# D61 complete local augmented update, selected source gate before code

Baseline13d69be6/ec4b6378; full4143internal/16004public per actual11/10 pass,
CI42074585232 running. D60 representative refresh uses frozen13d separately.
D57 layout remains withdrawn, lifetime fences/correctness fixes preserved.

Evidence: D58 complete basic loop has dispatch381/912self, store129, exact add76,
range70, operand op44, augmented evaluation28, operator mapping12. These contain
real work and are not removable fractions. Source confirms ordinary local/name
augmented assignment emits adjacent Augmented +StoreLocal at the SAME statement
span. RHS and old LHS have already been evaluated in Python order. Existing
ForwardValues already rewrites adjacent immutable/local operand sources; binding
arrays already share locals. Do not claim another argument copy or scalar dominance.

Selected coherent removal: combine that validated pair in the EXISTING forwarding
pass, not another analysis or backend. One AugmentedLocal instruction evaluates
the existing in-place/reflected operator and commits the local via StoreLocalValue,
without publishing a transient operand result, taking it again, separate store
dispatch or per-iteration operator remapping. No change to integer representation.
Global/nonlocal/member/subscript/slice targets retain their actual supported paths;
ordinary StoreLocal remains for other assignments. NativeBody preparation precedes
forwarding and keeps its measured native-helper shortcuts.

Keep five instruction ints: primary=prepared AugmentedAssignmentOperatorSyntax,
secondary=second source, InputStart=first source, ResultSlot=target LOCAL physical
slot, LiveExtent=retired operand extent. Target is below first temporary slot and
not part of retirement. Fusion after layout preserves physical input/lifetime and
handler depths; conservative original capacity stays funded. Validate original
Augmented result feeds exactly StoreLocal and spans match. Prepare operator once
using one shared validated enum mapping; move the existing mapper rather than
duplicate it. Linearization preserves these facts, no side table/carrier/bitset.

Runtime: keep old LHS snapshot and RHS values through actual operator completion.
Use same operator logic for all supported values/protocols, including genuine
asynchronous __iadd__/reflected calls. Check active refusal/token before committing
binding, including after await; StoreLocalValue preserves cells/live globals/
mirroring. Retire original operands only after success; on failure clear their
same original range, never target locals. First refusal remains terminal; no
retry/refund of live aliases, new reservation scheme or optional storage escape.

This is one Python augmented statement per instruction, with checks before work
and before binding. Exact old IR checkpoint locations/counts are not a contract.
No suppression of cancellation after actual suspension. Diagnostics retain the
statement span; internal failures during the combined operation use its existing
Python routing. Explicit guest MemoryError remains catchable. Preserve evaluation
order and in-place side effects already completed before a later failure.

Preserve CLR temporary lifetime fencing: a no-inline complete-update handler owns
computed object temporaries and returns no guest value to the long dispatch loop;
ordinary store/worker barriers still serve their real callers. Completed work
stays synchronous; only incomplete operations get existing-style continuation.
No per-frame flags/pending-result fields, new control records/argument arrays,
owners, modes, watchdog/fuel, unsafe code or alternate dispatch framework.

Semantic acceptance: prior read before RHS rebinding, every in-place/reflected op,
alias identity/mutation, exact integer promotion, mutable cells/closures, locals/
globals visibility, generator suspension, actual delayed protocols/cancel/refusal,
preflight before mutation and cleanup/budget reuse. Existing augmented suites first;
add only a demonstrated gap. One source invariant control for fused compact slots
may help, but no timing/checkpoint counters or implementation-mirror test campaign.

Stop/refine if five fields or existing ownership cannot represent the complete
operation without new state/packaging, or if a fast-only strategy would bypass
ordinary Python protocols. Before timing, record exact displaced code/fact proof
and focused Release actual11/10. One bounded <=210s complete basic-loop/ordinaryCSV/
text/JSON/report warm plus ordinaryCSV compile-run source pair, unchanged current
control/CPython/workerb94 and all quiet/identity/golden/Closed/finalzero/join/removal
gates. Preserve adverse observations; bounded correction permitted for material
uncertainty, no percent veto. Separate removed work, allocation, elapsed and causal
CPU; qualify retained delivery coherently. Broader call/frame/frontend/regex remains.
