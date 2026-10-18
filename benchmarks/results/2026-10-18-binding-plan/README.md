# Function binding-plan construction, 2026-10-18 UTC

The constructor counts parameter kinds and fills final positional, keyword-only
and layout-name arrays directly. Three temporary lists, their backing storage and
array copies disappear. The existing ordinal slot map, per-function defaults,
conservative admission estimates, rollback and retained aliases are unchanged.
There is no layout cache, pool, default sharing or new ownership mechanism.

Baseline: `de6efc09ebd6ad0404939ab12bb3ece7d011359a`; candidate: that revision plus the
[frozen patch](candidate.patch), including permanent public regressions.
The [scope and gate](scope.json) were frozen before production edits.

| Warm native construction | Baseline | Candidate | Status |
| --- | ---: | ---: | --- |
| Empty plan | 320 B | 224 B | 96 B less per plan |
| Single positional parameter | 648 B | 488 B | 160 B less per plan |
| Mixed seven-parameter plan | 1,904 B | 1,600 B | 304 B less per plan |
| Fresh execution-context control | 16,080 B | 16,080 B | All sample arrays identical |

One before/after pair uses two warmups and eight samples of 64 constructors per
route. All plan samples improve. Exact positional/keyword/variadic layout metadata
and all 27 admission/rollback records match, including refusal at narrow budgets,
first admission origin, live supplied default aliases and cleanup to zero.
[Diagnostic](diagnostic.cs), [raw baseline](baseline-native.trx),
[raw candidate](candidate-native.trx), process receipts and
[comparison](comparison.json) preserve the evidence. The temporary diagnostic
is removed. The final runtime source is the measured source; no repeated capture.

Local Windows x64 build 26200, Intel Core i7-14700KF (28 logical processors),
.NET SDK 10.0.300-preview.0.26177.108/runtime 10.0.13. [Environment](environment.json)
reuses the earlier machine configuration with its original capture timestamp.
This allocation-only native comparison excludes guest execution, compilation,
public projection, scheduling and startup. No elapsed, compile-to-result,
complete-job or CPython speedup is established.

All 306 distinct focused Release cases pass, including 58 complete CSV/text/
JSON/file jobs. Checks cover mutable defaults, positional-only/keyword/variadic
binding, methods, lambdas, generators, actual async callbacks, frame cleanup,
managed stack, terminal admission refusal, caches and shared-budget owners.
All 18 new public rows also pass before the runtime edit. Eight independent
CPython 3.13.2 programs provide semantic goldens in [goldens.json](goldens.json).
[Validation](validation.json) records filters, counts, receipts and TRX hashes.

Two initial baseline failures found pre-existing binding-error spelling differences
from CPython, preserved in [baseline receipts](baseline-wording-failures.json).
The error program compares TypeError classes; separate rows pin exact legacy
Lython strings. This optimization preserves that behavior and does not claim
CPython-identical error messages. Original Stress scales, assertions and timeouts
remain. No language or memory-ownership contract changes; no regex implementation.
Release/package/platform qualification is recorded in PLAN.
