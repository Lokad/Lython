# Dictionary self-dispatch prototype — withdrawn

2026-11-04. Baseline **eff471b3 / 9588b3c8**, prototype **a6adcfe9** based on the
same source. Quiet Azure F4as_v7/Ubuntu24.04.4/.NET11RC3, normal GC/tiering.
Exact [source identities](candidate-identity.json) and [snapshot](source-snapshot.tgz).
Do not describe this prototype as current runtime or a performance improvement.

The prototype removed PyDict's implementation of/calls through its own raw storage
interface, retained native table fields and an optional real view bridge, narrowed
namespace extras to typed raw access, and removed three unused interface operations.
No new owner/table/backend or policy. Initial focused **768internal/2258public per
actual11/10** pass. Production source net−28lines, no meaningful allocation reduction.

## Controlled complete scripts

Ten cases **276seconds** below360s owned limit; three isolated images/two old-source
roles, six-second guest warmup/image, alternating preparation, two-second settle,
nine rotated200ms batches. All cores/aux/CoreLib/hash witnesses/CPython goldens/
quiet gates/ownership baselines/finalzero/fixture/user joins pass. No failed timing
capture. [Full metrics](assessment.json), [raw observations](observations.tgz), SHA256
`d9691927e3ce95000baf0ab18639c6e9082976f69137d359dadcb73a2d9de21e`.

| Complete script | API | Elapsed vs old / old control | Old-role spread / candidate IQR |
| --- | --- | ---: | ---: |
| csv-summary-helpers | Run | +0.31% / -3.93% | 4.41% / 0.65% |
| csv-summary-helpers | RunAsync | -1.12% / -0.40% | 0.73% / 0.41% |
| text-summary-helpers | Run | +3.81% / +4.35% | 0.51% / 1.18% |
| text-summary-helpers | RunAsync | +0.08% / -4.36% | 4.64% / 0.19% |
| json-summary-helpers | Run | -0.69% / -0.25% | 0.44% / 0.40% |
| json-summary-helpers | RunAsync | +1.79% / +1.18% | 0.61% / 0.78% |
| report-render | Run | +0.88% / +1.32% | 0.43% / 2.45% |
| report-render | RunAsync | +0.02% / +0.83% | 0.81% / 0.59% |
| config-edit | Run | -1.72% / +0.39% | 2.10% / 0.25% |
| config-edit | RunAsync | +0.73% / -0.75% | 1.49% / 0.43% |

Allocation stays flat apart from <=0.02% background noise; admitted setup peaks
are identical. Sync text+3.8–4.4% against0.51%old spread is a demonstrated adverse
source-pair result. AsyncJSON+1.2–1.8% also exceeds0.61%old spread. CSV/asynctext/
config controls move substantially; retain those uncertainties. Report has weaker
adverse direction/dispersion. No attributed CPU/RSS/embedding-latency conclusion.
Invocation/actual mediated RAMIO/fullchecks/result cleanup included; reset/staging/
startup/compilation/final cached teardown excluded. Real suspended semantics tested
separately; these async host operations complete.

## Bounded diagnostic and decision

A local Windows one-shot JIT diagnostic reached onlyTier0; preserve it and make no
optimized-CPU inference. A bounded actual-VM correction used the same checked text
worker, six-second warmup and normal tiering. Initial ordinary no-override guard
rejected intended diagnostic flags before warmup; original script/identity/Tier0/
failed unit/joined worker are preserved. Narrow allowlist correction permits only
JitDisasm/assemblies/stdoutfile, leaving GC/tiering unchanged. Final unit succeeds,
workers/goldens/quiet gates/cleanup/lease pass. An inline-shell inspection quoting
error executed no measurement. [All JIT conditions/results](jit-observations.tgz), SHA256
`22050db07839fc5768268751cc94e2a7c721486f98758ad2cb84d8d542f220e6`.

Optimized candidate TryGetValue already inlines the typed lookup helper. This does
not justify a blind inlining-hint series, nor establish the CPU cause of the source-
pair regression. Withdraw the complete self-dispatch deletion rather than promote
it on source count alone. This is not a permanent architectural ban or fixed-percent
rule. All replacement helper/view-dispatch code is removed from production.

## Independent ownership correction retained

Source review found a pre-existing violation: private rollback refunds a live view
BEFORE its eventual rejection. Two new attribute/module view regressions fail on
exact committed baseline9588b3c8 and original prototype; budget drops while the
alias remains live. Both cases pass after validation precedes refund. Corrected
full prototype8d9c24f3 passes **770internal/2258public per actual target**; it is
archived separately and was not the timed source. [Correction contract](ownership-correction-plan.md),
[red/green/build logs](local-validation.tgz), [corrected source](corrected-full-dispatch-source.tgz).

The selected refinement preserves baseline data dispatch while removing capacity/
release from the raw interface. Native owners already implement those operations;
three view noops/three throwing methods disappear, with one owner-side rejection
before any refund. It needs its own final-source checks/comparison/delivery.
[Refinement decision](refinement-plan.md). No public contract change or publication.
Whole-script performance objective remains open; this experiment does not close it.
