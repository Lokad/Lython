# R200 — cold opcode bodies and hot instruction footprint

Both variants are withdrawn; main remains fully qualified `66ebdb4b`.

The isolated original variant moves contextful target/general/yield/definition/
container/context-manager/slice bodies out of ExecuteInstructionPrefix into one
private synchronous helper. Existing pause/pending/jump results and outer per-
instruction exception/cancellation/disposal routing remain. Original inline bodies
are deleted; native call/operand/formatting/iteration/control paths stay inline.
Source grows27lines (337→364); no state, owner, option or alternative engine.

**Original variant is not promoted.** Main remains fully qualified66ebdb4b. The
source pair is frozen039 reference versus9e0b prototype on a63229c2; all568 source
hashes, complete source/patch, original conditions, JIT bytes and test logs are in
[preparation](original/preparation.tar.xz). Focused6838public/1237internal cases
pass on each actual.NET11/10; no full platform/package replay for isolated code.

## Code and stack-shape evidence

Windows.NET11RC3 diagnostics validate every byte offset against declared native
size. A simple counter's optimized OSR prefix shrinks8932→6725B, primary stack
subtraction512→336B. This is a hot-method result, not total stack usage or CPU gain.
On a generator kernel, OSR prefix11083→9218B and primary subtraction544→416B;
the new helper separately has2363B Tier0 code and896B primary subtraction. Its
optimized tier was not observed, including the bounded longer65536-iteration
confirmation. No total code/stack reduction or Linux-byte identity is claimed.
The extra helper frame and dispatch are real tradeoffs; all dumps survive.

## Controlled complete scripts

Same five existing CSV/text/report/search/generator-text scripts, fixtures and full
stdout/file goldens; independent CPython3.13.2 verifies semantics, not timed baseline.
Existing common workers and optional adapter0.4 are unchanged. CLR allocation is
effectively flat. Desktop syncCSV/text gains coexist with report+2.2..5.5%, search
+1.9..9.9%; async search+4.7..10.8%/generator+0.9..9.2%, and compile variability.
[All desktop observations](original/desktop/) preserve every pairing. Run-only
excludes reset/goldens/result disposal; its full envelope/allocation adds them.

Quiet Azure F4as_v7/Ubuntu24.04.4/.NET11RC3, stockGC/tiering, six jobs215.904s:
five warm scripts plus text compilation, two independent processes per source,
5s warmup and seven rotated400ms rounds. All identity/output/ownership/quiet/
join/RAM/archive/SHA checks pass. [Raw](original/observations.tgz),
[all medians/CPU/IQR/pairings](original/summary.json), [receipt](original/archive-receipt.json).

| Complete workload / phase | Elapsed change | Wider process CPU | Reference / candidate control spread |
| --- | ---: | ---: | ---: |
| csv-validated-records.large / warm | -1.60..+1.51% | -2.27..+0.12% | 0.95% / 2.19% |
| text-summary-helpers.large / warm | +0.50..+1.79% | +0.29..+0.85% | 0.64% / 0.63% |
| report-render.large / warm | -3.77..-0.74% | -3.79..+1.59% | 1.35% / 1.74% |
| file-search.large / warm | -5.22..+9.41% | -3.30..+7.29% | 0.58% / 14.76% |
| text-summary-helpers.generator.large / warm | -2.36..+0.48% | -4.67..+0.00% | 1.14% / 1.75% |
| text-summary-helpers.large / compile | -0.30..+0.85% | -2.48..+3.00% | 1.08% / 0.07% |

Ranges span all four source-median pairings, not confidence intervals. Wider CPU
includes reset/protocol and coarse100Hz ticks. Warm VM clocks include mediated
RAM I/O, full validation and prior-result cleanup; reset, compilation/startup are
excluded. Compile includes construction/disposal. Async desktop I/O completes
synchronously; genuine suspension remains covered by correctness tests.

Report elapsed improves modestly with uncertain CPU. Text has a small consistent
adverse range. CSV/generator remain inconclusive. Identical candidate search
processes differ14.76% with low within-process IQR; that uncertainty cannot establish
a design regression or gain. Compilation is inconclusive and allocation flat.
Smaller hot code alone is insufficient for promotion, and unstable controls do not
establish a failed design. No primary CPython row or general CPU claim changes.

## Bounded next refinement

The helper returns jump status twice: through an out bool and through its existing
ValueTask<bool> for pending context-manager operations. Consolidate on the existing
pending result, matching comparison/iteration operation handling, and remove the
extra bool output. Hypothesis: eliminate redundant transport/address exposure
without adding a state or dispatcher. Preserve original9e0b and all adverse data.
Validate exact match/context-manager jumps, async suppression/cleanup, cancellation,
terminal refusal and owned operands before another bounded source pair. No fixed
percentage veto, permanent one-attempt rule or automatic promotion.

All VM users joined, RAM removed and persistent archive/SHA verified; terminal
owner is cached. Preparation uses the frozen uncompressed payload footprint as
well as compressed size, preserving240MiB disk floor/12MiB setup headroom. Three
exact committed duplicate compressed archives were retired under an idle lease;
reference039, priorE294, prototype9e0b and sole evidence survive. Full receipt is
in raw evidence. Publication and adapter promotion remain postponed. The broader
performance roadmap stays active.


## Single-result refinement and decision

Frozen6e52 removes the duplicate out-bool jump channel. Completed operations return
the same ValueTask<bool> decision as suspended work, consumed centrally; yield-from
still observes errors before deciding its yield boundary. No new enum/state/owner
or dispatch mode. Focused218public/939internal pass on each actual.NET11/10,
including match/context-manager jumps, real suspension/cancellation, terminal
refusal and joined ownership cleanup. Original broader gates remain preserved.

Windows simple-loop OSR6733B/336B, generator9219B/416B; helperTier02443B/960B.
This does not improve the hot frame further and increases the observed helper frame.
[Refined source/patch](refined/preparation.tar.xz), [identity](refined/source-identity.json),
[all diagnostics/JIT/source/logs](refined/preparation.tar.xz). All568 source hashes
match the frozen refinement. The unchanged original compile controls are reused;
no frontend change or causal compile-gain claim. Desktop allocation remains flat,
with CSV/text/generator gains alongside syncsearch+5.2..8.3% and asyncreport+1.6..3.6%
adversity; [all desktop data](refined/desktop/) survives.

B551 completes six jobs217.415s with all identity/output/ownership/quiet/join/
RAM/archive/SHA checks: five same warm scripts plus async report, identical worker,
adapter0.4, role controls,5s warmup and seven rotated400ms rounds. [Raw evidence](refined/observations.tgz),
[all pairings/medians/IQR](refined/summary.json), [receipt](refined/archive-receipt.json).
Clock boundaries and range limitations are the same as the original above.

| Complete workload / phase | Elapsed change | Wider process CPU | Reference / candidate control spread |
| --- | ---: | ---: | ---: |
| csv-validated-records.large / warm | -6.46..+0.89% | -5.68..+1.14% | 7.56% / 0.28% |
| text-summary-helpers.large / warm | +0.08..+2.82% | +0.42..+4.39% | 0.74% / 1.94% |
| report-render.large / warm | -5.03..+3.14% | -4.65..+4.07% | 3.32% / 4.99% |
| file-search.large / warm | +0.87..+8.79% | +0.25..+7.37% | 0.77% / 7.04% |
| text-summary-helpers.generator.large / warm | -2.43..-1.13% | -1.56..+0.00% | 0.05% / 1.26% |
| report-render.large / warm-async | -2.19..+2.61% | -2.00..+4.41% | 0.19% / 4.50% |

Decision: **withdraw both variants**. The smaller hot code does not establish
enough useful work eliminated to offset the added dispatch/helper frame and
remaining regression debt. The refined generator's modest improvement remains
valid evidence; ordinary text is adverse, search stays adverse/variable, and
CSV/report controls are inconclusive. Flat allocation provides no storage benefit.
This is a workload/complexity/debt decision, not a percentage veto or a declaration
that uncertain controls prove a failed design. Both variants and all adverse data
remain available for a changed design with new evidence; no unchanged retiming.

Main stays fully qualified66ebdb4b. The private implementation will be removed
after committed archive verification; no dormant runtime/backend will survive.
No new semantic tests or public options need removal. No full platform/package
replay for withdrawn code. Next source assessment targets actual hot operand/result
work rather than moving cold bodies; broader performance/independent regex tracks
remain active. Publication stays postponed.
