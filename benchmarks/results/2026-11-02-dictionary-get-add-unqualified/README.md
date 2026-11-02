# Guarded dictionary get/add: withdrawn

Measured 2026-11-02 on the dedicated Ubuntu benchmark VM, based on `650b4766`
(runtime source `4cae38f4`). **No prototype remains and no speedup is claimed.**

The candidate replaced the ordinary native `d.get(key, 0) + amount` RHS with one
guarded executable operation. It preserved the original assignment target reads,
protocol fallback, integer admission, cancellation and managed frame admission.
Its compiler estimate and coupled generator-budget fixture change are withdrawn
with the opcode. The frozen hypothesis, controls and acceptance are in
[pre-code-plan.md](pre-code-plan.md); the exact patch is inside the raw archive.

The sole owned comparison timed out after **45.059 seconds**, during the final
file-manifest control. Completed CSV/text/JSON helper prefixes had observed gains
of only 3.93–4.94%, 6.37–7.49% and 1.63–3.96%, respectively: below the frozen
10% requirement even before the incomplete control. These are partial internal
observations, not qualified improvements or CPython comparisons. No rerun,
relaxed gate or candidate fragment was retained.

The three completed cases passed their full goldens, ownership balances and
normal fixture cleanup. The unfinished control has no natural-cleanup/final-zero
proof. After the service and all its users were confirmed absent, its three
owned fixtures were validated and removed under the VM lease. The archive
records this external cleanup separately; it does not substitute for normal
cleanup. There is no abandoned matching or benchmark work.

The original seven source/fixture files were restored byte-for-byte and the
solution rebuilt nonincrementally. **330 internal / 1,036 public focused Release
checks pass on each actual .NET 11 RC3 and .NET 10.0.8 runtime**, both before and
after withdrawal. Eight independent CPython protocol/evaluation-order goldens
and four admission/pending-user cases remain in the normal suites; the removed
opcode has no surviving architecture-specific test. Initial fixture mistakes and
their failures are preserved rather than presented as runtime failures.

[Raw capture and exact patch](evidence.tar.gz),
[exact transferred collector/driver source](driver-source.tar.gz),
[partial assessment](partial-assessment.json),
[source restoration](restoration.json), [validation logs](validation-logs.tar.gz),
[package/worker identities](candidate-identity.json).

## Comparison completion assessment

Offline inspection of the original saved clocks explains the timeout without a
new guest run. Approximate complete case wall times were 13.72 s CSV, 9.79 s text
and 9.62 s JSON. Their timed batches totalled 5.53 s, 4.43 s and 2.94 s; the
remaining 20.23 s includes setup, warmup, settling, verification and cleanup,
which were not individually timed. Only 11.87 s remained for file-manifest.
The timestamp boundary has one-second precision; these are not phase medians.
[Derived original-clock data](completion-costs.json).

Development should use the maintained **one-case, 15-second diagnostic** command
and its 30-second owned-process bound, adaptive 25 ms batches, atomic partial
receipts, complete output/file goldens and joined worker cleanup. Build, tests
and transfer happen beforehand. Run affected scripts as separate bounded jobs;
do not bundle long fixed batches and repeated startup into one short deadline.
The command provides old/new Lython and CPython context, not noise-qualified
speedups. Independent semantic/refusal/lifetime tests remain required. Qualified
milestone comparisons keep their stronger controls and separate time budget.
No benchmark framework, policy constant or completed trial was changed.
