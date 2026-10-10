# Lazy function namespaces

Function frames now create their namespace dictionary only when it is accessed.
The separate complete-invocation diagnostic measures about **11% fewer managed
bytes** on the existing positional and keyword call jobs. The change is integrated
as an allocation improvement. **No reliable speed improvement is established**:
ten short timing micros have small or mixed call changes and noisy sort results.

Each ordinary micro takes **6.38–7.29 seconds**; its owned service completes and
stops in **8.04–8.05 seconds**, under a **30-second external process-group cap**
including cleanup. Separate constructor groups take about two seconds and complete-
invocation allocation groups take about four seconds each. No
full benchmark lanes run; consolidated qualification waits for a declared milestone.

## Source and design

Baseline `93cdde5f6c206d3f86d97021b19e20c151d1f956` has production tree
`121846918199fcf4df8f237ec15bc7fa2894affe`. Candidate
`a5c4f2595f67d4b1bbf702393e3d8a8ee9a99963` has production tree
`ea89552dfd889d8cfea12314b4c0c35e5f21753e` and test tree
`33fd54cf15c84698c040112a211b6bf6af6624dc`. The whole benchmark-project tree is
unchanged at `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.

Executable function locals usually occupy compiled slots. Previously every child
context still allocated an empty dictionary, including simple calls that never
needed it. The frame now publishes an ordinal dictionary on first access using
`Interlocked.CompareExchange`, preserving dictionary identity and supplied aliases.
Class-cell lookup tests an existing namespace without creating one. Closures and
mirrored locals still obtain the same namespace when required. Host mediation,
governor limits, allocation checkpoints and invocation defaults are unchanged.

The [original namespace experiment](../2026-10-10-call-frames/README.md) remains
preserved at `8d20133d`, with its failed full Debug receipt and held timing.
The [streaming reservation correction](../2026-10-10-streaming-reservation-progress/README.md)
subsequently reproduces and fixes a controlled failure of that fixture on the
original source. This round uses a **new combined source**, with all current
corrections and its own successful full correctness gate before timing. The
original failed suite is not rerun or rewritten; its historical GC chronology
was not recorded.

## Complete-invocation allocation diagnostic

A separate helper calls the exact comparison worker's compile and invoke methods
on frozen Release binaries. Compilation stays outside the metric. Each case has
16 warm invocations and 100 measured invocations through the ordinary public
`Run` boundary, with full expected output checked after every invocation.
`GC.GetTotalAllocatedBytes(precise: true)` measures process-wide managed bytes,
including background/runtime threads. No forced GC. This is one fixed collection
per producer, without normal timing, native/resident/peak-memory measurements or
a CPython memory comparison. Small residual differences include runtime effects.

| Complete job | Before bytes/invocation | After bytes/invocation | Fewer bytes |
|---|---:|---:|---:|
| Empty | 29,440.00 | 29,440.00 | 0.00% |
| Integer loop | 1,079,513.68 | 1,079,513.68 | 0.00% |
| Positional calls | 1,490,501.12 | 1,326,653.68 | 10.99% |
| Keyword calls | 1,507,134.16 | 1,343,291.60 | 10.87% |
| Canonical full sort + output | 3,572,191.12 | 3,554,656.80 | 0.49% |

The call jobs execute 2,048 guest calls plus a loop and checksum per invocation;
they do not isolate one call's latency or allocation. Their reduction is about
163,844 managed bytes per invocation. Empty and loop controls are exactly unchanged.
The full-sort difference is only 0.49%; it supports no substantial sort-memory claim.

A separate retained-constructor check corroborates the mechanism: child context
plus frame storage with an unused namespace falls from 256.0152 to 176.0152
bytes/context (exactly 80 bytes saved), while explicitly observed namespaces stay
at 256 bytes on both producers. It keeps 1,024 warm and 10,000 measured contexts
alive per shape, uses a current-thread managed counter and forces no collections.
Its parent is a service/frame shell; it performs no guest execution. This helper
alone would not establish public invocation savings or decide acceptance.

## Ordinary invocation timings

Both producers use SDK 10.0.401 / CLR 10.0.12 on the dedicated quiet Ubuntu VM.
Context uses pinned isolated release CPython 3.13.16. Both frozen Release producers
are prepared and checked before timing; full catalogs are byte-identical and all
five jobs explicitly verify against CPython. Workers retain ordinary runtime,
governor, instruction and GC settings. Only the separate controller disables
tiering. No tracing, builds, tests, downloads or allocation instrumentation run
on the VM during these normal timings.

Each case has one second of warmup per engine, roughly 25 ms batch calibration
and seven rotating-order measured rounds. Sources, fixtures, complete golden
outputs and invocation boundaries are identical across engines. Cheap complete-
output and success checks remain inside each clock; compilation, transport and
output hashing stay outside. The integer loop has 16,384
iterations; call jobs use 2,048 calls; the canonical stable sort prints every tuple
in its 2,048-item result. These are complete jobs, without subtracting setup or
variants into synthetic costs.

Cells show median **microseconds/invocation** and **IQR/median**. Every replica is
retained; replica 1 finishes before replica 2.

| Case / replica | Before | After | CPython |
|---|---:|---:|---:|
| Empty / 1 | 25.180 (2.0%) | 24.988 (2.1%) | 1.546 (0.6%) |
| Integer loop / 1 | 1712.983 (1.3%) | 1757.234 (0.4%) | 693.215 (0.2%) |
| Positional calls / 1 | 807.033 (1.5%) | 804.830 (2.2%) | 128.689 (0.4%) |
| Keyword calls / 1 | 879.669 (1.4%) | 863.777 (1.1%) | 149.628 (0.3%) |
| Canonical full sort + output / 1 | 2705.917 (9.5%) | 2873.519 (5.2%) | 314.451 (0.7%) |
| Empty / 2 | 23.729 (3.3%) | 23.250 (3.6%) | 1.481 (0.2%) |
| Integer loop / 2 | 1647.543 (0.4%) | 1627.625 (1.1%) | 637.454 (0.2%) |
| Positional calls / 2 | 810.188 (1.2%) | 801.567 (1.3%) | 123.725 (2.7%) |
| Keyword calls / 2 | 840.696 (1.6%) | 843.015 (1.5%) | 144.632 (0.4%) |
| Canonical full sort + output / 2 | 2793.063 (11.8%) | 2722.422 (4.4%) | 308.315 (0.5%) |

Positional calls take 0.27% / 1.06% less time, below their spreads. Keyword calls
take 1.81% less / 0.28% more time; full sort takes 6.19% more / 2.53% less time.
Loop controls take 2.58% more / 1.21% less time. These results show no material
repeatable regression and **no reliable throughput gain**. Acceptance rests on
the demonstrated complete-call allocation reduction, the exact constructor
mechanism and correctness, with the timing outcome retained as a limitation.
No certified CPython ratio or milestone qualification is claimed. The remaining
call-performance gap needs current profiling and a separate design experiment.

## Correctness and audit

New checks cover concurrent first-access dictionary identity, independent ordinal
child namespaces, recursive mutable closures and nested class cells/local shadowing
with implicit `super`, in both execution modes. Local full frozen Debug passes
**9,161 checks** (1,404 white / 7,757 public) after the matching probe build, before
timing. Focused white/public Release pass **144 / 378**. VM baseline passes
**142 / 374** selected Release checks; candidate passes **144 / 378**, with matching
Release probes and explicit CPython output checks. No passing full gate is repeated.

Independent audit recalculates all ten medians/IQRs from seven rounds and checks
**423 normal-micro responses**, exact request IDs/counts, complete pre/post outputs,
source/fixture/catalog hashes, SDK/version provenance and unchanged worker defaults.
Both allocation helpers' source/binary manifests, boundaries, fixed counts and
producer hashes are checked. Service journals prove every bounded collection
started after its prospective declaration and stopped within its cap. Preparation
has separate 180-second bounds; it is not a benchmark lane.

Final cleanup rehashes both producers and both helpers, confirms clean source
trees, **16 owned services terminal**, all 36 recorded preparation/helper/micro
PIDs absent and a fresh available shared lease. No failed collection is retried
and no noisy result is recollected to chase improvement. Raw declarations, TRX,
receipts, helper sources, journals and cleanup proof remain private in
`.git/agent-notes/function-frame-current-20261010/`; maintained
[evidence](evidence.json) retains observations and hashes. Original CSV/pipeline
questions, external Utf8Regex integration and milestone/secondary-lane qualification
remain open.
