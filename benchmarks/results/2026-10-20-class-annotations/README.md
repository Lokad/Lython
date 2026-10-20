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

[Independent goldens](cpython-goldens.json) contain 17 ordinary Python sources,
expected and actual stdout, CPython version and the test-source hash. Each runs
with a fresh namespace and `compile(..., dont_inherit=True)`, avoiding CLI
`__main__`'s preseeded annotation dictionary. Both Lython execution APIs run the
same public cases under a shared allowance.

The requested shared-memory/cancellation/regex milestone remains delivered.
Core still has no Utf8Regex dependency; the adapter's inability to interrupt the
immutable backend remains explicit and separately documented. This correction
does not change or requalify that backend capability.
