# Synchronous execution thread reuse, 2026-10-09

The synchronous thread reuse candidate is accepted on the declared short
comparisons and correctness checks. Empty invocation medians fell 71–72%,
integer-loop medians fell 3–4%, and ASCII-pipeline medians fell 27–29% in both
repetitions. The pipeline samples have visible variation. These are diagnostic
observations, without milestone qualification or a claimed confidence interval.

Each collection took **6.44–6.94 seconds**, including startup, warmup,
verification and shutdown. Each owned process group had a **30-second hard
collection and cleanup cap**. No full comparison lane ran in this round.

## Change and measured boundary

Baseline `e3e483a382783143aa3291ff902f1ced49ac6f47` creates and joins a new
16 MiB execution thread for every synchronous invocation. Candidate
`63116eb6ea17ddfa35d3b4b783dabaa762e98713` reuses idle execution threads with
the same stack reserve. The idle count is limited to the processor count,
capped at eight, and idle workers expire after 30 seconds. Active calls are
not queued behind busy workers; concurrent and nested host calls can start
additional workers.

Each call captures its caller's logical execution context, enters that context
on the worker and restores the clean worker context afterward. Suppressed flow
uses the clean context. Finished requests clear their runner, captured context,
result and exception references. Exception dispatch preserves the original
failure and stack. Host callbacks may run on reused threads; the host interface
documents that implementations cannot rely on thread identity or thread-local
state surviving between executions. Asynchronous entry is unchanged.

Before changing production code, a separate trusted diagnostic compared the
empty public API with the existing private dedicated-thread helper returning a
prebuilt result. Its seven measured batches gave medians **85.57 µs** and
**71.08 µs**, respectively, in a **4.82-second** collection. This controlled
decomposition identified thread setup as a useful target. The private helper
shape is not a Python job, and its time is not subtracted from public timings.

The unchanged quick catalog supplies `control.empty.control`,
`loops.integer.large` and `strings.pipeline-ascii.medium`. Both engines
precompile the same ordinary Python source, execute complete jobs with fresh
guest state and validate complete output on every invocation. Lython uses its
normal synchronous public API, including containment, memory limits and result
projection. CPython uses ordinary isolated execution and capture, without an
equivalent in-process governor. Entry cost is included in every job.

The declared order was empty, loop, pipeline, repeated once. There are seven
measurement batches and two verifications per engine per collection. Worker
GC, tiering and PGO settings remain normal. Preparation, builds and correctness
tests occur outside collection. First-call startup and async invocation costs
are outside these warmed measurements.

| Job / repetition | Baseline µs | Candidate µs | CPython µs | Baseline / candidate IQR fraction |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 81.23 | 23.05 | 1.46 | 0.019 / 0.036 |
| Integer loop / 1 | 2,147.23 | 2,082.33 | 576.48 | 0.006 / 0.003 |
| ASCII pipeline / 1 | 439.48 | 311.28 | 41.58 | 0.116 / 0.119 |
| Empty / 2 | 81.12 | 23.41 | 1.51 | 0.029 / 0.024 |
| Integer loop / 2 | 2,181.02 | 2,091.40 | 600.51 | 0.004 / 0.009 |
| ASCII pipeline / 2 | 468.49 | 342.97 | 44.83 | 0.075 / 0.048 |

Thread reuse reduces invocation overhead, but these samples still show
substantial loop and string-processing costs relative to CPython. They do not
attribute the pipeline improvement to a particular allocator or JIT effect.
Further runtime rounds remain necessary before a separately declared milestone.

## Correctness

Six added public checks pass before and after the change. They cover caller
context/culture/principal flow, suppressed flow, consecutive calls after host
mutations, concurrent contexts, nested synchronous calls and release of finished
host/script/result/context references. Six white-box checks cover actual thread
reuse, idle expiry, bounded idle retention without queuing active jobs,
disposal during execution, original exception delivery and failure-payload
release; these pass in Debug and Release.

The pinned Linux Release candidate passed **533 focused public checks** and
all six white-box checks before timing. Existing stack guards, recursion limits,
cancellation, delayed host composition, comparison-supervisor behavior and CSV
retained-row budgets are included in those checks. Guest limits and host-mediated
I/O remain unchanged. Passing CSV checks do not establish the cause of the
separate intermittent retained-row lifetime issue.

The original candidate passed all **9,067 local Debug checks**. The first
delivery's Ubuntu Release CI passed; Windows passed all 7,748 public checks but
failed an existing ZIP cancellation test that started an async job and then
cancelled without ensuring the job was still pending. That test now waits for
the existing host read barrier before cancelling, with unchanged failure
assertions and bounded watchdogs. This is a test-only correction; the measured
production tree is unchanged and no extra timing collection is required.

## Reproduction and evidence

Run `--compare list --profile quick --out catalog.json` from the Release
comparison executable. Run its `--compare micro` command with that catalog,
pinned executables, a baseline worker and fresh receipt filenames, selecting
the three IDs above in the declared order. Hold the shared VM lease and wrap
each collection in an owned service with `RuntimeMaxSec=30`,
`TimeoutStopSec=0` and `KillMode=control-group`. Never overwrite receipts.

The dedicated machine and toolchains match the preceding
[module completion experiment](../2026-10-09-module-completion/README.md):
AMD EPYC 9V45, four-core Ubuntu 24.04.4; SDK 10.0.401/runtime 10.0.12;
optimized PGO/LTO CPython 3.13.16 with normal GC/GIL and experimental JIT
disabled. Source trees were clean before and after collection. All services
succeeded, recorded workers exited, the lease was free afterward and recorded
inputs rehashed unchanged.

An independent audit checked all six receipts and **262 responses**, including
complete invocation counts, source/fixture/golden/actual output digests,
Release SDK/library identities, worker settings, clocks, measurement counts
and service completion evidence. Declaration order and all 18 medians were
independently verified. The preceding boundary diagnostic's 835 samples,
14 measured batches, input hashes and service completion were also checked.
Raw evidence remains local under
`.git/agent-notes/execution-threads-20261009/`.

| Evidence | SHA-256 |
| --- | --- |
| Quick catalog | `e6cf197948d903c59d4db5e940d9ca50433cbb0c6399ab999817f48775fa4fe1` |
| Baseline Lython library | `6a913cfbf63a90e0a13de5ffb3d05f11f06d2a525af55895938e43a33454170e` |
| Candidate Lython library | `931e2fb67692c9eb08025e5fe8882348eb189e0a5d4b7bfaebcbf486529d8b78` |
| Baseline worker | `82be8db2b25f2edc787d50fb0112eabc8ab020a466f7c8b5e15a6be00dc73035` |
| Candidate worker | `76bc3d5b05f50a590f64970a8a27a10ea8871254cd67bba89f09f9cc1b2570fe` |
| Boundary diagnostic receipt | `41b5f923740c2b32c5c6cb359cf30442f45914daf98b0578492e946ba87ce718` |
| Empty / 1 receipt | `1eb9afbd20dd5ff838957a5b1bf2127d9f1d9f29e2e0036845b80fe837707eec` |
| Loop / 1 receipt | `c9cc824e87ce627b8f3273a4cdbcfe84b2e998a56ff5103c6c79b8dedf129d7a` |
| Pipeline / 1 receipt | `539c519a15982045d4a28a3097008e4879eeda3c9b2c67919c2dcc541004c2f7` |
| Empty / 2 receipt | `e519e5181bfb3cedc2452ee97b100d4a8a92de068acfa68da41bbf9d1b3b7184` |
| Loop / 2 receipt | `f3b75a5383bfb2c2c789e905c210cfca1514e3d751e319c9c899b5afdd6d6034` |
| Pipeline / 2 receipt | `583dabce88105f67f1da4e32cc66846bb27155c253d72c7467c212608aab2583` |
