**B57 — demand-driven annotation dataflow at lexical scope boundaries.**
Hypothesis: the existing annotation pass replays abstract bindings, call summaries,
mutation scans and branch joins after its last possible diagnostic. B45 has43/613
saved compiler chains containing AnnotationDiagnostics.AnalyzeStatement; groups
are inclusive/incomplete, not removable fractions. Maintained CSV/text helpers
have annotated functions first, followed by unannotated file-processing flow.
Their trailing dataflow cannot affect earlier annotation checks. This removes
unneeded analysis work in a material compilation phase; no claim about warm gaps.

Before code, replacement/deletion: retain the current annotation type rules and
its separate diagnostics order. At module/function/class scope boundaries, find
the last statement whose subtree can emit an assignment/default/return mismatch;
run existing binding analysis through that prefix only. Keep the COMPLETE binding
flow inside branches/loops/try/with/match before a later required check: those facts
can escape to that check. Do not merge this analyzer into the runtime/static flow
engine, introduce a memo/cache/configuration or remove independent diagnostics.
A small conservative syntax-only frontier scan replaces unused trailing abstract
interpretation. Over-inclusion is safe; under-inclusion is a bug. Preserve function
and class scope resets, nested declarations and uninitialized annotations. Check
the current compile token in the frontier scan; use existing compilation admission.
No public API/ownership/semantic contract change. Tiny-budget thresholds may differ
because unused diagnostic working storage is no longer created; no failed admission
is retried/suppressed. No regex producer capability required.

Semantic acceptance: all existing annotation/static-diagnostic, scope/closure,
mutation/alias, frontend cancellation/refusal and syntax goldens. Add independent
minimal cases for later annotations depending on non-annotated branch/loop/try/
with/match effects, nested scopes/defaults/returns, annotation-free scripts and
provider work that still must be validated by real consumers. Both actual target
runtimes and public APIs where execution matters. Preserve diagnostics before
observable host effects, first refusal, cancellation and cleanup/budget reuse.

ONE <=45s owned same-process comparison after a fresh nonincremental build and
source/copy audit: current original/candidate/identical-original images share
CoreLib and actual key hashes, ordinary GC/tiering. Complete fresh compilation
of maintained CSV/text helper scripts, plus complete warm CSV as unaffected
control.1024 interleaved compile warmups/image/case,128 warm-run warmups;2s settle
per case; seven rotating4096-compilation or32-run batches. Compilation disposes
every script owner and includes that cleanup; warm runs include real mediated I/O,
full output validation/result cleanup. No phase subtraction or CPython claim.
All outputs/diagnostics/funding/cleanup/quiet/exit gates mandatory; every batch
>100ms, every relative IQR<=10%, original medians within3% per case. Retain only
>=5% complete compilation improvement on BOTH scripts against BOTH originals,
<=3% warm CSV regression against both. Keep all outliers. Failed/inconclusive means
restore fresh/nonincremental, retain useful independent regressions/evidence and
stop; no tuning, threshold change or second collection. If retained, established
Windows/Ubuntu ordinary/original stress/both runtime/package qualification at
one delivery boundary, without publication or unchanged historical CI queries.
