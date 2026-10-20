# Class annotation correctness

This is compatibility and ownership evidence, not a performance measurement.
CPython 3.13.2 is the external semantic reference. The old ordinary-class path
fabricated annotation markers after execution; timing that incorrect path as an
equivalent workload would be misleading. No speedup or current-HEAD benchmark
claim is made.

The runtime change shares the variable-annotation evaluator between modules and
classes, initializes class dictionaries before the body, and resolves implicit
stores against the local mapping before globals, independently of global/nonlocal
directives. Dataclass decorators read live metadata. Parameterized InitVar values
retain their real argument and use existing coarse admission and ownership hooks.

Removed: the post-body StoreAnnotations pass, modern-only annotation switch and
helper, fabricated PyDataclassAnnotationValue and formatter/factory, syntax-only
dataclass field collection and an unused members parameter. Actual class/lowered
consumers and named-tuple/static field classification remain.

Intentional compatibility correction: eager effects and missing-name errors are
now observable; field types expose real values, including builtin identity and
qualified typing aliases. Quoted/future annotations retain their behavior.
Upfront class dictionary admission can refuse before earlier body effects.
No public API, new allowance, collector, mode or dispatch framework is added.

Focused Release evidence is in `checks.json`. Selections overlap. Genuine delayed
host callbacks prove cancellation during value evaluation, annotation evaluation
and replacement-mapping stores. Refusal remains terminal through guest catches;
shared results retain funding until disposal and accounting returns to baseline.
Representative CSV/text/JSON/file jobs retain cancellation, publication, retention
rejection and bounded host-batch checks. A separate final CI receipt records full
Windows/Ubuntu, original Stress and explicit core-only/adapter package consumers.

[Independent goldens](cpython-goldens.json) now contain 19 ordinary Python sources,
expected and actual stdout, CPython version and the test-source hash. Each runs
with a fresh namespace and `compile(..., dont_inherit=True)`, avoiding CLI
`__main__`'s preseeded annotation dictionary. Both Lython execution APIs run the
same public cases under a shared allowance.

The follow-up fixes a source-level admission gap in the newly parameterized
InitVar renderer: UTF-16 decoding and interpolation previously happened before
output admission. The existing governed UTF-8 builder now funds working capacity
and replacement overlap and refunds scratch through failure. Large Unicode and
terminal-refusal controls are added. The initial commit had 17 goldens; two more
cover Unicode and substantial rendering. No timing claim is made.

The same bounded batch removes redundant async operand forwarding, constant-false
jump plumbing and an unreachable synchronous ForNext case. Actual operand helpers
and their borrows/awaits/cleanup are unchanged. [Follow-up checks](followup-checks.json)
record 486 public, 210 internal, 21 isolated managed-stack and 297 representative/
shared-budget/provider checks (selections overlap), plus a successful Release
solution build. Final qualification is recorded separately from the initial gate.

The requested shared-memory/cancellation/regex milestone remains delivered.
Core still has no Utf8Regex dependency; the adapter's inability to interrupt the
immutable backend remains explicit and separately documented. This correction
does not change or requalify that backend capability.
