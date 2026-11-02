# Structured executable fallthrough — B131

2026-11-02. Retained compiler simplification; baseline060a8359/core12342de9,
candidate16e38f8463b0352756721f06f0680f2f07d3c32b9487526a161070e365f6ea5b.
No new public API or runtime dispatch mechanism.

`for`, `while`, `if` and comprehension success arms now append to their current
instruction block, using existing ForNext/JumpIfFalse fallthrough. Fifteen
compiler lines become four; no fresh body/filter block or unconditional entry
jump. Protected suites retain explicit entry blocks and stack restoration.
The range body repeats seven instructions instead of eight. Existing operand
ownership, callbacks/suspension, exception routing and per-instruction checks remain.

## Controlled comparison

Quiet Azure F4as_v7 Ubuntu24.04.4/.NET11RC3, ordinary workstation GC/tiering.
Existing unchanged worker/protocol, four-second warmup per image, nine interleaved
~200ms batches; full output/file checks, actual mediated I/O and prior-result
cleanup. Warm execution excludes compilation/startup. Final retained result,
cached program, process and fixture teardown is outside batches and joined.
External Linux TGID CPU includes runtime/JIT/response, not guest-only CPU.

| Warm complete case / block | Elapsed change | Process CPU | Unchanged elapsed / CPU |
| --- | ---: | ---: | ---: |
| CSV /0 |−2.19%|−1.29%|+1.70% /+0.90%|
| CSV /1 |−1.03%|−0.19%|−2.30% /−2.14%|
| Text /0 |−3.37%|−3.86%|−1.01% /−3.50%|
| Text /1 |−7.43%|−10.00%|−6.75% /−9.41%|
| Range /0 |−8.83%|−9.15%|−0.68% /−0.97%|
| Range /1 |−8.48%|−7.69%|+0.48% /+0.54%|

Range elapsed falls about8–9% against controls. Two additional90785737/core137b563a
images qualify the earlier shared-iteration regression directly: candidate elapsed
−6.34%/−8.57%, identical legacy controls+0.32%/−0.63%; process CPU−7.91%/−7.76%
versus−0.57%/−1.57%. This closes the recorded2–3% range debt for these inputs.
CSV relative controls move in opposite directions; text CPU mostly follows controls.
No broad whole-script CPU improvement, allocation reduction or CPython ratio is
established. No fresh/compile-only timing was requested for this small code change.

All six jobs, complete goldens, image/manifest identities and both quiet gates PASS.
Owned service exits0 in138.954s; inactive, workers joined, fixtures removed, lease free.
[Assessment](assessment.json), [collector](collect.py) and
[all raw warmups/samples](raw-results.tar.gz) retain adverse results and both legacy
controls. Median comparisons are within this source pair, not independent histories.

## Correctness and qualification

Focused Release670 internal/1,701 public cases PASS on each actual.NET11RC3 and
.NET10.0.8: loop targets/else/break/continue, comprehensions, generators, protected
cleanup, async functions, native helpers, exception/traceback behavior, cancellation,
terminal admission refusal and governed ownership. Existing assertions remain;
one internal minimum-block-count assertion described the displaced layout and
is deleted. Its initial failing run is preserved with the passing checks.

Full platform/stress/installed-package qualification is batched at the next coherent
checkpoint. Run41535109342 describes exact060a8359 and does not cover this compiler
change. External CPython measurements remain90785737 plus named historical rows.
Publication/tagging remain postponed; the broader operand/call objective stays open.
