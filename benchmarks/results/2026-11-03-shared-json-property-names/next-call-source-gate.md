# Current-source call/activation assessment, 2026-11-03

No implementation selected. Existing source already eliminates the review's old
argument packaging for ordinary positional executable functions: Binding.cs passes
operand spans, Executable.Calls.cs explicitly suspends to an admitted child frame,
PyFunctionBase.BindPositionalOperands builds final bound storage, and
CreateExecutableInterpreter reuses it as locals. Native closed returns may omit the
context/frame; general native packets alone use tiny cleared pools or separately
admitted substantial storage. Bound receivers do not require a prefixed array.
B132 has removed runtime operand layout discovery/growth and nested block driving.

This is not evidence that a remaining second copy or call arena is missing.
Remaining context/interpreter/control/local/member/cell activation is semantic:
closures, suspended calls, locals(), exceptions and generators expose lifetimes.
A future complete-call design must name an actual displaced allocation/transport
and representative workload where it is dominant. Current B134 CPU leaves do not
show separate frame construction dominance; do not infer it from source volume.
Preserve slower measured arena/call-control results. No speculative frame pool,
reused public locals namespace, alternative native statement VM or tagged-value
migration is selected by this map. B138's storage outcome is independent.

Next gate after B138: one bounded allocation/cost attribution using existing named
helper and report/text scripts, paired with source counts and an unchanged control.
Distinguish required activation from redundant representation, then select a
coherent deletion/replacement milestone or document uncertainty and move to actual
remaining conversions/frontend work. Do not silently repeat broad CPU captures.
