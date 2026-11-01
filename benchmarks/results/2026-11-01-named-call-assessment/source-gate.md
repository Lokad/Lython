# B73 source gate, before profiling or runtime edits

Source081a2280, runtime source unchanged from9003d045/core andd703767c/adapter.

Confirmed: Executable.Calls.TrySuspendForExecutableCall rejects !IsPositionalOnly.
Named calls reach Executable.Binding.ExecuteExecutableCall ->
Calls.InvokeFunctionFromOperands -> PyFunctionBase.InvokeFromOperands ->
RunBound/ExecuteBody -> nested ExecuteExecutableCodeObject. Positional eligible
calls use BeginPendingCall and the existing caller/child driver.

Important negative finding: general named binding already reads existing operand
spans, creates CallArgumentValue structs one at a time and fills final local storage.
There is no argument-array allocation/copy or parameter dictionary to eliminate.
Both routes keep interpreter admission, function entry, bound ownership and cleanup.
Do not describe protection deletion as the improvement.

Potential bounded eligibility: existing SupportsDirectPositional signature and
nonmirrored code, ordinary explicit positional/named call metadata; reuse the
existing general binder for names/errors/defaults, then transfer to the existing
BeginPendingCall child runner. No speculative callable identity cache. Existing
expansion/protocol-default/generator/mirroring semantics keep their real routes.
Only actual nested runner/reentry/control wrappers could disappear.

No production edit or new timing has occurred. Before selecting a redesign, freeze
one short source-variant diagnostic with full Python goldens and ownership/current
token controls as required by PLAN.md. Saved B58/B65 helper effects do not by
themselves establish a dominant named-call penalty. Do not repeat B69 frame arena
or B72 instruction fusion; no wholesale frame/engine rewrite.
