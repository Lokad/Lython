# JSON byte-run scanning assessment — withdrawn

2026-11-05. Restore original production: the complete workload evidence does not
justify the two search tables and added scan branches. Keep public boundary,
strict-UTF8 and cancellation controls. This is a bounded decision, not a permanent
veto on bulk scanning or a causal explanation of the remaining performance gap.

Baseline **cf3dc9cd**, frozen **43fdf658**. Completed prototype **eb1d8de3** is
787348c8 plus the archived production patch. Quiet Azure F4as_v7, Ubuntu24.04.4,
.NET11RC3, normal GC/tiering; same frozen auxiliary DLLs/CoreLib and string hashes.
Three isolated images share process GC; two are unchanged baseline roles. Six
seconds guest warmup/image,2ssettle,9rotated~200ms batches. Full invocation,
mediated RAM-file IO, stdout/file checks and prior-result cleanup are included;
reset/staging/compilation/final cached teardown excluded except the named phases.
All users joined, balances zero, owned fixtures removed, exact identities and both
quiet gates pass in every collection. Process allocation includes reset/background
work; no guest-only CPU, RSS, startup or real pending-IO speed claim.

## Original scope correction

The [encoder-only plan and source](encoder-only/plan.md) incorrectly attributed
the report profile's **ReadJsonString**6.13% leaf to output emission. The report
only reads JSON, and the summary emits two keys. Preserve the original **162s**
encoder-only collection, all controls and exact **52c2fe9c** bits, rather than
claiming an encoder gain from unaffected scripts. Its controls vary and allocation
is unchanged. [Original observations](encoder-only/observations.tgz),
[assessment](encoder-only/assessment.json).

The [corrected pre-code contract](reader-and-encoder/plan.md) adds bounded managed
byte-run searches to the actual native token scan. It keeps one decoder and all
strict/lenient/UTF8/error/name-reuse/ownership paths. The encoder retains strict
scalar validation, including ensure_ascii=False. No new owner, pool, token tape,
opcode, options, dependency, native code or runtime policy was introduced.

## Completed source comparison

**161s**, all gates. Elapsed changes versus the two baseline roles:

| Complete job | Elapsed change | Baseline spread | Candidate IQR |
| --- | ---: | ---: | ---: |
| Report warm | −0.23% / −0.50% |0.27%|1.09%|
| Report async/completed IO | −1.67% / −1.12% |0.56%|1.10%|
| Report compile/run | +0.16% / −1.59% |1.78%|1.36%|
| JSON-lines warm | −2.00% / −0.69% |1.32%|0.28%|
| Text warm control | +0.15% / −0.36% |0.51%|0.25%|
| Report compilation control | +1.03% / −0.86% |1.90%|0.47%|

Allocation effectively unchanged. Modest favorable async-report signs do not
establish a substantial general improvement. Fresh/compile and JSON magnitudes
are limited by the unchanged-role spreads. [Raw](reader-and-encoder/observations.tgz),
[all role samples/summaries](reader-and-encoder/assessment.json).

## Actual output coverage

One justified **83s**, unchanged-bit follow-up fills the material encoder gap.
Reuse the canonical1024 short/escaped-Unicode rows from the existing
json.transform-roundtrip catalog in a complete file job: read JSON, toggle active,
write compact ASCII JSON. Input/output55,723B, full independent isolated CPython
goldens. Both one json.dumps/write and streaming json.dump qualify actual output.
These are output-qualification jobs, not replacement primary CPython workloads or
a favorable long-token fixture. Warm only; no new fresh/startup claim.

| Output qualification | Elapsed change | Baseline spread | Candidate IQR |
| --- | ---: | ---: | ---: |
| Read/edit/encode/write | **+7.17% / +4.72%** |2.34%|0.59%|
| Read/edit/stream write | **+1.38% / +1.99%** |0.60%|0.53%|
| Text control |−1.64% / +2.07%|3.64%|0.32%|

Allocation effectively unchanged. The target regressions exceed within-role IQR;
control spread limits their exact magnitude. No cause is attributed to a specific
scan or runtime mechanism. [Plan](output-coverage/plan.md),
[raw](output-coverage/observations.tgz), [assessment](output-coverage/assessment.json).
Do not add these changes to other source-pair gains or derive current CPython ratios.

## Correctness and disposition

Completed prototype: nonincremental Release35.73s/0warnings/errors,
**493 internal/1,163 public focused checks per actual .NET11/.NET10**. Both API
routes, strict/lenient JSON, escaped and malformed UTF8, cancellation, terminal
refusal, retained output, shared budget and awaited cleanup are covered. Added
eight reader/eight encoder input-window goldens independently checked under
isolated CPython3.13.2, plus malformed UTF8 with both ASCII settings and the
existing cancelled scanner test extended to plain input. No timeout or original
containment assertion was relaxed.

Both production files are restored. Nonincremental Release32.82s/0warnings/errors
and the same493internal/1,163public checks per actual target pass on the original
production source with retained semantic controls. No alternative scan machinery
remains. [Restored-source identities/results](restored-validation.json),
[logs](restored-validation.tgz).
Full original-source platform/Stress/installed-consumer qualification remains the
cached41944972624 result; new control portability is part of the next coherent
delivery. Publication postponed. Continue whole-helper preparation/lifetime work;
do not chase this with a sequence of scan thresholds or local opcode hints.

Each subdirectory preserves its original source snapshot, patch, identities,
premeasurement plan and immutable raw archive. The focused validation archive
describes the corresponding prototype; restored-source checks are separate.
