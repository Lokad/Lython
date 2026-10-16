# Executable member augmented assignments

| Warm method job, 2,048 calls | Lython baseline | Candidate | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Construct accumulator, call add repeatedly, print | 1.1127 ms | 1.1439 ms | 0.11515 ms |

One [short diagnostic](warm.json) observed a **2.8% higher candidate median**,
with IQR/median 1.2% / 2.6% / 0.2%. **No performance benefit is established.**
The small change remains inconclusive in this unqualified run; no confidence
interval, qualified slowdown/speedup or current overall CPython multiplier is
claimed. The owned command took 7.455 s including cleanup. No repeat, retuning,
new producer/framework or long lane.

Baseline `9d0902a373f0ab3515a9d947952e4dc844ec5f61` and candidate
`58aece733e1123a11a7d043e606bd4cca4360695` differ in exactly three production
files frozen in [the declaration](comparison-declaration.json). Loaded library
versions independently identify those revisions. Comparison/catalog/Python sources
are identical, with correct role-specific binary metadata.

Member augmented assignments now evaluate the receiver, duplicate it, load the
member, evaluate the RHS and apply the in-place operator using existing
instructions. The shared member store consumes the updated value last, keeps both
operands through awaited mutation, then clears them on success or failure. This
removes their statement fallback, fallback-only mirroring and unreachable prepared
member-read arm. The same route handles yielding receiver/RHS forms. Required
prepared member stores and subscript/slice/lowered routes remain. There is no new
opcode, owner, frame, cache, read dictionary/array or 1,024-byte working fee.

Source consolidation is retained independently of timing. Existing call/slot paths
can now apply where that fallback was their last blocker, but this diagnostic does
not isolate their contribution or establish a warm-execution improvement. Earlier
in-place mutations survive setter failure, as Python requires. Fuel and narrow
admission thresholds can differ. Managed stack admission, host mediation,
preflights, cancellation and funding for retained aliases remain enabled.

The unchanged `calls.method.medium` script has 207 UTF-8 source bytes and eight
expected stdout bytes. It repeatedly executes `self.value += value + 3` and prints
`2102272`. The [baseline](baseline-correctness.json) and
[candidate](candidate-correctness.json) match independent goldens at all three
existing sizes. Both reuse compiled code and create fresh invocation state.
Timers include class/function/instance setup, the method loop, captured output/
projection, cheap golden checking and previous-result cleanup. Compilation,
process startup, transport and full hashing are excluded. This is synchronous
Run against ordinary warm CPython, without file fixtures or a file-host boundary.

Measured 2026-10-16 on the dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS,
four AMD EPYC 9V45 vCPUs / 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython
3.13.16 with ordinary GIL/GC and `-I -S`. Normal Lython protection, cooperative
allowance, GC/tiering and managed stack policy remain active; instruction fuel is
unset. CPython has no equivalent containment. This short run has no milestone
noise/host-latency qualification. [Cleanup/source proof](cleanup-source-proof.json)
checks all three frozen differences, seven exited workers, inactive services and
an available exclusive VM lease. This in-memory case creates no file fixtures.

[Local validation](local-validation.json): 2,459 ordinary white-box, 408 focused
public and 320 governed/representative/shared-consumer cases pass; groups overlap
and each owned group completes under 30 s including cleanup. Five new native
cases cover executable routing, before-mutation attribute denial and sync/async
dispatch. Thirty-one new public cases include fifteen independent
[CPython programs](member-goldens.json), twelve real-host ordinary/yielding
pause/cancel cases, retained results through a second execution and target/caller
diagnostic spans. A failed test-analyzer build and its unqualified public receipt
are preserved and excluded; authoritative baseline tests follow a successful
build. Funded behavior already passed at baseline; one structural witness rejected
its fallback. No timeout or containment assertion was relaxed. Original Stress and
full local public suites were not rerun; package/platform qualification is recorded
separately at delivery.
