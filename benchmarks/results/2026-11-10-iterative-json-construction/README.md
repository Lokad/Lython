# R196 — iterative JSON container construction

**Original variant is not promoted. The decoder-admission refinement is retained
as a delivery candidate.** Last fully qualified runtime remains1a83c64f/ac6; full
platform/Stress/package qualification of this source is pending. This is Lython-versus-earlier-Lython
evidence, not a new CPython comparison or completion of the performance objective.

2026-11-10. Original isolateda9d5620f above3da4c742 replaces recursive child-
container completion and its separate async remainder with one construction loop.
Four managed inline frames cover shallow documents; deeper frame backing uses
existing admission/replacement overlap and remains owned through suspended hooks.
Scalar parsers, per-token stack checks,64depth and Python behavior are unchanged.
The replacement adds111productionlines; the earlier approximate105claim was wrong.
[Actual line counts](line-count-correction.json) correct it without rewriting the
original plans/conditions in immutable evidence.

Original focused Release checks pass1001public/864internal on each actual.NET11/10.
Six deeply nested scalar/object-hook cases cover success/cancellation/refusal and
budget reuse. No full platform/Stress/package delivery qualification is claimed.
Source, exact 568-file hashes, binaries, logs, complete-job diagnostics and setup
corrections are in [the original archive](original-observations.tgz).

B544 finishes6jobs/215.280s on quiet F4as_v7 Ubuntu24.04.4/.NET11RC3. Same common
worker, adapter0.4 and dependencies; ordinaryGC/tiering, four independent source/
control processes per job,5s warmup/role,7rotated400msrounds. All identities,
stdout/file goldens, balances, quiet gates, joins, emptyRAMcleanup, archive-under-
lease and local SHA checks pass. Terminal owner is cached; no users remain.

| Complete script / phase | Elapsed change | Wider process CPU change | Measurement status |
| --- | ---: | ---: | --- |
| Report / warm | −0.03..+0.95% | 0..+2.33% | No demonstrated gain |
| JSON-lines / warm | +1.40..+2.51% | +0.36..+1.83% | Small adverse result |
| Report / warm async | −0.20..+1.22% | +0.22..+1.64% | Inconclusive |
| JSON-lines / warm async | −4.66..+1.53% | −4.63..+4.28% | Inconclusive; identical reference spread6.38% |
| Report / compilation | −0.43..+1.15% | −1.88..+0.72% | Does not reproduce large desktop slowdown |
| Unchanged text / warm | −3.42..+0.66% | −2.94..−0.95% | Controls variable; no attribution claim |

[All pairings/IQR/medians](original-summary.json) and original desktop adversity
survive. Ranges compare two independent medians per source, not confidence
intervals. Allocation is effectively flat. No CPU neutrality/equivalence claim.

Warm clocks include mediatedRAMI/O, full output validation and prior-result
cleanup, exclude reset/compilation/startup. Compile includes construction and
prior-script disposal. External processCPU also includes protocol/reset and has
coarse ticks. The desktop driver's Run-only clock excludes reset/goldens/result
disposal; its full envelope includes them. These are distinct boundaries. Original
six-case guard rejection happened before guest work; zero-Run compile-summary
failure was corrected offline without rerunning. All originals remain.

Decision: original iteration alone is not a delivery improvement. One bounded
refinement is justified by a concrete remaining mechanism: per-key/scalar/number
stack admission repeats despite nonrecursive container construction. The planned
replacement owns one complete managed decoder admission, rechecks the current
stack after real suspension and removes leaf enter/leave/pending-frame cleanup.
Cancellation and first-refusal scans, capacity preflights, actual callback guards
and supported managed protection remain. New admissions can conservatively deny
earlier near the managed nesting boundary; no new public option/cap is introduced.
Refinement results and final code/ownership/regression-debt decision are pending.

Exact canonical B543 terminal copies were retired under a fresh idle lease only
after committed local archive SHA verification; qualifiedac6 stays protected.
Full receipt is in the raw archive. Publication and adapter pin promotion remain
postponed; missing producer capabilities do not block core development.


## Decoder-admission refinement retained for delivery

Frozen678fc7df removes per-key/literal/number admission, numeric pending-frame
handoff and its nullable cleanup. One complete managed decoder admission spans
parsing/hooks; current-stack checks follow continuation resumes. All cancellation,
terminal-refusal, capacity/overlap and callback guards remain. Container depth64
is unchanged. Netproduction+102lines versus original+111, not105.

Native containers/constants/object hooks gain one conservative entry admission;
they may reject earlier near the existing managed nesting bound. Public API,
Python call-depth settings and ordinary results are unchanged. The single decoder
lifetime and nonrecursive container construction are the retained simplification;
there is no claim that fewer source lines or fewer allocations were delivered.

Focused checks pass1002public/880internal eachactual.NET11/10. NET11 public uses
1001unchanged passing cases plus the corrected fixture separately. Original new
fixture timed out25s with1.45sCPU because every recursion repeated25mshost delays.
That source/log survive. The corrected fixture retains one genuinely suspended
hook, one default-depth rejection and three real callback recoveries; it passes
747ms without deadline relaxation. New white-box cases deny before construction
at the managed nesting bound and then reuse both decoder entry points.

B545 completes6jobs/214.221s, all gates/SHA/join/emptyRAM/archive checks pass, using
the same protocol/catalog/role/warmup/round settings as B544. [Raw evidence](refined-observations.tgz),
[all pairings](refined-summary.json), [receipt](refined-archive-receipt.json).

| Script / phase | Elapsed change | Wider process CPU change | Debt / interpretation |
| --- | ---: | ---: | --- |
| Report / warm | −4.09..−0.84% | −3.23..+1.63% | Modest elapsed gain; sync CPU uncertain |
| JSON-lines / warm | −1.13..+2.02% | 0..+2.44% | Inconclusive; possible small debt |
| Report / warm async | −5.53..−1.74% | −6.96..−1.29% | Supporting CPU; ordinary completed-I/O timing |
| JSON-lines / warm async | −4.76..+3.46% | −3.15..+1.89% | Uncertain; reference/candidate controls spread4.60/3.71% |
| Report / compilation | −1.23..+0.60% | −3.46..+0.42% | No demonstrated compile gain or regression |
| Unchanged text / warm | −0.51..+5.63% | 0..+5.33% | Possible debt; candidate control spread4.56% |

Allocation remains effectively flat. Refined desktop sync report+0.67..+7.57%,
text+8.50..+11.35%, async config+13.49..+15.57% and all other adversity remain in
the archive. The reused runner repeated its zero-Run compile-summary mistake;
all raw phases passed and were summarized offline without re-execution. This is
a tooling correction, not a discarded measurement or runtime failure.

Decision: retain for coherent decoder admission and modest report benefit, carrying
JSON-lines/text uncertainty explicitly. No general CPU/neutrality claim, fixed
percentage veto or further unchanged isolated retiming selected. The representative
CPython checkpoint must cover these workloads at delivery; overall performance
work remains active. Native helper/call-arena lessons and other roadmap priorities
are unaffected. Full Windows/Ubuntu, actual11/10, originalStress and installed
Release consumers follow this retained boundary. Publication stays postponed.

Main promotion passes fresh nonincremental builds and222public/54internal focused
checks per actual target. [Source proof](main-source-normalization.json) covers all
568production files against measured678 after explicit EOL normalization; no
postcommit rebuilt-DLL identity is claimed. Main logs and all original failed
conditions survive in [preparation](refined-preparation.tar.xz). The optional
adapter/provider pin is unchanged and separately qualified.

Exact-canonical original compressed evidence and all originala9VM aliases were
retired only after archive verification under an idle lease. Currentqualifiedac6
and refined678 images remain. No workers/RAMfixtures remain; owners are terminal
and cached. No producer or sibling repository was edited.


## Delivery qualification

Committed/pushed runtime `8d44bd4ecdb0db96fee7a23113ba28b1e9cff558` is fully
qualified by [42813970365](https://github.com/Lokad/Lython/actions/runs/42813970365):
Windows/Ubuntu, actual .NET 11/.NET 10 ordinary tests, original Stress and installed
Release consumers all pass. The [cached terminal receipt](delivery-ci.json) does
not change measured images, uncertainty or source-pair claims. Publication remains
postponed. The next CPython checkpoint uses a fresh postcommit nonincremental core,
not an asserted binary identity with the measured precommit image.
