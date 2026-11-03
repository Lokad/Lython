# Shared analysis eligibility — B187

2026-11-03. **WITHDRAW both frontend prototypes.** Restore978b51b5 production
source; retain public provider-capability and diagnostic-order regressions. Current
qualified runtime is978b51b5; its full delivery41730446899 is separately recorded.
No performance objective or current CSV/fresh-JSON debt is closed by this experiment.

## Source and correctness

The existing lexical preparation walk recorded declaration requirements, allowing
irrelevant frontend passes to be skipped. A fixed direct pipeline replaced the
three-delegate pass array. No new walk/cache/backend/configuration or runtime limit.
Unprepared internal syntax conservatively ran all passes. Current imports already
use executable code and ordinary lowering already prunes its unused lowered body;
neither was proposed as another cleanup.

A blind annotation/abstract-flow merge was rejected at source inspection: annotation
checks deliberately visit unreachable branches and directive-bearing functions,
while general abstract analysis prunes paths and suppresses catchable contract errors.
The initial eligibility candidate also skipped annotation analysis without annotated
syntax. General checks passed (1114 main /286 regex-import selections), but a targeted
same-source contract comparison found missing-provider LA2010 compilation failures
were lost for dead assignments and deferred/directive-bearing functions, including
an invoked function. Annotation binding updates have real regex analysis effects.
**This candidate fails the contract even where its timings look favorable.**

Refinement restores unconditional annotation/binding analysis and removes its feature
bit; only irrelevant directive and named-function scans are skipped. No regex textual
heuristics or eligibility exceptions. All eight targeted compilation cases match the
baseline. Focused **148 internal/1129 public per actual .NET11/.NET10 PASS**; new
controls cover capability failure across scopes, diagnostic-family ordering in dead
nested declarations and budget cleanup. Compiler cancellation/refusal/retained-owner
and existing class/super/global facts remain covered. No full prototype CI claimed.

## Initial candidate (contract failure, not qualified performance)

Baseline qualified B186, frozen4a64c429; initial source978b51b5+patch, frozen5c2fbbef.
Owned247.022s, all worker goldens/quiet gates/balances/joins PASS. Golden workloads
did not cover the discovered capability-analysis contract. Three-second warmup,
nine rotated ~200ms batches, one-second settle. Five complete scripts compile/fresh,
three warm controls. Compilation and fresh allocation captured separately.
Positive means slower; candidate versus each old worker, old2/old1 control:

| Lane | Complete script | New/old1, new/old2 | Old control |
| --- | --- | --- | --- |
|compile|csv-summary-helpers|-2.28% / -1.67%|-0.62%|
|compile|text-summary-helpers|+0.07% / -1.71%|+1.81%|
|compile|json-summary-helpers|-0.41% / -0.00%|-0.41%|
|compile|report-render|-7.81% / -7.15%|-0.71%|
|compile|config-edit|-2.23% / -3.61%|+1.44%|
|compile-run|csv-summary-helpers|+1.60% / +1.29%|+0.31%|
|compile-run|text-summary-helpers|+4.52% / +2.40%|+2.08%|
|compile-run|json-summary-helpers|-2.01% / +1.95%|-3.88%|
|compile-run|report-render|+0.30% / -0.72%|+1.03%|
|compile-run|config-edit|-21.96% / -3.98%|-18.73%|
|warm|csv-summary-helpers|-0.05% / -2.25%|+2.26%|
|warm|report-render|+2.07% / -0.43%|+2.51%|
|warm|config-edit|+0.01% / +1.89%|-1.84%|

Preserve apparent compile-report improvement alongside adverse fresh-text and moving
config controls. These points do not qualify a semantically invalid design.

## Corrected candidate and decision

Same baseline4a64c429; corrected frozen61e25c60. Owned231.738s, all gates/goldens/
joined cleanup PASS. Same stated sampling; five compile/fresh cases, FOUR warm
controls (text added to inspect initial fresh-text adversity). Compilation allocation
diagnostic only; no duplicated fresh-allocation run. No CPython retime.

| Lane | Complete script | New/old1, new/old2 | Old control |
| --- | --- | --- | --- |
|compile|csv-summary-helpers|-0.94% / -0.38%|-0.56%|
|compile|text-summary-helpers|-1.23% / -0.63%|-0.60%|
|compile|json-summary-helpers|-0.03% / +0.13%|-0.15%|
|compile|report-render|-1.43% / -3.09%|+1.71%|
|compile|config-edit|-2.30% / -0.15%|-2.16%|
|compile-run|csv-summary-helpers|+0.35% / -2.74%|+3.18%|
|compile-run|text-summary-helpers|+4.54% / +3.36%|+1.14%|
|compile-run|json-summary-helpers|-0.16% / +2.41%|-2.51%|
|compile-run|report-render|+2.19% / +2.00%|+0.18%|
|compile-run|config-edit|-24.29% / -2.22%|-22.57%|
|warm|csv-summary-helpers|+1.53% / -0.23%|+1.76%|
|warm|text-summary-helpers|-0.67% / +1.20%|-1.84%|
|warm|report-render|+0.69% / -0.34%|+1.04%|
|warm|config-edit|-3.62% / -1.58%|-2.08%|

Compilation allocation−0.22..−0.58%, saving288–968B/program. Compile timing mostly
near controls; report−1.4..−3.1%. Fresh text is+3.4..+4.5% versus both old roles,
fresh report+2.0..+2.2%. Warm text/report straddle their controls. CSV/JSON vary;
config's22.6% unchanged-old spread makes its apparent large fresh gain inconclusive.
The cause of phase-specific elapsed differences is unproven; do not attribute it
merely to the new root field, skipped scans, or CLR layout.

The corrected design adds root metadata, feature eligibility and invariants while
saving modest compilation storage and producing adverse fresh-script outcomes.
Withdraw rather than accumulate that state or retain only favorable rows. This is
not a fixed percentage veto or permanent ban on shared analysis. A future design
must account explicitly for annotation binding effects and differing flow semantics,
and remove a larger repeated representation/work cost. No unchanged third retime.
Current native dictionary source/qualification and accepted CSV/fresh-JSON debt stay
intact. Publication postponed; whole-script performance remains OPEN.

Existing protocol2, quiet Azure F4as_v7 Ubuntu24.04.4/.NET11 RC3, normal GC/tiering.
Persistent catalogs/workers/evidence, tmpfs guest fixtures. Compile-only excludes
execution/I/O; fresh includes compilation/disposal plus actual mediated I/O, full
output/file checks and prior-result cleanup. Fixture reset/staging, final cached
cleanup/startup are outside clocks and joined afterward. No phase subtraction or
embedding RSS/latency claim. Both initial/refined archives verified locally.

[Initial observations](initial/observations.tgz), [initial summary](initial/summary.json),
[refined observations](refined/observations.tgz), [refined summary](refined/summary.json),
[original contract difference](contract-diagnostic/observations.json),
[corrected contract comparison](refined/contract-observations.json).
Initial archive SHA256:db1180c1ea8a50db1640c88012b2d6f5fd874591b5d1251288534eb7a83860ba.
Refined archive SHA256:67c7cab3f240b0caf7807eef4f58d167ecf094ef52710a12b8c05ff16f321e2d.
