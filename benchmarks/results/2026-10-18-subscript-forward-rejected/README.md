# Rejected native subscript forwarding, 2026-10-18 UTC

The candidate missed its frozen gate and was removed. Current runtime/test source
is restored byte-for-byte to `cbe7fd1ca68f913005143ef024c617f77d3a277b`.
This is an experiment against an earlier Lython revision, not a CPython comparison
or a performance claim about a later HEAD.

The [one-file candidate](rejected-candidate.patch) returns native reads through a
completed ValueTask and forwards existing awaitable operations directly. Two
static local continuations retain dictionary-result handling and guest-index
coercion. No new dispatcher, binder, cache, owner or indexing kernel was proposed.

| Existing-context sample, 4,096 list reads | Baseline | Candidate | Status |
| --- | ---: | ---: | --- |
| Synchronous control median | 2.84510 ms | 2.90525 ms | 2.1% higher; within the 10% control limit |
| Asynchronous native median | 3.52750 ms | 3.46905 ms | Local diagnostic only |
| Async/sync ratio, candidate divided by baseline | — | 0.96307 | 3.7% lower; required at least 5%, rejected |
| Managed allocation per synchronous sample | 146,432 B | 146,432 B | All sample arrays identical |
| Managed allocation per asynchronous sample | 145,664 B | 145,664 B | All sample arrays identical |

Both paths read `[1]` list items inside the same precompiled loop, 128 reads/run,
32 runs/sample. There are eight warmup pairs and twelve measured pairs, alternating
API order. No host I/O occurs in the timing loop. Frontend compilation, context and
input setup, scheduling, public projection and process startup are excluded.
The [source/acceptance pin](scope.json) was written before both captures; the
[diagnostic](diagnostic.cs), [baseline raw results](baseline.trx),
[candidate raw results](candidate.trx) and [comparison](comparison.json) preserve
the sole pair of captures. No retiming or threshold adjustment followed.

Machine: local Windows x64 build 26200, Intel Core i7-14700KF, 28 logical processors;
.NET SDK 10.0.300-preview.0.26177.108 and runtime 10.0.13. The unchanged machine
configuration captured earlier that morning is retained in [environment](environment.json).
This box was not qualified quiet. The raw medians establish neither a reliable
elapsed improvement nor a whole-job, compile-to-result, embedding-latency or CPython
ratio. There is no managed-allocation reduction.

All 320 exact native/guest-index/item result, identity, exception, message, span,
callback-count and fuel records agree. Those diagnostic callbacks are synthetic,
completed operations, not measurements of pending asynchronous I/O.
After restoration and removal of the temporary diagnostic, 147 focused Release
indexing, mutation, protocol, admission/ownership and managed-stack checks pass;
they include existing controlled pending-callback lifetime checks.
See [validation](validation.json) and [restored raw results](restored-white.trx).
Original stress scales, assertions and timeouts are unchanged.

Close this native subscript candidate. Keep the existing helper; do not revive
or retime this candidate from these samples. Remaining plan work needs a different
demonstrated removal or correctness requirement.
