# Current runtime: short method CPU profile

The current method workload spends visible CPU time in instance attribute access,
function invocation and lowered assignments. Retained caller stacks separate the
attribute reads in `self.value += value + 3` from the outer `item.add(i)` lookup.
This identifies concrete design leads without establishing a speedup or a Python
timing ratio. Production remains unchanged at `87eaf498`.

One declared ten-second capture completed in **14.07 seconds including startup
and cleanup**, within the 30-second owned-process-group limit. It contains
**1,184 CPU samples**, no reported lost events or missing stacks, and **6.25%
unresolved leaves**. All 268 protocol responses and 8,484 complete invocations
verify the expected output. No normal timing, allocation/native collection or
full comparison lane ran.

## Observed leaf samples

| Resolved path | Samples | Share of all samples |
|---|---:|---:|
| Instance attribute read | 115 | 9.71% |
| Main interpreter loop | 92 | 7.77% |
| Function invocation | 77 | 6.50% |
| Instance attribute assignment | 70 | 5.91% |
| Lowered expression evaluation | 36 | 3.04% |
| Executable call routing | 32 | 2.70% |
| Argument binding into slots | 22 | 1.86% |

The read and write symbols include inlined helpers. An offline classifier of the
same retained stacks partitions their leaves as follows; it runs after capture
and causes no new VM collection.

| Resolved caller route | Samples | Share of all samples |
|---|---:|---:|
| Attribute read inside a function and lowered assignment | 62 | 5.24% |
| Attribute read outside a function | 53 | 4.48% |
| Attribute write inside a function and lowered assignment | 67 | 5.66% |
| Attribute write inside a function through another statement route | 3 | 0.25% |

The 62 read stacks contain `ResolveLoweredAugmentedAssignmentTarget`; the 67
write stacks contain `ExecuteLoweredAssignment`. The 53 outer read stacks contain
`ExecuteStackTransfer`. Given the canonical source, these correspond to the
body's augmented member assignment and the outer method lookup. The other three
write samples are consistent with initialization, but the resolved stack alone
does not pin an exact source line. Inlining can omit ancestor frames.

Inclusive function-invocation stacks contain 48.90% of samples; lowered assignment
contains 28.38%. These overlap and cannot be summed or treated as removable
costs. EventPipe writing is visible in 11.91% of inclusive stacks, alongside
kernel/profiler and background work. Instrumented sample shares do not establish
ordinary uninstrumented CPU shares or predicted savings.

The original parser's highest 50 leaf rows contain 934 samples, leaving 250
outside that table. The new offline classifier retains **all leaf rows** and
complete selected caller partitions in [profile.json](profile.json), together
with the original top 50 inclusive rows and hashes of the raw trace and tools.
Every percentage uses the complete 1,184-sample denominator.

## Next source assessment

Instance reads first resolve the live `__getattribute__` slot, then perform data
descriptor, own-attribute and ordinary class-member resolution. Writes resolve
the live `__setattr__` slot and preserve descriptor assignment and actual funded
instance storage. The existing sealed default-object slot shortcut is already
integrated. Neither lookup order nor these mutable guest values may be skipped.

One narrower source lead is the second MRO lookup for ordinary class members in
`TryResolveInstanceMemberWithoutGetAttrFallback`: the first lookup already
obtains the candidate, then the side-effect-free own-attribute lookup can fall
through to another class lookup. Assess whether an operation-local result can
be reused safely in synchronous and awaited paths, with independent descriptor,
shadowing, mutation, failure and reentry checks. This would target the outer
lookup; it would not remove the body's own-attribute access or its assignment
cost. It introduces no persistent guest-value cache and predicts no gain.

The larger call/body execution design remains pending, including real frame and
namespace costs. Retain actual contexts/services, receivers/cells, binding
admission and rollback, all fees/checkpoints, argument leases, actual async
effects and generator lifetime. Any prototype needs its own short paired target
and complete control timings, ordinary allocation evidence and all native tiers.
Earlier rejected compiler and integer-routing experiments remain rejected.

## Validation and capture conditions

The unchanged qualified producer `35aa09eb` is reused after source, binary,
tool and catalog identity checks. It has the same production/test/harness trees
as delivered head `18ff1d39`: `87eaf498` / `6ea4d482` / `c194ce88`. Its previously
passing **9,818 Debug and 9,818 VM Release checks** (1,844 white-box / 7,974 public)
and matching Release probe are audited from retained receipts, without rebuilding
or rerunning unchanged suites. Seven complete canonical cases match CPython
3.13.16 once before capture, including empty, integer loop, positional/keyword/
method calls, complete stable-sort output and ASCII pipeline output.

The worker uses .NET SDK 10.0.401 / runtime 10.0.12 with ordinary tiering/PGO/GC,
workstation GC and Interactive latency. BenchmarkDotNet is not loaded. The source
performs 2,048 method calls and prints `2102272`; its full source, fixture and
expected output are retained in the metadata. Each invocation creates normal
mutable state and preserves the embedding and host boundaries.

The preparation helper initially reads the wrong identity field after successful
correctness verification. Its completed outputs are audited offline, with no
repeated correctness run or profile attempt. A follow-up check also corrects its
assumption that a stopped failed systemd unit becomes inactive: that preparation
unit retains its failed marker, while its MainPID is zero and cgroup is empty.
The successful profile unit is inactive. All six **recorded** preparation/capture
PIDs are absent; the initial verification controller PID was not recorded, so
cleanup relies additionally on the preparation group's verified emptiness.
Inputs rehash identically, the checkout is clean and the VM lease is free.

Raw traces, original failure records, protocol outputs and journals remain
retained privately. Incomplete GC pairs and sampled allocation ticks cannot
establish complete GC/pause statistics or exact per-type allocations. The
original hashed parser is reused; the additional caller classifier is built
offline after capture, with its source/binary/package hashes recorded.
