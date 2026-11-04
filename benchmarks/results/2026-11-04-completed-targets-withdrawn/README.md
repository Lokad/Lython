# Completed prepared-target prototype withdrawn

2026-11-04. Exact baseline qualifiedf6e51d1c/frozendf206f47, candidate7245014f.
One production file replaced two always-async target switches with a shared
completed prefix and actual-pending continuations, +68net lines. Same child
operations/ownership/ordering; no whitelist, new representation or call binder.
[Source patch](source.patch), [snapshot](source-snapshot.tgz), [pre-code contract](pre-code-plan.md),
[pre-timing plan](pre-measurement-plan.md), [all metrics](assessment.json),
[raw evidence](observations.tgz). ArchiveSHA256
`979a80a2ca7dd5be2d39c547fda2ee6c2d94e72f34092f86eebd84bfab5e389a`.

Six jobs165s, service240s limit. Three isolated images/two unchanged baseline roles,
6s warmups/2s settle/9rotated200ms batches, ordinary GC/tiering. Actual loaded
identities/CoreLib/full independent CPython goldens/quiet gates/zero final balances/
joined workers/owned-fixture removal all pass. No timing retry.

| Complete script | API | Candidate | Change against baseline roles | Baseline spread | Allocation |
| --- | --- | ---: | ---: | ---: | --- |
| scripts.text-summary-helpers.large | warm | 2.9554ms | -1.54% / -6.68% | 5.50% | Near unchanged |
| scripts.text-summary-helpers.large | async | 3.5094ms | +3.21% / +4.62% | 1.35% | Near unchanged |
| scripts.csv-summary-helpers.large | warm | 5.0245ms | +2.44% / -1.66% | 4.17% | Near unchanged |
| scripts.csv-summary-helpers.large | async | 5.7779ms | -1.68% / -1.78% | 0.10% | Near unchanged |
| scripts.report-render.large | async | 4.7509ms | -0.17% / -0.34% | 0.17% | Near unchanged |
| scripts.config-edit.large | warm | 0.1969ms | +1.13% / +1.49% | 0.35% | Near unchanged |

Async text regresses3.21–4.62% beyond1.35%unchanged spread (IQR0.18–0.69%);
async CSV improves1.68–1.78%. Sync text/CSV controls move5.50%/4.17%, so apparent
gains there are uncertain. Report async near unchanged; config warm+1.13–1.49%.
No meaningful allocation saving. Added manual continuation/cleanup machinery does
not justify this mixed outcome. Withdraw the complete prototype; no CPU cause or
permanent architectural veto inferred. Existing always-async engine remains.

Nonincremental final Release37.08s/zero warnings;1,573internal/2,056public per actual
11/10 pass. [Focused validation](validation.json). Eight independent CPython probes
cover48immutable-mutation receiver cases across both APIs/targets. They disprove
the proposed index-hook bug; preserve actual hook/error order, no behavior change.
Initial PowerShell argument construction failed before guest execution; original
error and corrected probe launcher are retained. A pre-timing source review fixed
await-before-mutable-struct-publication, followed by a full nonincremental rebuild.

Raw reused labels incorrectly mention the previous declaration experiment/ee8020f0
and eight jobs; candidate-identity.candidateSource also names ee8020f0. These are
stale descriptive labels, retained unchanged. Actual loaded baseline is df206f47,
candidate7245014f, six jobs; snapshots/hashes/observations establish the comparison.
No qualification claim follows from a label. Original source is restored; nonincremental Release40.31s/zero warnings and
77public mutation/unpack/pending-lifetime checks per actual11/10 pass. [Restoration](restored-validation.json). Full platform/package
qualification is not repeated for a discarded source.

Timing includes warm invocation, mediated RAM I/O, full checks/prior-result cleanup;
async uses completed I/O. Fixture staging/reset/startup/hash/final cached teardown
excluded. Initial untimed warm-admission peaks are not fresh overlap/RSS/latency.
Real pending operations/cancellation/refusal are separate correctness checks.
Whole-script performance work continues; publication remains held.
