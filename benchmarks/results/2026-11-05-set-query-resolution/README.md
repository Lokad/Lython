# Actual-hash candidates and shared set resolution — retained with debt

2026-11-05, combined D64B1/B2 source pair against corrected A/C/D `aec4f785`,
frozen reference `47833d32`. B1 is `f64bc7c5`; final B2 source is recorded by all
six source/test hashes and frozen core `8050cc0f`, built before commit metadata.
The [validation record](validation.json) verifies those files. Qualification here
is focused Release on both actual runtimes, **not full platform qualification**.
The earlier A/C/D [GREEN delivery](../2026-11-05-keyed-validation/delivery-ci-status.json)
qualifies its own source only. Publication remains postponed.

Equality candidates now use actual Python hashes. The structural reverse index
remains for stored-entry/hash reuse; unique insertion-ordered positions permit
one final candidate array. Removed candidate HashSet, sorting, intermediate lists,
missing-index scan, duplicate registrations and redundant selector arguments.

One resolver replaces the three contextual set add/contains/remove loops and
the separate removal path. Empty queries invoke hashing, custom queries can
match native members, and canonical current entries preserve stored hashes after
dataclass fields change. Positive equality after clear is accepted by Python add;
queries/removal require a current match. Identity completion needs no second
liveness check because no guest code intervenes. Actual equality keeps execution,
terminal-refusal and current-entry checks before use. No new frame, ledger,
dispatch framework, owner, policy or async engine.

Protocol-entry metadata is reserved before index/backing/member publication.
The new regression demonstrates the old refusal observed Count1; the corrected
path observes Count0 and unchanged funding. Existing retained-capacity ownership
and rollback remain; new failures cannot resume guest work through suppression.

## Controlled comparisons and retention decision

Unchanged workerb94/protocol2, independent identical-current control and
CPython3.13.16, normalGC/tiering. Five-second warmup/role/job, seven rotated~150ms
batches, dedicated Standard_F4as_v7/Ubuntu24.04.4 x64/AMD9V45x4/.NET11RC3.
Original B29785.76s/three jobs; one source-justified B29858.83s/two-job refinement.
All source/fixture/library/fullgolden/quiet/Closed/finalzero/worker-join/fixture-
removal gates pass. The synthetic baseline performs the required Python callbacks
correctly; no speed claim is based on skipped callbacks.

Warm clocks include invocation, actual mediated RAM-file I/O, complete stdout/file
checks and prior-result disposal. Compilation, startup, staging/reset, transport
and final program/result/process/fixture cleanup are excluded. Synthetic pure
queries have no guest file I/O. No allocation, causal CPU, RSS, embedding-latency,
fresh-execution or compilation claim.

| Block/workload | Candidate/control ms | Reference ms | CPython ms | Current/reference elapsed | Status |
| --- | ---: | ---: | ---: | ---: | --- |
| Original complete CSV,4096records |4.679/4.662|4.658|3.229|+0.46/+0.09%|Small/diagnostic; current spread0.37%|
| Original complete text,3072lines |2.244/2.199|2.274|1.161|−1.33/−3.30%|Variable; current spread2.00%|
| Original synthetic set queries |0.924/0.919|0.880|0.138|+5.05/+4.39%|Credible adverse result; spread0.63%|
| Refined complete text |2.266/2.238|2.331|1.213|−2.78/−3.99%|Diagnostic; controlIQR1.74%, spread1.24%|
| Refined synthetic set queries |0.913/0.909|0.893|0.140|+2.30/+1.80%|Residual regression; spread0.49%, IQR0.50/0.62%|

`Current/reference=(elapsed/reference−1)×100`; negative means faster. The synthetic
case creates128 stable custom keys and performs1024 membership queries. It is
not a representative file-processing script. Reference medians also moved between
blocks; do not attribute the entire difference to the refinement or claim a
whole-script gain. Its source justification is [preserved](identity-refinement.md).

**Retain the correctness and deletion with the residual synthetic regression
explicitly carried forward.** The stage removes mechanisms and demonstrated Python
errors; it does not complete the whole-script performance objective. No further
retiming campaign is selected here. Future runtime work must account for this debt
and earlier CSV/JSON uncertainties rather than add favorable percentages together.

[Original observations/source](original/observations.tgz),
[original medians/dispersion](original/summary.json),
[refined observations/source](refined/observations.tgz),
[refined medians/dispersion](refined/summary.json).
Raw SHA256: original `001c13b5622786e94fb51e3907495f6679ffa030fe79e7195d046057df430918`,
refined `6b79d73a71c40194f3f990978b0634ebac7278f9c4fcafd6c0057ed3b6ff93ae`.

## Correctness, qualification and remaining work

Focused1400public/464internal per actual.NET11/10 pass, zero-warning/error Release
build. Twelve independent isolatedCPython3.13.2 public goldens run through both
invocation APIs. Actual hash/equality cancellation and caught refusal still stop
removal with unchanged targets/effects, preserve the first origin and return shared
funding after cleanup. Existing real async set-relation lifetime checks pass.

[Correctness history](correctness-history.tgz) preserves the original empty/native
query mismatches, erroneous initial set-clear golden, genuine stored-hash regression
before correction and demonstrated post-publication metadata denial. Existing
semantic/stored-hash/ownership assertions were preserved. Both new original query
probes now match CPython through both APIs.

Remaining nested slice/dataclass hash dispatch is separate. Custom dict/set/cache
key callbacks still explicitly require synchronous completion; genuine async set
comparison remains on its existing awaited route. Full platform, original Stress
and installed-consumer checks are batched for the next combined delivery, without
replaying unchanged A/C/D qualification.
