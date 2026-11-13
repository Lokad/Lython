# Ordered execution over final slots — R289

2026-11-12. Active prototype based on source commit4cc057f0; qualified9bca1337
production/e4cc65b2 is the unchanged reference. Neither prototype is a release.
Publication remains postponed. The complete-script performance objective is open.

## First form: B643

Replace the native expression graph, preparation, whole-body guard and recursive
evaluator with a small ordered managed span over final executable slots/PC. Actual
operands are inspected at each operation. Unknown protocols transfer live values
and the exact PC into the existing ordinary frame/trampoline without rebinding or
replaying earlier effects. Python tuples remain real tuples. The migration removes
287 production lines;571 production files include567 C# files. No public policy,
budget, unsafe code, controller, background operation or third backend was added.

Both targets build.3260 focused checks pass after correcting a short-circuit Dup
slot bug;30 additional closure/refusal checks pass after fixing a test's invalid
assumption that a cached frame always requires fresh admission. The corrected
fixture explicitly refuses a callback allocation after a closure write, through
direct/map calls and both APIs. Three new public cases preserve retained inputs
through late protocol I/O, cancellation, awaited cleanup and budget reuse. Local
CPython3.13.2 independently produces their golden. All seven existing scripts pass
warm sync/async, compile and compile+run output, zero-balance and fixture checks.

Warm allocation is essentially flat. Compilation saves0.14–1.03% allocation;
desktop elapsed is diagnostic, including adverse observations and moving controls.
The initial compilation error about the generator default hook survives in tool
output but its first raw build log was overwritten; later build/test failures and
all timing captures are preserved. No missing raw log is reconstructed.

| Complete script / API | Elapsed change across both controls | Wider process CPU change | Reference/candidate control spread |
| --- | ---: | ---: | ---: |
| CSV / Run | −4.01..−1.64% | −5.33..−1.09% | 2.07/0.35% |
| CSV / RunAsync | −8.49..−2.40% | −8.33..−2.84% | 2.37/3.96% |
| Native search / Run | +11.71..+13.71% | +10.12..+11.80% | 1.26/0.52% |
| Guarded search / Run | +3.79..+8.47% | +3.12..+5.50% | 3.93/0.55% |
| Captured text / Run | +3.84..+8.12% | +4.69..+6.81% | 0.99/3.00% |
| UTF8 report control / Run | +0.91..+3.25% | +0.00..+0.61% | 0.52/1.75% |

Negative means shorter. Ranges cover four process-median pairings, not confidence
intervals. The CSV benefit does not justify retaining the native/captured debt
without a decision. One bounded completion refinement removes redundant clearing
from the short-lived16-reference span, preserves deterministic private-temporary
retirement, clears before ordinary transfer, and reuses the existing definition-
owned record-key cache. It also deletes obsolete argument/proof-shape metadata.
This is a testable hypothesis, not an attribution or a predicted improvement.

Quiet Azure F4as_v7/Ubuntu24.04.4, stock.NET11RC3 GC/tiering, fixed fec79 worker and
adapter0.4. Four independent Lython processes,5s warmup (12s captured,8s report),
seven rotated500ms batches. Warm includes mediated RAM I/O, complete output/file
validation and prior-result cleanup; excludes compilation, startup, reset/protocol
and final caches. Compilation/fresh desktop observations remain separate. No
CPython timing or RSS/pending-I/O/embedding-tail claim is made here.

Six jobs finish280.101s under420s owner/30s stop/30s margin. All actual image/
auxiliary/source identities, goldens, balances, before/after quiet, exit/join,
RAM unmount and persistent/local archive SHA checks pass. Terminal is cached.
The first disk preflight refused before mutation; exact committed archived
duplicates were retired under fresh idle lease/path/UID/SHA/inode/all-link proof.
OS240MiB/evidence12MiB floors remain unchanged. Final free266645504B; no users or
RAM fixtures remain. Original adverse conditions and all controls are retained.

[Raw source/checks](first-form/source-checks.tar.xz),
[controlled summary](first-form/summary.json),
[raw VM observations](first-form/observations.tgz).

## Refined form: B644 and delivery correction

Frozen2bc15497 retains the same final-slot migration, reuses the existing
definition-owned record cache with post-admission protocol rechecking, and clears
the private synchronous span at transfer/exit rather than every operation. It
deletes the obsolete argument wrapper and speculative input/result shapes.
3286 focused checks and all seven full scripts/both APIs/compile/fresh pass.
The private driver's initial relative witness-path error was corrected before
any collection; no lost/failed timing is replaced. Desktop adverse rows remain.

| Complete script / API | Elapsed change | Wider process CPU change | Reference/candidate control spread |
| --- | ---: | ---: | ---: |
| CSV / Run | −11.25..−10.43% | −11.11..−8.48% | 0.19/0.72% |
| CSV / RunAsync | −15.29..−12.68% | −14.66..−12.07% | 2.66/0.33% |
| Native search / Run | +0.41..+9.52% | +0.00..+6.74% | 8.07/0.27% |
| Guarded search / Run | +0.40..+7.64% | +1.50..+8.41% | 4.63/2.25% |
| Captured text / Run | −2.50..+0.58% | −2.05..+0.90% | 0.30/2.84% |
| UTF8 report control / Run | −2.65..+1.33% | −1.77..+2.31% | 3.23/0.73% |

Conditions/boundaries are unchanged. Six jobs finish279.172s; all identity,
golden, zero-balance, quiet, exit/join, RAM-unmount and independent archive gates
pass. Terminal cached; final free267194368B and no users/fixtures. All first-form
adversity survives. Warm allocation remains essentially flat; simpler structure
and supported CSV CPU benefit are separate outcomes. Native control movement
prevents a precise debt estimate; guarded debt remains explicit. One focused
confirmation on the final delivery source is justified by this movement and the
following ownership correction, not an unchanged open-ended retiming campaign.

Source review then identified a real prototype cleanup gap: a fused Call can
produce a private return cursor even when compiler LastOwnedTemporarySlot has no
GetIter in that function. A bounded ownership regression models cancellation
before publication with a real prepared cursor and ended idle reuse. It fails
before the fix and passes afterward: final native cleanup scans its live temporary
range, releasing the loan once; successful return still transfers ownership.
Both actual targets pass3288 focused checks after that correction. The test uses
no timer race, fuel or checkpoint counter. B644 timings describe2bc15497, before
this final correction; do not silently relabel them as the final delivery binary.
Platform/stress/installed-package qualification and final debt decision are separate.

[Refined source/checks](refined/source-checks.tar.xz),
[controlled summary](refined/summary.json), [all raw observations](refined/observations.tgz).

## Committed final source: 2d91eb1a / B645–B646

Final161cd935 matches all570 committed production files (566 C#), including the
cancelled-return fix;346 production lines are removed versus4cc057f0. [Full
Windows/Ubuntu actual-target/stress/installed-package delivery is green](delivery/README.md)
on43327264124. Terminal is cached; this qualification is not repeated for metadata.

B645 completes three warm rows and CSV compilation, with all six process-role
identities, goldens, zero balances and individual Closed/exit/join checks. Its next
warmup fails because the private collector sends the unsupported lane fresh instead
of compile-run. All workers join and fixtures are empty. The original normal end
gate is missing; recovery separately passes quiet, archives/SHA-verifies the complete
partial trace and unmounts RAM under lease. Keep these four rows **diagnostic**,
not a completed campaign. Do not repair the original raw status or hide the failure.

| Final-source row | Lython | CPython | L/P ratio | Observation |
| --- | ---: | ---: | ---: | --- |
| CSV warm | 4.098–4.169ms | 4.302–4.311ms | 0.95–0.97× | Completed row; campaign end gate missing |
| Native search warm | 0.256–0.262ms | 0.158–0.160ms | 1.60–1.66× | Variable; maxIQR5.96%; end gate missing |
| Guarded search warm | 0.276–0.282ms | 0.160–0.166ms | 1.66–1.76× | Variable; reference spread9.59%; end gate missing |
| CSV compilation | 70.24–70.33µs | 66.80–67.09µs | 1.05–1.05× | Completed row; campaign end gate missing |
| CSV compile+run, B646 | 4.170–4.249ms | 4.464–4.520ms | 0.92–0.95× | Corrected isolated lane; all gates pass |

B646 reuses the exact immutable images/workers/catalog via verified hard links;
only the failed lane is repeated with its correct existing protocol name. It
finishes60.276s under120s owner/30s stop/30s margin; initial/final quiet, all
goldens/balances, Closed/exit0/join, RAM unmount and archive SHA gates pass. Initial
local/self-process and staging preflights fail before starting work; both are
preserved. Hard-linking immutable metadata corrects the staging estimate without
lowering OS240MiB/evidence12MiB floors. Final free267366400B, no users/mounts.

**Disposition:** retain the coherent deletion and CSV whole-script benefit. Final
warm CSV improves7.92–10.80% elapsed /9.02–10.47% widerCPU; corrected compile+run
improves9.29–11.35% /8.03–10.03%CPU versus the qualified e4cc65b2 baseline. Warm
allocation stays effectively flat. Accept the observed native-search debt
+1.07..+6.41%elapsed/+1.92..+6.49%CPU, with control/IQR uncertainty; do not call
it equivalent. Guarded search crosses zero (−7.28..+4.83%elapsed), with9.59%
reference spread: its true debt remains uncertain. B644 captured/report ranges
also cross zero. These remain priorities for current-source whole-script CPU
attribution; no multiplied gains, universal speedup or performance-complete claim.

The final comparison uses stock CPython3.13.16 and ordinary.NET11RC3 GC/tiering,
fixed fec79 worker/adapter0.4, quiet AzureF4as_v7/Ubuntu24.04.4 and the same
source/fixtures. Four independent Lython roles plus two CPython roles,5s warmups,
seven rotated500ms batches. Warm/fresh include mediated RAM I/O, full output/file
validation and prior-result cleanup; compilation is separately timed. Startup,
fixture reset/protocol/final caches are excluded. Separate phase medians do not
sum. RSS, pending I/O and embedding-tail effects remain unqualified. Publication held.

[Final source/checks](final/source-checks.tar.xz),
[B645 completed rows and limits](final/b645/summary-partial.json),
[B645 original partial raw](final/b645/observations-partial.tgz),
[corrected B646](final/b646/summary.json), [B646 raw](final/b646/observations.tgz).
