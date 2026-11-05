# Identity completion refinement — before code

Original B297 retained:85.76s/three jobs/all gates. Combined B1/B2 versus correct
478 baseline: CSV+0.46/+0.09%; text-1.33/-3.30% with2.00% identical-current spread;
synthetic custom-set queries+5.05/+4.39%, current spread0.63%, roleIQR1.08/0.51%,
reference0.43%. The synthetic regression is credible, not dismissed by native
controls. No allocation/CPU/embedding claim; preserve original source/conditions.

Source: ResolveProtocolItem verifies current side-entry sequence before equality,
then re-verifies it even for a reference-identity hit. No guest code/await occurs
between those checks on identity. Complete identity matches after the first check;
keep both actual current-entry checks around real guest equality and the mandatory
post-callback execution/refusal check. No new state/index/cache/owner or weakened
retained-alias guarantee. Real cancellation/refusal inside new equality path gets
focused coverage, distinct from existing hash failures.

One justified refinement and <=120s two-job confirmation, target~60s, through the
same worker/protocol: synthetic stable set-query case and complete text control,
same frozen478 reference and independent identical-current/CPython processes.
Existing warmup/rotated-round/normalGC/source/golden/quiet/Closed/finalzero/join
gates. Keep the original+5% result regardless of confirmation. Decide remaining
regression debt explicitly; thresholds do not veto necessary correctness or
coherent deletion. No broad retiming/profiling campaign.
