# R458: bulk generator advancement

Base 82e45812; private prototype, no performance claim. Reuse Advance/AdvanceAsync
for single-item and admitted-capacity bulk consumers. Enter once per capacity
batch, check each resume, preserve caller/handler exception state, leave before
capacity growth. Default next/send/throw retain one-item behavior. Existing cursor
and drain lifetime own all storage and awaited cleanup. Full drain/error order,
maximum-item consumers, aliases, reentrancy, memory refusal and genuine async
work are semantic gates. R438 wrapper-only losses and R403/R456 regressions remain
relevant; this displaces repeated frame transitions, not just enumerable wrappers.

Stop/refine on an ownership/semantic obstacle; otherwise compare complete warm
and compile-to-result normalization/report with unchanged control, using existing
quiet VM tooling and short allocation diagnostics. Do not relax tests or claim an
elapsed gain from allocation alone. Publication held.

Before observing B861: source confirms generators.drain.large uses list(values()),
whose constructor has its own admitted array path. This is an unaffected generator
control, not an affected bulk-drain benchmark. Text normalization is the affected
complete script; report/numeric/CSV are additional controls. No catalog/source is
changed after freeze. Four new semantic programs agree with isolated CPython.
