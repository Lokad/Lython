# Sum acquisition and async cursor, 2026-10-18 UTC

`sum` now acquires its Python iterator before rejecting a string/bytes start,
without pulling an item. The baseline rejected the start too early, skipping
guest effects and masking acquisition failures. Argument count/start selection
and start-type validation are now separate phases. Synchronous sum keeps its
existing sequence acquisition; general async sum drives the existing cursor
directly instead of two forwarding objects. Its validation is inside awaited
cleanup. No core cursor, arithmetic, Any/All or ownership infrastructure changes.

Baseline: `74804edcc9cf4f5b382470bdc9dd8b3f358dd5ca`; candidate: that revision
plus the [one-file patch](candidate.patch). [Eight CPython programs](goldens.json)
confirm acquisition effects/error precedence, no premature next(), empty-start
identity, iterator resumption after errors, mutation and addition dispatch.
Of 18 initial public cases, 10 pass and 8 fail on the baseline; all pass after
the correction. Two further admission cases confirm an acquisition refusal keeps
its original structured reason instead of becoming a start TypeError.
[SPEC](../../../SPEC.md) documents the behavior change.

| Native warm async sum | Baseline allocation | Candidate allocation | Saved per call |
| --- | ---: | ---: | ---: |
| Empty list | 576 B | 480 B | 96 B |
| 65-item list | 2,656 B | 2,560 B | 96 B |
| Empty tuple | 536 B | 440 B | 96 B |
| 65-item tuple | 2,648 B | 2,552 B | 96 B |
| Range | 4,808 B | 4,712 B | 96 B |
| Generator | 3,880 B | 3,784 B | 96 B |
| Custom addition | 38,536 B | 38,440 B | 96 B |
| Exact bytes control | 384 B | 384 B | 0 B |

All synchronous allocation arrays are unchanged, as are exact-byte controls.
All native step arrays and 126 valid-start/result/error/span/fuel records match.
Invalid-start acquisition order is an intentional correctness delta, qualified
separately; it is not included in a blanket behavioral-equivalence claim.
One frozen pair uses two warmups, eight samples and eight invocations per sample.
All allocation arrays are stable. No recapture or post-gate production tuning.
Frames, temporary storage and the shared allowance return to baseline/zero.

This is an **allocation-only diagnostic**. Elapsed was not measured. Compilation,
context/input setup, scheduling, projection, process startup and host I/O are
excluded. Pure async values complete on the measured thread. The catalog's helper
comprehension jobs call sum(list); file-manifest exact-byte sums are controls.
No latency, compile-to-result, CPython or whole-job gain is claimed.
[Environment](environment.json), [scope](measurement-scope.json) and
[source screen](source-screen.json) state the boundary and original proposal.
The [ordering finding](ordering-bug.json) supersedes that proposal's incorrect
assumption that start rejection should precede acquisition.

All 328 distinct focused Release checks pass, including 58 complete CSV/text/JSON/
file jobs. Eight native paused-cleanup cases pass before/after; a ninth confirms
the first governor refusal remains stored through a cleanup failure. Tests cover
exhaustion, pull/add failures, cancellation, refused admission, cleanup errors,
once-only awaited disposal/funding, nested sum/accumulator reentry, actual add and
iterator I/O/cancellation, retained aliases/results, caches and managed stack.
The extra native case initially tested public error selection at an internal
executor boundary; its corrected assertion checks the stored refusal and raw
cleanup error. Public command-level terminal-refusal tests remain unchanged.
Original failed receipt is preserved privately; no production fix is claimed for
that fixture correction. Original Stress scales/assertions/timeouts remain.

[Diagnostic](diagnostic.cs), [raw allocation/work baseline](baseline.trx),
[candidate](candidate.trx), [baseline compatibility failures](baseline-compatibility.trx),
[candidate compatibility](candidate-compatibility.trx), [admission](candidate-admission.trx),
[baseline cleanup](baseline-cleanup.trx), [comparison](comparison.json) and
[validation](validation.json) preserve the evidence. Temporary diagnostic removed.
Measured production source is final. Release/platform qualification lives in PLAN.
Stop at sum acquisition/cursor consumption; regex remains planning-only.
