# Call/frame CPython checkpoint — 2026-11-06

Twelve complete primary-script jobs finished328.12s, all quiet/golden/funding/
join/fixture gates pass. [Design/boundaries](contract.md), [identity](identity.json),
[raw evidence](observations.tgz), [summary](summary.json). Frozen core520b3eb0
matches committed1134d6e8 runtime source; built before commit, assembly metadata
5932fe20. Per-file hashes verified unchanged. Source includes compiler-proven
lexical loads, unified invocations, nonlocal-map deletion and ordinary method
operands. Reference is F04/core0050f188, not P01 or K01; do not compound deltas.

Primary [current performance guide](../../../docs/BENCHMARKS.md) leads with the
external CPython comparison and separates compile, warm and compile-to-result.
It records exact machine, ratios, role spreads/IQRs and the included mediated
RAM I/O/complete validation/prior-result cleanup. Startup/staging/reset/transport/
final teardown excluded. Normal GC/tiering; CPython imports persist while Lython
imports are execution-isolated. No causalCPU/allocation/RSS/embedding claim.

Compared with F04, primary CSV and warm text roles straddle the reference. JSON
elapsed is small/mixed; fresh text is+1.75–2.01%, despite only0.25% current-role
spread. **Accept that possible fresh-text regression for F05's completed calling
convention**, with explicit debt for the next coherent redesign; neither dismiss
it as noise nor launch another measurement merely to change signs. New extra
instance-method proof work on native calls is a hypothesis requiring source/CPU
evidence, not established attribution. Whole method CSV's separate process
allocation−7.26–7.28% and warm elapsed gain support retaining the simpler transport.

Report compile/warm/fresh candidate IQR6.60/5.10/7.49% stays variable. Cross-block
CPython elapsed also differs; historical ratio changes alone do not isolate
Lython source effects. K01's older warm CSV/text debt remains preserved separately.
No broad whole-script speedup is established by this checkpoint. Existing earlier
source qualifications were not replayed. Full local4210internal/16088public and
installed Release consumers pass both actual runtimes; full delivery on1134d6e8 is GREEN, cached42158371981/all8.
[Scope and steps](../2026-11-06-direct-method-operands/delivery-ci.json) exclude B01. Publication/tagging postponed; performance objective stays open.
