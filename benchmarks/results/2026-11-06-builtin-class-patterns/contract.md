# R05 builtin class patterns; before implementation

Productiona24a82dc includes retained R03, focused only; older794e552b platform
receipt excludes it. Both actual runtimes and isolated CPython independently
confirm list(x) incorrectly decomposes the list, list(a,b) incorrectly succeeds,
int(x) incorrectly errors, and user-class extraction runs subpatterns before all
attributes. Preserve original JSONL probes; R05 fixes the builtin boundary only.

Use existing supported-type identity/name and instance helpers on the evaluated
class value, not a source-spelling table. Python match-self builtin types accept
one positional pattern matched against the entire subject (bool, bytes, dict,
float, int, list, set, str, tuple supported here); other builtin classes allow no
positional pattern unless an existing explicit match-args branch handles them.
An alias must behave like the actual class; a non-type shadow must raiseTypeError.
Check subject type before excess-position errors, as Python does.

Delete LoweredClassPattern.ClassName, the artificial Sequence payload and its
prepared workspace, recursive class-spelling preparation and duplicate builtin
subject classification. Reuse one actual-class check. Ordinary sequence patterns
still use their existing extraction/storage. User-class/URL named-args behavior
remains; do not claim their attribute-order/value-protocol limitations fixed.
Do not add a dispatch framework, new public option or hidden owner/cap.

Validate whole-subject identity, mutable aliases, each supported builtin,
empty/overspecified patterns, failed-type short circuit, builtin alias/shadow,
keyword-only runtime classes and class-body lowered execution independently
against CPython. Replace existing wrong list(a,b) golden with list([a,b]); this
is a Python-compatibility correction, not removing supported valid syntax.
Both APIs + shared budget baseline/zero, existing pattern cancellation/refusal/
cleanup controls. Existing class-expression callbacks must keep active context
and first refusal; no new async support claim. No timing prediction or extra
campaign for this semantic correction. Batch full platform/stress/installed
qualification with R03 at coherent delivery, then continue whole-script roadmap.
