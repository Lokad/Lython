# D21 precedence consolidation reassessment, before code

2026-11-05. Baseline committed ab487dfa, frozen04f3e00d (D19); delivery CI
41938239957 is independent and must stay tied to that source. Publication held.

Current B237: text774/report728 compiler-thread samples, parser217/245 inclusive
(28/34%), expression traversal52/42 leaf; LoweredScript owners20/22 (~3%).
Incomplete/overlapping chains are not predicted removable CPU. The source still
descends six arithmetic/bitwise grammar layers for each ordinary primary.

Earlier B46 removed five forwarding methods and30 net parser lines. Compile CPU
CSV−6.87/−7.61%,text−6.08/−6.76%,config−13.54/−13.05%, allocation flat; all713
focused checks per actual11/10 passed. It was rejected for missing a rigid10%
threshold on two scripts, while fresh config had19.05% unchanged-control drift.
Original conditions/raw/adverse control remain untouched. Current evidence and
the superseding measurement policy justify one current-source confirmation;
do not automatically promote it or claim earlier fresh-config gains.

Reapply the complete bounded precedence-climbing design, inspect it against the
current parser, remove the five forwarding methods/ParseNextLayer and replace
layer+token dispatch with one token-to-layer/operator decision. Preserve separate
boolean/not/comparison/unary/power entry paths, exact AST associativity/spans,
original diagnostics and per-layer expression-chain bounds. Descend only for
tighter operators. No AST backend, public option, specialized syntax or new cache.

Affected complete scripts: compile-to-result CSV/text/report/config; no warm
guest execution gain is expected. Cancellation checkpoints, managed recursion
admission, compilation allowance and exact Python protocol/evaluation order stay.
Existing23 arithmetic/diagnostic/protocol cases, compiler in-flight cancellation,
syntax/lexical/generator/type/fstring, compile refusal/ownership/reuse and current
executable frontend tests must pass; add only a real missing semantic regression.

One<=240s current-source pair with two unchanged baseline roles: CSV/text/report
compile-only, configfresh and warmtext control using the existing complete-job
driver. Compile-only validity/disposal excludes guest IO; fresh/warm full goldens,
mediated IO and prior-result cleanup included, staging/reset/startup excluded.
Normal settings,6s warmup,2s settle,9rotated>=200ms batches; verify frozen images,
full output/file goldens, admission balances/cleanup/quiet/owned joins. Record
elapsed/allocation separately; do not call elapsed CPU. One bounded refinement
requires an identified cause. Decide retain/debt/withdraw from simplification plus
usable observations, without rigid percent veto; preserve all adverse/control data.

Stop this milestone with that decision and a coherent delivery boundary; it does
not close whole-script performance work. Final qualification must cover the source
actually retained, not silently inherit D19's currently running checks.
