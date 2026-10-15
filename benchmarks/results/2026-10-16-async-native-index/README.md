# Native integer-index reads through RunAsync

Exact `BigInteger` indices on sealed native list, tuple, bytes, UTF-8 string and
range values reuse the synchronous observed-value handler. This avoids entering
generic async operation helpers for ordinary reads. Other indices, mappings,
custom containers and slices keep their required asynchronous routes. Arithmetic,
indexing, result admission and ownership implementations are shared.

| Existing ASCII indexed-text job | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| 4,096 input bytes, 512 dynamic reads, checksum | 0.4388 ms | 0.3918 ms | 0.05844 ms |

One [fixed diagnostic](warm-async.json) observed a **10.7% lower Lython median**.
IQR/median was 2.0% / 4.0% / 0.2%, respectively. This seconds-long diagnostic has
no milestone qualification or claimed confidence interval; it does not establish
an overall/current CPython ratio. The owned command took 7.550 s including cleanup.
No repeat, retuning, new workload/framework or long lane.

Runtime baseline: `06e7f2dfef2d1dd012a9b6cd9ec4890b5b36e116`.
Candidate: `917d8bb9704af61bb62e49c5fab294a2d076766f`, with exactly one frozen
production difference in [the declaration](comparison-declaration.json).
The existing `strings.index-ascii.medium` script, catalog, comparison machinery
and CPython worker are identical. Both roles already contain the shared index
correctness prerequisite below; its effect is **unmeasured**.

Measured 2026-10-16 Europe/Paris (raw UTC timestamps fall on October 15), on the
dedicated Azure Standard_F4as_v7: Ubuntu 24.04.4 LTS, four AMD EPYC 9V45 vCPUs /
16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with ordinary GC/GIL
and `-I -S`. Normal Lython protections, cooperative allowance, GC/tiering and
managed stack admission remain enabled; CPython has no equivalent containment.

The boundary is warm completed-host `RunAsync` versus ordinary warm compiled
CPython code. Both create fresh invocation state, execute/check/project output
and dispose prior results inside the worker boundary. Compilation, startup,
transport and full hashing are excluded. Host I/O does not suspend in this job;
actual protocol suspension/cancellation is checked separately. The synchronous
interpreter is not the timed candidate change.

[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent expected output at all three existing ASCII text sizes.
[Cleanup/source proof](cleanup-source-proof.json) verifies one production change,
identical comparison/Python sources, seven exited workers, inactive services and
available VM lease. No file fixtures are used in this diagnostic.

The prerequisite fixes two confirmed Python behavior gaps: RunAsync rejected
valid native-sequence `__index__` objects, while Run coerced dictionary keys through
`__index__`. Native sequence/range reads now share XML's existing type-level index
converter, awaiting descriptor binding and calls in RunAsync and preserving
arbitrary-precision indices. Mapping keys remain raw. The duplicate lowered
synchronous read body is removed. Other consumers of the global legacy index
coercion helper are unchanged; no broad protocol/ownership redesign was attempted.

[Local checks](local-validation.json): 2,354 white-box and 112 focused public cases
pass (overlap; owned groups under 30 s). Controls cover all five native types,
negative/bool/huge indices, bounds failure before assignment, Unicode scalars,
raw custom keys, descriptors and invalid/error results, retained result aliases,
cleanup to baseline, and actual custom-container/index suspension/cancellation
without replay. Five [exact-read](exact-read-goldens.json) and six
[index-protocol](index-protocol-goldens.json) independent CPython 3.13.2 programs
match. Earlier 103-case prerequisite checks are preserved privately.

Initial probes preserve six supported-behavior failures and two unavailable
`bytearray` cases. The latter were removed from supported-sequence controls;
bytearray support was not introduced. No timeout, allowance or containment
assertion was relaxed. No collector, forced GC, native shortcut, second indexing
implementation or configuration is added. Exact package/platform qualification
is recorded at delivery; original Stress and full local public suites were not
rerun. The broader AP02 operation-coverage work remains partial.

Full CI38825310166 caught an existing error-text contract outside the focused
group: the shared converter omitted the returned Python type in invalid-index
errors. The regression is reproduced locally; a follow-up restores the type name
and checks None/string/float results in both APIs. All 143 expanded focused public
checks pass, with one additional independent CPython program.
[Post-label evidence](post-label-validation.json) and [golden](post-label-goldens.json)
are separate from the original validation. This one additional resolution-source
change is **unmeasured**; the frozen dispatch file is unchanged. Original CI/log,
failing receipt and timing remain preserved. No assertion relaxation or timing
repeat. Final HEAD therefore includes this declared follow-up beyond the timed
candidate; exact package/platform qualification is checked again at delivery.
