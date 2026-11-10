# R192 receiver-aware native call entry

Baseb57bfa47, fully-qualified e5731404 runtime/frozene364. Actual existing class-callable
text source stores Words().from_line, a lambda self,line:line.lower().split(). The
native call opcode rejects bound receivers and PyBoundMethod targets, even though
the shared Span binder already supports a separate receiver. The later linear call
runner binds into owned capacity before native execution. Verify native-body facts
for this real definition before editing; no benefit forecast from old profiles.

Hypothesis: complete the existing receiver-aware call convention by passing the
already-evaluated receiver separately into the same16-slot managed native binding
buffer. Eliminate owned binding arrays/storage and pending-call/resume for proven
closed ordinary methods; no prepended self array, new binder/cache/frame dispatcher,
owner/pool/arena/value representation or policy. Other shapes keep the existing
owned/general path before effects. Pure native eligibility, live defaults, current
metadata and original admission/cancellation/stack/exception behavior remain.

Target existing complete R145 bound-method text processing, optionally natural
bound CSV/JSON helpers if source facts justify them. Same source/fixtures/goldens
per image; unchanged ordinary text/search/CSV/report controls. Keep these variants
separate from the main CPython table. Use focused actual11/10 semantic/ownership/
cancellation/refusal checks and short existing warm/compile driver; bounded quiet
confirmation only after actual allocation/work removal and acceptable source cost.
No CPU speedup forecast; preserve desktop/control adversity and compilation debt.

Accept bound alias/direct member/classmethod/staticmethod semantics, descriptor/
method rebinding and argument effects, live defaults/keyword errors/variadic/wide/
custom protocol fallback, shared budgets, retained receiver/results, true pending
host operations and joined cancellation/refusal cleanup, managed stack/recursion.
Do not fallback/retry after work or refusal. Bound buffers cleared after the final
user; values retain existing caller/definition owners. Do not count architecture-
only tests as external behavior; remove temporary source-fact inspection.

Complete by deleting receiver exclusion/owned-bind detour for eligible calls,
keeping one binder/native evaluator and real dynamic fallback. If native facts are
absent or gains do not justify additional dispatch, withdraw/refine with evidence;
not a rigid percentage veto. Full platform/Stress/installed packages only at a
retained boundary. This does not complete the broader performance objective.
