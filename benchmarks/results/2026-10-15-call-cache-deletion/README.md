# Remove the identity-only executable call cache

The executable call cache stored the target cast to `ICallable`; the prepared
invoker still performed that interface check on cache hits. It cached no binding
or member resolution. Async frames allocated its array without consuming it.

Removed the record, per-frame arrays, compiler counter/allocator, call instruction
operand/projection and cache-only assertions. Generator admission now reflects
the remaining storage. The general binder and member-resolution cache remain
needed. No replacement cache, public option or execution route is introduced.

| Warm `Run` | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Keyword helper, 16,384 calls | 4.171 ms | 4.262 ms | 1.174 ms |

The difference is within observed spread: baseline/candidate/CPython IQR/median
is 3.5%/6.8%/0.5%. **No timing improvement or regression is established.** Keep
the mechanism and allocation removal, without claiming an elapsed speedup.
The single [diagnostic](warm.json) retains seven rotating batches, calibration,
warmup and loaded identities. No samples were replaced and no timing was retried.

The owned comparison takes 8.037 seconds including cleanup under a 30-second
deadline. Timers include fresh invocation state, execution, capture/projection,
golden checks and prior-result cleanup. Compilation, startup, transport and full
response hashing are excluded. Both engines run ordinary warm code; no I/O delay
is injected. This does not qualify a multiplier, confidence interval or overall
application result.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16,
ordinary GIL/GC, `-I -S -X utf8`. Lython's public policies and shared allowance
remain enabled; GC/tiering are unchanged. CPython has no equivalent containment.

## Provenance and validation

Baseline is 6262ea9437952ac46dc4d3ea8a448bcec348eed0. The candidate was built before
commit on that revision plus eight production files, identified by
[frozen hashes](comparison-declaration.json). Comparison tooling is unchanged
and identical. [Baseline](baseline-correctness.json) and
[candidate](candidate-correctness.json) match CPython at all three keyword-call
sizes. [Cleanup/source proof](cleanup-source-proof.json) checks all seven workers
exited, exact producer differences, inactive services and machine-lease availability.

Ten [independent Python programs](cpython-goldens.json) include new mixed builtin/
compiled/non-callable targets at one site and a changing instance `__call__` slot.
Both public APIs require exact outputs and shared-budget cleanup. Existing
argument/result lifetime, fuel/span, failure, cancellation and generator controls
remain intact. Only assertions requiring the discarded cache metadata are removed;
call-site and member-resolution controls still run.

Focused Release groups pass 114 white-box / 70 public cases and 103 governed
CSV/text/JSON/file controls. Groups overlap; do not sum them. Each includes cleanup
and takes less than 30 seconds. [Validation](validation.json) preserves receipts
and TRX hashes; package/platform gates are recorded at delivery. No new full stress
qualification is claimed. Source checks find no remaining call-cache machinery
in production or tests; historical evidence is preserved.

This cache deletion is complete. Bound-values/frame-slot copying, direct methods,
receiver-independent builtin dispatch and the larger performance roadmap remain
open. Managed stack protection and cooperative ownership are unchanged.
