## B99 - native single-operation function returns (ACTIVE design)

Hypothesis/source gate: complete helper expansion in saved B58/B65 shows material
call-state cost (CSV16-18% elapsed source-shape difference, not recoverable CPU).
Current source still creates a resumable interpreter plus operand/cache/region
state for ordinary `return int(value)` / `return record['amount']` helpers.
Existing simple returns already skip this for values/exact-integer addition.

Generalize that EXISTING guarded return operation, replacing IntegerAdd's shape
with one native-operation shape (two evaluated load operands, one operation,
return). Retain its existing addition behavior; also cover native dictionary
string-key reads and native unary builtin calls using the existing operations.
Share pure global lookup with the normal engine. This removes actual interpreter/
operand construction on eligible calls, without an arena, pool, delegate backend,
new call framework, general inliner, new owner or public option. Do not split
lexical contexts from their existing ownership; preserve the real function frame.

Affected whole scripts: maintained CSV-helper and JSON-helper jobs. Unchanged
text-helper is the control. Body/target guards run BEFORE operations; custom keys,
protocols, callbacks, captured/mirrored/observable state or possible suspension use
the existing executable interpreter. Name/builtin rebinding, binding/defaults,
argument evaluation, alias identity, exception context/spans, recursion/managed
stack admission, current cancellation and terminal first refusal must remain.
No retry or fallback after an operation starts or admission fails.

Before timing: inspect actual helper IR; retain old simple-return semantic coverage
and add independent pure Python native/protocol/error/rebinding/alias controls.
Existing gated async function/native-host and finite-refusal/owner tests remain.
One controlled dedicated-VM comparison only: original/candidate/identical-original,
same current-source/input/host/auxiliary bits/CoreLib, complete warm CSV and JSON
helper jobs plus text control, full stdout/file goldens, cleanup and budget reuse.
Use existing short driver,128 interleaved warmups, seven rotating32-job batches,
ordinary GC/tiering and quiet before/after gates, external45s owned group limit.

Retention prospectively: semantics/ownership must all pass; material construction
benefit requires >=10% whole-job allocation reduction on BOTH affected scripts
against BOTH originals, unchanged unaffected allocation within1%, and no stable
material elapsed regression (>5% against both controls with IQR<=10%). Timing
qualification is separate: noisy clocks establish no speedup. Keep ALL observations
and failed conditions; no retiming, tuning, post-result gate correction or longer
lane. If preserving contracts requires general frame/owner infrastructure or the
bounded comparison does not support retention, remove the entire prototype,
keep independent regressions, force a fresh rebuild and stop this trial.

