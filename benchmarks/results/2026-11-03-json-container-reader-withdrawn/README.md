# B137: resumable JSON container reader, withdrawn

2026-11-03, quiet Azure F4as_v7 Ubuntu 24.04.4, .NET 11 RC3. Baseline runtime
fd7bc7e4 (HEAD c31d639d changes only documentation/evidence). No production change
from this trial survives. Four public reentrant-hook regressions are retained.

The source gate identified duplicate object grammar and recursive async container
calls, but did not establish their removable CPU fraction. JSON's roughly 30%
inclusive report profile also contains useful scanning, allocation and construction.
The prototype replaced both recursive container methods with one resumable reader,
an admitted parent array for nesting, and an awaited continuation for actual I/O.
The refinement removed a completed-value mailbox and fed native values directly.
The added manual workspace/continuation logic needs demonstrated value to retain.

| Trial / warm complete job | Elapsed candidate vs baseline | Unchanged control vs baseline | Decision |
| --- | ---: | ---: | --- |
| Initial report, first / second | -2.86% / +0.92% | -2.60% / +0.65% | Tracks control; no gain established |
| Initial JSON lines, first / second | -0.33% / +0.40% | +2.83% / +3.68% | Some relative benefit amid drift |
| Refined report, first / second | +0.22% / -4.45% | +0.13% / +0.25% | Mixed, no consistent gain |
| Refined JSON lines, first / second | +1.93% / +1.45% | +0.47% / +0.43% | Small regression |

Initial allocation: report +0.0027%, JSON lines -0.0217%, config +0.0208%.
Refined: report +0.0020%, JSON lines -0.0183%. These are immaterial and mostly
track controls. Fresh-job CPU controls move substantially; elapsed/CPU summaries
are diagnostic, not a CPU improvement claim. Full samples and adverse results
are preserved in both assessments and raw archives.

**Withdrawn:** the observed tradeoff does not justify the added state machinery.
This is not a permanent ban on parser consolidation or a fixed-percentage veto.
One bounded refinement was justified by a concrete unnecessary mailbox; its
result did not warrant another timing attempt. Future construction work should
remove real storage/admission/copy costs rather than infer dominance from async.

Warm lanes exclude compilation and reuse compiled code. Fresh lanes include
compilation; both include ordinary execution, supplied in-memory file I/O, golden
checking and completed result cleanup according to the preserved driver. Allocation
is a separate 192-warmup, nine-by-32 complete-Run diagnostic, not timing. No
forced-GC CPU performance claim is made. Initial timing/allocation services finished
in 189.386/48.130 seconds; refined services 133.369/31.031 seconds. All exited zero,
were inactive, joined workers, and left the shared VM lease free.

Baseline core SHA256:
547825f9dfe52ddf22e24e76b8bc54ae8975428dae5b5e22052dd223624d2c42.
Initial candidate: 7bdf2edfe9669886ec5697daa6ce3968a6329f909abac6677e96bd5ba1852890.
Refinement: cf926259c79f4ee2084d6e509d102c60e752ed1cfc83fd8caef3f9b8cc419be1.
Identity manifests and raw archives preserve full inputs, goldens and runtime IDs.
Compressed source snapshots are evidence, not retained alternate backends.

Focused Release JSON/admission/shared-budget/managed-stack checks passed 179 internal
and 822 public cases on each actual .NET 11 and .NET 10 target, before and after
restoring the original runtime with fresh nonincremental builds (zero warnings).
Four new sync/async x object/pairs hook cases exercise actual host I/O, reentrant
loads, retained container aliases, duplicate keys and sequential budget reuse;
isolated CPython 3.13.2 goldens agree. Existing continuation tests retain deeper
nesting, concurrent shared-budget executions, cancellation, refusal and funding
through awaited cleanup. No assertions were weakened. The original initial
nullable compiler diagnostics and all corrected/restored logs are preserved.

Full Windows/Ubuntu, stress and installed-package qualification remains the cached
41540517622 result at exact fd7bc7e4; this withdrawn trial does not require repeating
unchanged runtime qualification. Publication remains postponed.
