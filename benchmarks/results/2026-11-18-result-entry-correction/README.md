# Result-consumer entry failure correction

R518, 2026-11-18 UTC. Corrected frozen **84b1a159** versus failed full-delivery
**7db1de02 / c0367d98**. Both local stores and discarded results use the existing
consumer continuation when cancellation/refusal is already pending. The normal
instruction entry reports the error outside the producer's operation handler.
Running consumers use the same nonthrowing predicate; no new authority/counter.
Pending cleanup, first refusal identity, origin/context and side-effect order remain.

Original full failure is retained in [failed-delivery](failed-delivery/README.md).
Assigned-result cases now join the existing discarded-result regressions. Both
actual .NET 11/10 pass 145 internal / 78 public focused Release checks, including
24 routing cases per framework. Nonincremental solution build, 13 independent
goldens/allocation/budget reuse and the exact four timed-case goldens pass.
Full replacement delivery 44250743374 is pending. No public API change.

Short B947 changed-source comparison: four jobs / 187.83s, all image/golden/quiet/
join/archive gates pass. Original text elapsed 0.81–1.97% higher, CPU straddles
zero; helper text elapsed 1.08–2.15% and CPU 1.20–2.93% higher. Floating elapsed
straddles zero, CPU 0.91–3.51% higher with 3.21% reference process disagreement.
Positional elapsed 0.89–2.87% lower, CPU uncertain. Allocation and accounted peaks
remain effectively flat; budget reuse passes. Preserve this measured regression
debt: correctness requires the repaired interruption path; do not revert it for
the earlier timings. Reassess costs at the next coherent operand/call milestone,
without another unchanged campaign. Earlier c036 comparisons remain separate,
not additive. [Summary](summary.json), [raw](observations.tgz),
[procedures](measurement-procedures.tar.xz).

Only the exact archived superseded716176a8 image was retired, with fresh idle
lease/path/UID/hash/all-four-alias verification. The initial incomplete alias
proof failed without mutation and remains preserved. Current2ede/a71/c036 images,
catalogs/toolchains, unmatched evidence and existing254/252MiB floors remain.

[Exact source](candidate-sources.tar.xz), [identity](frozen-identity.json),
[patch](correction.patch.gz), [checks/procedures](focused-checks-and-procedures.tar.xz).
Earlier c036 timings remain specific to that revision. Publication postponed.
