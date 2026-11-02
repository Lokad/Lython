# B83 lexical preparation consolidation — before code

Source gate B82 identifies a real duplicate immutable computation, without
conflating annotation and abstract-flow analyses. ModuleUsesSuper performs its own
binding collection and syntax walk before Prepare walks all lexical scopes;
ExecutableScript later recollects the same root binding facts through lowered
statements. B80 observes13.11MB List<StatementSyntax> enumerators across callers, with
2.24MB in stored ModuleUsesSuper origins, plus shared ReadExpressions/complex-
expression iterator families. Do not attribute entire families to this walk. These
are allocation observations, NOT CPU shares or predicted speedups.

Hypothesis: delete ModuleUsesSuper's independent walker, derive its decision in
existing Prepare traversal, and give module compilation the same completed
binding facts using the existing PreparedScopeFacts pattern. Keep the module's
empty lexical enclosing scope distinct from its recorded module binding facts.
Defer marking eligible super calls until module-wide reads/bindings/global
statements have been seen. No new cache table, visitor framework, analysis skip,
new engine/value type, ownership system, dependency or unsafe code.

Semantics: preserve module and nested global shadowing, deferred lambda/generator
bodies versus eager defaults/first iterables, eager comprehensions' bound super,
class suites versus methods, class-cell closure/first-parameter preload/error
order, annotation postponement, module annotations/locals, imports, aliases,
evaluation order, generators, real async suspension and Python diagnostics.
Module facts reused only after immutable AST/name preparation finishes; coarse
compilation funding continues to cover surviving syntax/code and scratch overlap.
Current cancellation stage checks/refusal/compiled lifetime stay; no finer
post-parse cancellation guarantee. No retention after compiled owner disposal.

Existing focused Release class/super/scope/comprehension/module annotation/
compiler cancellation/admission/import tests, plus independent CPython edge
controls, both actual.NET11/10. Remove whole candidate if semantics/refusal or
ordinary source mechanism removal fails. No production test checkpoint counters.

ONE<=60s VM before/candidate/identical-original comparison; reuse complete-job
ALC driver/ordinary0.4 auxiliary bits/CoreLib/hash witnesses, no forcedGC/runtime
overrides. Unchanged complete CSV-helper and text-helper compile-only workloads;
fresh complete config-edit.small and warm complete text-helper controls. Compile
includes compiled-owner disposal. Fresh includes compilation/full execution/
mediated I/O/output+file goldens and result/compiled disposal; warm excludes
compilation. Startup excluded; no external ratio.1024 interleaved warmups/image
for compile cases,128 for execution controls,2s settle/case;7 rotating batches;
4096 compile jobs,512 fresh config jobs,32 warm text jobs. Larger batches/warmup
are frozen now for this different lexical redesign; B81 is never retimed.

Retain >=7.5% elapsed compile improvement in BOTH compile cases versus BOTH
originals; execution control regression/original drift<=3%, inclusive-linear
IQR<=10%, batches>50ms. Every golden/balance/final0/fixture removal/quiet gate and
image identity check passes. One capture, no retiming/rescue/threshold changes.
Otherwise withdraw whole candidate, architecture-specific tests and source
rewrites, fresh nonincremental rebuild; preserve independent semantics/evidence.
Full platform/originalStress/installed-package qualification only if retained.
No producer dependency or new publication/tag.
