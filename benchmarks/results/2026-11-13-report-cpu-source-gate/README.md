# Whole-report CPU source gate — R294 / B649–B650

2026-11-13, committed917e01d8, measured core9e53b9fa; fixed fec79 worker,
optional adapter0.4, actual.NET11RC3 on quiet F4as_v7 /Ubuntu24.04.4. Same complete
JSON-to-Markdown report as the maintained comparisons. Normal GC/tiering, PerfMap
diagnostic override only. Twelve-second warmup and an eight-second CPU-clock/fp
capture at199Hz;7.43 selected guest CPU seconds /7.766851s execution clocks.

1,477 selected samples;150 unknown leaves(10.16%),0 ambiguous leaves and0 malformed
CLR map lines. Complete all-thread CPU, IP intervals and original native stacks
survive. Saved maps/stacks are losslessly compressed only after their users finish.
This is CPU attribution, not wall-time or a source-pair speed comparison. Nearest
frames include inlined, runtime and useful work and are not removable shares.

| Selected leaf | Samples /share |
| --- | --- |
| ExecuteInstructionPrefix | 148 /10.02% |
| Unmapped CLR leaf | 80 /5.42% |
| ReadJsonString | 73 /4.94% |
| Unmapped kernel leaf | 60 /4.06% |
| Enter | 56 /3.79% |
| AppendInterpolatedStringValue | 55 /3.72% |
| TryExecuteFormattedInstruction | 54 /3.66% |
| Exit | 52 /3.52% |
| ContinueJsonConstruction | 51 /3.45% |
| ExecuteInputInstruction | 39 /2.64% |
| Reserve | 35 /2.37% |
| CompleteLocalResult | 33 /2.23% |
| FillPadding | 30 /2.03% |
| WriteAsync | 28 /1.90% |
| BindPositionalFunctionArguments | 27 /1.83% |
| Unmapped CLR leaf | 25 /1.69% |

Nearest-frame attribution includes MemoryGovernor.Reserve9.75%, formatted-opcode
execution4.67%, JSON string reading5.69%, JSON construction4.27%, integer formatting
3.86% and ordinary call dispatch2.57%. Do not add these to overlapping leaf shares.

## Source decision

Ordinary native fields already append into the final UTF-8 builder. Integer output
uses managed UTF-8 formatting and final windows; JSON tokens already borrow
immutable input windows and reuse record keys. These observations do not support
another per-character CLR-string conversion project or prove boxed values/call
arrays dominate. Static format descriptors are prepared once, while field/value
evaluation order and genuinely suspending hooks remain in the executable engine.

Inspect complete formatting operand/instruction construction as one potential
follow-on. A useful redesign must eliminate an actual intermediate stage without
reintroducing a second expression graph/controller. Preserve conversion-before-spec
evaluation, lazy/dynamic fields, original failures, hook I/O, exact PC/live values,
cancellation and funded buffers. Source/semantic gates precede a bounded complete
report/compile-to-result comparison with CSV/text controls. No prototype or CPU
speedup follows from these sample shares alone.

This source review also identifies a separate **ownership hypothesis**: shared
byte-builder growth/release publishes storage fields after an external refund.
First prove whether refusal, throwing or reentrant refund can over-release another
retained input's charge, then consolidate the existing publish/commit/refund
transaction. This is a focused correctness gate, not a claim that removing host
budget locks is safe or that9.75% of CPU can be eliminated. No new budget policy,
pool, tracing, global cache or broad ownership audit is proposed.

## Collector correction and limits

B649 captured samples and per-batch goldens but decoded a caller-owned perf file
as root. perf refused that ownership, and the old collector failed before asking
the worker to quit. Its forced exit−9, missing final verification/normal close and
end quiet gate make that capture **partial**. It is preserved, with the original
journal, perf data/maps and recovery metadata. A fresh idle lease joined all users,
preserved the dead worker's maps and removed only its identified orphan runtime
IPC and empty RAM mount. No runtime defect is inferred from this collector failure.

B650 changes offline decoding order/identity only: normal verification, quit,
exit0, map retirement and fixture cleanup happen first; perf decoding runs as the
file owner. Its outputs/allowance balances, Closed/exit0/join, before/after quiet,
RAM unmount and independently verified persistent/local archive all pass. Same
source/workload; one corrected bounded capture, no repeated timing comparisons.
Owner120s/stop30s/margin30s. Last owned VMfree265154560B, no workers/mounts remain.
OS240MiB/evidence12MiB floors remain; exact archived source duplicates were retired
under fresh path/UID/inode/link/SHA proofs. No RSS or embedding-tail claim.

[Valid raw capture](observations.tgz), [attribution](attribution.json),
[receipt](archive-receipt.json), [terminal](terminal-status.json),
[original partial capture](failure-observations.tgz), [recovery](failure-recovery.json),
[collector/analysis sources and failures](collection-sources.tar.xz).


## Follow-on source gate

The actual report's render(record) returns three formatted dictionary fields.
ExecutableCodeObject.IsInlineInstruction excludes StartFormattedString and its
other format opcodes, so CanInlineInstructions rejects that entire helper and
each row constructs an ordinary invocation context/workspace. Native dictionary
lookup and definition-owned caches already exist. This is a concrete call/frame
construction opportunity, unlike bare-local field fusion (the report fields are
subscripts). Assess shared native formatting operations in the existing final-slot
prefix with one exact-PC transfer for dynamic protocols. Do not add an expression
graph, formatter backend or call controller. The builder transaction correction
has priority; its [demonstrated failures and fix](../2026-11-13-builder-refund-ownership/README.md)
are independent of any performance claim. Full report/compile/warm/fresh controls
must precede promotion of a new formatting execution path.
