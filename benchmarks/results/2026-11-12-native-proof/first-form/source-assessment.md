# R254 source assessment

Current-source B599 samples justify assessing the entire native proof phase,
particularly helper-based search. The prototype uses the existing postorder node
array, validates child consumption once during compilation, then keeps only
transient enum shapes in bounded stack scratch. Ordinary argument/default binding,
execution, owners and dynamic fallback remain. It deletes recursive GuardNativeReturn
and GuardNativeFormattedValue dispatch; actual formatting reads and live reads in
evaluation remain. No guest-value arena, call controller or cached caller token.

The new proof visits all branches as the old conservative proof did. Guard reads
remain free of guest protocols/admission; canonical dictionary formatting reads
can materialize already-funded cached scalar values, as before. Conditional bodies
with speculative formatting reads are still excluded by frontend eligibility.
Stored cells remain live and actual values are read again after admission/effects.
Compilation rejects an inconsistent native-node order as a defect, without silent
fallback to a different execution strategy. Python-supported dynamic cases retain
the ordinary executable engine.

Initial 1,646 internal/5,836 public checks pass on each actual target. The only
subsequent source change makes the new linear proof check cancellation/refusal at
natural 64-node boundaries. That exact form passes 1,646 internal/333 public checks
per target; earlier source and all four original TRX/logs survive in initial-checked.
No checkpoint-count assertion or replacement quota is introduced. Initial C# list
signature build error is preserved; the parameter now uses existing IReadOnlyList.

Six unchanged complete scripts both APIs and separate compilation pass full stdout,
files, balances and cleanup. Allocation stays flat. SearchRun diagnostic elapsed
improves; JSONAsync +26.38..+27.34% with0.76%control, CSVAsync +5.49..+7.68% with2.08%
control and ordinary compilation +7.05..+7.10% with0.06%control are preserved. No CPU
claim follows. One12-job B600 comparison explicitly includes adverse paths and
ordinary compilation; no full platform/package cycle until retention is decided.

The compiler performs an additional short postorder validation pass and stores one
capacity fact. Its cost must be reported separately from warm execution. The proof
copies only input indexes and type shapes, not guest arguments or closure values.
No allocation reduction is predicted. Retention needs whole-script evidence and an
explicit decision on remaining regression debt; a fixed percentage is not a veto.

Later construction review: current split preparation scans all words before first
effects to preserve eager admission, then scans while consuming. Estimates are
linear per word (128B + conservative payload), but replacing them with a worst-case
source-length allowance can sharply increase rejection for long single-word input.
Caching per-line ranges adds retained cursor capacity/ownership; duplicating the
Unicode scanner creates another semantic route. No such change is selected merely
because preparation has samples. Native histogram work remains material; do not
repeat withdrawn stack-scope hoists or generic return-ABI migration without new
workload-specific evidence.

Reentry during admission, all-branch type proof, formatter callbacks, exact integer
promotion, identity/aliases, missing inputs, real async protocols, first refusal and
joined cleanup retain existing tests. Managed stack admission is unchanged. Fully
managed source only; no unsafe APIs, native probing, new scopes or resource policy.
