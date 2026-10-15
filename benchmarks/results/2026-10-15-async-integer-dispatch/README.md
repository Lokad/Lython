# Async integer addition and yielding in-place addition

Exact integer `+` and `+=` now reuse the synchronous observed-value handler in
the async instruction driver. The existing admission, operand cleanup and
instruction checkpoints apply. Other operand types retain resumable dispatch;
no guest operation is attempted synchronously and then replayed.

A genuinely yielding `__iadd__` regression exposed a separate correctness bug:
the async augmented-assignment fallback called user methods synchronously.
User addition now awaits `__iadd__` and, when declined, ordinary/reflected
addition in Python order. Builtin list/deque drains retain their existing path.
Other augmented operators remain separate work.

| Warm execution | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Integer reduction, 16,384 iterations, `RunAsync` | 3.108 ms | 2.660 ms | 0.579 ms |

The single diagnostic observes a **14.4% lower Lython median**. IQR/median is
1.1% baseline, 6.0% candidate and 1.3% CPython. This is not a qualified multiplier,
confidence interval or overall application speedup. Do not combine it with the
earlier helper-call improvement: that uses a different workload and revision.
[Raw samples](warm-async.json) preserve all seven rotating 25 ms batches,
calibration, warmup and loaded identities. The owned comparison took 7.900 seconds,
including cleanup, under a 30-second deadline. No samples were replaced or retried.

Both timers include fresh invocation state, execution, capture/projection,
golden checks and prior-result cleanup. Compilation, process startup, transport
and full response hashing are excluded. `RunAsync` includes startup-thread
creation; host operations complete without injected delay. CPython executes
ordinary warm code. This measures neither guest async syntax nor I/O throughput.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; .NET SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16,
ordinary GIL/GC, `-I -S -X utf8`. Lython's public policies and shared allowance
remain enabled. GC/tiering are unchanged; CPython has no equivalent containment.

## Provenance and validation

Baseline is 88f781061d5a628b1f134994d14b4c117a4e1261. The candidate was built before
commit on that revision plus exactly two production files, identified by frozen
[source hashes](comparison-declaration.json). The comparison harness is identical
for both roles. [Baseline](baseline-correctness.json) and
[candidate](candidate-correctness.json) match CPython at all three loop sizes.
[Cleanup/source proof](cleanup-source-proof.json) verifies the source differences,
exited workers, inactive services and released machine lease.

Focused Release groups pass 54 white-box / 131 public cases, then strengthened
cancellation guards pass 46 white-box and governed/CSV/text/JSON/file controls
pass 103 public cases. These groups overlap; do not sum them as unique coverage.
Every group, including cleanup, takes less than 30 seconds. Existing promotion,
fuel/span, admission-before-publication and cancellation witnesses run on both
drivers at unchanged allowances. Cancelled/failed in-place addition retains its
original binding. [Validation](validation.json) records receipts and TRX hashes.

Independent CPython traces cover [yielding in-place addition](cpython-yielded-iadd.json)
and [declined/reflected fallback](cpython-yielded-iadd-fallback.json), with each slot
called once. The initial public-test failure is retained in validation evidence;
it preceded the async protocol fix and was not resolved by weakening assertions.

The parent revision's full ordinary/stress/platform/package qualification is
historical evidence for that parent. This candidate uses focused checks during
development; delivery qualification is recorded separately. Broader async dispatch,
direct calls, mutable function metadata and worker reuse remain open.
