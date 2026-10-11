# Current runtime: short basic-loop CPU profile

This capture supplies current CPU leads for the next performance experiment.
It profiles producer `ed512fff` / production `87eaf498`, after the accepted
dispatch/call changes and independently delivered compatibility corrections.
It makes no before/after CPU, Python timing-ratio or speedup claim.

One declared ten-second capture completed in **14.07 seconds including startup
and cleanup**, under the 30-second owned-process-group limit. It contains **1,394
CPU samples**, no reported lost events or missing stacks, and **4.66% unresolved
leaves**. All 304 protocol responses are checked; all 9,636 complete invocations
produce the expected output. No full comparison lane or normal timing collection ran.

## Observed leaf samples

| Resolved path | Samples | Share of all samples |
|---|---:|---:|
| Main interpreter loop | 603 | 43.26% |
| Value-operation routing | 86 | 6.17% |
| Fresh-integer ownership | 72 | 5.16% |
| CLR reference-array store | 58 | 4.16% |
| Control-flow routing | 44 | 3.16% |
| Checked range iteration | 41 | 2.94% |
| Augmented-assignment evaluation | 35 | 2.51% |
| Memory reservation checks | 29 | 2.08% |
| Exact-integer addition | 29 | 2.08% |
| Augmented-operator mapping | 16 | 1.15% |

The main loop's leaf attribution includes inlined code; it does not isolate
switch overhead, guards or individual operand transfers. Inclusive stacks
overlap: the main interpreter contains 79.70% of samples, value routing 18.79%,
and augmented-assignment evaluation 11.26%. Those percentages cannot be added
or interpreted as directly removable costs. The parser's highest 50 leaf and
inclusive rows remain in [profile.json](profile.json), including kernel, profiler
and background work. Those leaf rows contain 1,267 samples; the other 127 remain
in the retained raw trace. All percentages use the complete 1,394-sample denominator.

## Next design experiment

The source routes bytecode augmentation through `EvaluateExecutableAugmented`,
an executable-to-syntax operator conversion, and `EvaluateAugmentedAssignment`
before reaching its existing exact-integer addition branch. Test this operation
routing boundary independently, retaining the actual integer helper, context,
operand order, instruction checkpoints, ownership charges and rich/async fallback.
Keep it separate from representation or iterator changes and further main-loop
switch growth. Inspect all native tiers and complete-job allocations alongside
repeatable short micros before deciding whether to integrate it.

Reference-array stores are another visible lead, but this trace does not isolate
which array or inlined call site owns their cost. The broader function-body
compiler remains open; earlier rejected candidates and their evidence remain
unchanged. This profile does not qualify those approaches.

## Validation and capture conditions

The exact producer passes **9,802 local Debug and 9,802 VM Release checks**
(1,832 white-box / 7,970 public), with a matching Release probe. Seven complete
canonical cases match CPython 3.13.16 before capture, including empty, integer
loop, positional/keyword/method calls, complete stable-sort output and ASCII
pipeline output. The unchanged harness tree is `c194ce88`. The producer's
[first-attempt Windows/Ubuntu CI](https://github.com/Lokad/Lython/actions/runs/38125158662)
also passes matching probes, full Release tests and package consumers.

The worker uses .NET SDK 10.0.401 / runtime 10.0.12 with ordinary tiering/PGO/GC,
workstation GC and Interactive latency. BenchmarkDotNet is not loaded. The
canonical module performs 16,384 integer-loop iterations and prints `134209536`.
Each invocation creates its normal mutable state; the profiler does not replace
the embedding context, namespace, fees or host boundary.

All controller/worker/tracer PIDs exited, the owned service is inactive, inputs
rehash identically, the producer checkout is clean and the VM lease is free.
The private raw trace, protocol records, journals and original preparation outputs
remain retained; public metadata includes their hashes. Incomplete GC event pairs
and sampled allocation ticks cannot establish complete GC/pause statistics or
exact per-type allocations. The existing hashed offline parser was reused.
