# D30: plan whole multi-part formatted-string construction before code

2026-11-05. D28 source8475a8dd/frozen81deb3be fully qualified41958212610 terminal
GREEN (cached,all8logicalgates). D29 current1108text/1613report CPU samples/all
identity/golden/quiet/join gates show required allowance transitions and shared
primitive formatting, not a demonstrated duplicate budget lock. Standalone scopes
already share one gate; do NOT reopen B198 credits/quantization/scalar accounting.
Primitive formatting already appends native ranges, so no invented CLR/string copy.

Selected coherent hypothesis: compile modest INITIAL builder capacity from the
existing static literals/parsed field widths for multi-part fstrings. Both normal
ExecutableStartFormattedString and prepared native formatted return use it.
Eliminate common intermediate growth arrays/copies/reserve-refund transitions,
without moving/caching any actual operand or field read. No per-row value packet,
new dispatch framework/owner/cache/governor credit/JIT/public configuration.

Construction policy: initial hint remains modest (use the existing small-native-
formatter256B scale, rounded within it), independent of arbitrary large/invalid
widths. This is initial storage planning: ordinary governed growth still handles
larger output and no new output/width/string ceiling is introduced. Admit actual
allocated capacity through the same borrowed root, including replacement overlap;
refused hint admission is terminal, never retry smaller. Conservative earlier
denial/capacity retention is an explicit documented contract tradeoff. Without a
budget there is no hidden allowance, and giant invalid widths cannot provoke a
speculative giant initial allocation.

Implementation/deletion contract: add the hint to the EXISTING start-operation
metadata; consume each prepared format specifier once while compiling, no duplicate
parsing or retained parallel format plan. A temporary compiler backpatch can fill
the start descriptor after the existing parts walk; no placeholder or unused old
start constant may remain in published executable code. Native prepared source
coordinates and normal physical stack layout stay unchanged. Reuse the ONE
governed builder. Before using nonzero construction capacity, make its existing
constructor refund an admitted reservation on failure and observe the active token
after admission/before allocation; cache no token in compiled metadata. This is
construction rollback/cancellation, not another memory subsystem audit.

Preserve Python conversions, invalid specifier/error ordering among fields, Unicode
width/precision/fill, nested/dynamic specs, formatted generators, callback order,
genuine async and awaited cleanup, aliases/results and first structured refusal.
Earlier storage refusal may precede a callback; record this rather than weakening
terminal or funding assertions. Native casing/formatting and managed stack policy
remain. No source rewrite or fallback after failed admission.

Focused Release: existing formatting/native-return/constructor/ownership/admission/
cancellation families; add only missing initial-construction cancellation/refund
and dynamic/invalid-large-width controls. Small deterministic fixtures, no timeout
relaxation. Then both actual targets. One <=240s existing whole-source pair: report
warm/async/fresh, integer numeric report warm, text warm control and report compile.
Two baseline roles, normal runtime/exact binaries/full independent goldens/quiet/
budgets/owned joins. Separate allocation, elapsed and compiler/capacity debt.
No predicted percentage or new external ratio. Stop retain/debt/withdraw with
explicit remaining-debt decision; bounded refinement only for actual evidence.
Full platform/stress/package qualification only after a retained coherent change.
Whole-script objective stays open; regex final adapter gaps remain independent.
