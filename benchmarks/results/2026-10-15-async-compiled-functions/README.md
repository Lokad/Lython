# Async compiled functions: one bounded delivery

Eligible ordinary functions now preserve executable code, bound argument slots
and captured cells under `RunAsync`. Shared definition construction awaits
defaults/decorators once; a genuine async body supports yielding protocols and
callbacks. Blanket async module mirroring is removed. Async imports update their
own bound slots without copying the namespace back over unrelated locals.

| Public API boundary | Earlier Lython | Candidate Lython | CPython 3.13.16 | Candidate IQR / median |
| --- | ---: | ---: | ---: | ---: |
| Warm `Run` | 3.262 ms | 3.289 ms | 0.965 ms | 2.6% |
| Warm `RunAsync`, completed host | 11.091 ms | 6.618 ms | 0.989 ms | 1.4% |

The short async diagnostic observes a **40.3% lower Lython median**. The sync
difference is within sample spread; no sync improvement is claimed. Baseline
IQR/median is 3.2% sync and 1.1% async; CPython spread is 0.6% and 0.3%.
These are one-collection diagnostics, not qualified multipliers, confidence
intervals or overall application speedups. The combined change is measured;
individual contributions are not isolated. It does not explain earlier
synchronous Lython/CPython gaps.

The workload performs 16,384 positional calls to a small integer helper and
prints the aggregate checksum. [Sync](sync-warm.json) and [async](async-warm.json)
receipts preserve all seven rotating rounds, calibration, warmup and loaded
identities. Complete owned runs take 7.758 and 7.848 seconds under separate
30-second hard deadlines. No measured samples were discarded or replaced.

Both timers include fresh invocation state, execution, capture/projection,
golden checks and prior-result cleanup. Compilation, process startup, transport
and full response hashing are excluded. `RunAsync` includes its per-invocation
dedicated startup-thread creation; the host operations complete without injected
delay. CPython runs the same ordinary warm Python code in both boundaries.
This is neither guest async syntax nor an I/O-throughput comparison.

Measured 2026-10-15 on the dedicated Azure Standard_F4as_v7: Ubuntu 24.04.4 LTS,
four AMD EPYC 9V45 vCPUs, 16 GiB RAM, SDK 10.0.401/runtime 10.0.12, and
CPython 3.13.16 with ordinary GIL/GC and `-I -S -X utf8`. Lython public policies
and a shared unbounded allowance remain enabled; GC/tiering are unchanged.
CPython has no artificial equivalent containment limit.

## Provenance, qualification and limits

Earlier Lython is 015184e60b95826a5b890f7d28ea63aaf4335348. The candidate was
built before commit on that revision plus six production files. Informational
versions name the parent; frozen [sync](sync-comparison-declaration.json) and
[async](async-comparison-declaration.json) source hashes identify the producers.
The production tree is identical across boundaries. Each boundary uses identical
comparison sources for its baseline/candidate pair.

The initial [async setup failure](async-setup-failed.json) was a client lane
validator rejecting `warm-async` after verification and before timing. It has no
timed samples. A distinct paired harness corrected only that validator; the
runtime producer did not change. The valid sync collection is not repeated.
Do not turn these separate collections into a qualified cross-API ratio.
An earlier full-test failure exposed the namespace refresh bug; it was corrected
before either timing collection. All failed evidence remains visible.

Nine [independent CPython programs](cpython-goldens.json) validate exact stdout
and event-file bytes for defaults/decorators, escaped cells, yielding properties
and operators, annotations, generators, failed publication and imports. Tests
also require compiled callable identity, real suspension with bound slots,
cancellation before definition publication and shared allowance cleanup.
Two tests that demanded async-only whole-scope rejection now require parity at
the same allowances. Paired captured-data rejection remains unchanged.

The full Release gate passes 10,916 ordinary and 377 original stress cases.
The final client/file/protocol checks pass 64/64; [validation](validation.json)
records counts, elapsed bounds and TRX hashes. [Cleanup/source proof](cleanup-source-proof.json)
confirms all nine workers exited, staging is absent, services are inactive and
the VM lease is free.

Annotations/type parameters, class-suite factories and unsupported executable
bodies retain existing lowered routes. Async opcode helpers/member dispatch and
startup-worker reuse remain separate work. Live `__defaults__` mutation is a
[pre-existing discrepancy](preexisting-default-mutation.json): the pre-change
probe returns 5 where CPython returns 7; argument binding ignores the dynamic
attribute. This change does not claim to implement mutable function metadata.
The supported-default witness checks fixed defaults and escaping cells instead.

Stopping criterion reached for compiled-function construction/body parity.
Keep the demonstrated improvement and correctness fixes; do not broaden this
delivery into all async dispatch, new guest syntax or a timing campaign.
