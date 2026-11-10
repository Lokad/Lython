# R180 fixed-fact refinement: withdrawn from promotion

After B526/B527, this version moves immutable string method/signature/return facts
to compilation, isolates branch-only proof copies and removes one unused window
cache wrapper. Runtime checks, shared-record admission/reentry correction and all
original adverse observations remain. It changes neither Python semantics nor
public options; the production runtime was never replaced.

Quiet B528, 2026-11-10, F4as_v7 Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering:

| Complete workload | Elapsed change | Wider process CPU change |
| --- | ---: | ---: |
| CSV with ordinary conversion helper | -1.39 to -0.74% | -6.25 to 0.00% |
| Original text, synchronous | +31.21 to +37.49% | +29.31 to +36.84% |
| Original text, asynchronous | +38.75 to +44.58% | +36.54 to +53.00% |
| Original report control | +0.22 to +4.49% | +4.44 to +6.25% |
| Unicode control | -0.09 to +2.09% | +0.40 to +6.68% |

Ranges span all four independent process-median pairings, not confidence intervals.
Text sync/async process spreads and IQRs remain in summary.json. Controls vary;
coarse CSV CPU ticks do not prove equivalence. B526 is a different source version,
so the two sessions are not an isolated estimate of this refinement's benefit.
The remaining whole-text regression is material and the current version is not
promoted. A shared-prefix architecture is not permanently ruled out.

Desktop helper-CSV allocation remains42.95% lower; other warm allocation is flat
apart from tiny setup differences. Desktop elapsed is unstable and diagnostic.
Compile allocation falls0.09-0.76% on affected scripts, controls flat; compile
elapsed is also diagnostic. The original compile analyzer used execution clocks
(zero in this lane) and failed after completed measurements; the corrected
postprocessing uses elapsedMilliseconds without rerunning. Both survive.

Final focused Release checks:64internal/135public per actual11/10, including
cooperative cancellation, retained aliases/refusal/reuse and original stack cases.
This is not final platform/package qualification. Exact sources/core/goldens,
commands/raw observations and all cleanup proofs are retained. Main keeps its
qualified runtime and independently useful record semantic regressions.

All five jobs complete141.251752591s; identity/golden/balance/quiet/join/unmount/
archive/localSHA gates pass. No remaining VM workers or RAM fixtures. Persistent
space was recovered from exact redundant copies of two superseded prototype cores
and one compressed archive. All frozen core bytes remain in tracked canonical
archives; all hardlink aliases were proved under an idle lease before retirement.
Current production baseline/source/raw trees remain. See space-recovery.json.

Next work profiles existing production CSV/search scripts to select a coherent
call/frame, record or text redesign from dominant costs, without reopening this
runtime proof loop or repeating unchanged qualification. Publication is postponed.
