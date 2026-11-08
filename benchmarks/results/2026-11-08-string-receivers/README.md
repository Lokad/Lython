# Shared string receiver declarations — R78

Retain the segmentation/search/padding migration. It reuses the existing
ReceiverMethod convention and borrowed operand storage: two provider classes and
instances, per-access receiver closures, immediate bound wrappers and their
positional argument packaging disappear. Python-visible methods keep bound values,
metadata and receivers. Three duplicate, now-unreachable provider arms are deleted.
Existing native no-argument/helper-return proofs remain; no new name-specific proof,
dispatcher framework, pool, owner, budget or option is added.

The migration fixes actual callback suspension under RunAsync. All48 new pending
index/width/truth cases fail against fully qualified baseline903eb346 because they
enter synchronous host I/O; they pass after migration, including cancellation,
receiver withdrawal, retained borrows and awaited cleanup. Additional tests cover
evaluation/binding, terminal refusal through suppression/finally, result retention
after script disposal and allowance reuse. The semantic golden is independently
checked with isolated CPython3.13. Existing keyword signatures are preserved;
pre-existing padding/search keyword permissiveness is a separate compatibility item.

## Measurements and source identities

Baseline: source38f53c33/core903eb346, fully qualified CI42439532114. Controlled
development core045517ea includes the family migration. Final retained source is
104fbe74 plus the archived patch, core63a7cc5e; it additionally deletes three
unreachable replace/startswith/endswith provider arms. All final production sources
and both measured patches/identities are preserved. Controlled elapsed results
belong to045517ea; the final cleanup was allocation-checked locally, not retimed
on the VM. Informational SourceLink alone does not establish source equivalence.

| Complete workload | Final allocation change | Controlled migration elapsed |
| --- | ---: | ---: |
| Text report using helper lower().rsplit(), sync | −21.26% | 14.54–16.82% lower |
| Same report, async API | −21.26% | 14.25–14.72% lower |
| Existing optimized lower().split(), async control | Neutral | 0.04–2.81% lower |
| JSON-to-Markdown report, async control | Neutral | −6.74 to +1.90%; inconclusive |
| Ordinary CSV, sync control | Neutral | 1.32–1.47% lower |

Allocation uses the existing same-process isolated-image driver: common CoreLib/
hash witness, normal GC/tiering,32warmups, five rotated batches of four complete
jobs per role. Goldens, balances, image isolation and owned cleanup all pass.
Desktop elapsed is diagnostic: original native-text async+21–25%, reportasync
+13–17%, final mixed controls and all unchanged-role drift remain raw. Quiet
controls do not explain or erase those original adverse observations.

B441 controlled five jobs finish in141.343s on the existing quiet Azure F4as_v7,
Ubuntu24.04.4, four EPYC9V45 vCPUs,16GiB/no swap, .NET11RC3. Two baseline/two
migration processes, normal GC/tiering, five-second warmup per role/job and seven
rotated150ms rounds. Report's unchanged baseline spread6.76% makes its elapsed
result inconclusive; native-text control stays near baseline. Coarse processCPU
supports the affected direction but is quantized, not precise kernel attribution.
This is not a CPython, universalCPU, pending-I/O, startup, RSS or embedding-tail claim.

Warm includes invocation, mediated RAM-file I/O, full stdout/file validation and
prior-result cleanup; compilation/startup/staging/reset/protocol/final cache teardown
excluded. Genuine suspension is tested separately. All source/core/auxiliary/
actualReady/runtime/golden/quiet/Closed/exit/join/fixture/archive gates pass. Every
worker joins; owned128MiB RAM fixtures are empty/unmounted before archive under
lease and independent local verification.180s owner+30s stop+30s observation margin.
Only redundant old JSON/sequence core hardlinks were retired, after verifying all
links/users, immutable VM archive and independent local copies. All original bytes
remain recoverable.240MiB OS floor and7MiB evidence headroom preserved.

Measured prototype passes161internal/406public per actual.NET11/10. After final
dead-arm cleanup,257focused public actual.NET11 checks pass; final full platform,
original Stress and installed-package delivery is recorded separately when complete.
Publication remains postponed. The residual helper gap (~3.09MB versus optimized
split~1.50MB) includes remaining activation/intermediate-text work; it is not all
attributed to one frame, and this milestone does not close the performance objective.

[Assessment](assessment.json) and [complete evidence](observations.tar.xz) preserve
all local variants/adverse controls, baseline failures/test assembly, focused logs,
source snapshots/patches, exact cores, unchanged driver and verified VM raw archive.
