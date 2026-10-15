# Exact integer operators under asynchronous hosting

The executable `RunAsync` loop now uses its existing synchronous observed-value
handler for binary and augmented operations when **both** operands are exact
boxed `BigInteger` values. These operands cannot supply guest methods. This
extends the earlier addition gate without adding operator implementations or
changing guest integer representation. Result preflights, transfer cleanup,
instruction accounting and error spans use the same existing path.

Mixed operands and user instances retain asynchronous dispatch. New genuinely
yielding controls exposed an existing bug: non-add augmented user methods still
ran synchronously. Prerequisite commit `60277f65` shares the existing in-place,
binary and builtin mappings, awaiting user-instance in-place/ordinary/reflected
methods and exact-integer reflected fallbacks once. Matrix handling and builtin
container mutation order remain. Other builtin scalar/container suspension paths
are outside this change; this does not close all asynchronous operation dispatch.

| Warm branch/integer loop using `RunAsync` | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| 16,384 iterations; modulo, comparison, addition/subtraction | 9.846 ms | 6.321 ms | 0.9599 ms |

One [diagnostic](warm-async.json) observed a **35.8% lower Lython median**.
IQR/median was 3.0% / 10.4% / 0.5%, respectively. This is a short unqualified
measurement, without a confidence interval, complete-job claim or overall
speedup. The remaining CPython gap is substantial. No retries, retuning or
replacement samples. The owned command took 7.908 seconds including cleanup.

Both roles include the protocol correctness fix. Baseline revision is
`60277f65c07e56fd13c4a3e47f5da7f7742c527d`; the candidate changes one frozen
interpreter source in [the declaration](comparison-declaration.json).
The existing [case](catalog-micro.json), comparison sources and CPython worker
are unchanged. Warm execution includes fresh run state, execution/projection
and prior-result disposal; compilation, process startup, protocol transport and
full response hashing are outside the timer. CPython compiles before warm timing.
Host operations complete synchronously here; real suspension is tested separately
and injected I/O delays are not used for a speed ratio.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with
ordinary GC/GIL and `-I -S`. UTF-8 protocol I/O, ordinary Lython GC/tiering,
execution limits and cooperative admission remain. CPython lacks equivalent
containment. No unsafe/native runtime technique, cache or configuration added.

[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent output witnesses at all three existing sizes.
[Cleanup/source proof](cleanup-source-proof.json) verifies seven exited workers,
the exact frozen source difference, identical tooling, inactive services and an
available VM lease. No file fixtures are involved in this case.

[Focused/local validation](local-validation.json): clean Release build;
2,306 ordinary white-box checks, 48 focused white-box, 106 focused public and
333 governed/representative/target-retention/file-worker controls pass. Groups
overlap and each completes within 30 seconds including cleanup. Cutoffs cover
every instruction for the remaining eleven augmented integer operators; denied
multiplication/shift/power admission preserves the previous value and funding.
Thirteen genuinely yielding slot chains preserve in-place/ordinary/reflected
order; reflected subtraction cancellation and escaping magnitude results preserve
owner cleanup and funding. Seven [goldens](cpython-goldens.json) independently
match local CPython 3.13.2. Initial twelve failures are retained privately as
correctness evidence; no timeout or assertion was relaxed. Full platform/package
qualification is recorded at delivery, not inferred from these focused checks.
