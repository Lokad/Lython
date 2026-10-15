# Direct builtin list append

Ordinary one-positional-argument `items.append(value)` calls on exact builtin
lists now carry receiver and immutable operation separately. They avoid creating
a bound method, the packaged call-argument array and the builtin bound-value array,
including across different lists at one call site. Both execution drivers share
the same synchronous append operation, storage preflights and guard checkpoints.

Fallback member lookup still happens before argument evaluation, can suspend, and
keeps the resolved callable with a neutral receiver operand. A list-valued guest
attribute cannot be mistaken for an internal receiver. Exposed methods remain
fresh. Keyword/expanded/unbound and other method families retain general binding.
No cache, pool, option or universal callable framework is added.

This work also fixes builtin `list.append(value=...)` accepting a keyword and
mutating. Its existing signature now marks the value positional only. Exposed,
descriptor-bound, unbound, expanded and lowered calls reject it before appending;
argument side effects still occur before binding errors. User methods can accept
keywords. Unsupported builtin-list subclassing remains explicit and outside this
change. [SPEC](../../../SPEC.md) records the binding contract.

| Warm `RunAsync` | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Append to 16,384 distinct lists and total their values | 9.998 ms | 7.819 ms | 1.132 ms |

One [diagnostic](warm-async.json) observed a 21.8% lower Lython median; baseline /
candidate / CPython IQR-to-median is 2.5% / 3.7% / 1.5%. No qualified speedup,
confidence interval or overall multiplier. All seven rotating batches, calibration,
warmup and loaded identities remain. No retries, retuning or replaced samples.

The owned command took 7.806 seconds including cleanup under a 30-second bound.
Timed work includes fresh invocation state, execution, capture/projection, golden
checks and prior-result cleanup. Compilation, startup, transport and full response
hashing are excluded. Async host operations complete synchronously in this CPU
diagnostic; actual suspension/cancellation is covered separately by lifetime tests.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16, ordinary
GIL/GC with `-I -S` (UTF-8 mode off, explicit UTF-8 protocol I/O). Independent
local goldens use CPython 3.13.2 with `-I -S -X utf8`. Lython's normal public
policies/shared allowance and ordinary GC/tiering remain; CPython has no equivalent
containment. This case does not establish current complete-script performance.

## Provenance and validation

Baseline is 99e8701fe3d4d3e043d8ff1d4ea761003305580c. Candidate was built before
commit on that revision plus nine production files, identified by
[frozen hashes](comparison-declaration.json). Both roles use the identical new
distinct-list catalog case; comparison/execution tooling is unchanged. [Baseline](baseline-correctness.json)
and [candidate](candidate-correctness.json) match CPython at all three sizes.
[Cleanup/source proof](cleanup-source-proof.json) verifies all seven workers exited,
exact producer differences, identical tooling, inactive services and lease access.

Thirteen [independent Python programs](cpython-goldens.json) pass both public APIs
and return shared-budget accounting to baseline. They cover keyword rejection
without appending, `**kwargs`, descriptor/unbound/lowered calls, exposed methods,
argument effects, receiver rebinding, non-callable list attributes, raw function
attributes, generators and deque fallback.

Storage witnesses prove argument-pool avoidance and absent bound-method caches
across distinct receivers. Suspended successful/cancelled arguments retain the
evaluated receiver. A suspending/cancelled fallback property resolves once before
argument evaluation, including a getter on the retained lowered route.
Exact ten-step guard tests retain error spans and mutation
order, including the existing post-mutation collection checkpoint. A blocked
64 KiB allowance rejects replacement capacity before mutation and preserves old
contents/funding; it does not depend on tiny scalar thresholds. General binding,
exception translation, managed stack admission and operand cleanup remain.

Focused 96 white-box / 161 public cases, 103 governed CSV/text/JSON/file controls
and all 2,182 ordinary white-box tests pass. Groups overlap; each takes less than
30 seconds including cleanup. [Validation](validation.json) retains receipts and
TRX hashes. Initial fixture failures remain recorded: the module convenience API
throws ReturnSignal, the collection checkpoint adds the tenth step, a 64-byte
case denied later result observation rather than storage, and bytearray is an
explicitly unsupported builtin. A getter fixture also incorrectly required an
executable slot frame inside a lowered getter; it now checks the active enclosing
invocation, single lookup and argument ordering. Fixtures were corrected without
changing runtime limits or timeouts; final capacity testing uses ordinary owners.
No new full stress run is claimed. Package/platform gates are recorded at delivery.

This closes the exact list-append family and its keyword bug. Dictionary/string
methods, other signatures, larger function-local layouts and the broader plan
remain open. No general builtin signature audit or subclass redesign was started.
