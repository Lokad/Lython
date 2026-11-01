# Immutable text-window trial rejected (B64)

2026-11-01. **Prototype removed.** Ordinary text misses the frozen7.5% elapsed
improvement with valid controls. Helper text is slower and inconclusive at73.64%
candidate IQR. No speedup, allocation saving or causal mechanism is claimed.
Production remains delivered35d8942c; four independent storage regressions remain.

## Hypothesis, ownership and semantic gate

PyString previously stores a whole byte array; contiguous slicing allocates/copies.
The [candidate](source.patch.gz) changes its payload to ReadOnlyMemory<byte> windows
and removes those copied-array allocations for governed contiguous slices. Native
encoding/copy/index/hash/cache operations respect relative window coordinates.
Unowned/foreign memory, noncontiguous slices and ASCII interning retain their
existing semantics. This is distinct from earlier casing/whitespace/JSON trials.

A first view preflights its128-byte shell plus32 bytes and the entire source backing
through the existing governor, independently of the private producer's charge.
One intrinsic funding flag lets descendants reuse that admitted backing. View
rollback refunds only shell/caches; original producer/window/cache-key refunds
can finish while the separately funded backing remains with the batch/result.
No refcounts, identity tables, traces, GC, owner API, public option or new framework.
Proposed tradeoff: early denial/overlapping funding for a tiny view of a large
source, and conservative backing retention until the existing batch owner ends.
No retries after refusal; new views are admitted before allocation/publication.

[Criteria before code](pre-code-plan.md), [fresh identities](candidate-identity.json).
Candidate zero-warning Release checks pass1,411 native/4,867 public per actual
.NET11RC3/10.0.8. Four new tests cover actual retained backing after private-source
refund (direct/nested), refused creation preserving source/rollback/first error,
and unknown partial-memory independence. They inspect values and funding, not
private flags or a requirement that payloads share. All pass original copying too.

Four initial failures inject refusal at the removed copied-payload allocation.
The [emission fixture](../../../tests/Lokad.Lython.Tests/TextReadEmissionOwnershipTests.cs)
now permits only required working capacity, then denies returned-value storage
whether copied or shared. Typed error, first-denial latch, reserved/committed
rollback, reader ownership and later refusal remain. The exact old payload-length
assertion becomes a minimum admitted string-shell assertion; no timeout, semantic
or cleanup check is weakened. Initial failures and all final logs are preserved.

## One fixed complete-job comparison

Baseline726466ba, same production35d8942c; fresh core526b0a18. Candidate that source
plus patch, cored44f1c89. Core/worker/test/probe copies match after nonincremental
build; auxiliary hosts/adapters are byte-identical and all jobs are non-regex.
Dedicated Ubuntu24.04.4 Azure F4as_v7, four EPYC9V45 vCPUs/16GiB, .NET11RC3.

Separate images share CoreLib/actual hash witnesses and ordinary GC/tiering.
128 interleaved warmups/image,2s settle/case, seven rotating32-job batches. Full
warm public Run includes fixture reset, mediated I/O, full stdout/file goldens,
result disposal and balances; compile/startup excluded. [Exact sources](catalog.json).
Text has3,072 lines, CSV4,096 records; JSON is maintained large scale1,024.

| Complete job | Original ms | Candidate ms | Original control ms | Candidate vs both | Status |
| --- | ---: | ---: | ---: | --- | --- |
| CSV helpers | 8.10717 | 8.16353 | 8.15498 | +0.70% /+0.10% | Valid contrast controls |
| Ordinary text | 5.62722 | 5.53419 | 5.48003 | -1.65% /+0.99% | Valid controls; misses gain gate |
| Text helpers | 6.51694 | 6.65816 | 6.46339 | +2.17% /+3.01% | Inconclusive: candidate IQR73.64% |
| JSON | 7.64359 | 7.58466 | 7.60019 | -0.77% /-0.20% | Valid contrast controls |

Every original drift is within3% (largest-2.62%). Other relative IQR0.57–2.49%,
all batches>=172.47ms. The failed helper dispersion remains; no sample/outlier
is removed. Ordinary text already fails the required gain with valid controls.
No causal inference about layout, sharing, funding or GC; no favorable retiming,
threshold change, CPython ratio, phase subtraction or allocation-only claim.

Quiet/hash/golden/balance/owned-cleanup gates pass. One45-second UTC window inside
60s, service exits0/inactive/dead, MainPID0/empty group and VM lease free.
[All observations/gates](summary.json), [driver](Program.cs), [collector](collect.sh),
[logs](validation-logs.tar.gz) and [raw results](raw-results.tar.gz), SHA256
`3d4fcade408917443377bbcc9a1d8b9e5a4c99ff065644b633d47e5dd6c2e491`,
preserve complete evidence.

Original PyString restored byte-for-byte; window representation, funding flags,
constructor and backing grants are absent. Fresh core/worker/test/probe copies
match526b0a18. The same1,411/4,867 focused checks pass per actual runtime after
restoration, including new regressions and the representation-independent fixture.
No full platform/package replay for unchanged production. Cached runtime delivery
41319281716 remains green; no publication, tag or producer polling. Stop this trial.
