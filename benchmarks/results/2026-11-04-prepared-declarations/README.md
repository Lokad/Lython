# Prepared executable declarations and shared defaults

2026-11-04, quiet AzureF4as_v7 Ubuntu24.04.4/.NET11RC3. Baseline fully qualified
ee8020f0/core38c03e5e; candidate base878894a3 plus recorded patch, frozen core
`df206f479ab6e016a9b0a54ce68e82fd9e4cdd7fcca51879e8d2ef1b2179b6cd`.
No public API/Python contract change. Full delivery41919086629 for exact f6e51d1c
passed all eight platform/framework/original Stress/installed-consumer gates,
terminal observed22:15:11UTC. [Cached result](delivery-ci-status.json). Publication held.

Retain this coherent simplification:
- Executable bindings hold their real immutable header and prepared code, not a
  LoweredFunctionDefinitionStatement retaining Function.Syntax.Body. Genuine
  lowered class/annotation/fallback consumers remain supported.
- Remove the compiled default-expression dictionary (parameters already own the
  expressions), the extra lowered-definition/default-map clones during prepared
  capture, and MaterializeExecutableDefaultValues.
- Replace separate delegate-driven sync/async default-map builders with one
  completed prefix and a continuation only on actual suspension. Function
  constructors accept read-only construction maps; zero defaults use the existing
  immutable empty dictionary. Python tuple/dictionary defaults remain separate,
  mutable, per-function values. No user-value cache/registry/owner/framework.
- Keep ordinary call binding, the hot code-object layout, allocation preflights,
  explicit owner lifetimes, first-refusal behavior, cancellation and managed stack
  protection. Remove working admission specific to the deleted map clone. No D15
  layout borrowing/count tuple/inheritance/estimate variant is retained.

Nonincremental Release30.09s,0warnings/errors.744internal/1677public focused checks
per actual11/10 pass. After adding one mixed completed/pending default regression,
14hosting-class cases per target pass (overlap with the main suite is explicit).
Independent isolated CPython3.13.2 stdout/event-file bytes match. Existing cases
cover real delayed default/decorator I/O, cancellation/awaited cleanup, terminal
refusal, mutable/retained defaults, factories, generators, type annotations/scopes,
class cells/super and sequential/shared allowance reuse. No assertion/timeout relaxed.
Internal test edits adapt private header/default-map types, not their assertions.

Eight complete jobs221s. Three isolated images/two unchanged old roles, identical
frozen auxiliaries,6s guest warmup/image,2s settle,9rotated200ms batches, ordinary
GC/tiering/no overrides. Every DLL/CoreLib/hash/full independent golden/quiet/
baseline/finalzero/owned-fixture-removal/join gate passes. Positive signs mean worse.

| Complete script | Phase | Candidate ms | Elapsed vs old / old control | Old spread | Process allocation vs old / old control |
| --- | --- | ---: | ---: | ---: | ---: |
| text-summary-helpers | compile | 0.059888 | +2.59% / +0.70% | 1.88% | -0.06% / -0.06% |
| text-summary-helpers | fresh | 3.144848 | -3.39% / -1.69% | 1.73% | -0.00% / -0.01% |
| text-summary-helpers | warm | 3.052780 | -4.37% / +1.29% | 5.59% | -0.00% / -0.00% |
| report-render | compile | 0.058465 | +1.16% / +1.30% | 0.14% | -0.07% / -0.04% |
| report-render | fresh | 4.887006 | -0.17% / +1.33% | 1.48% | +0.00% / -0.00% |
| report-render | async | 5.042504 | -2.10% / -0.69% | 1.42% | -0.01% / -0.01% |
| config-edit | fresh | 0.295796 | +0.86% / +1.83% | 0.95% | +0.01% / +0.02% |
| config-edit | warm | 0.206781 | +0.76% / +1.93% | 1.15% | +0.02% / +0.02% |

Fresh text−1.69–3.39% is favorable in this collection; warm text straddles signs
with5.59%old spread and remains inconclusive. Report compile+1.16–1.30% with0.14%
old spread is an observed small regression (~0.7µs); retain that debt explicitly
for the removed representations/construction paths. It is not hidden by a threshold
or a favorable row. Other report/config rows are small and/or control/dispersion-
limited. Whole-script process allocation effects are tiny; compile−0.04–0.07%.
Do not claim a general elapsed/CPU speedup, additive gain, RSS/latency improvement
or quantitative retained-heap reduction. The source establishes removed metadata,
not its physical embedding impact. No unchanged retiming selected for nicer controls.

The architectural cleanup and bounded debt decision complete this declaration
milestone only after full delivery gates. Substantial whole-script CPU work remains;
continue from actual attribution/source evidence rather than calling this the goal.

Compile includes public Compile/validity/Dispose. Fresh adds full Run, actual
mediated RAM I/O, complete stdout/file checks and result/compiled cleanup. Warm/
async include invocation/completed I/O/checks/result cleanup. Reset/staging/startup/
hash/final cached teardown excluded; all users joined. Allocation includes reset/
background noise; initial untimed warm setup admission is not fresh overlap/RSS/
embedding or pending-I/O latency. No CPython timing claim from this source pair.
Primary external CPython table still identifies its older5c827e68 revision.

[Metrics](assessment.json), [validation](validation.json), [original plan](pre-measurement-plan.md),
[raw observations](observations.tgz), SHA256
`148b4fa4e037f21f2f305f49f3c9bed99891019286c5b94d18110c544e381cff`.
