# Remove unused module class-cell analysis — R65

2026-11-07. Baseline `dc266746` / core `74c88248`, the R62 source fully
qualified by CI 42412531882. Current R65 is focused-qualified separately;
full delivery will be batched at a coherent retained checkpoint. Normal 0.4
optional adapter and comparison driver are unchanged. Publication is postponed.

Lexical preparation computed outward module class-cell demand after its complete
binding/scope walk. The executable builder uses hidden captures only for
parameterized function scopes; runtime module contexts use empty scope facts.
Static analysis has no consumer of the module demand. Delete that whole trailing
walk and disable the same unused calculation in standalone `ForModule`.
No replacement helper, cache, option, execution route or public API is introduced.
Function/class/lambda/generator capture queries and `ClassCellSyntaxFacts.Scope`
have genuine callers and remain. All module binding order, super preload decisions
and Python scope rules remain; no contracts change.

The wider proposed combination would need distinct demands for function/lambda/
comprehension shadowing, nested class suites versus methods and postponed
annotations. Caching a partially prepared fact before those decisions would add
another invariant. This deletion removes an actually unused analysis without
introducing that state. Larger consolidation and whole-script runtime work remain
open; this is not completion of the performance objective or a permanent ban.

## Validation

**591 internal / 1,793 public focused Release checks per actual .NET 11 and 10
pass**, including scopes, closures, classes/super, generators, annotations,
cancellation, refusal and cleanup. Both matching probes are built and contain
the corresponding core bytes. No architecture-only test, timeout or containment
assertion was relaxed. Coarse compilation preflights, terminal refusal and existing
stage cancellation are unchanged; later passes can finish before their next check.

The existing isolated diagnostic tool compares 358 sources with and without a
provider: **716 identical validity / ordered diagnostic records**, including spans.
Every compiled script is disposed and its borrowed allowance returns to zero.
Eight public probe sources across both cores and both APIs produce **32 CPython
matches**: module locals/global rebinding, absent `__class__`, `super`, nested class
suite captures, defining-class methods, postponed annotations, lambda/generator
escapes. The initial six-source successful run is also preserved. Existing complete
script diagnostics pass all goldens, image isolation, funding and fixture cleanup.

## Short affected-workload diagnostic

Same existing five whole scripts, isolated baseline/current/identical-baseline
load contexts, shared CoreLib/process GC; 32 warmups per image, five rotated
four-job rounds. Compile, fresh and warm are separate. Construction/disposal are
included for compile; fresh adds compilation to public execution, mediated I/O,
full outputs and result cleanup; warm excludes compilation. Driver startup and
image setup are excluded. The raw tool's legacy purpose text identifies its first
use; the frozen source/cores here identify this comparison.

Positive elapsed percentages mean current slower. Desktop clocks are diagnostic.

|Complete script / compile|Allocation change|Elapsed vs both old roles|Old elapsed spread|
|---|---:|---:|---:|
|file-search.large|-4.64%|-3.19%/-8.22%|+5.48%|
|text-summary-helpers.large|-4.19%|-10.89%/-7.19%|-3.98%|
|csv-summary-functions.large|-4.34%|-5.18%/-5.11%|-0.07%|
|json-summary-helpers.large|-4.28%|-9.90%/-7.19%|-2.92%|
|report-render.large|-3.53%|+2.76%/+1.89%|+0.85%|

Compilation allocation falls **3.53–4.64%**; fresh allocation falls roughly
0.13–1.46%, warm allocation is essentially unchanged. Elapsed includes an adverse
report result and drifting controls (warm search +15.61%, text +13.49% old-role
spread). Preserve all raw observations; **no CPU or warm-execution gain is
established**. Retain for deletion of a complete unused analysis and lower compile
allocation. No new VM timing campaign or unchanged full qualification was run.

[Exact source/core inventory](identity.json), [pre-code selected plan](selected-plan.md),
[focused checks](focused.json), [all phase observations](local-assessment.json),
[source/cores/tests/probes/raw diagnostic evidence](local-observations.tgz).
