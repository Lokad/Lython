# B226 final bounded simplification before code

B224 combined representation and B225/d6fd5e6b separate-plan revision are NOT
promoted. B225 six-case164s, all gates: text warm+2.89/+3.17%, old spread0.27%,
candidate IQR0.68%; fresh text+0.13/+0.48%, allocation~0.01–0.03%. This does not
support the combined-function-size hypothesis; no CPU cause attributed.

Remove newly introduced code-object parameter counts/tuple and its compiler pass,
restoring original hot compiled-code representation. The plan derives counts at
function creation, borrowing only existing code.LocalNames/LocalNameToSlot.
Inline sole-use count helper; no replacement metadata object/cache/owner. Keep
separate sealed plan, mutable defaults, conservative320B coarse object estimate,
preflights/rollback/cancellation/terminal refusal/retained aliases.

This is less machinery than either prototype and avoids moving a count pass to
compilation for scripts executed once. Exact removal is a design reason; performance
effect remains a hypothesis. Four jobs: text warm/fresh, reportasync, configwarm.
Same frozen old controls/ordinaryGC/quiet/goldens/identities/ownership/cleanup;
service180s. Focused actual11/10 checks before freeze. If adverse, withdraw layout
work and proceed to the broader prepared declaration milestone. No further local
variants/hint series selected without a different design/evidence.
