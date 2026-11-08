# Exposed receiver binding correction

R85 follow-up,2026-11-08. Original source02c76e52/coreaa4acebe passed focused tests
but failed full CI42457767068 on both platforms. Retained logs show environment
views rebinding their private PyDict receiver to PyEnvironmentMapping, plus two
unrelated Decimal error-text regressions from the general binder override.

The correction reuses the existing fixed-arity exposed-callable wrapper: its bound
receiver remains fixed and it rejects raw arguments with the existing diagnostic.
The global binder override is deleted. Shared method declarations/immediate-call
optimization remain; no new policy, owner, wrapper class or native-body expansion.
Original assertions are unchanged.2030affected+103additional public checks on
actual.NET11,2115public on actual.NET10 and41internal per target pass, including
ownership/refusal, metadata, binding, cancellation and protocol checks.

Exact corrected core and production inventory are in [assessment](assessment.json).
Five complete-script short source pairs per API retain the established affected
allocation reduction:44.96–50.38%sync/46.59–52.00%async, controls neutral. Desktop
elapsed is unstable: unchanged spreads reach16.43%, with adverse report/CSV
observations retained. This is no new elapsed/CPU confirmation; controlled B448
timings remain measurements of the earlier aa4acebe binary. Full corrected-source
Windows/Ubuntu actual11/10, original Stress and installed consumers are fully
green for committed7796a24f in [CI42459221409](delivery-ci.json). This qualifies
the corrected production inventory, not later prototypes. The frozen focused
snapshot predates that receipt. Publication postponed.

[Complete evidence](observations.tar.xz) preserves corrected source/core, terminal
failed CI and logs, focused regressions and all short original observations.
