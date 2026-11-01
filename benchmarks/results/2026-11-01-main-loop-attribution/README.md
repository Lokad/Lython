# Current main-loop native-offset attribution (B43)

Source **5d16ead3** (runtime statements **9e99f640**), 2026-11-01, dedicated
Ubuntu 24.04.4 Azure F4as_v7, four EPYC 9V45 vCPUs / 16 GiB, .NET 11 RC3.
One fixed text-helper capture: **6.980 seconds sampled / 13.105 seconds collection**,
below the prospective 10 / 45-second bounds. Full stdout/file goldens, both
quiet-machine gates and all worker/profiler exits pass. Service inactive, VM lease
available. No runtime change, retiming, CPython ratio or speedup claim.

## Question and exact boundary

Earlier profiles attribute material self samples to ExecuteUntilCall, but the
method includes inlined handlers and checks. This assessment asks which native
instructions receive those samples, before selecting a coherent redesign.

The existing owned perf driver runs 1,024 complete warm synchronous public text
jobs at 199 Hz, with ordinary worker GC/tiering. Compilation/startup are excluded;
fixture reset, mediated I/O, full checks and declared cleanup are included. The
normal shared unbounded allowance exercises ownership paths; finite refusal is
qualified separately. The actual named guest thread selects guest samples.
All other threads and unknown/native origins remain visible. Tiered-JIT CPU is
zero at the recorded counter's resolution; this does not establish steady state.

Fresh nonincremental core/worker/test/probe copies agree. Supported Release
`DOTNET_JitDisasm`, `DOTNET_JitDisasmWithCodeBytes` and `DOTNET_JitStdOutFile`
emit only the selected method to a separate file, preserving protocol output.
The pinned runtime's local check confirms valid CLI JSON and code bytes. Debug-
only address/debug-info flags are unused. See the primary [.NET diagnostic
documentation](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/viewing-jit-dumps.md).
All diagnostic plumbing is external; the production library is unchanged.

## Mapping and observations

| Observation | Count |
| --- | ---: |
| Guest CPU / samples | 5.26 s / 1,043 |
| Main method self samples | 165 |
| Main samples mapped to instruction bytes | 165 |
| Unmapped main samples / block gaps | 0 / 0 |
| Instruction-fetch and guard block | 85 |
| Guard-related subrange within that block | 67 |
| SigmaPropertyContains self samples | 102 |
| Unknown guest leaves | 70 |

The unique sampled Tier1 perf-map range is **3,857 bytes**, exactly matching the
unique Tier1 disassembly. Two preceding Tier0 listings remain in the raw dump.
IP offsets map through emitted code bytes and group offsets, without requiring
an unsupported address flag or reading process memory. All instruction/block
counts, including zero-sample instructions, are preserved in
[main-loop-native-offsets.json.gz](main-loop-native-offsets.json.gz).

The hot check subrange correlates with the source path through context, services,
guards and state to the governor's first admission failure and cancellation.
Cold branch targets independently identify refusal and cancellation throwing.
This is **source/call-target correlation**, not instruction-time accounting:
sample skid, inlining and dependent-load effects prevent treating 67 samples as
a removable fraction or predicting a speedup. Opcode dispatch itself has other
samples; the whole main-method percentage cannot be called dispatcher overhead.

## Bounded source decision

Production creates one ExecutionServices per ExecutionState, with one
ExecutionGuards pointing back to that state. ExecutionLimits holds that same
execution's token and nesting fields. These are shared execution objects, distinct
from lexical frames/contexts whose lifetimes were assessed in B34.

Select B44: consolidate the actual service/guard/limit operations into the existing
shared state and delete the forwarding objects/back-references. Keep lexical
frames separate, managed stack checks, current tokens, terminal refusal order,
host mediation and explicit ownership. No new cache, guard framework, policy or
pool. Source/semantic acceptance and a fixed controlled comparison are recorded
in PLAN.md before code; timing must determine magnitude. Sigma-property work stays
visible, without reopening a closed casing comparison. Stop B43; no recapture.

[summary.json](summary.json) records exact boundaries, identities and all-thread
CPU. [Raw capture](raw-capture.tar.gz), SHA256
`8c6c75fae8788e914dd582e3d3eb67a704f595d45ae80b6f7c4f7c2f40801c1b`, preserves
perf data/maps, complete leaves/chains, disassembly, goldens, machine gates,
catalog, source identities and collector logs. Existing collector/analyzer
adaptations accompany it. This evidence qualifies attribution, not final-source
throughput, physical memory or embedding latency.
