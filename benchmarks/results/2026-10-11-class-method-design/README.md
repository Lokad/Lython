# Class-method execution: current CPU leads and baseline boundaries

Two separately declared **ten-second method traces** completed in **16.09s / 14.08s**
including cleanup, under **30-second external process-group caps**. They reuse the
frozen Release producer from the [corrected scoped-dispatch round](../2026-10-11-scoped-dispatch-corrected/README.md).
No normal timing collection, full performance lane, runtime change or recollection
was made for this assessment. Longer qualification remains an infrequent milestone.

The traces point to attribute lookup, assignment and method invocation as remaining
design costs. **23 independent class-factory boundary checks pass on unchanged
production**, before any executable class-method prototype. The complete Windows
Debug white-box suite passes **1,597 tests**.

## Captures and interpretation

| Observation | First capture | Second capture |
|---|---:|---:|
| CPU samples | 1,247 | 1,298 |
| Lost events / missing stacks | 0 / 0 | 0 / 0 |
| Unknown leaves | 64 (5.13%) | 63 (4.85%) |
| Instance attribute resolution, leaf | 8.58% | 9.55% |
| Member assignment, leaf | 5.69% | 4.93% |
| Function invocation, leaf | 5.61% | 5.24% |
| Function invocation, inclusive | 46.19% | 46.07% |
| Direct lowered statement dispatch, inclusive | 29.51% | 29.58% |

Inclusive stacks overlap: these percentages cannot be added or treated as
removable time. Profiler/kernel work, background JIT and unknown leaves remain
visible. This is current-source diagnostic evidence; it does not establish a
before/after CPU reduction or forecast a speedup.

The actual canonical `calls.method.medium` case is unchanged. All **496 worker
responses / 15,688 completed invocations** have their complete output hashes
checked. Normal timing, native inspection and allocation groups from the preceding
round were terminal before this declaration. Both trace services are terminal,
all six recorded controller/worker/tracer PIDs are absent, the producer is clean,
inputs/helpers rehash correctly, and the shared collection lease is free.

Both traces retain their complete leaf/inclusive tables, allocation ticks and GC
event accounting in [evidence.json](evidence.json). Sampled allocations include
execution contexts, integers, compiler display classes, dictionaries and argument
arrays. Allocation ticks are sampled evidence, without exact per-type byte claims;
the display-class row has not been tied to a specific source closure. GC event
pairs are incomplete, so complete collection counts and pause totals are withheld.
Raw traces, responses, receipts, helper versions and journals remain retained;
their hashes are included in the evidence.

Producer: `d10564e7e2e35a6ad9ddc1788548f123f3178ef3`, production tree
`5acf16ebcbc079b5f164c9c7cbb73deddfeb732c`, identical to green delivery `f33a4119`.
Benchmark project tree: `c194ce88a7e4d52c576f3929ef82bce60bf37e2d`.
Workers retain ordinary .NET 10.0.12 / SDK 10.0.401 tiering, workstation Interactive
GC and no runtime overrides. Profiling providers are separate from normal timing.

## Independent baseline checks

[ClassMethodExecutionBoundaryTests.cs](../../../tests/Lokad.Lython.Tests/ClassMethodExecutionBoundaryTests.cs)
executes real class definitions through executable synchronous and lowered
asynchronous module entry, then binds their actual function values to real
factory-created instances. It covers zero, one and two explicit arguments:

- All identity-body fuel boundaries, failure spans/source information and unwind.
- Fuel before cancellation, rejected frame admission, preserved active exceptions,
  balanced funding and a successful subsequent invocation.
- Actual frame services, argument identity, owner binding and the live lexical
  class cell used by implicit `super`.
- A governed local list: **384 additional accounted bytes**, no outstanding
  reservation, **13 + argument width checkpoints** and interpreter depth one
  at the observation site in both modes. These values were pinned from original
  production observations before any body prototype.
- Genuine async callable suspension, same-run reentry while the first method
  retains its arguments and binder lease, and cancellation cleanup. The test
  callable rejects synchronous invocation, exposing a synchronous body wrapped
  in an async return value.

The initial fixture passed 23 checks. It was strengthened to use same-run reentry,
then a governed list, then pinned observations; every attempt passed and its
source/binary/TRX evidence is retained. Final checks pass within the full 1,597-test
white-box suite. Public delayed-host coverage and the preceding full Windows/Linux
9,399-test gates are separate evidence; these new checks do not complete the
remaining compatibility inventory.

Actual class factories currently produce `PyFunction` in both modes. Synchronous
calls use the accepted function-scoped direct lowered dispatch, and asynchronous
bodies use the awaited route. Existing `PyExecutableFunction` has no async body
override: its base entry runs synchronous `ExecuteBody`. Executable body entry also
adds a null-span interpreter checkpoint and per-instruction checks. The current
class-factory identity method completes with three checkpoints; existing compiled
identity-body tests require five. Reusing the module compiler therefore needs a
design that preserves method checkpoints and genuine suspension.

Executable class-method bodies remain open. Defaults, decorators, annotations and
generic type scopes, transitive closures, globals/nonlocal behavior, generator
lifetime and public host suspension must retain their own baseline proofs before
integration. No body prototype or gain prediction is introduced by this report.
