# One empty/unbound local-storage state

R185 / B533, 2026-11-10. Retained source above32d1ea1a, not final-qualified.
Reference is R184 coref8995f65; candidate26d43e66. Exact sources/hashes and failed
conditions are retained. This is not a CPython timing comparison.

Replace UninitializedLocal with internal null, retain PyNone for guest None,
and remove per-invocation Array.Fill over already-cleared final workspace.
Local reads raise Python errors before checking the temporary-operand invariant.
Unbound class cells need no synthetic namespace placeholder. Nullable cell reads,
generator refresh, deletion and introspection share the same state. No new owner,
pool, cache, execution route, policy, counter or tagged-value layer is introduced.

## Complete-script comparison

QuietF4as_v7 / Ubuntu24.04.4 / .NET11RC3, ordinaryGC/tiering. Three synchronous
warm jobs, four independent processes(two/source),5swarmup/role, seven rotated
400msrounds. Same workers/adapter/auxiliaries/catalog/goldens; onlycorechanges.

| Workload | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| scripts.csv-validated-records.large | -4.91 to -0.28% | -4.71 to -1.22% |
| scripts.file-search.large | -1.53 to +3.49% | -1.26 to +1.94% |
| scripts.text-summary-helpers.large | -3.71 to +3.66% | -3.79 to +4.93% |

Ranges span all four process-median pairings, not confidence intervals. CSV
reference spread3.98%, candidate0.70%. Search/text controls overlapzero; no
equivalence, universal gain or CPU attribution is established. Wider100Hz process
CPU includes reset/protocol. Warm includes mediatedRAMI/O, full output validation
and prior-result cleanup; compile/startup/reset/protocol/final caches excluded.

Desktop warm allocation is effectivelyflat and compileallocation exactlyflat.
Original desktop search elapsed+9.03–18.51%, variable text/report and compilation
observations remain diagnostic and preserved. Quiet search does not reproduce
that point; its small possible cost remains unresolved. No causal explanation or
claim of no regression follows. Compilation is separate, including construction
and disposal, with no compiler logic changed.

## Correctness and decision

The first semantic run failed15unbound-name cases(2591passed) because the
nonempty temporary indexer rejected an empty local before Python diagnosis.
The correction reads the existing backing slot, raises the original Python error
at its read span, then enforces the temporary invariant. The failing source/logs
survive. Initial nullable compiler diagnostics and setup mistakes also survive.

Corrected focused checks pass3124public/432internal per actual.NET11/10, including
closures/generators/classes/super/annotations, cancellation, terminal refusal,
retained results and shared-budget cleanup/reuse. Three additional None-versus-
deleted-cell cases match isolatedCPython3.13.2 and also pass against unchanged
R184 runtime(42public cases eachtarget). These are independent semantic coverage,
not tests requiring a particular internal architecture. Full delivery of commit3a413c59 is GREEN: Windows/Ubuntu, actual.NET11/10, original Stress and installed Release consumers. [Cached terminal result](qualification/delivery-ci-status.json).

All3jobs finish112.706047518s, identity/output/balance/quiet/join/unmount/owned-
archive/localSHA checks pass. No remaining VM users or RAM fixtures. One exact
redundant withdrawn core and two compressed copies were retired only after tracked
canonical-byte/hardlink/receipt proofs under an idle lease. First recovery aborted
without mutation because a pathname substitution renamed a canonical candidate;
its source/error and corrected exact configuration survive. No worker run restarted.

**Decision:** retain the simpler empty-storage invariant and removed per-call
initialization, supported by modest affected CSV CPU improvement. Allocation
savings are negligible. Accept unresolved small control debt; do not count this
as completion of the broader performance objective. Batch platform/stress/package
qualification at the next retained-source delivery boundary; publication postponed.

[Raw observations and cores](observations.tgz), [summary](summary.json),
[desktop measurements, failed conditions and focused logs](development.tar.xz),
[source delta](source.patch), [source identity](final-source.json).
