# Keyed queries and validation — retained correction

D64A/C/D, 2026-11-05. Source `14f7e054` plus the archived21-file correction;
frozen core `47833d32`, reference D62/`084aaa38`. Two subsequent comment-only
corrections are [preserved separately](post-freeze-comments.patch); executable
code matches the measured snapshot. Delivery source `aec4f785` is fully qualified:
[CI42100526619](https://github.com/Lokad/Lython/actions/runs/42100526619), Windows/
Ubuntu, actual11/10, original Stress and installed consumers, all8 gates GREEN.
[Terminal receipt](delivery-ci-status.json) is cached; no unchanged replay.
Later candidate-selection/set-resolution work is separate from this qualification.

Dictionary/set queries and removal now invoke custom hashes even for identical
keys, propagating errors. Native `dict(source)` copies reuse stored hashes, like
the existing copy constructor; the frozen baseline confirms this mismatch
predated the change. Set pop removes its retained member without a new hash query.
Async constructor ambient scopes do not span awaits.

One typed unhashability error carries the failing value. Shared int normalization
replaces `EnsureHashableValue`, `BucketHash` and broad exception conversion.
Tuple shape/cycle validation no longer hashes every child subtree; one root
structural hash follows the pure walk. Storage still performs its own hash;
this does not remove every duplicate computation or add a prehashed-key ledger.
Preflight, actual callback effects, terminal refusal/cancellation and retained
funding remain. More required Python callbacks can cost more than the incorrect
baseline; no protocol speedup claim is made against skipped callbacks.

## Focused native comparison

One140.41s five-job group, unchanged workerb94/protocol2, five-second warmup per
role/job, seven rotated~150ms batches. Standard_F4as_v7/Ubuntu24.04.4 x64,
AMD9V45x4, .NET11RC3 and CPython3.13.16, normalGC/tiering. Independent identical
current control; reference frozen084. Quiet/source/fixture/library/full-output/
file checks, Closed/finalzero, worker joins and fixture-removal gates all pass.

Warm clocks include invocation, actual mediated RAM-file I/O, full output/file
checks and prior-result cleanup. Compilation, startup, staging/reset, transport
and final program/result/process/fixture teardown are excluded. No allocation,
causal CPU, RSS, embedding-latency, compilation or fresh-execution claim.

| Workload | Candidate/control ms | Reference ms | CPython ms | Current/reference elapsed | Status |
| --- | ---: | ---: | ---: | ---: | --- |
| Complete CSV/helper,4096records |4.702/4.648|4.629|3.213|+1.57/+0.41%|Possible small regression; current spread1.15%|
| Complete text/helper,3072lines |2.214/2.238|2.256|1.194|−1.86/−0.80%|Diagnostic; current spread1.08%|
| Complete JSON/helper,4096records |4.419/4.322|4.337|4.074|+1.90/−0.34%|Conflicting; current spread2.20%|
| Synthetic compound grouping,2wrappers |4.991/4.974|5.430|0.461|−8.09/−8.39%|Native algorithmic control; current spread0.33%|
| Synthetic compound grouping,8wrappers |10.615/9.961|14.792|0.941|−28.24/−32.66%|Direction consistent; current spread6.16% limits magnitude|

`Current/reference = (elapsed/reference−1)×100`; negative means faster. The last
two rows use2048records and deliberately nested native tuple keys; they are not
representative complete processing scripts. Independent isolated CPython3.13.2
goldens precede timing. This supports eliminating repeated subtree hashing,
without establishing a whole-script speedup or closing CSV/JSON regression debt.
Substantial CPython gaps remain; no additive gain or extrapolation is justified.

[Raw observations/source snapshot](observations.tgz),
[summary/dispersion](summary.json), [identities](identity.json),
[collection gates](archive-receipt.json), [prior contract](source-contract.md),
[prior measurement plan](pre-measurement-plan.md).
Raw SHA256 `8da22f3aa142a5660b7e95bc6aa8df371d1901b78e42bf937b3d18a944d45230`.

## Correctness and remaining work

[Validation](validation.json): full4154internal/16015public per actual11/10 pass,
zero-warning/error Release solution build. Three original public mismatches now
match isolatedCPython3.13.2 in both invocation modes. New public goldens cover
callback counts/errors, native copies/pop and nested failing values; ownership
cases exercise real cancelled/refused callbacks, unchanged targets, first-origin
propagation and shared-budget recovery without collection.

[Preserved correctness history](correctness-history.tgz) includes original probes,
native-copy baseline proof, first full-public five stale expectation failures and
independent CPython corrections. It also retains the removed-wrapper build
failure, invalid unsupported-types fixture and dataclass raw-error failure before
correction. Meaningful semantic/effect/ownership assertions were preserved.

Open, demonstrated follow-ups: structural collisions can trigger extra equality
callbacks despite different stable custom hashes; slice bounds and generated
dataclass fields can omit nested custom hashes. Original probes remain in the
history. Structural reverse lookup has a real set stored-entry consumer; do not
delete it blindly. Custom key callbacks still explicitly require synchronous
completion. This milestone does not claim those limitations fixed. Publication
remains postponed; whole-script performance work continues independently of regex.
