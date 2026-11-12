# Linear native proof — first form, not retained

2026-11-12. Three production files replace recursive native proof with a linear
pass over the existing postorder nodes. Compilation validates consumption once;
runtime uses bounded transient index/shape scratch. Actual evaluator, call ABI,
binding, owners, stack admission and dynamic protocols remain. **No retention or
CPU improvement claim.** Qualified reference is `2ec9d67f` / frozen `c9829194`.

Initial form passes **1,646 internal/5,836 public checks per actual .NET 11/10**.
The only subsequent source change adds cancellation/refusal checks at natural
64-node boundaries; that exact form passes **1,646 internal/333 public per target**.
Original source/checks survive, including the first compile signature error.
All six unchanged scripts through both APIs and separate compilation pass complete
stdout/files/allowance/cleanup diagnostics. Allocation is flat, not a new reduction.

Desktop elapsed is mixed: searchRun−11.21..−7.41%, but JSONAsync+26.38..+27.34%
with0.76%control, CSVAsync+5.49..+7.68%with2.08%control, ordinarycompile+7.05..+7.10%
with0.06%control. These are diagnostic; all observations and unstable controls remain.

## Incomplete quiet comparison

B600 planned12jobs with390s supervisor, but the copied collector still had the
old295s cutoff for10jobs. It stopped after **11 completed warm jobs**, before
ordinary compilation and the original final quiet gate. Source did not time out
or fail a semantic check. `complete:false` remains unchanged in the raw record.
The old archive/retrieval receipt code also retained10-job assumptions; these are
preserved as failed preparation, not rewritten into a successful campaign.

Saved partial observations do not support retention:

| Complete script/API | Elapsed change, all four process pairs | Wider CPU change |
| --- | ---: | ---: |
| Captured text / Run | +5.17..+9.37% | +5.06..+9.41% |
| Captured text / RunAsync | +4.80..+9.21% | +0.92..+6.10% |
| Ordinary text / Run | +1.52..+4.05% | +0.30..+4.10% |
| Helper search / Run | +1.16..+2.13% | −0.65..+8.11% |
| JSON-lines / RunAsync | −1.02..+0.61% | −2.33..+6.25% |

Other cases and every control/IQR remain in [partial summary](summary-partial.json).
SearchAsync/CSV controls are unstable; report is mixed. These are not confidence
intervals or complete-campaign qualification. The desktop JSONAsync adversity is
not reproduced in partial data, with no cause/recovery claim.

All workers were already joined. Recovery acquired the lease, preserved the
failure journal and raw data, independently verified every archive entry, then
unmounted the empty owned fixture. No user/RAM remains. A delayed quiet snapshot
is explicitly **not the missing original final gate**. [Recovery](partial-recovery.json).
The runtime source and all failed collector/receipt assumptions remain unchanged.

Next bounded source refinement assesses removing the newly introduced per-node
input/shape packing loops. Keep linear proof and compiler validation; use actual
node operands and local shape values without a second runtime metadata traversal.
Preserve all branches, formatting reads, live inputs, current token, first refusal,
reentry and stack safety. No unchanged11-job replay is selected. A new form must
pass focused checks and complete-script diagnostics before a correctly sized
bounded comparison. This is evidence-guided refinement, not a failed-design label
or permanent veto caused by the collector error.

[Identity](diagnostic-identity.json), [source assessment](source-assessment.md),
[before](sources-before.tar.xz), [candidate](sources-candidate.tar.xz),
[focused checks](focused-checks.tar.xz), [raw partial observations/core](observations-partial.tgz),
[net10 candidate](net10-candidate.tar.xz), [exact preparation/failure](preparation.tar.xz).
Reference core remains in the earlier committed CPython archive. Exact loaded
images match all11jobs. No full prototype platform/package qualification or
publication. [Archive identities](archive-identity.json).
