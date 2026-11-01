# Iterative JSON parser trial: inconclusive, removed (B53)

2026-11-01. **Prototype removed.** The fixed60-second comparison deadline ends
before final JSON/CSV controls; completed numeric process-CPU controls drift
-39.72%/+29.79%. No accepted speedup or final-source timing is claimed. Eighteen
independent semantic/lifetime regressions remain and pass on the original parser.

## Prospective design

[Criteria before code](pre-code-plan.md): ONE iterative UTF-8 JSON reader replaces
recursive array/object readers and duplicated dictionary/pairs-hook object grammar.
A private unfinished-container array uses existing temporary admission, covers
geometric growth/replacement overlap and remains funded until real callbacks settle.
Keep existing scalar scanners, depth64, managed stack admission, Python prefix/error
coordinates and callbacks. No second parser, fallback, owner framework or public
option. The [exact patch](source.patch.gz) removes78 net production lines.

The preceding [source/attribution assessment](../2026-11-01-numeric-report-attribution/README.md)
is sparse; it does not establish a dominant removable fraction. This bounded
algorithmic trial tests a concrete transport deletion, not a value-layout rewrite.

Retain only >=10%CPU AND >=5%elapsed improvement against both controls on at least
two of three affected scripts, <=5%CPU/elapsed regressions everywhere, stable
<=5%CPU/elapsed and0.2%allocation controls, resolved CPU batches, all quiet/golden/
balance/cleanup gates. Failure/inconclusive means restore, no tuning/retiming.
Before the FIRST comparison, a [documented batch correction](pre-measurement-batch-note.md)
uses256 numeric-report jobs/64 JSON/CSV jobs: B52's original CPU shows64-report
batches cannot resolve the unchanged>100ms gate. Seven batches/128 warmups and
60s total/30s child limits remain. No criterion is relaxed.

## Partial observations, kept without selection

Baseline:20e208c0 original production (same runtime source95b5955c).
Candidate:bfcd7248 + source.patch. Fresh nonincremental equal core/worker/test/probe
copies and byte-identical auxiliary hosts/adapters. Dedicated Ubuntu24.04.4 Azure
F4as_v7, four EPYC9V45 vCPUs/16GiB, .NET11RC3, ordinary GC/tiering.

| Complete warm job | Before CPU ms | Candidate CPU ms | Control CPU ms | Before/candidate/control elapsed ms |
| --- | ---: | ---: | ---: | --- |
| Integer report | 2.22291 | 1.45220 | 1.33989 | 2.21038 /2.06509 /2.09586 |
| Float report | 1.60552 | 1.93435 | 2.08384 | 2.30049 /2.12065 /2.10359 |
| JSON helpers | 8.66083 | 9.20019 | Missing | 8.53788 /8.71536 /missing |
| CSV, unaffected | 8.10748 | 7.88559 | Missing | 8.06679 /8.23636 /missing |

Ten processes finish with full stdout/file goldens and funding-balance checks.
The next control is killed/reaped at the remaining2.489s deadline; final CSV
control and post-quiet gate are absent. Service exits1 after60.083s. Never restart
because observation ended: service is authoritatively failed, MainPID0 and empty
ControlGroup; VM lease available. A timeout fixture is verified by exact path/UID/
input bytes before removal. [Cleanup receipt](timeout-fixture-cleanup.json).

Numeric CPU controls fail decisively. Integer elapsed-6.57%/-1.47%, float
-7.82%/+0.81%; JSON/CSV show+2.08%/+2.10% against first baseline only. Missing
controls, ongoing tiering/process-cost uncertainty and the hard deadline prevent
qualification. No repeat, longer budget, relaxed gate or claimed improvement.
Warm execution includes reset/mediated I/O/full validation/result disposal;
compilation/startup excluded. Do not infer phase latency by subtraction.

## Semantics and restoration

Candidate zero-warning Release build;167 native/815 public focused cases pass on
actual.NET11RC3/10.0.8. Six new isolated CPython3.13.2 goldens cover nested grammar,
duplicates/postorder/pairs precedence, aliases, controls, numeric/raw prefixes,
Unicode indices and depth64. Windows reference CRLF is normalized to capture LF.
Twelve deterministic nested loads/raw_decode cases cover numeric/pairs callbacks,
actual held host users, cancellation/refusal despite guest suppression, two shared
executions, retained aliases/results, awaited cleanup and fresh-token/budget reuse.
A host deliberately ignores cancellation until released; entry/exit checks alone
cannot satisfy these tests. No test asserts an internal parser shape or threshold.

Original parser restored with fresh timestamps, prototype absent and production
diff empty. Fresh nonincremental rebuild and the same167/815 checks pass per target,
including all18 new cases. [Restored identity](restored-identity.json) records equal
fresh copies. Informational revision differs from measured baseline; no binary
reproduction claim. Existing production qualification at95b5955c/41296168049 is
terminal green; new test platform qualification is batched at the next delivery.

[All saved samples/controls](summary.json), [identities](candidate-identity.json),
[validation logs](validation-logs.tar.gz), exact patch/criteria/goldens and
[raw results](raw-results.tar.gz), SHA256
`29dd9b4627fbc57f96fddee22f6e92d81ef3ba70dabf0f01ae1a36a002c4ed41`,
preserve the failed/incomplete comparison. Stop this prototype. The next decision
must address short-comparison validity using existing tooling before another
performance-selected trial; no new benchmark framework or qualification campaign.
