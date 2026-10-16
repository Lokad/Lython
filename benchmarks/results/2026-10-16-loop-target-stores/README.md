# Shared executable loop-target stores

| Warm stable-sort job, 2,048 tuples | Lython baseline | Candidate | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Build with comprehension, sort with lambda key, print | 1.6082 ms | 1.5503 ms | 0.3033 ms |

One [short diagnostic](warm.json) observed a 3.6% lower candidate median, with
IQR/median 3.5% / 0.9% / 0.5%. **The timing improvement is inconclusive:** its
magnitude is comparable to baseline variability. No qualified speedup, confidence
interval or current overall CPython multiplier is claimed. The owned command took
7.703 s including cleanup. No repeat, retuning, new producer/framework or long lane.

Baseline `722f4babf813eb3ed2f95fc4e7f29e36bc951355` and candidate
`155843d0f55319f7952dc30b9522156c1e72c55f` differ in exactly three production
compiler files frozen in [the declaration](comparison-declaration.json). Loaded
library versions independently identify those commits. Comparison/catalog/Python
worker sources are identical; binary metadata correctly identifies each role.
The earlier AL lambda snapshot remains historical, not a baseline for this change.

Ordinary for targets and eager-comprehension targets now share the existing
executable unpack/member/subscript/name compiler. Comprehension names use the
existing resolved namespace store, which selects the inlined binding before
caller directives or slots. Ordinary loop names keep their slot/cell stores.
This removes eligible general target bindings and visitor dispatch; it adds no
opcode, invocation frame, runtime owner, cache or blanket namespace mirroring.
Required slice fallback and lowered/lazy-generator routes remain. The input
comprehension exercises the changed name-store path; this timing does not isolate
all structured targets or other interpreter costs.

Starred-rest admission now follows the previously delivered executable policy:
refuse before prefix effects, retain completed outer stores, admit frame transfer
while the input buffer is live, then keep surviving values funded. Exact fuel and
resource-failure thresholds can change. No guest tracing or collector machinery
was added. Managed stack admission, host mediation, preflights and cancellation
remain enabled.

Both roles use unchanged `lists.stable-sort.medium`: 109 UTF-8 source bytes,
2,048 generated tuples and 21,419 expected stdout bytes. The
[baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent output goldens at all three existing sizes. Both engines reuse
compiled code and create fresh invocation state. Timers include input construction,
sort, captured output/projection, cheap golden checking and previous-result cleanup;
compilation, process startup, transport and full hashing are excluded. This is
synchronous Run against ordinary warm CPython, not a fresh compile/run measurement
or a file-host workload.

Measured 2026-10-16 on the dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS,
four AMD EPYC 9V45 vCPUs / 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython
3.13.16 with ordinary GIL/GC and `-I -S`. Normal Lython protection, cooperative
allowance, GC/tiering and managed stack policy remain active; instruction fuel is
unset. CPython has no equivalent containment. This short command has no milestone
noise/host-latency qualification. [Cleanup/source proof](cleanup-source-proof.json)
checks all three frozen changes, seven exited workers, inactive services and an
available exclusive VM lease. This in-memory case creates no file fixture folders.

[Local validation](local-validation.json): 2,391 white-box, 426 focused public and
320 governed/representative/shared-consumer cases pass; groups overlap and each
owned group completes under 30 s including cleanup. Nineteen native target cases
also pass. Thirty-two new public cases cover directives, nested shadowing,
locals/class/walrus/closure behavior, error spans/callers, retained rest aliases
across executions, actual target/descriptor suspension and cancellation without
replay, and cleanup to baseline. Fifteen [independent CPython outputs and three
syntax-error controls](cpython-goldens.json) preserve the distinction between a
yielding outer iterable and an invalid yield inside a comprehension target.
Baseline funded behavior already passed; ten new native checks rejected its old
routing/admission order. No timeout or containment assertion was relaxed. Original
Stress and full local public suites were not rerun; exact package/platform
qualification is recorded separately at delivery.
