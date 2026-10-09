# Readonly instruction references, 2026-10-09

The instruction-reference candidate is accepted for its repeated **22% lower
integer-loop time**, with mixed secondary pipeline results retained. This is a
loop improvement, not evidence of a general improvement for every job. The
pipeline took 2.9% less time in the first repetition and 7.6% more in the second;
that possible regression remains a follow-up. No collection was repeated to
seek a favorable result.

The six declared micro collections took **6.40–7.02 seconds** each, including
startup, warmup, verification and shutdown. A separate ten-second CPU trace
preceded the experiment. Each collection had a **30-second hard process-group
collection and cleanup cap**. No full comparison lane ran in this round.

## Evidence and isolated change

Baseline `6776baf2445735ca852d830f628e5cac9b1ca194` includes the delivered
synchronous thread reuse. A refreshed Linux on-CPU diagnostic of the canonical
16,384-iteration integer loop completed in 10.99 seconds of tracing supervision,
after a two-second warmup. The reader found 5,280 samples, no lost events or
missing stacks, and 477 unresolved leaves (9.0%). Exclusive samples were 43.5%
in synchronous dispatch, 10.4% in stack transfers, 8.6% in execution checks,
6.4% in value handlers and 2.0% in control flow. Inlined work is included; these
fractions do not predict savings from any particular change.

Instructions are 48-byte readonly structs. Candidate
`db73573563c22e417f536fa7efb54cd664100996` changes five synchronous handler
parameters from by-value instruction arguments to readonly `in` references:
definition/fallback, stack transfer, structure, value operations and control
flow. The handler bodies, instruction lookup, opcode order, spans, abrupt
routing, value observation and every execution checkpoint are unchanged.
Asynchronous callers of shared synchronous handlers remain supported; async
handlers still receive their own instruction values.

This tests a cheaper transfer boundary for existing dispatch. The controlled
old/new timing supports the loop improvement, but neither sampling nor source
inspection isolates the exact machine-copy or JIT contribution.

## Short comparisons

The unchanged quick catalog supplies `control.empty.control`,
`loops.integer.large` and `strings.pipeline-ascii.medium`. Both engines
precompile the same ordinary Python source and perform the complete job with
fresh guest state. Every invocation validates complete output. Lython uses the
normal synchronous public API with its execution stack, containment, memory
limits and result projection. CPython uses isolated ordinary execution and
capture, without an equivalent in-process governor. Entry cost is included;
no control time is subtracted.

The declared order was empty, loop, pipeline, repeated once. Each engine has
seven measured batches and before/after verification in each collection.
Worker GC, tiering and PGO settings remain normal. Builds and correctness
checks occur outside collection. These are diagnostic observations without
milestone qualification or a claimed confidence interval.

| Job / repetition | Baseline µs | Candidate µs | CPython µs | Baseline / candidate IQR fraction |
| --- | ---: | ---: | ---: | ---: |
| Empty / 1 | 23.80 | 23.49 | 1.48 | 0.042 / 0.029 |
| Integer loop / 1 | 2,150.31 | 1,670.06 | 621.58 | 0.054 / 0.065 |
| ASCII pipeline / 1 | 350.61 | 340.56 | 45.37 | 0.071 / 0.116 |
| Empty / 2 | 24.09 | 23.59 | 1.49 | 0.009 / 0.044 |
| Integer loop / 2 | 2,097.83 | 1,634.08 | 617.08 | 0.010 / 0.003 |
| ASCII pipeline / 2 | 309.65 | 333.16 | 41.64 | 0.033 / 0.026 |

Loop medians fell 22.3% and 22.1%. The first loop repetition has visible spread;
the second has substantially tighter batches and the same direction. Empty
medians fell 1.3–2.1% amid comparable variation, so no empty-call improvement
is claimed. Pipeline medians changed in opposite directions. Preserve those
results and investigate the second repetition's higher candidate time rather
than attributing it to external interference or dismissing it as noise.
The remaining loop and string-processing gap to CPython still requires work.

## Correctness and reproduction

The candidate passed **1,906 focused public Release checks** locally. The
frozen pinned Linux Release candidate passed **2,141 public checks** and six
worker lifecycle checks before collection. Coverage includes both execution
modes, generators, augmented/exact integer operations, exception cleanup,
checkpoints/cancellation, stack guards, bound names, scopes, imports, matching
and comparison-supervisor behavior. No guest limits were relaxed, and I/O
remains host-mediated. Existing behavioral checks are used for this mechanical
transfer change rather than tests that mirror the parameter declarations.

Use the Release comparison executable's `--compare list --profile quick`
command to produce the catalog, then `--compare micro` with the pinned
executables and baseline worker, selecting the three IDs above in the declared
order. Hold the shared VM lease and wrap each collection in an owned service
with `RuntimeMaxSec=30`, `TimeoutStopSec=0` and `KillMode=control-group`.
Keep fresh receipt filenames and preserve all results.

The dedicated machine and toolchains match the preceding
[execution-thread experiment](../2026-10-09-execution-threads/README.md):
AMD EPYC 9V45, four-core Ubuntu 24.04.4; SDK 10.0.401/runtime 10.0.12;
optimized PGO/LTO CPython 3.13.16 with normal GC/GIL and experimental JIT
disabled. All source trees were clean and inputs rehashed unchanged. Services
succeeded, workers exited, and the shared lease was free after cleanup.

Independent audits checked six receipts and **264 responses**, complete
invocation counts, all input/output digests, Release producer/SDK identities,
ordinary worker settings, clocks and service completion. Declaration order and
all 18 medians were recomputed. The profile audit checked 162 protocol
responses and 5,092 completed invocations, trace identity and CPU samples.
Raw evidence remains local under
`.git/agent-notes/loop-dispatch-profile-20261009/`.

| Evidence | SHA-256 |
| --- | --- |
| Quick catalog | `e6cf197948d903c59d4db5e940d9ca50433cbb0c6399ab999817f48775fa4fe1` |
| Baseline Lython library | `089a284eaae5a10e3af2b45afe9a251354d6f66f90cf6adb9148a9e4d30f2412` |
| Candidate Lython library | `7e7d442c21a5401eb2a0f6428b37744206360e6f40d35990fdefa78e4cf21227` |
| Baseline worker | `bbc4087512eef80de78a69db4dad17cc0094d12917b5128a4ed4dedba1cc4c49` |
| Candidate worker | `f364ec3947046e51050d8f42cd04b8ae9a8a03207cac515b1c6b1e8ae03fee82` |
| CPU trace | `9db323d57c5059225a95a3374487ddb6dcd3eb3f512d4a6df619302c80c7b0ae` |
| CPU summary | `c7a966f4e5f65f913942ede7b44c6b24a39492b563df04909e08d6333f2ba169` |
| Empty / 1 receipt | `1e28e7c128336cc5d7be339f4062f5400d54e50d883a8c7840b8d4438ccb5fab` |
| Loop / 1 receipt | `0cba2ed3d4cb98cf9848b5016974193168b09e2e18cbf7a5ef5ce67ebd0a2fb1` |
| Pipeline / 1 receipt | `0c10da6f3a58e215e1f5196840b40f2df075aa2076e48c67c3e7891f4cc976ae` |
| Empty / 2 receipt | `b55efe67efccb1d8c74cf71cf3a6f8c8b988af35a94ad79828047d9bbf40fa23` |
| Loop / 2 receipt | `f58f7be251cd7adaa0db89af4241f028b8434a8a12582bc673b0a8afdee05c49` |
| Pipeline / 2 receipt | `d884cf181748b2b5132bb5f5f0c56a49c32d7660f35d85181ecacde7ed2e5c5a` |
