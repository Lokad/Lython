# Expanded set/dictionary displays stay executable

Ordinary `{*items}` and `{**mapping}` displays now share the prepared operations
already used by yielding generators. The compiler removes both expression
fallback arms and local mirroring required only by them, and extracts one shared
dictionary lowering helper. No opcode, runtime handler, staging owner, key table,
prehashed-key wrapper or iterator is added. Required lowered paths remain for
lowered code; comprehensions and other expression families remain open.

Prerequisite5a8a2b16 corrects dictionary entry order: evaluate key, evaluate its
own value, then validate/insert. Previously an unhashable key suppressed value
effects, suspension, cancellation or a value exception. Syntax, lowered sync and
lowered async paths now agree with prepared execution. Error location remains
the key; native structural scopes stay outside awaited evaluation.

Three earlier probes suggested a general contiguous-group hashing rule. Primary
[source assessment](hash-order-assessment.json) and [small/16-entry probes](ordering-probes.jsonl)
show CPython changes batching with compiler thresholds. Exact callback counts,
probe sequences and cross-entry hash timing are implementation details, rather
than a new Lython staging contract. That proposed machinery was discarded before
implementation. The own-value-before-validation regression is independently
qualified and does not depend on those thresholds.

| Warm helper merging mappings and set members | Earlier Lython | Candidate Lython | CPython3.13.16 |
| --- | ---: | ---: | ---: |
| 2,048 calls; mixed literal/expanded entries | 5.444 ms | 4.963 ms | 0.6326 ms |

One [short diagnostic](warm.json) observed an **8.8% lower Lython median**;
IQR/median0.7%/1.3%/1.4%, respectively. No qualified multiplier, confidence
interval, overall speedup or complete file-job claim. No retry or retuning.
The owned command took7.871seconds including cleanup. The CPython gap remains
substantial; this comparison does not attribute it to a single cost.

Both roles contain the ordering fix. Baseline28c41f1de7f5df3cfdbf7fddb6a5eee426a2c631
adds the shared helper fixture to5a8a2b16; the candidate changes only two frozen
compiler sources in [the declaration](comparison-declaration.json). The
[case](catalog-micro.json), timing machinery and CPython worker are identical.
Warm timing includes fresh run state, execution/projection and prior-result
disposal, excluding compilation, process startup, transport and full hashing.
Real host suspension is tested separately, not used for an injected-delay ratio.

Measured2026-10-15 on AzureStandard_F4as_v7, Ubuntu24.04.4 LTS, four AMD
EPYC9V45vCPUs/16GiB RAM; SDK10.0.401/runtime10.0.12 and CPython3.13.16 with
ordinary GC/GIL, `-I -S`, explicit protocol UTF-8. Normal Lython public policies,
cooperative allowance, GC/tiering and managed stack protection remain; CPython
has no equivalent containment.

[Baseline](baseline-correctness.json) and [candidate](candidate-correctness.json)
match independent outputs at all three core sizes. [Cleanup/source proof](cleanup-source-proof.json)
verifies both frozen compiler files, identical tooling, seven exited workers,
inactive services and an available VM lease. No file fixtures are used here.

[Local checks](local-validation.json):2,319ordinary white-box,13focusedwhite,
24focusedpublic,376semantic and284governed/representative/file-worker cases pass
(overlap), each under30seconds including cleanup. They cover no-fallback/no-forced-
mirroring helpers, syntax/lowered key-value order, mapping-key snapshots and
yielding keys/getitem, set iteration, cancellation, overrides/key identity,
closures/locals, yield/send, admission-before-publication and retained funding.
Eleven [independent CPython3.13.2 goldens](cpython-goldens.json) match. Baseline
regression failures remain private; no budget/timeout relaxed. Package/platform
qualification is recorded at delivery.

Existing per-item fuel applies. Unpublished partial containers may remain charged
until their finite execution/result owner ends; earlier denial than a retiring
lowered constructor is possible. SPEC.md and MEMORY.md make that conservative
contract explicit. No forced GC or guest-object tracing is introduced.
