# Positional calls without packaged arguments

Eligible exact compiled functions now consume a managed view of existing operands.
Binding copies values into the existing owned bound storage before any body can
suspend. Converted calls avoid `CallArgumentValue[]` packaging/copy/clear and the
async argument-factory closure. Eligibility is prepared once for all-positional
sites and signatures without keyword-only/variadic parameters or local mirroring.
Positional defaults remain live. Frame entry, body execution, annotation of Python
exceptions and cleanup are shared with ordinary invocation.

| Warm `RunAsync`, completed host | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Integer helper, 16,384 positional calls | 6.234 ms | 5.378 ms | 1.001 ms |

One diagnostic observes a **13.7% lower Lython median**. Baseline IQR/median is
9.4%, candidate 1.7% and CPython 1.9%; the noisy baseline remains visible. This is
not a qualified multiplier, confidence interval or overall application speedup.
The measured change combines operand transport with canonical/completed-binding
async invocation; their individual contributions are not isolated. No samples
were replaced, no timing was retried, and the producer was not retuned afterward.

[Raw timing](warm-async.json) preserves all seven rotating batches, warmup,
calibration and loaded identities. The owned group takes 7.928 seconds including
cleanup under a 30-second deadline. Timers include fresh invocation state,
execution, output capture/projection, golden checks and prior-result cleanup.
Compilation, startup, transport and full response hashing are excluded. Lython's
per-invocation startup thread is inside its timer; CPython runs ordinary warm code.
No I/O delay is injected.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12 and CPython 3.13.16,
ordinary GIL/GC, `-I -S -X utf8`. Lython's public policies and shared allowance
remain enabled; GC/tiering are unchanged. CPython has no equivalent containment.

## Provenance and validation

Baseline is 1f2ef306a704caadec419924dfe3737f366cb610. The candidate was built before
commit on that revision plus eight production files frozen by
[source hashes](comparison-declaration.json). The comparison harness is unchanged
and identical across both roles. [Baseline](baseline-correctness.json) and
[candidate](candidate-correctness.json) match CPython at all three helper sizes.
[Cleanup/source proof](cleanup-source-proof.json) checks all seven verification/
timing workers exited, exact source differences, inactive services and machine
lease availability.

Eight [independent CPython programs](cpython-goldens.json) cover zero/wide arities,
live defaults, missing/excess arguments, target-before-arguments, escaped cells,
and keyword/variadic/mirroring controls. They run through both public APIs with
shared-budget cleanup. Existing fuel/span, result-admission and cancellation
witnesses now exercise both binders through synchronous/asynchronous invocation
at unchanged limits. A transport witness requires eligible calls to leave the
packaged-argument pool untouched.

Focused Release groups pass 91 white-box / 73 public, 103 governed CSV/text/JSON/file,
69 white-box stack/state/function and 27 public stack/depth/host cases. Groups
overlap; do not sum them as unique coverage. Every group includes cleanup and takes
less than 30 seconds. [Validation](validation.json) records exact receipts and TRX
hashes. The full ordinary white-box group passes 2,158/2,158 in 13.009 seconds
including cleanup. Package/platform results are recorded at delivery; parent
qualification does not establish a new full stress result.

This completes one argument-representation removal, not AP03. The bound-values
array and frame-slot copying still remain where required. Keyword-only, variadic,
expanded, descriptor, bound-method, builtin and lowered/mirroring calls keep the
general binder until converted. Receiver-independent method dispatch and direct
frame population remain separate work. No universal callable ABI, new memory mode,
unsafe code or scalar representation is introduced.
