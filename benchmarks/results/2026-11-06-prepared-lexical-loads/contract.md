# F02 complete prepared lexical selection, before code

Source62901222 (runtime dd76df36), frozen baselinecoreb16fc0d7. F01 current
CSV sample has57 ResolveName leaves/1540 total; this includes necessary work,
not a predicted speedup. CSV repeatedly resolves `int` through LoadName.

Hypothesis: reuse complete enclosing binding facts to emit existing LoadGlobal
for ordinary function free names after locals/captured names/nonlocals. Delete
their general runtime lexical-chain walk, without a per-load guard/cache, new
opcode, dispatch framework or retained metadata. One compiler-only completeness
fact propagates from module through ordinary functions/lambdas/comprehensions.
Separately compiled class/generator bodies and type-parameter scopes retain
dynamic lookup; nested children cannot manufacture complete parent facts.

Affected complete CSV helpers and ordinary helper/global calls; native text,
JSON/report helpers are controls. Preserve live globals/builtin mutations,
explicit global/nonlocal, transitive closures, class barriers/cells, type
parameters, annotations, comprehensions/walrus, defaults and diagnostics. Reuse
focused Release scope/function/generator families plus terminal refusal,
cancellation, actual suspension, ownership and cleanup checks. No new runtime
allocation, token, owner or API. New goldens cover binding ambiguities only.

Bounded comparison: unchanged workers/protocol2, normalGC/tiering, quiet lease,
CSVwarm/compile-to-result plus independent helper/control. Two current processes,
original baseline/CPython, full goldens/closed/final zero/joined users/fixtures.
Use existing 5s warmup/role,7rotated150ms rounds; <=150s owned bound for4jobs.
Preserve all signs/controls. Decide retention for coherent phase elimination and
semantics separately from established elapsed/CPU/allocation gain. No automatic
extra timing, fixed-percentage veto or claim of substantial objective completion.
