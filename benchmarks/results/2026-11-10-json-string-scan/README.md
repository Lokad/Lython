# R201 — bounded bulk JSON string classification (active prototype)

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
assigned to the scanner. Report/config benefits are inconclusive. Select one
bounded two-job confirmation of JSON-lines/text with unchanged binaries and
independent controls before a retain/refine/remove decision. No promotion or
general CPU improvement is claimed. All original desktop adversity remains under
original/desktop; those shared-process timings are diagnostic.

Run clocks include mediated RAM file I/O, capture/projection and per-run cleanup;
exclude compilation, fixture reset, verification and worker protocol. Wider CPU
also includes those surrounding operations and coarse clock ticks. These are
Lython source-pair measurements, not new CPython performance rows.

The preserved offline R199 caller mapping adds no capture:57/58 monitor samples
have explicit reserve/grow callers, one an immediate string-copy caller. Split
outputs already have one bulk admission; remaining scratch growth is geometric
replacement with overlap. Hot instructions already use ref-readonly operands and
short strings already have inline payloads. No new budget batching or ownership
architecture is justified by these facts alone.

Main source and its qualified reference remain unchanged. Broader performance and
independent regex work continue; publication stays postponed.
