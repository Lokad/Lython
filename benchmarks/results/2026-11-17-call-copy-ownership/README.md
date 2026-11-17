# Copied callable storage: admission and pending ownership

**First attempt invalidated by full CI44091209377.** Both platforms caught
two existing retained-array regressions. The owned callable ABI permits retaining
fresh arrays, so clearing them at completion is incorrect. This page and B903
measurements describe frozen384327c2 only. They do not qualify its replacement.
See [failed delivery and exact inputs](delivery-first-failure/README.md).

R486/B903,2026-11-17UTC. Historical first attempt; qualification failed.
Qualifieddbdf8262/frozena95ff2a4 versus four-file source on9d9fd1e9/frozen384327c2.
585production/2382build-test inputs archived; public API unchanged. No wider
argument view/collection bridge or migrated binder signatures remain. Original
[R485 prototype and decision](../2026-11-17-borrowed-binding/README.md) stay separate.

General default copied packets and non-function bound receiver prefixes now use
one existing conservative capacity estimate and temporary reservation. Admit
before allocation/body effects, preserve references/funding through pending users
and awaited cleanup, then clear/refund. No independent allowance, new owner
framework, retry, tracing, per-object ledger, quota or abandoned work. Small
control headroom remains; major copies may reject earlier. Deterministic native
cleanup/refunds and terminal first-refusal behavior remain.

Four independent admission/cancellation regressions fail qualified4bfsource after
a nonincremental build and pass final source. Current actualWindows11/10 each
pass38internal/1050ordinarypublic/422continuation-generator cases; filters overlap
and are not summed. Extra18context-manager/native-cleanup11 cases pass. Private
baseline test copy retired after exact archived/current-source verification.

| Complete workload / lane | Elapsed change | Wider process CPU change | Reference/candidate control spread |
| --- | ---: | ---: | ---: |
| calls.bound-wide.cleanup / warm | -1.58..+4.71% | -2.44..+4.94% | 5.26/1.08% |
| calls.bound-wide.cleanup / compile-run | +2.13..+3.89% | -0.27..+5.76% | 1.30/0.42% |
| scripts.json-summary-helpers.large / warm | -1.22..-0.15% | -1.43..+0.00% | 0.77/0.31% |

The affected synthetic bound-wide max uses seven Python comparison callbacks per
call,1025calls including the identity assertion. Correct funding adds32800allocated
bytes (+2.86%) and288Baccountedpeak. Fresh elapsed+2.13..+3.89%; widerCPU−0.27..+5.76%.
Warm controls move5.26%/1.08%, so no precise warm CPU claim. JSON-lines control
allocation/peaks essentially flat. All shared-budget baselines restore. This cost
is accepted for required admission/lifetime correctness, not claimed as a gain.
One bounded diagnostic is sufficient; no unchanged confirmation campaign.

Actual copied32file workers pass both catalog goldens against CPython3.13.2 before
staging. Initial diagnostic used unavailable types module and failed before
timing; original source/result preserved. Revised supported classmethod(max)
keeps Python identity and comparison semantics. No production compatibility change
was made to accommodate the benchmark.

Quiet AzureF4as_v7/Ubuntu24.04.4/.NET11RC3,3jobs117.05seconds,6second rotated
warmups/five samples/four processes; normalGC/tiering. Mediated I/O/full output/
file validation/prior-result cleanup included; compilation only fresh. Staging,
reset/protocol/startup/final cache teardown excluded. WiderCPU is quantized.
All quiet/golden/Closed/exit/join/archive checks pass; RAM removed,terminal cached.
Only exact committed withdrawn912core/raw/archive and older profile archive
retired with fresh lease/path/UID/hash/link/all-alias proof. Currentqualifieda95,
toolchains/canonicals and254/252MiB floors preserved. Full Windows/Ubuntu,
original stress and installed Release consumers qualify the final four-file
boundary next. Publication held; substantial performance roadmap remains active.

[Decision](decision.json),[source](candidate-sources.tar.xz),[catalog](catalog.json),
[summary](summary.json),[allocation](allocation-summary.json),[raw](observations.tgz),
[identity](identity.json),[checks/original failure](procedures-and-checks.tar.xz).
