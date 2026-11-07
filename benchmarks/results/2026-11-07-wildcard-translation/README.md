# Native UTF-8 wildcard translation — 2026-11-07

R51 replaces the complete intermediate representation of `fnmatch.translate` and
`glob.translate`: decoded inputs, copied glob segments, UTF-16 regex builders,
per-class builders and final encoding. Both use the original UTF-8 and one
governed output builder, with input-sized initial capacity and ordinary funded
growth/overlap. Completed output takes over its backing. Matching itself and the
provider contract are unchanged; no producer release is required.

Baseline production is 80833f6e, frozen R50 core (informational revision predates
that commit); candidate is the exact source inventory in [focused.json](focused.json).
The [identity](identity.json) records cores, unchanged worker and reused driver.
No claim that informational metadata alone proves source/binary correspondence.

Two complete scripts read 512 filename/path patterns, translate Unicode classes
and recursive paths, retain a result list and write a report. Independent isolated
CPython 3.13.2 supplies complete output/file goldens. Three original CSV/text/JSON
scripts are unchanged controls. Windows newline mismatch initially failed the
unchanged baseline before any samples; its exact catalog/source/error remains.
Explicit file newline and canonical stdout corrected the fixture.

Normal .NET 11 GC/tiering, isolated images sharing CoreLib/hash seed/process GC,
32 warm jobs per image, five rotated rounds of four complete jobs. Warm includes
host fixture reset, mediated I/O, complete golden checks and result disposal;
compilation/startup/final cached teardown are excluded. This is a desktop
allocation diagnostic, not a backend or CPython timing qualification.

| Workload | Refined sync allocation | Refined async allocation |
| --- | ---: | ---: |
| fnmatch report | −25.029% | −25.017% |
| glob report | −31.797% | −31.809% |

Original pipeline allocation improves 21–23%; reserving useful initial capacity
removes repeated small admission/growth phases. Both stages are preserved.
Unchanged control allocation remains within 0.01% in the refined diagnostic.
Elapsed is **inconclusive**: initial async glob is about 8% adverse; refined
fnmatch sync ranges +0.59–23.83% with 23.1% identical-before drift, and async glob
ranges −0.96–+7.09% with −7.51% control drift. Do not transfer desktop gains or
declare that its cause has been explained. The controlled source pair below
supports affected elapsed improvement separately from these unstable observations.

## Quiet source pair

B407 completed three warm jobs in 86.62 seconds on the dedicated Azure F4as_v7,
Ubuntu 24.04.4/.NET 11 RC3. Two R50 and two R51 processes, five-second warmup per
role/job and seven rotated 150 ms rounds; normal GC/tiering and unchanged worker/
optional 0.4 adapter. Complete stdout/files, identities, quiet endpoints, Closed/
exit/join, owned RAM fixture cleanup and archive-under-lease verification pass.

| Workload | R50 ms | R51 ms | All pairings | R51 / R50 role spreads |
| --- | ---: | ---: | ---: | ---: |
| fnmatch report, 512 patterns | 0.479648–0.479863 | 0.415843–0.420506 | −13.34 to −12.33% | 1.11% / 0.04% |
| glob report, 512 patterns | 0.780896–0.791737 | 0.657754–0.659450 | −16.92 to −15.55% | 0.26% / 1.39% |
| Ordinary JSON control, 110,592 B | 3.898289–3.972189 | 4.137420–4.187951 | +4.16 to +7.43% | 1.22% / 1.90% |

Retain the simplification, allocation reduction and affected elapsed improvements.
Explicitly accept unresolved **4–7% adverse ordinary-JSON control debt**; its parser
is unchanged, but that does not establish the cause or make the observation
irrelevant. Coarse external process CPU moves consistently, without isolating a
method or proving embedding latency. No async elapsed or universal speedup claim.
The complete script/mediated RAM I/O/validation/prior-result cleanup boundary
excludes compile/startup/staging/reset/transport/final cached teardown.

B406 previously completed 23 CPython jobs, then all four wildcard fixture
directories disappeared during warmup. First error: `Invalid intermediate
directory`, followed by four cleanup `FileNotFound` errors. Cause is unestablished.
There are no sampled source pairs or final quiet gate; these partial observations
do not replace the primary tables. Failure/partial data were archived under a
maintenance lease after all users ended. B407 uses a collection-owned nested RAM
parent on the same filesystem; it does not alter the worker, runtime or inputs.

[B406 original failure and partial observations](b406/failed-observations.tgz),
[failure receipt/journal](b406/failure-preservation.json),
[B407 raw archive](b407/observations.tgz), [summary](b407/comparison-summary.json)
and [receipt](b407/archive-receipt.json) retain every adverse result.

B408's corrected CPython collection then failed with `ENOSPC` while publishing
a snapshot: the journal records five completed jobs and the 524,288-byte JSON
is truncated. This is a storage failure, not evidence against the runtime design.
[Original bytes and journal](b408/failed-observations.tgz) and
[maintenance receipt](b408/failure-preservation.json) remain; no primary table is
promoted. Exact inactive test `bin`/`obj` from three old checkouts were offloaded
to local persistent storage and verified byte-for-byte before retirement under
a fresh lease. Source, benchmark images and evidence were kept. The
[storage receipt](storage-maintenance.json) records the archive and restored
444.6 MB headroom. Future collection requires 256 MiB free, writes snapshots
atomically and owns a separate tmpfs fixture mount outside shared IPC storage.
The first fixture-loss cause remains unestablished; this does not explain it.

Focused Release checks pass on actual .NET 11 and .NET 10: 29 internal and 133
public per target, plus two real in-flight separator cancellation checks per
target. They cover Unicode/ranges/anchors/recursive-hidden paths, provider-free
translation, retained result funding, first refusal, scratch cleanup and allowance
reuse. The dedicated cancellation worker is joined before refund; its duration
does not establish a latency guarantee. Full Windows/Ubuntu Release, actual
.NET 11/10, original Stress and installed core-package qualification passed in
[CI42383291690](delivery-ci.json) for source55e510ff. Earlier R50 qualification
is separate; no unchanged qualification is repeated.

[Initial observations](local-summary.json), [refined observations](refined-local-summary.json),
[decision and archive hash](decision.json), and [development evidence](development.tar.xz)
retain source/cores, checks, all raw diagnostics and original failed conditions.
Conservative initial output capacity changes tiny-budget thresholds; output and
replacement storage remain admitted. No startup, isolated-method CPU, RSS, tail, matching speed or
universal performance claim. Publication remains postponed.
