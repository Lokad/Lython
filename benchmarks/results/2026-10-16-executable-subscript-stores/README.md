# Ordinary subscript stores: executable headers and type-slot correctness

| Warm complete text-summary job, 3,072 lines / 35,840 input bytes | Lython baseline | Candidate | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Read, lowercase/split/count, sort and write report | 6.7951 ms | 6.4143 ms | 1.2393 ms |

One [short diagnostic](warm.json) observed a 5.6% lower candidate median, with
IQR/median 12.4% / 4.8% / 1.9%. **The timing improvement is inconclusive.** No
qualified speedup, confidence interval or current overall CPython multiplier is
claimed. The compiler consolidation remains for genuine executable coverage and
removal of ordinary general target-read evaluation. The owned command took
8.484 s including cleanup; no repeat, retuning, new workload or long lane.

Baseline `a5a96af2612afa23eeafb5fbc59a8b4e7de7a74d` already contains the correctness
prerequisite: implicit `__setitem__`/`__delitem__` use type-MRO lookup rather than
instance fields/getattr, and async mutation awaits descriptor binding and the
resulting call. Both measured roles contain that fix; its effect is unmeasured.
Candidate `7caf483372a2f941e0eeacb85cb3470d29683cca` changes exactly three production
files frozen in [the declaration](comparison-declaration.json).

The compiler emits ordinary RHS, receiver and key expressions in their Python
order using the existing frame. A fixed-shape operation holds three existing
operand slots through synchronous or genuinely suspended mutation and removes
them in `finally`. It carries only the target diagnostic span; the statement
checkpoint span stays separate. Ordinary stores no longer create a general
LoopStoreTarget binding or call syntax/lowered target-read evaluation. There is
no new per-store array, read dictionary, frame, owner, cache or binder, and no
claim that this removes ordinary local mirroring or previous per-store arrays.
Explicit generator-yield, unpacking and augmented target routes remain required.
No collector machinery or unmanaged dependency was introduced.

Both roles use unchanged `scripts.text-summary.large` (fixture size 1,024 yields
3,072 physical LF input lines). Output is `6144` plus a 42-byte, four-line report;
[baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent stdout/file goldens at all three existing sizes. Fixture JSON
encodes binary file contents in Base64; encoded length is not input size.

Measured 2026-10-16 on dedicated Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS,
four AMD EPYC 9V45 vCPUs / 16 GiB RAM; SDK 10.0.401/runtime 10.0.12, CPython
3.13.16 with ordinary GC/GIL and `-I -S`. Both engines use warm compiled code,
fresh invocation state and matched efficient file-host operations. Timers include
file reset/staging, execution, projection, exact stdout/file checks and previous
result cleanup; compilation, startup, transport and full hashing are excluded.
This is synchronous Run. Normal Lython cooperative admission, public protections,
GC/tiering and managed stack admission remain enabled. CPython has no equivalent
containment. [Cleanup/source proof](cleanup-source-proof.json) verifies all three
frozen changes, identical comparison/catalog/Python sources, seven exited workers
and removed fixtures, inactive services and an available VM lease.

[Local validation](local-validation.json): 2,358 white-box, 416 focused public and
284 governed/representative public cases pass (overlapping groups, each under
30 s including cleanup). Four structural/admission/async-only descriptor cases
also pass. Sixteen [independent CPython 3.13.2 programs](cpython-goldens.json)
cover order, raw keys, chained partial mutation, type-only lookup, deletion and
local/cell headers. Real host gates exercise RHS/receiver/key/descriptor/setter
suspension and cancellation without replay; retained aliases stay funded,
denied native insertion leaves the dictionary unchanged, and cleanup returns the
shared allowance to baseline. No timeout or containment assertion was relaxed.
Original Stress and full local public suites were not rerun; package/platform
qualification is recorded separately at delivery.

An initial build failed on a missing required property span. Tests mistakenly
run against stale assemblies after that failure are explicitly excluded from
qualification; successful builds precede the passing groups above. A later
public run exposed an existing LA3157 false positive when a handler reads a key
written by an earlier target before a later setter throws. The [original source
and CPython result](chained-failure-reference.json) and [baseline diagnostic](chained-failure-baseline-probe.json)
remain evidence for a separate frontend follow-up. The runtime control prints
the complete mutated mapping instead, preserving its partial-write assertion.
