# Ordinary tuple storage — R287 withdrawn

2026-11-12. Baseline qualified9bca1337/e4cc65b2; current committed source7ea62157
has unchanged production. Three uncommitted source forms are frozen separately:
initialc1945390, incorrect refinementf4f57df2, correcteda3160fcb. **Withdrawn after B642. No production change, no CPU gain established.** Publication remains postponed.

The unchanged CSV helper returns an observable pair on each accepted row. Ordinary
tuple construction copies evaluated operands into a separate small backing array;
unpacking already borrows it. Keep the Python object/identity/aliases and copy short
completed spans directly into managed InlineArray storage. Large or adopted arrays
remain array-backed. No private return protocol, pool, unsafe API or new execution
route. Source compares one production file/four accounting fixtures; no general
heap or ownership audit.

Initial separate inline/array fields grow measured header48→72B and reduce complete
CSV allocation only1.62% despite eliminating a pair's array. Other allocations stay
nearly flat. Controls vary and several elapsed/compilation observations are adverse;
all survive.7866focused actual11/10 checks pass (1458internal/2475public each),
seven complete scripts both APIs/compilation/fresh pass. No CPU claim follows.

The single-storage refinement measures64B headers but incorrectly copies short
adopted arrays. Existing deepcopy coverage fails: CopyTupleAsync deliberately memoizes
the tuple shell before populating its private backing. Do not qualify or use timings
from that incorrect form to select a runtime. Its frozen code, failing check and
automatically launched seven-script diagnostics survive. The measurement launcher
was started before the failed test result was inspected; this is procedural error,
not supporting qualification. All diagnostic workers still join and close.

Corrected code keeps adopted arrays authoritative using a negative encoded count;
nonnegative count selects completed short inline spans. It deletes the premature
conversion helper and the overlapping array field. Header64B is measured with256
prewarmed constructor calls outside guest clocks, including identical reflection
overhead; no native layout/Unsafe assumptions. Large tuples pay16B more header than
baseline; small literal pairs remove an array. This tradeoff must be assessed on
whole scripts rather than field counts. Conservative metadata estimates rise;
earlier refusal is intentional. Fixture changes derive their capacity, preserve
the intended cache-insertion denial and retain independent refund/ownership checks.

Corrected Release solution build and8222focused checks pass across actual.NET11/10
(1494internal/2617public each), adding the full existing copy coverage. All seven
unchanged scripts pass both APIs, separate compilation and compile-to-result,
stdout/files, balance and joined-fixture cleanup. Normal workstationGC/tiering,
Windows desktop, same fixed local images/host and original trusted fixtures.
Three isolated load contexts share processGC/CoreLib; clocks are **diagnostic**.
Warm includes disk fixture reset/mediated I/O/goldens/result cleanup; execution clock
is separately recorded. Compile and fresh are separate, startup/final cache cleanup
excluded. This differs from quiet-VM warm timing and predicts no CPython ratio.

| Complete command/API | Allocation change | Diagnostic elapsed | Unchanged-control spread |
| --- | ---: | ---: | ---: |
| scripts.file-search.large / warm-sync | +0.04..+0.04% | -18.02..+1.26% | 19.04% |
| scripts.file-search.guarded-helper.large / warm-sync | +0.04..+0.04% | -16.58..-12.16% | 5.30% |
| scripts.csv-validated-records.large / warm-sync | -2.42..-2.42% | -1.51..+0.30% | 1.84% |
| scripts.text-summary-helpers.nonlocal-counter.large / warm-sync | -0.01..+0.01% | -3.55..-2.31% | 1.29% |
| scripts.report-render.large / warm-sync | +0.02..+0.02% | -2.58..-2.28% | 0.31% |
| scripts.json-summary-helpers.large / warm-sync | +0.00..+0.01% | -1.26..+4.36% | 5.70% |
| scripts.report-render.utf16.large / warm-sync | +0.01..+0.01% | +8.31..+9.19% | 0.81% |
| scripts.file-search.large / warm-async | +0.04..+0.04% | -21.47..-5.41% | 16.97% |
| scripts.file-search.guarded-helper.large / warm-async | +0.04..+0.04% | -9.09..-6.42% | 2.93% |
| scripts.csv-validated-records.large / warm-async | -2.42..-2.42% | +1.72..+14.86% | 11.44% |
| scripts.text-summary-helpers.nonlocal-counter.large / warm-async | +0.01..+0.01% | -13.75..+8.49% | 25.78% |
| scripts.report-render.large / warm-async | +0.01..+0.01% | +2.08..+9.86% | 7.63% |
| scripts.json-summary-helpers.large / warm-async | +0.00..+0.00% | -3.02..-1.17% | 1.90% |
| scripts.report-render.utf16.large / warm-async | +0.00..+0.00% | -6.38..-1.74% | 4.95% |

Ranges use candidate versus both baseline process-role medians. Preserve all original
compile/fresh and adverse observations in raw diagnostics. No unchanged retiming or
full platform/package cycle is selected automatically. At that checkpoint, retention/quiet-VM confirmation remained pending; the decision
below completes it. This bounded
storage experiment does not complete the substantial whole-script performance goal.
The next larger source gate concerns complete helper local/return construction;
it must preserve ordered effects and genuine suspension without replaying the
slower call-arena/generic-prefix migration. Regex final-package gaps are independent.

[Exact sources, patches, binaries, tests and all raw conditions](diagnostics.tar.xz),
[corrected warm](corrected/warm-sync/summary.json),
[corrected async](corrected/warm-async/summary.json),
[source identity](corrected/source-identity.json). Current guides remain unchanged;
this is historical/prototype evidence, not shipped performance documentation.

## Controlled decision and restoration

B642 uses the same quietF4as_v7/Ubuntu24.04.4/.NET11RC3, fixedfec79worker,
normalGC/tiering, two processes/image and seven rotated500ms rounds. Four jobs
complete in169.406s under300s owner/30s stop/30s margin. Warm includes mediated
RAM-file I/O, full validation and prior-result cleanup; wider processCPU includes
reset/protocol/runtime work. No compilation/startup/final-cache cost in warm clocks.

| Complete command/API | Elapsed change | Wider processCPU | Reference/candidate spread |
| --- | ---: | ---: | ---: |
| CSV Run | −0.46..+4.07% | 0..+4.83% | 1.56/2.84% |
| CSV RunAsync | −3.11..+1.69% | −1.22..+1.92% | 3.90/0.86% |
| UTF16 report Run | +0.12..+0.51% | −1.85..+0.67% | 0.37/0.01% |
| UTF8 report Run | −1.64..+0.80% | −1.89..+1.28% | 1.15/1.28% |

Ranges retain all four process-median pairings. No reliable targeted CSV CPU
benefit is demonstrated; Run includes possible adversity. The large desktop
UTF16 elapsed regression is not reproduced, but equivalence/cause is not proven.
**Withdraw the complete prototype.** Its2.42% CSV allocation reduction is real,
but a new storage tag/index branch,16B larger general header and larger admission
estimates do not justify retention without targeted CPU benefit. This is a design/
evidence tradeoff, not a fixed percentage veto, failed-design proof or permanent
ban. The substantial performance objective stays open. No further unchanged
confirmation is needed for this bounded decision; materially new evidence remains
eligible for a later refinement. No dormant tuple form or fixture threshold change
is retained, and no new full platform/package cycle of unchanged runtime is claimed.

All identity/golden/balance/quiet/Closed/exit/join/unmount/archive gates pass.
Raw archive SHAa1deaf1ca49f814c49f445592068da5328b1970c49afb053a958816f13657ed0
is independently verified; terminal cached, no live VM users/RAM fixtures. To stage,
only exact already committed withdrawnc8 image aliases, its source snapshot and
duplicate B641 archive are retired under fresh idle lease/path/UID/inode/all-alias/
Git+SHA proof. Qualified e4 images and all canonical evidence remain. Existing
OS240MiB/evidence12MiB/staging floors are unchanged; final VMfree266145792B.

All five prototype source/fixture files restore to committed production.568core
sources match qualified code with explicit newline normalization. Forced
nonincremental Release solution build and 5682 focused target checks pass:
internal-net11.0=1350, public-net11.0=1491, internal-net10.0=1350, public-net10.0=1491.
The previously committed generator alias regression remains. The corrected/failing
forms, all desktop/VM adversity and restoration evidence survive. Next is the
ordered complete-helper state design; this storage withdrawal is not completion
of the overall performance objective.

[Controlled observations](vm/summary.json), [raw VM evidence](vm/observations.tgz),
[completion/preparation/restoration](completion.tar.xz), [restoration proof](restoration.json).
