# ASCII pipeline allocation diagnostics, 2026-10-09

The pipeline's mixed result after readonly instruction references remains
unresolved. Short, separately declared diagnostics found almost identical
managed allocation totals and fewer instruction-transfer copies in generated
code. They do not establish a cause for the slower timing repetition. Both
original pipeline receipts remain intact: 2.9% less time, then 7.6% more, in
the [instruction-reference experiment](../2026-10-09-instruction-references/README.md).
No normal timing collection was repeated and no production change accompanies
this investigation. No full comparison lane ran.

## Workload and producers

The unchanged `strings.pipeline-ascii.medium` catalog case embeds 1,024 copies
of `abZ!::tail/` as a literal, then executes:

```python
print('|'.join(TEXT.replace('::', '/').split('/')))
```

This produces 2,049 split items and 10,241 output bytes including the newline.
It executes few bytecode instructions while creating many strings. Both
producers use the same precompiled source and independent golden, fresh guest
state, ordinary public synchronous execution, containment and result projection.
Every invocation validates the complete public result.

Baseline is `6776baf2445735ca852d830f628e5cac9b1ca194`; candidate is
`db73573563c22e417f536fa7efb54cd664100996`, with only five readonly instruction
parameter changes. Both frozen clean Release producers are the ones used for
the preceding timing round. The dedicated four-core AMD EPYC 9V45 Ubuntu
24.04.4 machine uses SDK 10.0.401/runtime 10.0.12. No CPython memory or timing
comparison was collected in this investigation.

## Separate CPU/GC traces

The declared order was baseline then candidate. Each worker warmed for two
seconds before a ten-second `dotnet-trace collect-linux` capture using
`cpu-sampling,dotnet-common` and the default CLR keywords `0x14c14fccbd` at
verbose level 5. This enables allocation and JIT events and perturbs execution;
trace throughput is not a performance result. GC, tiering and PGO overrides
were unset in both worker identities.

| Producer | Whole supervised run, seconds | CPU samples | Unresolved leaves | Validated invocations |
| --- | ---: | ---: | ---: | ---: |
| Baseline | 15.16 | 2,237 | 1,338 (59.8%) | 32,260 |
| Candidate | 14.26 | 1,279 | 759 (59.3%) | 31,652 |

Both traces report zero lost events and no missing CPU stacks, but GC start/stop
pairs are incomplete: 35 unmatched starts/92 unmatched stops for the baseline,
44/170 for the candidate. Suspension event pairs also mismatch. **Do not use
these traces to compare GC pause costs or extrapolate allocation totals.** The
reader's zero lost-event count is insufficient evidence of complete GC events.

Observed allocation types include byte arrays, `PyString`, reclamation entries,
weak references and entry backing arrays. Inclusive stacks reach
`OwnSplitListResult` and `ChargeReclamationPool.TrackCore`; about half of all CPU
leaves are unresolved inside `libcoreclr`. Source inspection confirms that each
owned split string gets its own byte array, string object, reclamation entry and
weak reference. This is an allocation/registration lead for a further experiment,
without assigning unresolved native cost to a particular operation.

## Fixed-count allocation and generated code

A second declaration specified baseline then candidate, two-second warmups and
exactly **1,024 measured invocations** each. An identical private diagnostic
helper loads the frozen workers and calls their existing `Verify`/`Batch`
methods. Their public `Invoke` bodies and result checks remain unchanged.
`GC.GetTotalAllocatedBytes(precise: true)` brackets 32 batches of 32 invocations.
The delta includes response serialization, reflection and all runtime threads;
it is cumulative managed allocation, not retained or peak memory. GC is ordinary
workstation/Interactive and no collection is forced.

The only runtime instrumentation is `DOTNET_JitDisasm` for the dispatcher and
shared handlers, `DOTNET_JitDisasmAssemblies=Lokad.Lython`, and an owned
`DOTNET_JitStdOutFile`. These are separate diagnostics, not normal timing runs.

| Producer | Whole diagnostic, seconds | Managed allocated bytes | Collection count delta, generations 0 / 1 / 2 |
| --- | ---: | ---: | --- |
| Baseline | 2.60 | 659,039,256 | 39 / 0 / 0 |
| Candidate | 2.59 | 659,104,792 | 39 / 0 / 0 |

The difference is 65,536 bytes (0.01%); both runs allocate approximately 644 KB
per invocation, including helper overhead. This observation does not support a
large increase in managed allocation caused by the parameter change. It cannot
establish identical reference lifetimes, native allocation or GC cost.

The captured optimized Tier1 dispatcher shrinks from 2,005 to 1,814 code bytes.
Its baseline calls copy the 48-byte instruction using a reference move followed
by five `rep movsq` words; the candidate passes its existing address. The Tier1
value handler grows from 2,189 to 2,209 bytes, stack transfer shrinks 2,310 to
2,292 and control flow shrinks 2,232 to 2,228. This confirms eliminated transfer
copies for the exercised boundaries; generated-code size cannot explain a
timing difference by itself or assign a share of the earlier loop improvement.

## Review and next experiment

Keep the accepted loop improvement and the possible pipeline regression visible.
The next isolated round should investigate split-string construction or
reclamation registration while preserving aliases, retained-value charging,
denial cleanup and all checkpoints. Declare fresh short comparisons before
collection; retain every result. The intermittent CSV lifetime issue remains
independent, with no causal link established by these diagnostics.

All four diagnostic services used an owned 30-second process-group collection
and cleanup cap (`RuntimeMaxSec=30`, `TimeoutStopSec=0`, `KillMode=control-group`).
All succeeded and were stopped; owned workers/tracers exited, the input hashes
and source trees remained unchanged, and the shared VM lease was free.
Independent audits checked 2,003 profiling protocol responses / 63,912 invocations
and 344 allocation responses / 10,888 invocations, including warmups and
verification. Both measured allocation sections contain exactly 1,024
invocations. All six preceding micro receipts were rehashed unchanged.

Raw traces, generated code, declarations, bounded helpers and the independent
reader/audit remain local under `.git/agent-notes/pipeline-transfer-profile-20261009/`.
The trace reader uses Microsoft.Diagnostics.Tracing.TraceEvent 3.2.8.0.
Production sources and tests are unchanged; the existing full Debug and both-OS
Release/package checks remain relevant to that same production tree.

| Evidence | SHA-256 |
| --- | --- |
| Quick catalog | `e6cf197948d903c59d4db5e940d9ca50433cbb0c6399ab999817f48775fa4fe1` |
| Baseline library | `089a284eaae5a10e3af2b45afe9a251354d6f66f90cf6adb9148a9e4d30f2412` |
| Candidate library | `7e7d442c21a5401eb2a0f6428b37744206360e6f40d35990fdefa78e4cf21227` |
| Baseline trace | `b0c4b9497628dd92603a2a56880a2957483b523925ca122925263607e5f33209` |
| Candidate trace | `3bb42c6862da8c7b71eeef06b595d110aafb82109f0b3e6b9aee4becc17ece32` |
| Baseline allocation receipt | `21efe077a77e4e5a030ab13103286302df2e4a6245d161ca2ac593bd922763cd` |
| Candidate allocation receipt | `a10a86ba06d866fefadcfd4cb7b2b3840cfc2719f974b725e3ad6b7d35e29c96` |
| Shared allocation helper | `701caf702060ee1a9eeb4fa3aca694a85b8ffba7b8d517b46bf899bfd806c76e` |
