# Validated JSON floating conversion — R74/B430

2026-11-08. Retained production change on21b0bc72, baseline production68d5bffe
(fully qualified CI42428096630). Measured candidatebb6e72b5 (.NET11); exact both-
target binaries/source inventories/patches remain. New-source full delivery is
pending, separate from the completed baseline. Publication postponed.

## Why reassess, and what changed

The [old candidate](../2026-11-01-json-float-entry-rejected/README.md) passed2,191
CPython rounding/consumption cases, but its unchanged whole-job CPU control shifted
36.7%; candidate equalled the final control. That demonstrated uncertainty, not a
stable design failure. Its no-retiming/permanent-stop rule is historical and
superseded by the user's current policy. All original source, conditions, failed
fixture and adverse results remain unchanged; no old component timing is repeated.

Current JSON still used the original invariant double.Parse entry. Replace the
entire default floating conversion entry with public UTF-8 Utf8Parser after the
existing prefix lexer validates the token. Require complete consumption; failure
is an invariant defect, never another-parser fallback/retry. General culture/
format entry overhead disappears. No parser framework, alternate execution mode,
representation, storage owner/cache, quota, unsafe API or dependency is added.
This does not retrospectively prove the old source would have improved, or explain
the cause of its unstable control.

Integer and explicit parse_float/parse_constant hooks retain existing behavior and
funded raw tokens. Grammar/error offsets, exact binary64 rounding/signed zero,
long mantissas/huge exponents and infinity/underflow remain. Both BCL conversions
are atomic: surrounding token scans/operations remain cancellable; no hard deadline
or abandoned background work is introduced. Public APIs are unchanged.

## Semantic, cancellation and ownership qualification

Existing semantic-only corpus, **2,191 independent CPython binary64/consumption
goldens**, matches both BCL entries on actual.NET11RC3 and.NET10.0.8. The reused
driver's historical B21 caption is retained; these are new semantic checks, no
component retiming. **257internal/1,160public Release checks per actual11/10 pass**,
matching probes. JSON scalar/nested/raw prefixes/options/hooks, actual callback
suspension/cancellation, admission/first terminal refusal, retained result/input
aliases and deterministic cleanup/budget reuse remain. No new architecture-only
test, changed expected Python result, relaxed assertion or timeout.

Existing five-script diagnostic uses independent old/current/old-control assemblies
sharing CoreLib/GC/hashing, both APIs, normal GC/tiering. Every full golden, shared
balance, image isolation and owned-fixture cleanup passes. **Allocation is neutral**
for float reports and essentially neutral for integer/CSV/text/report controls.
Desktop clocks are diagnostic: float sync−5.22to−12.91% with8.11%old drift, async
about−5.2%; integer sync is adverse+7.75–12.57%, text async+12.68/−5.12% with18.75%
old drift. Preserve all observations; they do not establish absence of regression.

## Controlled complete-script source pair

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, same worker7cf37ca5/normal optional0.4adapter,
maintained numeric-report.py (read two arrays, sum, compute variance, JSON report)
and independent CLR full-output goldens. Float input56,495B, integer36,231B;
each has two4,096-element arrays. No new kernel or benchmark framework.
Two independent baseline/two current processes, five-second warmup/role/job,
seven rotated150ms rounds, normal GC/tiering. Four jobs complete in **115.362s**.
Negative elapsed means faster.

| Complete job | Elapsed change, all process pairs | Baseline / current spread |
| --- | ---: | ---: |
| numeric-report.float.large / warm | -12.03 to -11.40% | 0.53% / 0.18% |
| numeric-report.float.large / warm-async | -14.08 to -10.05% | 2.36% / 2.21% |
| numeric-report.integer.large / warm | -1.01 to +0.61% | 1.12% / 0.50% |
| text-summary-helpers.large / warm | -1.05 to -0.49% | 0.37% / 0.19% |

Float sync elapsed improves11.40–12.03%; async10.05–14.08% with2.2–2.4%between-
process spreads. Integer stays−1.02to+0.61%, text−0.49to−1.05%. The adverse local
integer result is not reproduced here; its cause remains unproven. Coarse whole-
process CPU is also favourable for floats (sync−10.93–11.86%, async−4.14–11.79%)
and near-flat for controls. Ten-millisecond quantization/protocol/background work
limit precise causal CPU claims. Retain the simpler validated entry and controlled
whole-script benefit. No universal speedup, current CPython multiplier, allocation
gain or repayment of unrelated text debt is claimed. Whole-script roadmap stays
active; this single confirmation does not complete the performance objective.

Warm includes invocation, mediated RAM I/O, full stdout/file validation and prior-
result cleanup. Compile/startup/staging/reset/protocol/final cached teardown excluded.
Async timing uses completed I/O; genuine suspension is correctness-tested.
No RSS/startup/pending-I/O/embedding-tail claim; desktop clocks include additional
fixture reset/disposal and cannot be compared absolutely.

All source/core/every auxiliary/actualReady/runtime/golden/quiet/Closed/exit/join/
empty-fixture gates pass. Persistent records stay outside owned128MiB RAM fixtures;
all users join/mounts disappear before archive verification under lease and locally.
Staging271,749,120B passes256MiB after reclaiming exactly two redundant B424 retired-
prototype core links, verified against archived81fba80c and SHA42020660. Immutable
VM and independent local archives retain all bytes. Exact path/free-byte receipt
remains.150sowner+30stop+30margin; no timeout/restart or terminal requery.
B430 is terminal/archived/verified. Source68d5bffe's full green baseline receipt is
separate; new-source qualification is pending. Publication postponed.

[Controlled assessment](assessment.json), [allocation/adverse desktop](local-assessment.json),
[focused/semantic checks](focused.json), [exact identities](identity.json), [VM identities](vm-identity.json),
[source gate](selected-plan.md), [contract](contract.md),
[reclaimed redundant core links](reclaimed-prototype-links.json),
[VM raw archive](observations.tgz), [local sources/cores/corpus/logs/raw](local-observations.tar.xz).
