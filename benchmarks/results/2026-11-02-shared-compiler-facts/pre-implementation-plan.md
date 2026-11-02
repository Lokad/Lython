B97 ACTIVE - shared abstract-fact traversal (before implementation)

Demonstrated: InvalidateDictionaryFacts / CloneForDeferredBody recursively revisit
shared immutable tuple facts as a tree. A linear 24-assignment tuple DAG takes
3.99s in a fresh local probe vs0.559s for8 assignments (startup/compile/run included,
not a phase attribution or CPython speed ratio). Both full stdout goldens pass
isolated CPython3.13.2; source/images/observations are saved in
.git/agent-notes/review-20261031/abstract-facts-b97/before.json.

Hypothesis: shared fact payloads are revisited exponentially, while actual source
and runtime storage grow linearly. Replace the two recursive mutability checks
and captured-state visitor with ONE iterative, reference-identity-deduplicated
compiler-fact query. Remove recursive LINQ closures and CLR recursion. No retained
memo cache, runtime object ledger, collector, policy cap, or value-system redesign.
Queries remain temporary compiler work covered by existing compile admission;
ordinary scalar facts return without allocating traversal storage.

Affected: fresh compilation of generated manifests/config/report scripts with
reused nested tuple/closure facts; ordinary CSV/text/JSON compilation controls.
No expected warm-execution change. Preserve exact mutable-container inference,
shared/recursive captured states, classes/defaults, Python diagnostics and valid
program behavior. Forward the current compiler token through actual traversal.
Maintain existing reservation/refusal and compiled-owner disposal contracts.

Acceptance: public DAG scripts, nested mutable alias/closure cases and existing
inference/diagnostic regressions; direct cancellation of an in-flight fact query
where deterministic without test checkpoint accounting. ONE bounded before/after
comparison of exact same generated scripts (8/24/large nodes) and an unchanged
representative CSV compilation control, frozen original/candidate/original images.
Measure compilation separately from startup/run; include owner cleanup and shared
budget baseline. Keep one simple complete-script check; do not replay report timing.
Stop after that comparison and focused Release checks. Treat pathological-cliff
removal, ordinary-workload timing and allocation as separate claims. Full platform/
package qualification only if the coherent change is retained.

