# Fixed immutable builtin definitions

2026-11-13 UTC. Frozen7ee65002 against fully qualified6237552b/485e008e.
Share the fixed `object`/`type` definitions instead of rebuilding descriptors,
types and MRO storage for every invocation. Mutable builtin dictionaries and
callable hierarchy caches stay per execution. No new registry, lazy cache, owner
layer, option, execution engine or public API is introduced.

Shared definitions contain no caller, host, token or memory owner. Their namespace
views are admitted under each actual caller rather than retained on a shared type.
Repeated introspection can retain more minor wrapper storage until owner cleanup.
First initialization is process work; it is excluded from the warm measurements.
Existing coarse startup admission remains conservative.

Centralize immutability before internal dataclass metadata changes. The baseline
incorrectly permits `dataclass(object)` to mutate the builtin; corrected object/type
cases match isolated CPython. Remove the redundant `staticmethod` around builtin
`object.__new__`; raw namespaces now expose the builtin method. Three inherited
slot lookups reuse the canonical binder, retaining genuine descriptor binding.
Initial/refined failures exposed those old assumptions; their logs survive with
clearly labelled reconstructed source phases. Final94internal/683public Release11
checks pass, including metaclasses, original slot owners, mutable namespaces,
retained results, suspended/concurrent shared-budget runs, first refusal,
cancellation, awaited cleanup and reuse. The original full delivery then
[fails one existing factory-slot case per platform](delivery-failed/README.md).
The missed partialmethod consumer is corrected separately through canonical
binding; these earlier7ee timings do not qualify that corrected source.

## Allocation and complete scripts

The same private inspector,512 calls after256 warmups, measures builtin-factory
allocation on the current thread:10248→7680B/invocation, **2568B removed**.
This is25.06% of that factory, not complete execution. Its short local elapsed
observations are adverse(3.8274→4.3690ms) and diagnostic; no CPU claim follows.
Original factory clocks and the uncorrected decorator result remain archived.

B682 uses the existing quiet F4as_v7/Ubuntu24.04.4 VM, fourEPYC9V45vCPUs,
16GiB/no swap, ordinary.NET11RC3 GC/tiering, fixedfec79worker and adapter0.4.
Existing generator config181B/manifest4107B join the unchanged invoice/report
catalog. Two processes/source,5s warmup(8report), five rotated500ms rounds;
five jobs finish193.673s with all identities/goldens/balances/quiet/join/archive gates.

| Complete script | Elapsed change | Wider process CPU change | Reference/candidate spread; max IQR |
| --- | ---: | ---: | --- |
| Config warm |−1.83..−0.36%|−1.45..+0.07%|0.02/1.47%;1.34%|
| Config compile-to-result |−3.39..−1.25%|−5.28..−3.87%|2.10/0.07%;4.34%|
| Manifest warm |−0.97..−0.26%|−1.80..+0.30%|0.53/0.18%;2.96%|
| Invoice control warm |−1.80..+0.02%|−3.33..−1.49%|0.29/1.53%;1.78%|
| Report control warm |−3.63..−1.82%|−3.24..−0.54%|1.28/0.56%;2.31%|

Ranges span all four process-median pairings, not confidence intervals. Warm includes
mediated RAM I/O/full stdout+file validation/prior-result cleanup; compile-to-result
also compiles and disposes the compiled owner. Startup/staging/reset/protocol/final
cache teardown are excluded. Wider CPU includes reset/protocol. Compilation alone,
whole-execution allocation, RSS and embedding latency are not measured here.

Retain for fewer per-invocation constructions, correct immutable definitions and
caller-owned views. Config compile-to-result/report have modest supported direction;
other CPU evidence is small or inconclusive. Do not attribute every observed change
to the factory or infer a universal gain/new CPython ratio. This milestone does not
complete the substantial whole-script performance objective.

## Preserved failed conditions

B681 completes only config warm, then rejects my unsupported protocol label `fresh`;
the worker expects `compile-run`. Its partial warm range−0.16..+0.26% is effectively
flat, with0.20/0.21%control spreads and2.30%maxIQR. It lacks a final quiet gate and is
not a complete performance result. All users join; stderr/partial observations/RAM
evidence verify before unmount. B682 changes the driver label, retaining exact bits.
Its first staged-launch check incorrectly hashes reserialized LF text against Windows
CRLF bytes; no collection starts. Resume validates every staged file's actual SHA,
both exact images and catalog under a fresh idle lease. No identity gate is relaxed.

Only canonically verified obsolete91core aliases and the redundant B680 VM archive
are retired. Exact sources/bits/qualification/adverse evidence remain; active485/7ee
images survive and free-space floors stay unchanged. All B682 users join and archive
SHA verifies before RAM unmount. Publication remains postponed.

[All medians](summary.json),[complete raw observations](observations.tgz),
[partial original](initial/incomplete-summary.json),[original raw failure](initial/incomplete-observations.tgz),
[factory observations](factory-observations.json),[exact sources/bits/checks](source-checks.tar.xz).
