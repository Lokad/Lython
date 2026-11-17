# Managed scoped locking for the shared allowance

R488/B906/B907,2026-11-17UTC. Retain one source-file change on fully qualified
add2c00a/frozendfc4c83c: the private default LythonMemoryBudget gate becomes
System.Threading.Lock. Measured candidate38662446;585production/2382inputs archived.
Main retains identical production text; rebuilt-binary equality is not inferred.

C# emits managed scoped locking instead of Monitor entry/exit bookkeeping. Exact
reservations/refunds, concurrency/reentry, scope disposal and public API stay the
same. No new allowance, counter, cache, credit layer, ownership state or policy.
Custom host budget implementations are unaffected. This uses the existing
.NET10/11BCL; no new runtime package is introduced. Source motivation is the
[existing CPU gate](../2026-11-17-shared-allowance-source-gate/README.md), not a
claim that every sampled lock cycle disappears.

| Complete workload / phase | First elapsed change | First wider CPU change |
| --- | ---: | ---: |
| JSON-lines helpers / warm | -3.35..+5.38% | -1.59..+6.64% |
| Same / compile and run | -0.27..+1.78% | -0.77..+2.00% |
| Numeric CSV quality / warm | -0.54..+1.84% | -1.12..+0.00% |
| Text normalization / warm | -0.02..+0.95% | -1.15..+0.99% |
| Positional-call control / warm | -1.52..-0.40% | -1.96..-1.20% |

First JSON candidate processes differ7.84%, despite internalIQR≤0.30%; reference
spread1.11%. This is uncertainty, not a failed design. A bounded unchanged-image
confirmation keeps the same four-case catalog, workers and warmup/sample protocol:

| Workload / warm confirmation | Elapsed change | Wider CPU change | Ref/candidate spread |
| --- | ---: | ---: | ---: |
| JSON-lines helpers | -1.96..-0.93% | -2.67..-0.71% | 1.03/0.02% |
| Positional-call control | -1.45..+0.66% | -0.80..+2.41% | 1.25/0.88% |

Retain the small scoped-lock refinement: confirmation supports modest JSON benefit
without new machinery, allocation or semantic debt. CSV/text gains are unestablished.
The original adverse process remains unexplained and is preserved; no universal
speedup or causal explanation is claimed. Revisit if a later representative
checkpoint reproduces adversity; no further standalone unchanged campaign now.
Ranges combine four process medians, not confidence intervals or fixed vetoes.

All four original allocation diagnostics are exactly unchanged, including accounted
peaks and restored owner balances. Confirmation reuses these unchanged-image/
catalog diagnostics; it does not repeat allocation measurement. Private and main
actualWindows11/10each52internal/76public pass after nonincremental builds. Both
actual copied32file workers pass four goldens versus CPython3.13.2.

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, normalGC/tiering. First five jobs191.31s;
confirmation two jobs80.08s. Six-second rotated warmups/five samples/four workers.
Mediated RAM I/O, full output/file goldens and prior-result cleanup included;
compilation only fresh. Startup, reset/protocol and final cache teardown excluded;
wider CPU includes outside-clock request work. No heap/RSS or embedding-tail claim.
All quiet/golden/Closed/exit/join/SHAarchive-before-unmount gates pass. Terminals cached.

First staging retires only supersededa95core aliases and a completed backed catalog
after fresh lease/path/UID/hash/link/all-alias proof. Qualifieddfc/toolchains/
canonicals and254/252MiB floors remain. Confirmation hardlinks both verified
images/catalog without retirement or binary payload. All original conditions remain.

Full changed-source platform/stress/package delivery is pending. Publication held.
The representative CPython refresh and substantial numeric/call/record/text/frontend
roadmap continue; this modest change does not finish the performance objective.

[Decision](decision.json),[source](candidate-sources.tar.xz),[patch](prototype.patch.gz),
[first](first/summary.json),[confirmation](confirmation/summary.json),
[allocation](allocation-summary.json),[main source](main-source-match.json),
[procedures/checks](procedures-and-checks.tar.xz).
