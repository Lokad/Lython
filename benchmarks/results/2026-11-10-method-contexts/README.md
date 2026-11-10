# R198 — scope facts govern ordinary method contexts

The retained change removes the owner-type proxy from the existing deferred
function-context predicate. Owner binding affects context setup only when lexical
class-cell facts require it, which the predicate already excludes. Eligible
ordinary methods now use the same owned slot workstate and scope-sensitive
promotion as functions. No new execution path, cache, option or ownership layer.
Captured local cells, class cells, implicit super and protected bodies keep their
context; later namespace/protocol/I/O work promotes without replaying prior effects.

The complete class-renderer report reads 3,072 JSON records (189,441 bytes), calls
an ordinary multi-statement formatting method for each and writes a Markdown file.
It is the existing report with only the helper expressed as a class method.
Independent CPython3.13.2 verifies full stdout/file goldens; it is not a timed
external baseline. The primary CPython guide retains its qualified8d44 source.

Reference is fully qualified8d44, fresh postcommit core E294; prototype is the
one production restriction deletion on28cb769a, frozen039dc975. All568 production
files still match [source proof](source-proof.json). Exact cores, scripts, source,
test logs and both patches survive in raw evidence/[preparation](preparation.tar.xz).
Supplemental tests were added after freeze; no production code changed.

Desktop CLR allocation decreases28.51%synchronously/28.50%asynchronously on the
class report. Compilation allocation is flat. [All desktop observations](desktop/)
retain diagnostic elapsed gains and unchanged asyncCSV+6.2..6.5% / compileCSV
+17..20% adversity. These are not CPU qualification or evidence of neutrality.
Desktop Run-only clocks exclude reset, goldens and result disposal; its full
envelope/allocation includes those costs. Startup/compilation are separate.

The quiet F4as_v7/Ubuntu24.04.4/.NET11RC3 confirmation completes five jobs in
179.869seconds, stock GC/tiering, two independent processes per source,5s warmup,
seven rotated400ms rounds. Both source APIs use the existing worker and optional
adapter0.4. [Raw evidence](observations.tgz), [all medians/CPU/IQR/pairings](summary.json),
[receipt](archive-receipt.json). Ranges are all four median pairings, not confidence
intervals. Wider CPU includes reset/protocol and coarse100Hz ticks.

| Complete script / phase | Elapsed change | Wider process CPU | Reference / candidate control spread |
| --- | ---: | ---: | ---: |
| report-render.class-renderer.large / warm | -9.71..-7.06% | -9.17..-4.55% | 2.63% / 0.23% |
| report-render.class-renderer.large / warm-async | -9.89..-4.62% | -10.17..-3.54% | 2.01% / 3.63% |
| report-render.class-renderer.large / compile | -2.49..-0.79% | -5.14..-1.49% | 1.57% / 0.14% |
| report-render.large / warm | -2.12..+2.04% | -0.84..+1.57% | 2.41% / 1.81% |
| text-summary-helpers.large / warm | -4.27..-0.16% | -6.40..+0.00% | 2.51% / 1.74% |

Warm VM clocks include mediated RAM I/O, full output validation and prior-result
cleanup; reset, staging, compilation and startup are excluded. Compile measures
construction/disposal. Async uses completed I/O, not pending-I/O latency. Modest
compile differences have no demonstrated causal explanation in this runtime-only
predicate change. Report controls remain inconclusive; CPU up to1.57% is possible
debt. Async class-report candidate spread3.63%/maxIQR3.55% stays visible. Desktop
CSV adversity is not resolved by these other controls and remains qualification debt.

Decision: retain the shared scope invariant and measured whole-class-report frame
reduction. No general CPU/neutrality claim, unchanged retiming or publication.
The broader text/call/record/frontend performance roadmap remains active.

Focused102public/71internal pass per actual.NET11/10;18supplemental public cases
per target (15overlap) cover classmethods and exact method traceback spans. Scope,
super/closures, reentrant admission/refusal, real suspended I/O, cancellation,
retained aliases and cleanup/reuse remain covered. Full delivery qualification is
recorded separately against the committed source, not inferred from these checks.

The original B547 attempt omitted control-process hashes and failed during startup
before warmups/timing: [original archive](startup-observations-partial.tgz),
[receipt](startup-partial-archive-receipt.json). It was joined/cleaned and preserved;
B548 corrects only the manifest, using identical frozen code/settings. Two earlier
headroom refusals, original logs and canonical-copy recovery survive. A bounded
idle operation retired five verified committed duplicate compressed archives,
preserving every image and sole evidence. All successful users joined, RAM removed,
archive verified under lease/local SHA verified; terminal owners are cached.
