# Initial native-consumer consolidation comparison

R450/B852,2026-11-16UTC. **Private prototype; refine before promotion.** Reference
qualified R448 d77a8bdd/finale1204916; candidate5755fa15 at base3f98a725 plus exact
archived source. It removes ToSequence aliases, CheckedSequence/CheckedCollection,
AsyncSequence/AsyncEnumerator and their file, unused GeneratorOuter enumerable
fields and the orphaned eager Zip(object[]) overload. One existing cursor owns
native/CLR state and disposal. Initial124manual consumer loops add substantial
source; a simpler strongly typed foreach refinement is selected afterward.

| Complete source / phase | Elapsed change | Wider process CPU change | Reference / candidate process spread |
| --- | ---: | ---: | ---: |
| scripts.text-normalization.large / compile | -0.04..+1.89% | -2.31..+3.72% | 0.78 / 1.14% |
| scripts.text-normalization.large / warm | -7.88..-0.79% | -6.80..+0.25% | 7.19 / 0.48% |
| scripts.text-normalization.large / compile-run | -7.59..-3.15% | -7.24..-4.35% | 1.77 / 2.98% |
| scripts.numeric-squared-error.large / warm | -11.25..-4.28% | -10.33..-3.37% | 4.91 / 2.81% |
| scripts.csv-invoice.large / warm | -1.58..+1.52% | -2.70..+3.36% | 2.76 / 0.37% |
| scripts.report-render.large / warm | -0.78..+3.64% | -1.35..+3.05% | 0.85 / 3.57% |
| calls.positional.large / warm | -1.06..-0.32% | -2.35..+0.79% | 0.38 / 0.36% |

Seven jobs212.674s; same five-case catalog42531737, immutable32-file images,
unchanged scripts/fixtures/stdout/file goldens and benchmark worker. Two independent
processes per source,4s full-request warmup and five rotated~0.5s samples. Four
pairings of medians are ranges, not confidence intervals. All Ready/image/golden/
quiet/Closed/exit/join/archive gates pass; no users/RAM fixtures remain.

Warm includes mediated RAM I/O, full stdout/file checks and prior-result cleanup;
compilation/startup, fixture reset/protocol and final cache teardown excluded.
Fresh adds compilation/compiled-owner disposal; compilation measures construction/
disposal only. Wider CPU includes reset/protocol. Quiet F4as_v7/Ubuntu24.04.4,
four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 natural workstation GC/tiering. No
CPython timing, RSS, pending-I/O or embedding-tail comparison here. Frozen-worker
verify independently agrees with CPython for all five cases under both images.
An initial CLI omitted required arguments and stopped at usage before workers;
corrected command and original are retained, without changing source or images.

Normalization fresh elapsed/CPU favors candidate; warm has7.19%moving reference
copies and cannot establish a precise magnitude. Numeric source pairs also favor
candidate but have4.91%reference movement. CSV/report are inconclusive, preserving
adverse pairings. Call elapsed slightly favorable without a clean CPU result;
this does not prove R448's prior call regression fixed. Allocation is effectively
flat (<0.04%changes) with unchanged conservative peaks and budget baselines/cleanup.
Do not relabel consolidation as an allocation gain or inclusive sample savings.

Both actual Windows targets pass1,561internal/4,989public focused checks. This
snapshot's full local .NET11 ordinary passes4,806internal/17,935public. Later
review found a prototype-only partial-cursor bug: list extension unwrapped its
original source and restarted list/array input. Two regression cases fail with
three items instead of the remaining two. The selected refinement preserves cursor
position, uses known counts only as hints, and snapshots only an unstarted list.
It also replaces the124manual loops with standard foreach and a strongly typed
cursor enumerator, removing source scaffolding. No final-source performance or
platform qualification is inferred from these initial results.

Native materialization's initial duplicated loops were removed before this
measurement: one existing CLR drain pair consumes the consolidated cursor, keeping
capacity rules and admission order. The briefly proposed initial-scratch-before-
acquisition contract is withdrawn. CLR cursor bridges are single-pass; independent
enumerations create new cursors. Dictionary versions capture at acquisition.
Managed stack, current cancellation, terminal first refusal, aliases and awaited
cleanup remain. No new owner/controller, cache, policy, tracing, GC or unsafe API.

Full31 auxiliary hashes are in identity.json; unchanged auxiliary bytes are the
frozen/reference files in the committed [R448 procedure archive](../../2026-11-16-compact-iteration-result/procedure-and-checks.tar.xz).
Only the two core DLLs are duplicated here. Exact production sources, local checks,
original overload/compiler failures and command conditions are preserved. Only
five old working JSON replicas totaling14,253,749B were retired under idle lease,
after committed archive/member equality and fresh UID/path/hash/all-alias proof.
Selected images/toolchains/canonical evidence and254/252MiB floors remain.
Terminal cached forever; no unchanged requery. Publication/tagging remain held.

[Summary](summary.json), [allocation](allocation-summary.json), [raw](observations.tgz),
[exact candidate sources](candidate-sources.tar.xz), [image/source hashes](identity.json),
[cores/checks/procedures](cores-and-procedures.tar.xz).
