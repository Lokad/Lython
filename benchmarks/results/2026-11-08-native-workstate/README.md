# Native workstate dispatch — R79 retained

Retain the native-first dispatch change. Existing native zero-argument string
operations, dictionary views and exact list/tuple iteration no longer force a full
ExecutionContext. They use the existing owned slot workstate and execution services.
Two duplicate ordinary native-call branches and premature context guards disappear.
Other shapes still materialize a context before guest protocols/actual suspension;
admitted iteration positions move once with the workstate. No new name-specific
native-body proof, backend, dispatcher framework, pool, arena, owner or option.

Measured baseline is source5b9d569a/core06117d87; candidate is the archived R79
patch/core18b563e9. Full production inventories, source snapshots and exact cores
are preserved. Baseline source passes Windows/Ubuntu actual11/10, original Stress
and installed consumers, [cached CI42443700988](../2026-11-08-string-receivers/delivery-ci.json).
The original preparation manifest's pending wording predates observing completion.
Candidate passes140internal/326public per actual11/10; final full delivery is
recorded separately after the retained commit. Publication remains postponed.

## Complete workloads

| Workload | Allocation change | Controlled elapsed change |
| --- | ---: | ---: |
| Ordinary lower().rsplit() text helper, sync | −42.10% | 11.82–14.37% lower |
| Same helper, async API | −42.09% | 14.46–16.06% lower |
| Text helper with an ordinary guard, sync | −41.99% | 10.48–12.21% lower |
| Guarded helper, async API | −41.98% | Not separately timed on VM |
| Existing fused text helper, sync control | Neutral | −1.26% to +2.84%; inconclusive |
| Ordinary CSV, sync control | Neutral | +0.25% to +1.92%; accepted debt |
| JSON-to-Markdown report, async control | Neutral | −0.32% to +1.06%; near baseline |

The same-current-core selection gate independently verifies helper/inline stdout
and all files under CPython3.13. The ordinary helper costs~3.09MB/report versus
inline~1.79MB; the existing fused split helper costs~1.50MB. This separates the
large invocation difference from the smaller fused-text storage difference;
it does not attribute every byte/CPU sample to one object. Final source-pair
allocation uses the existing common-CoreLib/hash isolated-image driver,32warmups,
five rotated four-job batches/both APIs, normal GC/tiering. All golden, balance,
image-isolation and fixture-cleanup gates pass. Desktop elapsed is diagnostic;
original adverse CSV/report controls and unstable affected/native observations stay.

B443 completes six jobs in168.441s on the quiet F4as_v7/Ubuntu24.04.4, four
EPYC9V45 vCPUs/16GiB/no swap/.NET11RC3. Two old/two current processes,5s warmup
per role/job, seven rotated150ms rounds, normal GC/tiering, quiet endpoints.
Native-text's identical-current spread3.70% makes its elapsed inconclusive.
CSV's consistently adverse small elapsed range is consciously accepted debt;
flat/lower coarseCPU does not erase it. Report stays near baseline. Affected
coarseCPU agrees in direction but is quantized, not precise in-job attribution.
Earlier R76 float-async debt remains separate. No universalCPU, CPython, startup,
RSS, pending-I/O or embedding-tail improvement is claimed.

Warm includes invocation, mediated RAM I/O, complete stdout/file validation and
prior-result cleanup; compilation/startup/staging/reset/protocol/final cache teardown
excluded. Genuine suspension/cancellation is tested separately. All source/core/
auxiliary/actualReady/runtime/golden/quiet/Closed/exit/join/fixture/archive gates pass.
Workers join and owned128MiB RAM fixtures are removed/unmounted before archival
under lease and independent local verification.210s owner+30s stop+30s observation
margin;240MiB OS floor+7MiB evidence allowance. One byte-identical worker library
was atomically hardlinked to reclaim duplicate physical bytes: all original paths,
cores, sources and archives remain, with independent local hash proof.

New small regressions cover Unicode/branches, nested list/tuple break/continue,
escaping view aliases, native-loop-to-async state transfer, awaited cancellation,
terminal refusal through finally returns and nonallocating prefix-loop cancellation.
Closures, function defaults, traceback, protected loops and existing callback/budget
coverage also pass. No counters or tests of exact internal checkpoint locations.
The whole-script objective remains open; next work needs current cost attribution,
not another name-specific shortcut or repetition of completed qualification.

[Assessment](assessment.json) and [complete evidence](observations.tar.xz) retain
the selection gate, both actual-target logs, sources/cores, all adverse controls,
independent goldens, exact unchanged driver and verified six-job VM archive.
