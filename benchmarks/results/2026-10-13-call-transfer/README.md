# Ordinary-call operand transfers

Ordinary executable calls now preserve their inputs on the existing operand
stack through invocation and asynchronous suspension, then replace the consumed
tail with the result before its admission check. This is a bounded ownership
prerequisite. **Forced-GC independence remains open**: native evolving state,
lowered operations, dynamic callers, child/resource traversal and funded
workspace still need a complete representation. No logical refunds are enabled.

The change removes separate early-pop sequences and shares argument preparation
between sync and async handlers. The synchronous helper completes the transfer
instead of returning a temporary to its caller. Failure clears that call's tail;
surrounding operands remain for ordinary exception/cleanup routing. It adds no
retained watermark, extra stack slot, per-instruction completion hook or root
registry. The earlier [universal-retention candidate](../2026-10-13-frame-transfer-rejected/README.md)
remains rejected in its entirety.

All nine new white-box checks fail on the previous runtime and pass on this
change. They inspect actual operands from a callback, through a real pending
asynchronous operation, and during result observation; they also cover callback
failure, cancelled suspension, denied admission and clearing physical references.
124 focused call/operand/control-flow checks pass on the first candidate. Two
additional probes then exposed its argument buffers retaining dead inputs during
result admission. Correction `02d3a0d0221fd7496a075e68b6c5271fd24de3c6`
(production `c222a677`) restores scrubbing immediately after invocation, before
result admission. Both new failures become passes; all eleven transfer checks
pass within 39 focused controls and 64 public root/native-streaming controls.
The first candidate's complete 10,896-case Release gate is historical evidence;
the corrected runtime's normal checks are recorded separately. The broad public diagnostic
hit its 28-second cutoff and remains **incomplete**; the normal full-suite gate
is recorded separately in [validation](validation.json). The exact Release
package consumer passes. Original failures and receipts remain preserved in
`.git/agent-notes/call-transfer-20261013/`.

| Warm workload | Input | Earlier Lython | Intermediate candidate | CPython | Status |
| --- | ---: | ---: | ---: | ---: | --- |
| Keyword-call accumulation | 16,384 calls | 3.897190 ms | 4.035605 ms | 1.136683 ms | Historical short diagnostic; direction and external multiplier unqualified |

Measured on 2026-10-13, dedicated 4-vCPU x64 Ubuntu 24.04.4 VM, .NET 10.0.12
and CPython 3.13.16. Earlier runtime: `9dcbf6fc`, production `325120a6`, identical
production to the starting HEAD `6bed141e`. Measured intermediate candidate:
`09aeae83474eea40ed5950ee248c1861c83b9a70`, production `d3645bdc`.
Portable Release builds use the pinned Windows SDK
10.0.300-preview.0.26177.108; the VM SDK directory is 10.0.401. The reused
baseline observer carries revision `8366e832`; its source is unchanged and its
loaded runtime is the accepted `9dcbf6fc` DLL, not the rejected library.
Each observer and loaded library identity is frozen in the raw receipt.

The existing micro tool compiles once, warms persistent workers, and measures
fresh-state synchronous invocations including ordinary projection and captured
output. Compilation and startup are outside the timed region. All engines
match independent exact output. Ordinary settings remain unchanged; seven
rotated rounds retain all original samples. The owned group takes 8.491 seconds,
with no paging, major faults, steal or runtime overrides; its worker and service
finish, the cgroup is empty, and the lease is free. An initial service attempt
failed before extraction because the uploaded archive name differed from the
supervisor's expected name. That setup failure is preserved; it produced no
measurement. There is one measured group and no ten-minute lane or recollection.

The median moves +3.55%, while candidate IQR/median is 5.53% (earlier 1.55%;
CPython 0.41%). This small diagnostic establishes neither improvement nor a
reliable regression and supplies no confidence interval. It is not a general
embedding-latency, async, full-job or current-HEAD performance claim. The adoption
is for the directly validated transfer invariant; recurring cost remains
uncertain. The argument-buffer correction was not remeasured; these timings do
not describe the final corrected runtime. Do not generalize this representation
into an incomplete collector.

Raw evidence: [micro](micro.json), [process](process.receipt.json),
[manifest](manifest.json), [catalog](catalog.json), [supervisor](run-micro.py),
[stdout](stdout.txt), [validation](validation.json).
The immutable binary bundles/package remain private with recorded hashes.
See [BENCHMARKS.md](../../../BENCHMARKS.md) for the authoritative performance
account and the [complete ownership requirement](../2026-10-12-memory-roots/OWNERSHIP-RULE-2026-10-13.md).
