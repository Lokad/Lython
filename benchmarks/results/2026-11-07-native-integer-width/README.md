# Native integer width and admitted promotion — R50

2026-11-07. Baseline9990d591/R49 checked core; candidate9990d591 plus the preserved
single-production-file patch and329-case CPython fixture. Final core/source hashes
are in focused.json. R49's independently active full CI does not qualify this later
source. No new dependency, opcode, owner/cache, public value type or resource policy.

One managed UInt128 accumulator replaces UInt64 in the existing general UTF8 int
kernel. Native prefix/remainder overflow guards avoid per-digit division; larger
values retain the same cancellable radix chunks. Native>64bit final values admit
magnitude/metadata/replacement overlap before conversion to BigInteger. Only retained
payload is committed, scratch refunds after users end. Guest integers remain
BigInteger, byte inputs retain ASCII grammar, Unicode strings use existing decimal
properties. JSON's efficient CLR decimal parser is unchanged; this does not revive
R46's adverse parser consolidation. No decoded text, unsafe code, tiers/knobs, exact
object ledger, retry or collection dependency.

Admission can occur later because no heap magnitude is constructed while the value
fits128bits. Tiny thresholds and error-versus-first-admission timing can therefore
change; explicit refusal remains terminal. No exemption for retained native>64bit
results. Scan/chunk and post-admission cancellation remain. Individual BCL arithmetic
operations remain atomic; post-promotion scaling is unchanged, not a solution to
R46's arbitrary large-decimal allocation gap.

Initial and refined102internal/420public checks per actual.NET11/10 pass.
CPython3.13.2 freezes329cases, including140additional127/128bit, promotion and
negative boundaries across bases2–36. Existing signs/prefix/Unicode/underscore/
whitespace/bytes/errors, first refusal, input/alias/result funding and budget reuse
stay. Actual in-flight1MiB magnitude cancellation Stress passes both runtimes
(observed49/53ms, not a latency guarantee), joins users before refund and retains
the input owner. No timeouts or assertions weakened.

The initial source computes quotient/remainder separately. A supported.NET10/11
DivRem refinement removes that duplicate work, preserving all source/images and
initial adverse clocks. Refined warm allocation for numeric-string JSON falls
12.492%sync/12.487%async; other maintained inputs are within0.009%. Admitted peaks
and retained output funding are separate from GC allocation. There is no frontend
change or compilation improvement claim. The unchanged five-case reflection driver
uses32warmups/image,5rotated rounds*4jobs, isolated before/current/before-control
ALCs sharing CoreLib/hash witness/process GC. Normal GC/tiering. Its old R45 purpose
label is historical; R50's source/catalog identifies these jobs. Desktop clocks are
diagnostic: initial numeric-string sync+3.94–5.44%, final−10.87–11.59%, while
unaffected/control movement is substantial. Preserve every original observation.

## Controlled quiet-VM comparison — B405

Same unchanged fixtures, worker7cf37ca5/host/adapter27211dbe/normal0.4,
two baseline/two identical current processes. Quiet F4as_v7/Ubuntu24.04.4,
four EPYC9V45vCPUs/16GiB/no swap/.NET11RC3 normal GC/tiering.5s warmup/role/job,
7rotated150ms rounds. Three warm jobs complete87.38s,120s owner+30s stop+30s
margin, observed after180s. Full Ready/source/core/aux/golden/funding/quiet/Closed/
exit/join/fixture gates pass. Raw2f2e6618 archived/verified under collection lease
and independently after retrieval; no active users or fixtures.

| Warm input | Baseline ms | Current ms | All role pairings | Current / baseline role spread |
| --- | ---: | ---: | ---: | ---: |
| Padded CSV,191505B |4.290176–4.292434|4.335009–4.339769|+0.99to+1.16%|0.11%/0.05%|
|40-digit numeric-string JSON,278528B|7.555452–7.687586|6.726565–6.748830|−12.50to−10.68%|0.33%/1.72%|
| Ordinary JSON,110592B |3.957616–4.012621|3.976475–4.105685|−0.90to+3.74%|3.25%/1.39%|

Ranges span medians, not confidence intervals. Numeric strings include an explicit
int helper; CSV keeps its maintained source with padded values. Ordinary JSON does
not use this string-int parser and remains a variable control. Coarse process CPU
moves consistently on affected inputs but is not isolated-method CPU. Warm includes
mediated RAM I/O/full stdout/files/prior-result cleanup; compile/startup/staging/
reset/transport/final cached teardown excluded. No CPython remeasurement or cross-run
ratio. Keep the primary source8c9fd0d7 CPython tables historically labelled.

**Retain** lower intermediate magnitude allocation and numeric-string elapsed
improvement. Explicitly accept the measured~1.1%padded-CSV cost; do not add another
native tier merely to rescue it. Ordinary control variation, older regression
debt and larger-input uncertainty remain; no universal or async elapsed claim.
Full Windows/Ubuntu Release, actual .NET 11/10, original Stress and installed
core-package qualification passed for the retained source in
[CI42382037879](delivery-ci.json). This receipt does not qualify later R51 changes.
Publication remains postponed; the broader objective is active.

[Raw controlled observations](b405/observations.tgz), [four-role summary](b405/comparison-summary.json),
[receipt](b405/archive-receipt.json), [refined allocation/adverse desktop clocks](refined-local-summary.json),
[initial clocks](local-summary.json), [focused source/cores](focused.json),
[real in-flight cancellation](refined-stress.json), [decision](decision.json),
[all source/failure/initial/final/images/contracts](development.tar.xz).
