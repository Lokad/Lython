# One lexical nesting pass — R55

2026-11-07, parent b7aad281 / retained R53 runtime d04024ee. Remove the
96-line whole-source nesting scan and its diagnostic factory. The tokenizer's
existing depth counter now enforces the same public512 delimiter limit before
recursive parsing, using its shared string/f-string scanner. No new option,
analysis representation, ownership layer or runtime execution path.

This also fixes demonstrated false rejection of valid same-quote nested f-string
literals containing513 parentheses. Both quote forms fail on the baseline with
LA0003 and pass after consolidation, returning the expected text through Run and
RunAsync. Independent isolated CPython3.13.2 agrees. All three delimiter families
still refuse excessive actual nesting; existing parser64/unary256/f-string limits,
managed stack admission, cooperative cancellation and shared-budget refusal remain.
Leading-indent or lexical errors may now take priority in already-invalid source.

Focused Release **802 internal /1,555 public per actual .NET11/.NET10**, plus
**14 pathological-input subprocess checks per target**, pass. Matching probes are
built explicitly and their loaded core hashes agree with the checked production
cores. Original subprocess deadlines and safety assertions are unchanged. No
architecture-only tests or exact cancellation-check counters are added.

One existing five-script before/current/identical-before diagnostic separates
compilation, compile-to-result and warm control.32warmups/image, five rotated
rounds of four jobs, normalGC/tiering, isolatedALCs sharing CoreLib/hashseed/processGC.
The worker/catalog/optional0.4 adapter are unchanged. Full public compilation/
execution, mediated I/O, output/file validation and result cleanup are included
in their named phases; startup/final cached teardown are excluded. Compilation
does not execute guest code inside its clock. All15jobs pass goldens/funding/
isolation/fixture cleanup. The unchanged driver's purpose caption refers to R45;
the frozen images/catalog and this boundary identify the R55 comparison.

**Allocation is effectively flat; elapsed is diagnostic.** Compilation search
−0.27/−0.81%, text−4.87/−2.54%; CSV−12.28/+19.82% with−26.79% baseline drift,
JSON+11.15/−1.52%, report−14.98/−27.38% with17.06%baseline drift. Fresh report
is adverse+3.43to+14.86%; warm text+8.27to+20.07% with−9.82%identical-baseline
drift. Other controls are mixed. Every original observation remains. No CPU
gain, general warm improvement or quiet-VM elapsed result is established, and
possible timing debt is not dismissed because warm runtime source is unchanged.

Retain for the deleted pass/safety invariant and valid-Python correction. This
does not complete the whole-script performance objective or repay earlier debt.
No additional quiet-VM campaign is selected for this small frontend cleanup;
later coherent checkpoints can resolve the elapsed uncertainty. Publication and
tagging remain postponed. Full platform/Stress/installed-consumer delivery pending.

[Baseline source identity](baseline.json), [red regression run](baseline-red.log),
[focused sources/cores](focused.json), [fresh matching subprocesses](subprocess-stacks.json),
[CPython oracle](cpython-oracle.log), [diagnostic identity](comparison-identity.json),
[all observations](local-summary.json), [decision](decision.json)
and [source/core/command/raw evidence](development.tar.xz) preserve the result.
