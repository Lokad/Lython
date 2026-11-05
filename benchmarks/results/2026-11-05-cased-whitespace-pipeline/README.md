# Final-word casing and whitespace construction

2026-11-05, quiet AzureF4as_v7 Ubuntu24.04.4/.NET11RC3, normal GC/tiering.
Baseline f7fed42c/frozen0d5af60a; premeasurement documentation parentca05da4b.
Candidate `43fdf658f44bc47e48f4a4970abcd5b123dd37ab7d613447ec0107b48e62ab2b`.
[Exact source/binary identities](candidate-identity.json). Publication postponed.

RETAIN the complete guarded lower/upper -> default whitespace split pipeline.
Use the existing native-expression graph and Unicode casing algorithms to write
independently owned final words through the one whitespace materializer. Remove
the unobservable cased-line string/backing, its admission/copy and recursive
evaluation. No opcode, code-object field, descriptor, second evaluator, ASCII-only
shortcut, cache or owner is introduced. Standalone casing/split retain real users.

Range scratch now records final capacity alongside input bounds:12B rather than8B,
still covered by the existing16B/entry estimate and replacement-overlap admission.
One output reservation covers all word metadata/full capacities; existing ownership
transfer records actual UTF-8 length without shrinking/refunding retained capacity.
No views borrow source bytes. Removing the intermediate changes admission amounts,
order and possible failure spans; first refused final-storage request is terminal,
with no retry/fallback after work. Custom protocols and actual suspension retain
the ordinary engine. Cancellation remains active during sizing/scans/writing and
after admission, before allocation.

Python's29 whitespace scalars are case-stable and break final-sigma context.
Lower/upper mappings introduce no whitespace. Thus full-word casing preserves
boundaries, sigma context and expansions. [Independent CPython3.13.2/Unicode15.1
goldens and all-scalar mapping proof](cpython-goldens.json) support that boundary.
Explicit separators/maxsplit are not fused. No new policy or memory infrastructure.

Nonincremental Release86.95s, zero warnings/errors;1280 white-box/4772 public focused
checks per actual.NET11/.NET10 pass. The final66-case pipeline selection overlaps
63 cases already included, adding three custom-method real-IO success/cancellation
and awaited-cleanup checks. That test-only build15.93s passes with identical frozen
production hashes. Coverage includes all29 whitespace scalars, sigma/ignorable
neighbors, expansions, empty input, retained shared-budget results, independent
source/result owners, complete-output refusal before payload allocation and
cancellation during admission. Existing separator/maxsplit/protocol/native-function
families remain. [Validation](validation.json), [all focused logs](focused-checks/).

## One complete-source comparison and debt decision

Six jobs161s. Three isolated images/two identical baseline roles,6s warmup/image,
2s settle,9rotated200ms batches,240s owned service. All exact binary/CoreLib/hash,
independent CPython stdout/file golden, quiet before/after, baseline/final-zero,
owned-fixture-removal and process-join gates pass.

| Complete script | Phase | Candidate | Elapsed vs old / old control | Old spread | Process allocation |
| --- | --- | ---: | ---: | ---: | ---: |
| Text summary/helpers | Warm |2.822016ms|-3.68% / -9.99%|7.01%|-14.09%|
| Text summary/helpers | RunAsync |3.240384ms|-5.24% / -4.89%|0.37%|-14.08%|
| Text summary/helpers | Fresh |2.977229ms|-5.23% / -6.57%|1.44%|-13.17%|
| Text summary/helpers | Compile |56.29us|+1.32% / -0.26%|1.58%|unchanged|
| JSON aggregation control | Warm |4.917930ms|+0.91% / +1.06%|0.15%|unchanged|
| JSON-to-Markdown control | RunAsync |4.549346ms|-2.23% / -0.58%|1.65%|unchanged|

Allocation reduction and fresh/async elapsed improvements are established here.
Warm text favors the candidate but7% identical-source spread limits magnitude;
retain both baselines, no confirmation or discarded samples. Compilation and
report control are inconclusive. JSON's~1% adverse elapsed remains accepted debt
for materially less text allocation/storage and work; its cause is not established.
No general CPU, additive-gain or updated CPython-ratio claim follows. The whole-
script performance objective remains open. Full final-source delivery is pending.

Warm includes public invocation, mediated RAM-file IO, full checks and prior-result
cleanup; async host IO completes synchronously, with actual suspension tested
separately. Fresh adds compilation/disposal. Compile-only excludes execution/IO.
Fixture staging/reset, startup, response hashing and final cached-owner teardown
are outside these clocks; all users join. Process allocation includes reset/runtime
noise. Initial untimed admission peaks are not fresh overlap, physical heap/RSS or
embedding latency. [Every role's medians/IQR/capacity witnesses](assessment.json),
[compact table](measurements.md), [pre-code contract](pre-code-plan.md).

Raw archive SHA256:
`1601f4541db6d504676a85ff3c52e841e87598d7b4b7ed597e3a7a19377ee32e`.
Raw archive includes the existing private driver/catalog/gates and all samples.
The source snapshot has the initial focused tests; [final test snapshot](final-tests-snapshot.tgz)
adds the three custom-method controls, without production changes or retiming.
