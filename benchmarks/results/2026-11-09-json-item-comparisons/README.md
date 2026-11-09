# Native JSON item comparison and UTF-8 delivery decision

R165/B514, 2026-11-09. Replace JSON's key-only comparator with original item-pair
identity/equality prefix and value ordering, before skipkeys/key validation. Reuse
the existing native comparison/sort and ownership paths; no new framework or owner.
Three independent Python3.13.2 cases cover nested equality effects, failure during
the second equality, and value-ordering failure when keys become equal. The same
regression assembly fails all three against frozen R164 and passes current source.
Existing native custom-key/synchronous-comparison limitations remain documented;
this is not a general rich/asynchronous custom-key or stable-sort redesign.

Focused Release: **198 internal/1378 public .NET11** including explicit-filter Stress;
**196 internal/1372 public .NET10** excluding Stress. Includes JSON semantics, first
refusal/cancellation, native-sort scratch, async ambient restoration, retained results,
and cleanup/reuse. The original Stress tests remain for full delivery.

One quiet F4as_v7/Ubuntu24.04.4/.NET11RC3 confirmation, three jobs **88.558 seconds**,
four independent processes,5s warmup/role/seven rotated150ms rounds. Baseline is the
R163 final precommit freeze (core67612135), production-source equivalent to f6861aa5;
candidate is freshly nonincremental R164/R165 working source. Common worker and
adapter unchanged; exact source/binary hashes retained. Not a postcommit timing.

| Whole warm script/API | Candidate versus both unchanged baselines | Control spread |
| --- | ---: | ---: |
| CSV validation sync |−1.38% to+0.21% elapsed|baseline1.42%,candidate0.19%|
| CSV validation async |−7.71% to−0.18% elapsed|baseline7.50%,candidate0.61%|
| JSON configuration edit sync |−0.71% to+0.34% elapsed|baseline1.03%,candidate0.02%|

Includes mediated RAM-file I/O, output validation and prior-result cleanup; excludes
compilation/startup/reset/protocol/final cache teardown. Coarse process CPU includes
reset/protocol: sync CSV−3.03–3.23%, async−10.71–+7.41%, config−0.50–+0.55%.
These wider clocks and async control spread do not establish a general CPU gain.

Retain R164's cancellation/kernel consolidation and this correctness fix. The
recorded desktop CSV+2.41–5.45% did not reproduce in the controlled sync comparison;
async remains control-limited. Close the provisional debt decision without claiming
equivalence or explaining/deleting original adverse text/config/CSV observations.
Allocation remains the prior effectively-flat diagnostic, not a new final-CPU claim.
This does not refresh historical CPython rows or complete the performance objective.

Initial local preparation rejected binary identity before SSH: fresh postcommit and
earlier precommit binaries differ despite identical production sources. Corrected
setup explicitly identifies/reuses the prior freeze. Initial space recovery also
refused before mutation. Reuse the verified persistent catalog to remove its redundant
extraction; retain unchanged240MiB OS/12MiB evidence floors. Fresh deficit/exact SHA,
single aliases, owner/group/mode/parents and receipts prove only three redundant
B512/B513/B508 remote archives safe to remove. Their tracked canonical archives,
raw trees, receipts and frozen workers survive. The stale recovery print says two;
the persisted file list proves three. No wider cleanup or lowered floor.

All identity/output/quiet/owner/Closed/join/unmount/archive-under-lease/localSHA gates
pass. No VM users or RAM fixtures remain; terminal state cached. Full platform,
original Stress and installed-consumer delivery qualification is pending. Publication
postponed. Original failed conditions and exact source/cores/raw remain linked.

[Summary](summary.json), [decision/checks](decision.json), [source](source.json),
[identity](identity.json), [baseline proof](baseline-equivalence.json),
[Python](python-proof.json), [regression proof](regression-proof.json),
[raw/core archive](observations.tgz), [space proof](space-recovery.json),
[checks/setup failures](checks-and-preparation.tar.xz), [terminal](terminal.json).
