# R196 — iterative JSON container construction

**Original variant is not promoted. A decoder-admission refinement is active.**
The qualified runtime stays1a83c64f/ac6. This is Lython-versus-earlier-Lython
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
