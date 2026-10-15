# Reuse bound storage for matching executable locals

Executable functions whose complete parameter and local layouts coincide now use
the invocation-owned bound array directly. This removes a second local array,
its initialization and the parameter copy. Both execution drivers and positional
or keyword binding use the same frame construction and completion cleanup.

Captured locals still get independent cells. Exposed locals copy values. The
interpreter detaches before the bound array is cleared/returned, including genuine
async suspension, cancellation and errors. The existing simple-return shortcut
remains. Larger, differently mapped or mirrored frames retain the general copy;
allocating full local storage during binding would change allocation ordering.
No frame/argument framework, pool, option or tracing mechanism is added.

| Warm `RunAsync` | Earlier Lython | Candidate Lython | CPython 3.13.16 |
| --- | ---: | ---: | ---: |
| Full-body helper, 16,384 calls | 10.760 ms | 10.372 ms | 1.287 ms |

The helper updates its existing parameters and returns the result. It requires a
full executable frame; the existing simple-return case would not measure this
change. Its three sizes have independent arithmetic goldens. The single
[diagnostic](warm-async.json) observed a 3.6% lower Lython median, with baseline /
candidate / CPython IQR-to-median of 0.9% / 2.3% / 1.1%. **No qualified speedup,
overall multiplier or confidence interval.** All seven rotating batches, warmup,
calibration and identities remain. No retry, retuning or replacement samples.

The owned command took 8.420 seconds including cleanup under a 30-second bound.
Timed work includes new invocation state, execution, capture/projection, golden
checks and prior-result cleanup; compilation, startup, transport and full response
hashing are excluded. Both engines use ordinary warm code. RunAsync completes
host operations synchronously here; actual yielding is a separate lifetime test.

Measured 2026-10-15 on Azure Standard_F4as_v7, Ubuntu 24.04.4 LTS, four AMD EPYC
9V45 vCPUs, 16 GiB RAM; SDK 10.0.401/runtime 10.0.12, CPython 3.13.16 with ordinary
GIL/GC and `-I -S`. Its pure-worker identity records UTF-8 mode off; protocol I/O
still explicitly uses UTF-8. Independent local goldens use CPython 3.13.2 with
`-I -S -X utf8`. Lython's ordinary public policies/shared allowance and normal
GC/tiering remain enabled; CPython has no equivalent containment.

## Provenance and validation

Baseline is f93e92585c4b2c77564e0dcf3c3ba0390f55b2de. Candidate was built before
commit on that revision plus three production files identified by
[frozen hashes](comparison-declaration.json). Both roles use the identical new
catalog case; execution/comparison tooling is unchanged. [Baseline](baseline-correctness.json)
and [candidate](candidate-correctness.json) match CPython at all three sizes.
[Cleanup/source proof](cleanup-source-proof.json) verifies seven workers exited,
exact producer differences, identical tooling, inactive services and lease access.

Fifteen [independent Python programs](cpython-goldens.json) cover parameter updates,
keyword calls, recursion, locals snapshots, escaped captured cells (including a
function replacing its captured parameter), aliases, defaults, target order and
deletion/reassignment. Each passes both public APIs with shared-budget cleanup.

Internal storage witnesses check all three pooled widths under both drivers and
both argument forms; genuinely suspended successful/cancelled calls keep the same
array live and only clear it after detaching. Existing guards, denial boundaries,
operand cleanup and compiled/lowered fallback controls remain unchanged.

Focused 121 white-box / 79 public cases, 103 governed CSV/text/JSON/file controls
and all 2,172 ordinary white-box tests pass. Groups overlap; do not sum them.
Each group takes less than 30 seconds including cleanup. [Validation](validation.json)
retains receipts and TRX hashes. One first fixture incorrectly expected a
definitely deleted-local read to compile; existing LA3146 rejects it before
execution. The final fixture tests deletion/reassignment, with independent Python
outputs; this is no changed static-analysis contract. No new full stress run is
claimed. Package/platform qualification is recorded at delivery.

This closes matching-layout reuse. Frames with additional locals, direct methods,
receiver-independent dispatch and the rest of the performance plan remain open.
