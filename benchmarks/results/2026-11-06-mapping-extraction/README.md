# Mapping extraction and terminal field reads — retained with regression debt

2026-11-06 local, source `a39d6ba5` plus E3b2, frozen core `887002c2`;
runtime reference `612f7755`/frozen `f2078b20`. [Validation](validation.json):
zero-warning/error Release build,770 public/78 internal focused tests per actual
.NET11/10. Nine independent Python goldens and twelve ownership/containment cases.
Full combined view/mapping delivery remains pending. The prior view attempt failed
one stale negative fixture; its independent correction is committed separately.

Mapping matching now checks length before getters, extracts every key before
lookups, detects dynamic duplicates and captures values before subpatterns.
Rest copies the current mapping with native hash metadata, then removes matched
keys in order, preserving missing-key errors after callbacks. The raw structural
matched-key HashSet and filtering/rehashing rest loop disappear.

One admitted temporary key/value buffer includes existing scalar-slot headroom;
a private native seen set uses existing capacity admission and deterministic
unpublished cleanup. Larger payloads keep independent owners; surviving bindings
and results keep their own funding. Failed unpublished rest copies are refunded.
No tracing, object ledger, new backend, allowance or execution policy is added.
Conservative working charges may deny earlier. Pattern extraction keeps its
synchronous key/property boundary; guards and bodies retain real asynchronous I/O.

Native field-read callbacks also now check the current execution immediately on
completion, preventing another field read after swallowed refusal/cancellation.
General structural dataclass hash coupling remains separate and unresolved.

## Bounded source pair

Same quiet Azure Standard_F4as_v7, Ubuntu24.04.4, four AMD EPYC9V45 vCPUs,
.NET11 RC3 and CPython3.13.16 GIL release. Normal tiering/GC, existing frozen
worker `b94fa98c`/protocol2. B305 took85.11s for three jobs: five seconds warmup
per role/job, seven rotated150ms rounds, independent candidate/current-control/
prior-Lython/CPython processes. Quiet/source/golden/file/join/final-zero and
fixture-cleanup gates pass. These are diagnostic elapsed observations.

| Warm workload | Prior Lython | Candidate | Current control | CPython | Candidate/control versus prior |
| --- | ---: | ---: | ---: | ---: | ---: |
| Native mapping records,128 rows |0.158085ms|0.185813ms|0.182279ms|0.030946ms|+17.54%/+15.30%|
| Default dataclass keys,64 keys/four passes |0.292221ms|0.294986ms|0.287579ms|0.226427ms|+0.95%/−1.59%|
| Complete text helper summary,3,072 lines |2.258778ms|2.304517ms|2.190980ms|1.175488ms|+2.02%/−3.00%|

**The native mapping case regresses.** Both current roles agree on the direction;
spread1.90%, candidate/control/prior IQR1.04%/0.34%/0.56%. The script still takes
6.00× CPython. Retain Python correctness, admission and deterministic cleanup,
carrying the15–18% debt into an explicit compiled-match preparation milestone.
Do not restore the wrong phase ordering or unfunded working storage to recover it.

Dataclass current spread2.51% and text4.93% leave their source-pair differences
inconclusive. There is no broad whole-script CPU, allocation, startup, compilation,
RSS or embedding-latency claim. Incorrect callback/dynamic-key baselines are not
timed as speedups; the measured native scripts have equivalent prior outputs.

Warm clocks include invocation, mediated RAM I/O, complete validation and prior-
result cleanup. Compilation/startup/staging/reset/transport and final cached
program/result/process/fixture teardown are excluded. The representative fresh
CPython checkpoint remains separate.

[Raw observations](observations.tgz), [summary](summary.json),
[pre-code contract and discoveries](source-contract.md).
The archive retains exact runtime/test snapshots, identities, catalogs, all rounds
and machine/service receipts. Its hash is in the archive receipt.

## Correctness, controls and continuation

[Correctness history](correctness-history.tgz) retains original rest/duplicate/
phase failures, build nullability correction, two structural-field continuation
failures and final regressions. Field-read tests were subsequently moved to the
supported ReadField/ReadFieldAsync boundaries: future storage must not be required
to read dataclass fields. Runtime source and measured core remain byte-identical;
the final test-only refinement and hashes are recorded in validation.json.

Tests cover preflight before getters, native swallowed refusal/cancellation,
private scratch recovery on success/missing/subpattern failure, captured values
after mutation, native hash order, rest KeyError and large aliases surviving source
clear and genuinely suspended body I/O. Cleanup returns shared accounting and
unwinds structural/frame state without CLR collection.

[Four local fresh-process probes](hash-layout-diagnostic.json) demonstrate that
the text workload's four words get different native hashes/buckets in unchanged
processes. PyString uses HashCode.AddBytes; a four-key native table uses capacity4.
This is a plausible contributor to process differences, not causal proof for
elapsed results: the original VM roles did not record hash fingerprints. No fixed
seed or altered runtime hashing is introduced for benchmarking.

Next source gate: use existing compiled match-case/lowered/frame facts to retire
repeated key preparation and capture construction, preserving dynamic keys, binding
visibility, phase order, ownership and cancellation. Record compiler/runtime costs
and an explicit debt decision before promotion. Do not add a parallel matcher or
another measurement framework. Publication remains postponed.
