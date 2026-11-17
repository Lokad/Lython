# Direct closed-operation selection in the shared loop

R495/B918,2026-11-17 UTC. **Private86714ae4; not promoted.** Qualified33bd7efd/
frozen5e7 versus the changed source on22d5a5f3. Builds on the
[lean control-state footprint](../lean/README.md); previous source conditions and
adverse/control evidence remain separate.

Move existing closed arithmetic, sequence-index and comparison selection into
the one opcode loop. Delete TryExecuteBuiltinArithmetic, TryExecuteSequenceIndex
and ExecuteComparisonInstruction selector frames. Completed comparisons keep
bools instead of ValueTask wrap/unwrap; real async/protocol helpers retain their
pending work and error/retirement order. Pure numeric/index kernels, native returns,
managed control refs and shared synchronous retirement remain. No new primitive
peephole, owner, arena, dispatch framework, backend, resource policy or public mode.

Nonincremental Release build and actual Windows11/10 each1,006internal/4,217public
focused checks pass. Five actual copied32-file worker goldens and both allocation/
allowance reuse diagnostics pass. Accounted peaks unchanged; allocation effectively
flat. This is not full platform/stress/installed-package qualification.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference/candidate process spread |
| --- | --- | ---: | ---: | ---: |
| scripts.numeric-squared-error.floating.large | warm | +9.95..+11.48% | +11.07..+11.31% | 0.42/0.96% |
| scripts.numeric-squared-error.floating.large | compile-run | +8.02..+18.79% | +5.87..+18.43% | 6.70/3.06% |
| scripts.text-normalization.large | warm | +2.47..+4.76% | +0.64..+4.47% | 0.79/1.43% |
| scripts.json-summary-helpers.large | warm | +5.13..+8.63% | +3.73..+8.46% | 0.81/2.50% |
| calls.positional.large | warm | +18.49..+22.40% | +18.47..+20.36% | 2.38/0.89% |

193.09seconds/five jobs, quiet AzureF4as_v7,
Ubuntu24.04.4/.NET11RC3, six-second rotated full-request warmups/five rounds/four
processes, natural GC/tiering. Ranges compare all source/control medians; negative
is lower. Retain original distributions. Fresh reference control spread limits
magnitude. These remain adverse against qualified production. Do not subtract
percentages from earlier source conditions to claim a causal refinement gain.
No unchanged confirmation or further field/selector rearrangement selected.

The current representation needs a larger activation/operand design decision;
fewer lines and smaller construction have not established useful CPU gains.
Preserve all three private sources, not a permanent design rejection. Keep the
prototype outside production and retain the independently useful compiler cleanup
bound and external regressions. Before a new redesign, name actual work/storage
removed, affected complete scripts/control, semantic/cancellation/ownership
acceptance and compile-to-result tradeoffs. No new dispatch framework or hidden
mode. The broader performance objective remains active.

Warm clocks include complete Run, mediated RAM I/O, stdout/file goldens and
previous-result cleanup; fresh adds compile/compiled-owner disposal. Startup,
fixture reset/protocol and final cache teardown excluded; external CPU wider.
This does not refresh CPython performance ratios. All actual-image/golden/quiet/
Closed/exit/join checks pass; RAM SHA-archived persistently under lease before
unmount. Only superseded9d59 VM aliases and exact committed completed raw replicas
retire with fresh path/UID/hash/link/all-alias proof. Qualified5e7/private8671,
current catalog/toolchains/canonical evidence remain. Terminal cached; publication
held. Producer source4fdb981e changes do not close the three exact capability gaps;
no adapter pin/package qualification change.

[Summary](summary.json),[allocation](allocation-summary.json),[decision](decision.json),
[source](candidate-sources.tar.xz),[prototype](prototype.patch.gz),[identity](identity.json),
[checks](checks.json),[raw](observations.tgz),[procedures/codegen](procedures-and-checks.tar.xz).
