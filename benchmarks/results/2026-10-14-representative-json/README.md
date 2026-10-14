# Complete JSON-lines summary: decoder simplification

The candidate binds builtin load options directly into the existing parser,
avoiding an unobservable temporary decoder, seven public attributes, a default
constant callable and a bound decode method/argument-binding route. Explicit
decoders and guest subclasses keep construction, captured options and overrides.
The allocation evidence supports retaining this simplification. Timing is
inconclusive and establishes no speedup, CPython multiplier or host-latency claim.

The complete job processes 4,096 JSON records and blank lines from 110,592 UTF-8
input bytes, filters/aggregates them, prints `3072` and writes the exact sorted
report. It uses producer f95fb734 as baseline; the uncommitted candidate on
64ec1fd4 uses the identical worker sources and recorded changed runtime files.
[Source hashes](candidate-sources.json), [all timing samples/loaded identities](micro.json),
[CPU attribution](profile.json) and [allocation proof](allocation.json) identify
those measured states. Do not treat this snapshot as a later HEAD measurement.

| Engine | Median warm complete-job time | IQR / median | Status |
| --- | ---: | ---: | --- |
| Earlier Lython | 11.979 ms | 4.2% | Short diagnostic |
| Direct builtin decode candidate | 11.537 ms | 11.8% | Nominal 3.7% lower; timing inconclusive |
| CPython 3.13.16 | 4.225 ms | 0.6% | External baseline for this diagnostic |

The single comparison took 9.911 seconds including owned cleanup. It ran on the
four-vCPU AMD EPYC 9V45 benchmark VM, Ubuntu 24.04.4 LTS, .NET SDK 10.0.401/runtime
10.0.12 and isolated `-I -S -X utf8` CPython 3.13.16 with ordinary GIL/GC. Both
engines reuse code with fresh guest state, matched file staging/reset and exact
stdout/output-file validation. Compilation, startup, transport and response hashes
are outside the warm timer. Lython's shared allowance is unbounded; ordinary
per-execution policies apply. No artificial Python resource limits were imposed.

One separate allocation diagnostic reused the existing compile/invoke helper:
16 warmups then 100 complete jobs per isolated engine process, every output
checked, no forced collections, result/compiled owners disposed and shared
accounting returned to zero. Process-wide precise managed allocation totals show:

| Engine | Managed bytes allocated per complete job |
| --- | ---: |
| Earlier Lython | 13,789,675.60 |
| Direct builtin decode candidate | 9,922,945.52 |
| Reduction | 3,866,730.08 (28.0%) |

Each process took under 3.4 seconds including cleanup. These cumulative allocation
counters measure an optimization; they are not retained storage, process RSS,
per-type sizes, a latency distribution or Lython's ownership accounting policy.
No timing samples were repeated or replaced by the allocation run.

One ten-second kernel CPU capture completed in 14.971 seconds including cleanup:
6,400 samples, no lost events/missing stacks, 310 unresolved leaves (4.8%), 422
checked responses and 842 complete invocations. JSON loading has 34.0% inclusive
CPU; decoder constructor/option binding about 5% each. These shares overlap;
async parsing appears in many descendant stacks and is not isolated removable
cost. All owned worker/tracer/measurement services are inactive, their users
exited, temporary file staging was removed and the VM lease is free.

Focused validation passes 238 public JSON and two white JSON controls, covering
custom classes, hooks, actual async callbacks, strictness/error positions, retained
storage and refusal. The coherent local gate passes 10,816 ordinary Release checks
plus all four representative streaming stress cases. Explicit Release package and
cross-platform CI are recorded at delivery. Constructor-related internal
allocation/fuel checkpoints can change as absent work disappears; Python results,
input/error order, preflights, cancellation, managed stack/cycle protection and
retained-alias funding remain required. No second parser or guest-state cache is
introduced. There is no ten-minute lane or additional capture campaign.
