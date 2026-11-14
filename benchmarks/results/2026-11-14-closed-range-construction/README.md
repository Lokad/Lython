# Closed range construction — ownership refinement

R361, 2026-11-14 UTC. Initial frozen `98df40e9` against fully qualified
`98acaa5b` / frozen `a4bfbfd8`, including the independent matrix-helper correction.
**Initial prototype cannot be retained:** its shared range constructor has mutable
hierarchy/descriptor/NewSlot caches, including an `OwnedTypeHierarchy.Owner`
execution reference. Source inspection requires retaining per-execution constructor
identity. No measured heap-leak, contention or tail-latency claim follows.
The corrected source is separate and still needs final confirmation/delivery.

The design shares the existing range factory over already evaluated argument
storage. Exact integer/bool bounds complete without binder packaging or promoting
the whole remaining native helper. Python index protocols/keywords/starred calls
retain ordinary binding. One range algorithm/owner remains; no ordinary-frame
scalar storage, new call controller, pool, public option or numeric representation.

## Initial complete-script comparison

Quiet Azure F4as_v7, four EPYC 9V45 vCPUs, 16 GiB/no swap, Ubuntu 24.04.4,
.NET 11 RC3, normal GC/tiering, fixed workers/optional 0.4 adapter. Four fresh
processes/job, 5-second full-request warmup, five rotating 0.5-second batches.
B740 completes seven jobs in 239.162 seconds, all image/golden/budget/quiet/join/
archive gates. No users/fixtures remain. Raw SHA256
`863c57ed6a7554adc80b5b43a1036673ab0c774bb5317657271ce3d1561711a6`.

| Complete script / phase | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| 100,000-add inside-range counter / warm | −32.06..−29.64% | −32.25..−30.86% |
| Same counter / compile-to-result | −31.02..−29.44% | −30.81..−28.24% |
| Outside-range counter / warm | +0.34..+3.31% | +0.00..+2.89% |
| 4,096-row CSV while validator / warm | −0.33..+0.74% | +0.00..+0.95% |
| JSON helper summary / warm | −4.08..+1.74% | −2.96..+2.13% |
| Short/zero helpers / warm | −5.34..−4.82% | −6.36..−3.92% |
| Helpers exposing every intermediate / warm | −3.56..+3.12% | −3.45..+3.98% |

All process-median pairings, not confidence intervals. JSON reference spread
3.98% and observed-helper reference spread 6.08% limit those conclusions; retain
all adverse values. Outside-range has a small adverse direction. These are runtime
source pairs with identical algorithms, not a gain from moving source code.
No current CPython ratio or universal improvement is inferred.

Warm includes stdout/file validation, mediated RAM I/O where present, and result
cleanup; excludes compilation/startup, fixture reset/protocol and final compiled/
cache disposal. Fresh adds compilation/disposal. Wider CPU includes reset/protocol.
Counter has no file I/O. This runtime change does not alter frontend algorithms;
fresh is not a compilation-only speedup.

Four separate allocation processes/reversed order: inside counter 7,518,530 to
3,215,090 B/run (−57.24%); CSV approximately 3,033,904 to 3,033,811–15 B/run
(effectively flat). 548 focused actual .NET 11/10 checks, 68 isolated CPython
3.13.2 controls and 14 frozen-worker goldens pass. These checks describe the
initial source; corrected metadata has its own source/check identity.

## Ownership correction and preserved failed conditions

Restore execution-owned range constructors; classify the actual private builtin
constructor independently of guest naming/rebinding, as existing closed native
operations do. Keep the span factory and whole-body promotion removal. No new
global cache, ownership scope or policy. Initial timing remains useful design
evidence, never final-source qualification. Corrected-source checks additionally
cover builtin metadata/isolation, terminal refusal and actual I/O/cancellation.

The first image preflight expected already retired 6c35; a diagnostic also finds
510a already absent. Both stop before mutation/root/mount/worker. A local archive
member-name mistake also precedes remote work. Another space gate refuses before
mutation because old profile duplicates had already been retired. The successful
preflight retires only complete byte-proven b868 aliases and exact committed raw
result duplicates, preserving A4/new image, catalogs/auxiliaries/toolchains and
254/252 MiB floors. All logs/conditions survive; no timed owner is restarted.
The first development-source snapshot also correctly rejects a raw Git/archive
newline mismatch; the raw qualified source plus intentional guard is preserved
before any build/measurement. None is a runtime failure or missing adverse sample.

[Initial identity](initial/candidate-identity.json), [timing](initial/timing-summary.json),
[allocation](initial/allocation-summary.json), [source gate](initial/source-gate.json),
[all sources/binaries/checks/raw/failures](initial/sources-checks-and-observations.tar.xz).
Publication remains held; substantial whole-script and independent regex work continue.

## Execution-owned constructor comparison

Frozen `738a12da` restores per-execution constructor state. 536 additional focused
actual .NET 11/10 checks and 70 isolated Python controls pass, including builtin
metadata/isolation, aliasing, real I/O, cancellation and terminal refusal. One new
named-range test appeared in net11's seven-case list but incremental net10 listed
six: force rebuilding actual net10 runs all seven successfully. Original logs/TRX
remain; the stale-input cause is not independently established. All 16 frozen-worker
goldens pass. One separate allocation process per role still shows counter
−57.23% (7,518,842 to 3,215,498 B/run), CSV exactly flat in this diagnostic.

B741's three-job confirmation finishes in 105.678 seconds with all gates; no users
or fixtures remain. Same normal settings and boundaries as B740. Raw SHA256
`30035a844759e3bfa28ad0c69e486a9fd4b38162e210fd8477c183f2ee71d742`.

| Complete script / warm | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| Inside-range counter | −31.14..−29.70% | −31.03..−29.72% |
| Short/zero helpers | +5.16..+8.53% | +6.17..+8.51% |
| Helpers exposing every intermediate | +0.51..+1.95% | +2.75..+4.01% |

The short-helper adverse direction remains despite the counter win. Source places
range classification before every ordinary function call; the short control makes
8,192 such helper calls. This identifies unnecessary work, not a proven causal
CPU share. The selected completion removes that ordinary-router addition entirely,
restoring its baseline bytes. Only native-prefix range construction causes the
whole-body promotion cliff; ordinary execution already uses the same range factory
through existing binding. No extra general-call probe or new selection mechanism.
Final source/checks/comparison and retention remain separate from these timings.

[Identity](owned-constructor/candidate-identity.json), [timing](owned-constructor/timing-summary.json),
[allocation](owned-constructor/allocation-summary.json), [checks](owned-constructor/focused-checks.json),
[test-input correction](owned-constructor/test-input-correction.json),
[sources/binary/raw/conditions](owned-constructor/sources-checks-and-observations.tar.xz).
