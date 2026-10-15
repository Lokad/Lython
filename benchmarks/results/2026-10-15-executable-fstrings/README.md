# Ordinary f-strings use existing executable operations

Ordinary f-strings now call the same compiler helper used by yielding generators.
Field expressions and dynamic specifiers are compiled; existing conversion,
formatting and final-join operations handle the result. Three added compiler lines
remove the whole-f-string expression fallback and local mirroring required only
by it. There is no new opcode, runtime handler, owner or configuration. Embedded
expressions with other unsupported lowering still retain their own fallback.
Required lowered formatting remains for other code; its shared async helper is
not removed. This does not complete async dispatch, comprehensions or bulk text.

| Warm report helper, 2,048 calls | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Zero-padded row number, computed next value and newline | 2.454 ms | 2.125 ms | 0.4201 ms |

One [fixed short diagnostic](warm.json) observed a **13.4% lower Lython median**;
IQR/median was 1.3% / 2.5% / 2.5%, respectively. No qualified multiplier,
confidence interval, complete file-job or overall speedup is established. The
owned diagnostic took 7.722 s including cleanup; no retry or retuning.

Baseline 0c367762230b7700ec00fd6ba24ba067f401f55b adds the shared report-helper
fixture to 594bfa30. The candidate changes only one frozen compiler file in
[the declaration](comparison-declaration.json). Both roles use identical
[workload](catalog-micro.json), timing machinery and CPython worker. Timing is
warm synchronous Run with fresh execution state, projection and previous-result
disposal; compilation, process startup, transport and full hashing are excluded.
Real host suspension is tested separately, not included as an injected-delay ratio.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs / 16 GiB RAM. SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16 with
normal GC/GIL, `-I -S` and explicit UTF-8 protocol. Normal public Lython policies,
cooperative allowance, GC/tiering and managed stack protection apply; CPython has
no equivalent containment. This is a microbenchmark, not a complete report file.

[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent outputs at all three core sizes. [Cleanup/source proof](cleanup-source-proof.json)
verifies one frozen production difference, identical tooling, seven exited
workers, inactive services and an available VM lease. No file fixtures are used.

[Local checks](local-validation.json) pass: 2,325 ordinary white-box cases,
four focused source-path cases, 237 formatting/lexical/generator/lifetime public
cases, and 284 containment/representative/file-worker cases (groups overlap).
Each group is under 30 s including cleanup. Controls cover conversion/specifier/
format order, Unicode, debug/nested fields, closures/locals, yield/send, actual
conversion/specifier/format suspension and cancellation without replay, retained
text, admission before publication and final-join denial with no reservation leak.
Six [independent CPython 3.13.2 goldens](cpython-goldens.json) match. No timeout or
budget was relaxed. Package/platform qualification is recorded at delivery.

Formatted parts coexist with final output during joining; their estimates may
remain charged until the finite execution/result owner ends. Earlier denial and
different explicit fuel checkpoints from lowered formatting are documented in
SPEC.md and MEMORY.md. Pending callbacks and escaping results remain funded.
