# Shared JSON reader prototype — B176

2026-11-03. **Withdraw all prototype runtime code.** No contract change or newly
published runtime. Preserve existing native scalar/UTF-8/key-snapshot improvements.
This is a measured prototype decision, not a permanent rejection of explicit JSON
continuations or a fixed percentage veto.

## Design and correctness

One async parse invocation ran a native loop between real callbacks, with a current
value-type container and lazy parent array for nesting. Object/pairs/array separator
logic was shared. Replaced recursive ReadJsonObjectAsync/ReadJsonArrayAsync and
recursive value-dispatch plumbing. Retained scalar decoders, error helpers, immutable
record-key snapshots, final dict/list storage and the supported 64-level data errors.
No second default parser, staged member/pair representation, pool, ownership graph,
policy option, retry, trace roots or unsafe code.

Existing temporary reservations preflighted parent capacity and replacement overlap;
popped references cleared, working capacity refunded after awaited callback users.
Shallow documents needed no parent array or cursor object. Focused Release **747
public /172 internal per actual .NET 11 and .NET 10 PASS**, zero warnings. Existing
controls cover duplicate/hook order, original Unicode raw-prefix errors, reentrant
options/key snapshots, suspended callbacks, retained aliases, cancellation, terminal
refusal, shared budgets and cleanup. No architecture-only test suite added.

## Measurements and decision

Initial complete CSV/text/JSON/report/config, BOTH APIs, 256 warmups and nine rotated
64-job samples per isolated image, two-second settle. Three images share CoreLib,
process GC and hashes; unchanged old control. Initial service **147.607 s**, exit0.
All identities, goldens, quiet gates, ownership baselines, fixture removal and joins
PASS. Input/reset, mediated RAM-file I/O, full output/file checks and result disposal
included; compilation/startup/final cached teardown excluded and joined afterward.
No frontend change or external CPython retiming.

| Initial case | Sync elapsed / unchanged control | Async elapsed / unchanged control |
| --- | --- | --- |
| CSV |+2.64% /−1.65%|+2.19% /+1.69%|
| Text |−5.65% /−1.41%|−3.62% /−1.69%|
| JSON-lines |−6.25% /−4.64%|+1.10% /+0.42%|
| Report |−0.28% /+0.69%|+1.24% /−2.00%|
| Config |+0.04% /+0.59%|−0.85% /−0.65%|

Untouched CSV/text routes moved; do not attribute those deltas to JSON parsing.
Allocation essentially flat (config +0.079%); execution/root accounted peaks flat.
The moving JSON control and conflicting report points justified ONE bounded same-bit
confirmation, not a failed-design conclusion from the initial uncertainty.

Confirmation: **SOURCE ROLES SWAPPED**, tool before/control NEW B176, tool candidate
OLD B174; JSON-lines/report only, both APIs, 512 warmups and nine 128-job samples.
Owned **137.861 s**, exit0; every golden/balance/gate/identity/cleanup/join PASS.
Normalized new versus old:

| Case | Elapsed new roles % | CPU new roles % | IQR new/old/new-control % |
| --- | --- | --- | --- |
| Sync JSON-lines |−1.01 /−2.29|−1.06 /−2.34|0.49/0.69/0.21|
| Async JSON-lines |+2.37 /+3.14|+2.25 /+3.03|1.00/0.20/0.96|
| Sync report |+2.80 /−0.08|+2.44 /+0.12|0.61/1.17/0.86|
| Async report |−1.25 /−1.20|−0.92 /−1.02|4.96/1.84/1.24|

Sync JSON has a modest improvement; async JSON has a reproducible roughly 2–3%
cost. Report remains inconsistent, and allocation does not improve. Explicit parent
storage/copies and common state branches add machinery without a demonstrated useful
whole-script outcome. Withdraw now; a fresh implementation needs a different
mechanism/cost hypothesis rather than a third unchanged retime. No claim that the
reader can never be worthwhile. CLR/JIT/layout effects on untouched cases are
unresolved, not an attributed CPU cause.

B174's confirmed CSV cost and B171's report cost remain explicitly accepted debt
from their original source pairs. This withdrawn reader has not qualified them away;
do not sum independent percentage deltas. Whole-script performance stays OPEN.

## Identities and restoration

Baseline retained runtime source **7c183083**, test-only correction **7e96843b**,
frozen core **bf2ddeb1d6adb8b4dccda00e0980225502b3a5a92e8aec019ab753ac3a6e69ed**.
Prototype frozen core **41567127e71889edd448bf7be073d27f20cf5fd23328e7487a97fb13e007c71b**.
Quiet Azure Standard_F4as_v7, Ubuntu24.04.4, .NET11RC3, ordinary GC/tiering. Sources
and prototype bits frozen separately; snapshot includes the new reader omitted by
the ordinary unstaged patch. Full publication/platform qualification was not run
for this withdrawn prototype. Original runtime restored and nonincrementally rebuilt
before focused restoration checks; no dormant reader or compatibility option remains.

Raw archives retrieved and SHA-256 verified locally; frozen private prototype and
owned staging are retired only after joining users and validating those local copies.
[All initial tables](comparison-tables.md), [initial assessment](assessment.json),
[initial raw samples](observations.tgz), [confirmation](confirmation/assessment.json),
[confirmation raw samples](confirmation/observations.tgz), [identity](identity.json),
[source snapshot](source-snapshot.tgz), [source motivation](../2026-11-03-current-execution-cpu/README.md).

Restoration checks: nonincremental rebuild, **747 public/172 internal per actual
framework PASS**. No prototype runtime diff remains. Both owned RAM roots and the
private VM prototype image removed after local archive/hash verification and an
independently free lease; the immutable retained B174 comparison image remains.
