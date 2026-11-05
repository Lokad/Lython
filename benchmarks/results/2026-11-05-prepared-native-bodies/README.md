# Prepared native helpers — retained, delivery pending

2026-11-05. Baseline **cf3dc9cd**, frozen **43fdf658**. Completed source is
8243b05c plus the archived patch/snapshot, measured **81deb3be**. Retain the
coherent replacement; full platform/Stress/installed-consumer delivery pending.
Publication postponed. This does not complete the broader performance objective.

## Removed work and preserved contracts

Compilation prepares closed native operation kinds, method arity and supported
casing/formatting combinations. Remove NativeFirst/NativeSecond and constructor/
copy plumbing from every ordinary instruction, runtime casing-fusion recognition,
separate formatted-builder proof and formatted-value evaluator. Share one immutable
string-method inventory. Net15production lines removed including new body types;
no public API change.

Eligible helpers own16B prepared values referring to original instruction spans
and existing call inputs. Ordinary instruction stride32B, previously40B.
[Managed array-allocation diagnostic/source inventory](census.json),
[tool](census-Program.cs), [project](census-census.csproj). The six maintained source
shapes retain original instruction/native-value counts: CSV110/3,text91/3,JSON100/3,
report84/18,config79/0,longCSV110/3. Small retained-storage estimates are distinct
from measured compilation allocation; no heap/RSS/admission-threshold claim.

This replaces the existing native shortcut, without restoring B207's old raw
expression graph. Dynamic callable/type/namespace/protocol checks precede effects;
actual dynamic behavior uses ordinary executable code. No runtime value packet,
arena,pool,cache,JIT,owner,mode or fuel. Preserve readonly borrowing, native final-
word casing/direct UTF8 formatting, original spans/checkpoints, aliases/defaults/
closures, real async protocols, terminal refusal and awaited cleanup/funding.
Actual formatting-field guards still perform pure reads; their values are never
cached across later admission/effects. Shared native operation helpers remain.

## Complete source comparison

Quiet AzureF4as_v7,Ubuntu24.04.4/.NET11RC3,normal GC/tiering. Existing complete-job
driver,3isolated images/two unchanged old roles, same auxiliaries/CoreLib/hashes.
6s guestwarmup/image,2ssettle,9rotated200ms batches. Ten jobs **268s**; all exact
identities, independent CPython3.13.16 full stdout/file goldens, quiet endpoints,
budget baselines/finalzero, normal closes, joined users and fixture removal pass.
Invocation/mediated RAMIO/checks/result cleanup included; reset/staging/startup/
final cached teardown excluded. Fresh adds Compile/Dispose; compile-only excludes
guest execution/IO. Process allocation includes reset/background work. No attributed
CPU/startup/pendingIO/RSS/external-ratio claim.

| Initial complete job | Elapsed vs two old roles | Old spread | Candidate IQR |
| --- | ---: | ---: | ---: |
| CSV warm |+1.47% / −5.75%|7.66%|0.58%|
| JSON warm |+3.55% / −0.10%|3.66%|1.08%|
| Text warm |**−7.95% / −4.73%**|3.38%|0.48%|
| Text async/completed IO |**−5.28% / −7.25%**|2.13%|0.23%|
| Text compile/run |−2.14% / +0.31%|2.44%|0.61%|
| Report warm |+0.52% / −1.36%|1.90%|1.42%|
| Report async/completed IO |+1.17% / −0.18%|1.36%|3.35%|
| Report compile/run |−1.29% / +1.55%|2.80%|0.95%|
| Text compilation |**−2.16% / −1.46%**|0.71%|0.31%|
| Ordinary branching-call control |−0.84% / −3.83%|3.12%|0.89%|

Warm allocation effectively unchanged. Text compilation allocation **−2.30%**
against both roles; fresh text−0.18%,report−0.04%. Text warm/async improve against
both roles; no report/fresh/general CPU improvement follows.
[Plan](initial/plan.md), [metrics](initial/assessment.json),
[raw/source/identities/gates](initial/observations.tgz).

## Bounded confirmation and remaining debt

One unchanged-bit **81s** confirmation addresses CSV7.66%/JSON3.66% control spreads.
Same source/fixtures/boundary, reversed image construction/initial warmup order,
10s guestwarmup/image/9rotated250ms. All original gates pass; both conditions/results
remain. This is measurement confirmation, not candidate refinement.

| Confirmation | Elapsed change | Old spread | Candidate IQR |
| --- | ---: | ---: | ---: |
| CSV warm |−0.39% / +0.94%|1.32%|0.16%|
| JSON warm |−0.69% / −0.49%|0.20%|1.04%|

Allocation effectively flat. No confirmed material CSV/JSON regression; **possible
roughly1% CSV cost is accepted uncertainty**, alongside inconclusive fresh/report
timings. Original adverse signs and controls remain; no automatic further retiming
or causal attribution. Retain for complete machinery replacement, smaller ordinary
storage, lower compilation allocation and favorable whole text scripts.
[Plan](confirmation/plan.md), [metrics](confirmation/assessment.json),
[raw](confirmation/observations.tgz). Gains are not additive CPython ratios.

## Focused validation and correction

Nonincremental Release81.39s/zero warnings/errors: **1744internal/5171public per
actual .NET11/.NET10**. Additional overlapping40native-return cases include one
new independent CPython3.13.2 all15string-method golden; core unchanged during its
test-only build. Additional50internal/90public managed-stack/recursion controls per
target pass, overlap explicit. Existing cancellation, first refusal, cleanup,
retained aliases and genuine pending-host cases remain.
[Validation/identities](validation.json), [logs](validation-logs.tgz),
[independent golden](method-golden.json).

The first fixture writer decoded existing UTF8 with Windows' legacy encoding,
causing a C# character-literal error. Restore exact Git UTF8 and insert only the
new golden; corrected test-only build16.54s and40cases/target pass. Failed build
log preserved. No production source/binary or semantic expectation changed during
correction. No timeout or original containment scale was relaxed.
