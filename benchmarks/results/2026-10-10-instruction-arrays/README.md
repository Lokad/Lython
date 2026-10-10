# Direct instruction-array dispatch

Dispatch now reads the compiler's instruction array directly, and synchronous
execution reads each instruction by readonly reference. Two short replicas show
**13.4–14.8% less integer-loop time**, **7.0–8.0% less positional-call time** and
**9.4–12.1% less keyword-call time**. The change is integrated. Empty and ASCII
pipeline controls have small mixed changes; full-sort observations remain too
noisy for a reliable gain claim.

Twelve prospectively declared micros take **6.44–7.19 seconds** each. Every owned
service completes and stops in **8.04–8.06 seconds**, under a **30-second external
process-group cap** including cleanup. No full benchmark lanes run. These are
short diagnostic comparisons, without milestone qualification or certified
Lython/CPython multipliers.

## Source and design

Frozen baseline `a5c4f2595f67d4b1bbf702393e3d8a8ee9a99963`, production tree
`ea89552dfd889d8cfea12314b4c0c35e5f21753e`, is identical in production, tests and
benchmark project to the delivered green head `c353f1b2`. Its prepared Release
worker is rehashed and reused. Candidate `4b9748b33f0504826753e3eab44ee0c7fb7a9f54`
has production tree `a3b7fa7d5c24f735ea5f101ae3e034d2d0298e4f`. Tests are unchanged
at `33fd54cf15c84698c040112a211b6bf6af6624dc`; the whole benchmark project remains
`c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

The compiler already normalizes each basic block into an instruction array.
Previously dispatch accessed it as `IReadOnlyList<ExecutableInstruction>`, checking
the interface count and obtaining a 48-byte instruction value each iteration.
The basic block now retains the explicit array type, with the existing readonly
list projection available to other consumers. Both dispatch loops use array
length/indexing; synchronous execution uses a readonly reference to the current
element. Asynchronous execution retains its instruction value across awaits.

Opcode handlers, order, source spans, observation, control-flow routing, generator
suspension/resumption, logical charges and every execution checkpoint are unchanged.
No host capabilities or runtime overrides are added. This tests the combined
instruction-read boundary; timings do not isolate the interface-dispatch and
copy/JIT contributions, and no guest-allocation reduction is claimed.

## Baseline CPU lead

After the [lazy namespace round](../2026-10-10-function-frame-namespaces/README.md),
two separate prospectively declared ten-second call captures use the frozen
baseline. Their whole owned groups take 14.07 / 16.08 seconds under 30-second caps.
They finish before this candidate's preparation/timing and make no source change.
All 833 protocol responses / 26,472 complete invocations are checked, including
pre/post golden output. The reader finds 1,096 / 1,136 CPU samples, zero reported
losses or missing stacks, and 6.39% / 7.75% unresolved leaves.

Synchronous `Execute` accounts for 15.97% / 14.96% exclusive samples and execution
checks for 5.29% / 5.63%. Complete guest-frame execution appears in 72.26% / 75.79%
inclusive stacks. Allocation ticks identify interpreter/context/frame/stack and
object-array storage as further leads. Inclusive stacks overlap, profiler/kernel
and background JIT costs are visible, and GC event pairs are incomplete. These
figures predict no saving, establish no post-change CPU distribution, and support
no exact per-type allocation, complete GC count or pause claim. The experiment
targets an instruction-read design cost observed within baseline dispatch.

## Ordinary invocation timings

Both Release producers use pinned SDK 10.0.401 / CLR 10.0.12 on the dedicated
quiet Ubuntu VM. Context uses isolated release CPython 3.13.16, ordinary GC and
GIL-enabled execution. Lython workers retain ordinary governor, instruction and
runtime defaults; only the separate controller disables tiering. No tracing,
builds, tests, downloads or other diagnostics run on the VM during normal timing.

Identical canonical sources construct fresh guest state and complete inputs per
invocation. The integer loop runs 16,384 iterations; call jobs execute 2,048 guest
calls plus a loop and checksum. They measure complete jobs rather than isolated
operation latency. Canonical sort stably orders all 2,048 tuples and prints the
complete 21,419-byte result. Full catalogs are byte-identical; all six jobs explicitly
verify against pinned CPython before timing.

Each engine gets one second of warmup, roughly 25 ms batch calibration and seven
rotating-order measured rounds. Cheap success and complete-output checks stay
inside each clock; compilation, transport and output hashing stay outside. Every
replica is retained, with replica 1 completing before replica 2. Cells show median
**microseconds/invocation**, followed by **IQR/median**.

| Case / replica | Before | After | CPython |
|---|---:|---:|---:|
| Empty / 1 | 25.165 (4.4%) | 25.136 (3.5%) | 1.567 (0.3%) |
| Integer loop / 1 | 1745.617 (0.4%) | 1487.055 (0.7%) | 689.120 (2.5%) |
| Positional calls / 1 | 815.341 (1.1%) | 749.755 (2.1%) | 127.636 (0.4%) |
| Keyword calls / 1 | 890.549 (0.5%) | 806.501 (2.4%) | 143.062 (0.2%) |
| Canonical full sort + output / 1 | 2944.272 (18.5%) | 2820.026 (2.0%) | 317.916 (0.6%) |
| ASCII pipeline / 1 | 147.749 (2.7%) | 145.975 (2.8%) | 43.557 (0.3%) |
| Empty / 2 | 24.954 (2.5%) | 25.190 (4.5%) | 1.568 (0.5%) |
| Integer loop / 2 | 1768.364 (1.3%) | 1531.566 (0.7%) | 632.943 (0.3%) |
| Positional calls / 2 | 802.771 (0.9%) | 746.513 (1.6%) | 128.845 (0.6%) |
| Keyword calls / 2 | 919.924 (0.9%) | 808.790 (0.8%) | 147.510 (0.3%) |
| Canonical full sort + output / 2 | 3660.867 (27.6%) | 2936.687 (4.9%) | 327.287 (1.2%) |
| ASCII pipeline / 2 | 146.814 (2.5%) | 147.847 (3.1%) | 44.201 (0.4%) |

Loop time falls **14.81% / 13.39%**, positional calls **8.04% / 7.01%**, and keyword
calls **9.44% / 12.08%**, larger than their measured spreads in both replicas.
Acceptance rests on those repeated target gains and correctness. Empty changes
by -0.12% / +0.95%; ASCII pipeline by -1.20% / +0.70%. Their small mixed changes
establish no material repeatable regression. Full sort takes 4.22% / 19.78% less
time, but baseline spreads are 18.55% / 27.64%; no reliable sort gain is established.
No result is recollected to chase a gain or qualification. The remaining CPython
gap and per-call frame costs stay open; this change does not resolve them all.

## Correctness, audit and cleanup

Matching Debug probe build and the full frozen Debug suite pass **9,161 checks**
(1,404 white / 7,757 public) **before normal timing is declared**. The candidate
passes **104 selected white Release / 241 selected public Release** checks on the
VM after its matching probe build. They cover instruction operands and lowering,
control flow, generator suspension/cleanup, execution parity, cancellation,
scope behavior, memory limits and streaming. Existing tests are unchanged; no
passed correctness gate is repeated.

Independent audit recalculates every median/IQR from seven rounds and checks all
**504 normal-micro responses**, exact request IDs/counts, full pre/post outputs,
source/fixture/catalog hashes, loaded source versions, SDKs and ordinary worker
settings. TRX finish timestamps and frozen binary hashes prove the full gate
passed before declaration. Both preparation and collection journals bind their
starts to prospective declarations and prove whole-service caps. Preparation
has separate 180-second bounds and is not a benchmark lane.

Final cleanup rehashes both producers, confirms both source trees clean,
**14 owned services terminal**, all **50 recorded preparation/controller/worker
PIDs absent**, and a freshly available shared lease. The earlier baseline CPU
captures have their separate audited cleanup proof; controller PIDs were not
recorded there, while both groups are verified inactive and four worker/tracer
PIDs are absent. No failed test or timing attempt is retried.

Raw declarations, receipts, TRX, journals and cleanup proof remain private under
`.git/agent-notes/instruction-array-read-20261010/`; baseline CPU evidence remains
under `.git/agent-notes/call-cost-refresh-20261010/`. Maintained
[evidence](evidence.json) retains every observation and receipt hash. Original
CSV/pipeline investigations, external Utf8Regex integration and consolidated
milestone/secondary-lane qualification remain separate pending work.
