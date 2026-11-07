# Completed helper dispatch — R29

Retained 2026-11-07; full delivery qualification pending. Native closed helpers
with fully supplied ordinary positional arguments and no bound receiver now finish
in the existing native-call selection. Delete the displaced borrowed attempt from
BeginPendingCall. The same native evaluator and managed call/function/recursion
checks remain; arguments stay borrowed until completion, then the caller publishes
and retires them. Defaults, receivers, raw protocols, dynamic failures of the
read-only proof and genuinely suspending bodies retain the existing frame runner.
No new execution route, argument representation, policy, owner or public API.

Previously these already synchronous helpers manufactured pending state, left
the shared prefix, completed in BeginPendingCall, then resumed through
ResumePendingCall. Relocating that existing operation removes a complete synthetic
pause/completion phase. The [source gate](README.md) does not identify all21% of
prefix samples as removable and this result does not establish that they were.

## Controlled complete-script comparison

B378 uses exact candidate32dcf141 against fully qualified R269e1dbc38, both ordinary
.NET11RC3 on the quiet AzureF4as_v7/Ubuntu24.04.4, same b94fa98c worker/host/optional0.4
adapter, normal GC/tiering. Two baseline/two current processes,5s warmup per role,
seven rotated150ms rounds. Same complete scripts, mediated RAM I/O, full output/file
checks and prior-result cleanup. Compile/startup/staging/reset/final teardown are
excluded. No CPython ratio or genuinely pending-I/O latency measurement here.

Positive change means slower. Each range includes all four pairings of current
and baseline process medians; spreads are differences between identical images.

| Workload/API | Baseline ms | Current ms | Change | Current / baseline spread |
| --- | ---: | ---: | ---: | ---: |
| Text sync |2.137–2.166|2.002–2.046|−7.61 to−4.25%|2.20% /1.38%|
| Text async |2.493–2.494|2.278–2.303|−8.64 to−7.63%|1.07% /0.02%|
| Report sync |3.519–3.616|3.563–3.686|−1.48 to+4.75%|3.46% /2.76%|
| Report async |3.789–3.820|3.710–3.798|−2.88 to+0.26%|2.38% /0.84%|
| CSV sync control |3.874–3.972|3.973–3.992|+0.04 to+3.04%|0.48% /2.52%|
| JSON sync control |4.390–4.521|4.210–4.329|−6.87 to−1.38%|2.75% /2.90%|

All six jobs finish in168.46s. Loaded images/goldens/quiet/final funding/Closed/
exit0/joins/fixture cleanup and persistent under-lease archive verification pass.
Acknowledged03:10:31UTC; observation03:16:02 is after240s owner+30s stop+40s margin.
All original observations and coarse process-CPU counters are retained. The text
elapsed improvement supports retention; report and CSV retain adverse/uncertain
observations. Coarse CPU ticks and moving controls do not establish a broad causal
CPU gain. No extra confirmation is needed merely to make those controls favorable.

Separate existing eight-case complete-job diagnostics find allocation flat within
0.02% and unchanged admitted peaks on both APIs; goldens, balance, image isolation
and fixture cleanup pass. This relocation removes work, not heap storage. Desktop
elapsed from those shared-ALC diagnostics is not performance qualification.

## Correctness, ownership and delivery

Release build has0warnings/errors. Focused439 internal/1,958 public checks pass
per actual .NET11/10. Six new checks trigger actual output admission from a closed
helper through a deferred caller. Success, token cancellation and first refusal
keep the input funded, prevent following effects on failure, preserve the first
structured origin, restore interpreter/recursion depths and release the shared
allowance after cleanup. Existing function/diagnostic/source, alias, managed stack,
dynamic native fallback, actual async callback/cleanup and terminal refusal checks
remain. No timeout/assertion relaxed. Both installed Release consumers pass;
the final cross-platform/original Stress checkpoint remains pending.

Retain the coherent relocation and measured text improvement. Explicitly accept
remaining uncertain report/CSV debt for this milestone; do not label it parity.
R26's earlier possible~3% text/async debt remains uncertain and is not algebraically
repaid by percentages from different runs. Preserve its correctness fix; refresh
the representative CPython checkpoint with combined changes at delivery. Larger
whole-script gaps, startup/pending-I/O/embedding tails and subsequent redesigns
remain active. This milestone does not finish the performance objective.

[Raw comparison/source/gates](b378/observations.tgz),
[comparison summary](comparison-b378-summary.json),
[allocation](allocation-summary.json), [focused checks](focused-initial.json),
[checks/source/identity/binaries](implementation-evidence.tgz).
Publication/tagging and final missing producer qualifications remain separate.
