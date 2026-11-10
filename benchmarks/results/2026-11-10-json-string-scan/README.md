# R201 — bounded bulk JSON string classification (withdrawn prototype)

2026-11-10. Isolated source on717dfeb3, reference039dc975 matches fully qualified
66eb runtime; candidateb6d0c7ef is unpromoted. Core remains fully managed. A fixed
BCL search alphabet replaces ordinary per-byte classification within existing
1024byte cancellation windows. Escape/control handling, validation, original byte
and scalar positions, key reuse, allocation and ownership paths remain unchanged.
No per-token buffer, new parser, policy or memory subsystem.

Release JSON checks pass787public/194internal on each actual .NET11/10. New cases
cover all32C0controls and malformed escapes at UTF-8 window boundaries through both
APIs, preserving scalar error positions and per-execution budget cleanup/reuse.
Independent CPython3.13.2 semantic goldens pass. The first test fixture correctly
refused its512000byte allowance with32cases in one execution; its failure and source
remain. Bounded host-driven executions pass with the same allowance.

B552: six complete jobs,220.120s on the existing F4as_v7 Ubuntu24.04.4/.NET11RC3 VM,
normal GC/tiering. Four independent source/control processes;5s warmup per role,
seven rotated400ms rounds. All output/file/identity/ownership/quiet/join/RAM/archive
and local SHA checks pass. [Raw observations](original/observations.tgz),
[receipt](original/archive-receipt.json), [medians and controls](original/summary.json),
[preparation/source/failed fixture/focused logs](preparation.tar.xz).

| Complete script | Candidate elapsed change across both controls | Wider process CPU change |
| --- | ---: | ---: |
| JSON-lines helpers, sync | −5.24..−1.45% | −3.05..−0.90% |
| Report, sync | −1.72..+1.27% | −1.55..+2.38% |
| CSV control, sync | −0.47..+3.29% | 0..+3.78% |
| Text control, sync | +1.99..+3.87% | +1.56..+5.93% |
| Tiny config, sync | −0.67..+1.20% | −1.05..+0.37% |
| Report, async API | −1.77..+2.43% | −1.48..+2.33% |

Ranges are source/control comparisons, not confidence intervals. Allocation is
flat. Text never uses JSON; its adversity remains unexplained, not erased or
assigned to the scanner. Report/config benefits are inconclusive. The bounded unchanged-image confirmation below addresses this uncertainty.
Neither source promotion nor a general CPU improvement is claimed. All original desktop adversity remains under
original/desktop; those shared-process timings are diagnostic.

Run clocks include mediated RAM file I/O, capture/projection and per-run cleanup;
exclude compilation, fixture reset, verification and worker protocol. Wider CPU
also includes those surrounding operations and coarse clock ticks. These are
Lython source-pair measurements, not new CPython performance rows.

The preserved offline R199 caller mapping adds no capture:57/58 monitor samples
have explicit reserve/grow callers, one an immediate string-copy caller. `Grow`
also runs during reservation construction. The exact text fixture has three words
per nonempty line and never exceeds the eight stack ranges: these samples concern
bulk output admission, not scratch replacement. All6144words fit existing inline
payloads. Hot instructions already use ref-readonly operands. No new budget batching
or ownership architecture is justified by these facts alone. The original
preparation archive preserves the initial mistaken scratch attribution; the corrected
source gate is included with the confirmation evidence.

B553 confirmation: two jobs,76.006s, text first and reversed initial worker order,
unchanged binaries/fixtures/clocks/warmup/rounds. All gates pass, workers joined,
RAM removed, raw archive independently SHA-verified and terminal status cached.
[Raw confirmation](confirmation/observations.tgz), [summary](confirmation/summary.json),
[receipt](confirmation/archive-receipt.json), [preparation and corrected source gate](confirmation-preparation.tar.xz).
JSON elapsed−1.01..−0.04%; widerCPU−3.25..+1.52%. Text elapsed−0.24..+6.03%, CPU
−0.42..+6.64%, with candidate process spread5.16%. Original B552 text adversity
is preserved and unexplained; confirmation does not establish a scanner-caused
text regression or erase it. Stable within-process rounds do not imply stable
identical processes.

Decision: withdrawb6d0. The bounded bulk search remains semantically valid but
adds search metadata/an extra token lookup without deleting parsing/validation,
allocation or ownership machinery. The small JSON elapsed benefit, mixed CPU,
inconclusive report results and unresolved controls do not justify carrying this
performance-only path. This is a complexity/debt decision, not a fixed percentage
veto, permanent design rejection or claim of general failure. Both source/gates
and all adverse data survive. The independent new UTF-8/control/error-position
and budget-reuse regressions are useful against the existing parser and are retained.

Runtime source and qualified039/66eb reference remain unchanged. The next bounded
source assessment concerns coarse admission through the existing host reservation,
preserving cooperative cancellation, terminal refusal and cleanup. Current guards
are already inlined and remain required; no guard deletion is selected.
Broader performance and independent regex work continue; publication stays postponed.

Cleanup is complete: [local retirement](local-retirement.json) verifies the restored
568 production files before removing the isolated worktree. [VM retirement](vm-retirement.json)
proves all fourb6d0 hardlink aliases and two canonical compressed duplicates before
removal; qualified039/priorE294 survive. Private frozen diagnostics remain outside
project inputs. The [retained public tests](retained-test-validation.txt) pass14cases
on each actual runtime against main's original parser. No unchanged full runtime
qualification is repeated, and no CPython row is relabeled as current HEAD.
