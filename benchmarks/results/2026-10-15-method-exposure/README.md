# Fresh method exposure with immediate-call caching

Ordinary reads of builtin instance methods now expose fresh objects in synchronous
executable and lowered execution. Previously, repeated evaluation of one
`value.upper` expression reused its cached bound callable: `is` returned true,
where CPython and asynchronous Lython returned false. Saved methods still retain
their receiver. Stable module functions retain their identity.

Executable code distinguishes immediate call-target loads from exposed reads;
only the former may cache bound callables. Direct lowered member calls preserve
the same cache, instruction checkpoint and managed stack admission. Dynamic
descriptors still resolve anew before arguments are evaluated. No receiver-
independent implementation cache or direct argument binder is claimed here.
Parenthesized lowered targets and argument expansion retain their existing
general evaluation/binding routes.

| Warm `Run` | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Tuple-key dictionary updates, 16,384 iterations | 30.144 ms | 30.573 ms | 2.418 ms |

The difference is within observed spread; **no timing improvement or regression
is established**. IQR/median is 4.3% baseline, 2.9% candidate and 4.3% CPython.
The reason to retain this change is Python method identity while preserving the
immediate-call cache. This single diagnostic does not qualify a multiplier,
confidence interval or overall application speedup. It is not retried or retuned.

[Raw samples](warm.json) retain seven rotating batches, calibration, warmup and
loaded identities. The complete owned comparison takes 9.819 seconds including
cleanup under a 30-second deadline. Timers include fresh invocation state,
execution, output capture/projection, golden checks and prior-result cleanup.
Compilation, startup, transport and full response hashing are excluded. Both
engines execute the same ordinary warm script, including sorted final output.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16,
ordinary GIL/GC, `-I -S -X utf8`. Public Lython protection and shared allowance
remain enabled; GC/tiering are unchanged. CPython has no equivalent containment.

## Provenance and focused validation

Baseline is 0eb587a9facdff0fad52e130b926c9952bf935a7. The candidate was built before
commit on that revision plus seven production files, frozen by
[source hashes](comparison-declaration.json). Comparison sources are identical.
[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match CPython at all three dictionary sizes. [Cleanup/source proof](cleanup-source-proof.json)
checks all seven verification/timing workers exited, exact source differences,
inactive services and machine-lease availability.

Eight [independent CPython programs](cpython-goldens.json) cover module-function
identity, executable/comprehension/class exposure, saved receivers, user rebinding
and lookup-before-argument order. Public regressions execute each through Run and
RunAsync at unchanged budgets and require cleanup back to the shared baseline.
The old internal assertions demanding exposed callable reuse now require fresh
objects; the immediate-call cache and immutable data controls remain checked.

Focused Release groups pass 88 white-box / 80 public cases, 111 public governed/
CSV/text/JSON/file controls (including the final eight programs), 28 public method
lifetime/descriptor/stack cases and 40 white-box stack/compilation/funding cases.
Groups overlap; do not sum them as unique coverage. All include cleanup and take
less than 30 seconds. [Validation](validation.json) records receipts and TRX hashes.
Full platform/package delivery qualification is recorded separately; the parent
revision's stress qualification does not establish a fresh stress pass here.

The method-exposure prerequisite is complete. Receiver-independent dispatch,
argument packaging, mutable function defaults and remaining async operation
families are separate work. No performance claim is inferred from the new opcode.
