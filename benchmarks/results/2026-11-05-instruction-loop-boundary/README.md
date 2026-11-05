# Protected instruction-loop layout: withdrawn

Baselinef8ebdc7c/productionb69a0a49, frozen86cea356; prototype/controle55e7fcc.
One protected inner loop placed the existing three routing catches outside normal
iteration. Entry cancellation/refusal branched outward to existing CheckExecution;
operation failures still routed with their originating instruction ref even after
helper index changes. Original jump/return/pause/awaited cleanup remained. No phase
flag, counter, owner, policy, dispatcher, guest representation or unsafe code.
Tradeoff: outward branch and duplicated cold disposal/return tail.

This was a **JIT/code-shape hypothesis**, not elimination of counted runtime
try-entry operations. D52 prefix leaves include actual Python work, not a removable
fraction. No advance speedup or causalCPU/allocation/latency claim.

## Initial comparison and justified confirmation

B287139.70s within180s, five warm jobs, workerb94/CPython3.13.16, normalGC/tiering,
5s per-role/job warmup,7rotated~150ms samples, independent identical-current role.

| Warm job | Candidate/reference | Identical-current/reference | Limits |
| --- | ---: | ---: | --- |
| Ordinary CSV |+0.41%|−1.46%|Current spread1.87%; conflicting signs|
| Text helpers |−4.71%|−4.24%|Current spread.49%; reference IQR.93%|
| JSON helpers |−1.13%|−2.67%|Current spread1.56%; reference IQR.67%|
| Report |+3.63%|+2.60%|IQR3.91/2.58/3.42% candidate/control/reference|
| Basic integer loop |+0.92%|+0.59%|Reference IQR1.10%|

Report dispersion and the small basic-loop adverse result justify one same-binary
confirmation. B28897.10s within120s, those two jobs only,8s warmup/role/job and
11rotated~250ms samples; other boundaries/gates unchanged. Report−3.64/1.31%,
current spread2.41%, IQR4.03/3.21/2.65%. Basic−.67/4.33%, spread3.68%,
IQR5.17/5.46/5.77%. **The reversal establishes uncertainty, not failed design or
proven repair.** Original favorable/adverse samples and conditions remain.

**Withdraw:** extra cold control-flow structure lacks sufficiently consistent
complete-workload benefit to justify its complexity. This design/debt decision is
not a percentage veto or permanent ban. No narrower tuning rescue/further isolated
retiming; new architectural evidence may justify a different complete design.

Both blocks pass quiet/source/fixture/golden/library/Closed/final-zero/joined-worker/
removed-fixture gates. Warm includes invocation/full output checks/prior-result
cleanup; processing jobs add actual mediated /dev/shm I/O/full file checks. Basic
loop sums10,000 Python range values plus captured print; isolated localCPython3.13.2
independently verifies its golden. Compilation/startup/staging/reset/transport/final
cache/process/fixture teardown excluded. Frontend unchanged, compile-only gate not
replayed. No general/portable/causalCPU/allocation/RSS/embedding-latency claim.

## Preserved regression coverage and restoration

Clean32.01s prototype build;205internal/1274public focus per actual.NET11/10 pass.
Six added routing controls also pass both: entry cancellation/refusal keeps its
unchained origin, operation errors acquire active Python context, first refusal
keeps object/span/request, effects stop and storage returns without altering
retained exception/input state. No exact checkpoint counts/locations or relaxed
timeouts/containment. Keep these independent tests after withdrawing the layout.
First test-only build used the wrong PyList constructor; corrected to existing
capacity/governor/span API, failure log retained. Production image stayed identical.

Dispatch source restored byte-for-byte to HEAD; forced nonincremental Release
build62.23s/zeroW/E precedes restored checks/freezing. No prototype remains in core.
D56 platform qualification stays independent; broader performance work stays open.

[Contract](source-contract.md), [source gate](source-gate.md), [initial](summary.json),
[confirmation](confirmation/summary.json). Raw archives preserve sources/identities/
goldens/orchestration/full observations. InitialSHA256
`b3ec5da9ecd58e64008df77b842957be869bd1525e33f89413168e13b76310c4`;
confirmation`bbcf9e1e345ae73d6d9b0fb88057cd742d7c1375cd44a4fb88717fa8fd757555`.
