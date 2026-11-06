# F03 bounded refinement, declared before code

Preserve B312166.83s/six jobs/all gates and B313 one-job allocation diagnostic.
B312 CSVwarm+.05/-1.40%, fresh+.22/+1.44%; async+4.59/+.11% has4.28% current
spread. Text-2.30/+.99% mixed. Method+.91/+9.05% and8.06% current spread is an
adverse/inconclusive tradeoff control. B313 process allocation-1.77/-1.79%;
elapsed-.44/+.92% mixed, no causalCPU claim. Do not present these as a broad gain.

Concrete source issue: PyExecutableFunction.TryExecuteWithoutFrame rejects any
OwnerType, even when a prepared native body cannot observe an invocation namespace.
Ordinary methods then bind arguments and create a context only to execute that
same native body. With a unified context, this also constructs unused VM fields.

Remove that blanket owner check, not the existing pure-body proof, scope barriers
or binder. NativeBody excludes definitions and captured-cell instructions; runtime
guards retain lexical parents, class bodies/cells, annotations, comprehensions,
active handler bindings and exact native operations. Unsupported reads/protocols
use the existing executable body before effects, never retry after real work.
Receiver insertion/defaults/keywords already finish in admitted bound storage;
native return consumes those values before cleanup, with existing function/stack
admission, active exception state and named source frames. No new fast-path engine,
cache, owner, configuration, token handling or generalization to guest callbacks.

Also remove identity aliases/duplicate generator parameters left by the structural
migration: initializing a supplied context returns no second interpreter/context.
Focused existing method/function/class-cell/protocol/cancellation/admission tests,
new lifetime/headroom checks, then one bounded final five-job block: CSVwarm/fresh/
async, text control and method-return control; <=160s supervisor, same frozen
worker/normalGC/quiet/fullgolden/join/funding gates and identical-current role.
This confirms the changed call boundary, not another retiming solely for signs.
Decide retention and any regression debt afterward; allocation evidence applies
to measured c31 source, not automatically to the final refined binary.
