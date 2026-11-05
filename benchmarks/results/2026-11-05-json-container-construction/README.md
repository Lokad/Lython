# Native JSON container construction

2026-11-05 local date (captures 2026-11-04 UTC), quiet Azure Standard_F4as_v7,
Ubuntu24.04.4, .NET11RC3, ordinary GC/tiering. Baseline qualified f6e51d1c/core
df206f47; source parent56bf2769 adds tests/evidence only. Final frozen core
`04f3e00dad8e8673e620e34b3cf8b1a1376d25f58275e4ad94684b62672f0eaa`.
[Exact identities](final/candidate-identity.json). Publication held.

Retain the one-file simplification: admit a default object's shell and first native
table using the existing capacity constructor at the initial property token.
OrderedDictionary selects its normal minimum. Remove separate first-table
construction/admission, the initial end=-1 sentinel, obsolete opening continuation
arguments and redundant EnsureJsonComma whitespace rescan. Completed and genuinely
suspended children share separator handling. No new owner, cache, policy or backend.

Tradeoff: conservative combined admission can refuse before the first property or
number callback where shell-only admission previously succeeded. First refusal
remains terminal; cleanup is awaited. Empty/invalid initial property tokens use
shell-only storage; a quoted but truncated key can admit the table before its
syntax error. Native capacity, rollback, aliases and pending-user funding remain.

Final nonincremental Release39.55s, zero warnings/errors;242 white-box and782 public
focused checks per actual .NET11/.NET10 pass. Existing coverage includes grammar,
error positions, duplicates, Unicode, all hooks, actual suspension/reentrant aliases,
cancellation, refusal, retained results and shared-budget cleanup/reuse.
[Validation](final/validation.json), [final focused logs](focused-checks/).
Full platform/Stress/installed-package delivery is recorded separately when complete.

## Controlled observations and decision

Each variant has one six-job165s collection. Three isolated images, two identical
baseline roles,6s warmup/image,2s settle,9 rotated200ms batches, ordinary runtime
settings,240s owned service. All DLL/CoreLib/hash, independent CPython stdout/file
golden, before/after quiet, baseline/final-zero, fixture removal and owned-process
join gates pass. Positive changes mean worse.

| Final complete script | Elapsed vs old / old control | Process allocation |
| --- | ---: | ---: |
| JSON aggregation, warm | -2.51% / -4.59% | -3.79% |
| JSON aggregation, RunAsync | -1.70% / -0.16% | -3.79% |
| JSON-to-Markdown, warm | -2.60% / -1.85% | -2.24% |
| JSON-to-Markdown, RunAsync | -4.30% / -4.72% | -2.24% |
| Config read/edit/write, fresh | +0.79% / +1.30% | near unchanged |
| Text control, warm | -0.80% / -0.42% | unchanged |

Allocation reductions are established. Async report elapsed is favorable in both
collections; final baseline spread0.45%, role IQR0.35/0.87/2.16%. Warm JSON's2.18%
baseline spread limits magnitude; async JSON is control-limited. Warm report's4.33%
candidate IQR limits small claims. [All final metrics](final/measurements.md) and
[raw-derived assessment](final/assessment.json) retain medians/IQR/allocations and
initial untimed admitted-peak witnesses. [Initial measurements](initial/measurements.md)
include drift and opposing signs; they have not been replaced.

Fresh config's adverse result persists: final+0.79–1.30% (~2–4us), initial+0.89–2.03%.
Accept this debt for combined admission, fewer temporary reservation objects and
simpler parser state. Do not claim its cause or a general CPU gain. The whole-script
performance objective remains open.

Warm includes public invocation, mediated RAM-file IO, full checks and prior-result
cleanup. Async uses completed host IO; actual suspension is checked separately.
Fresh adds compilation/disposal. Staging/reset, startup, response hashing and final
cached-owner/fixture teardown are excluded; all users are joined. No compile-only
measurement in this pair. Process allocation can include reset/runtime noise.
Peaks describe initial untimed warm admission, not fresh overlap, heap/RSS or
embedding latency. No updated CPython ratios or additive-gains claim follows.

Final raw archive SHA256:
`4707ae6c41f51ccbd99caaa3dc4c0c8990583748f34c4f41bf1c4f49cac2fcb9`.
Archives include the private driver, catalog, gates and original observations.
Patches/snapshots preserve both variants. [Setup correction](setup-correction.md)
records a failed disk preflight and verified duplicate-upload removal before any
final sample; no failed performance observations were discarded.
