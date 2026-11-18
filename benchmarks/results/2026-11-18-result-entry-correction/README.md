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
Full replacement delivery and the short affected/control timing comparison are
pending. This correctness repair has no performance claim. No public API change.

[Exact source](candidate-sources.tar.xz), [identity](frozen-identity.json),
[patch](correction.patch.gz), [checks/procedures](focused-checks-and-procedures.tar.xz).
Earlier c036 timings remain specific to that revision. Publication postponed.
