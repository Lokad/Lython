# Direct dictionary view calls, 2026-10-18 UTC

Immediate zero-argument `items()`, `keys()` and `values()` calls on exact
dictionaries reuse the existing receiver/operation call path. Exposed methods
and immediate calls share the original view factories and 64-byte admission.
Receiver-specific wrappers and generic call forwarding disappear for these calls;
the factories still construct live views. No new cache, dispatcher or owner.

Baseline: `c362a081db760136579a8c0a6867ac23c79982c6`; candidate: that revision
plus the [five-source-file patch](candidate.patch). Native warm
execution, 64 view calls per invocation, gives identical results for all three
methods:

| Receiver/call form | Baseline allocation | Candidate allocation | Status |
| --- | ---: | ---: | --- |
| Distinct dictionaries, sync | 13,112 B | 6,456 B | 6,656 B less |
| Distinct dictionaries, async | 13,600 B | 6,432 B | 7,168 B less |
| Repeated dictionary, sync | 6,560 B | 6,456 B | 104 B less; existing member cache already helps |
| Repeated dictionary, async | 13,600 B | 6,432 B | 7,168 B less; this path previously bypassed member caching |
| Exposed/expanded/custom controls | See comparison | Identical raw arrays | No allocation change |

One frozen pair uses two warmups, eight samples and eight invocations per sample.
All invocations return 64, native step arrays match, and 576 fuel/error/span
records match exactly. Frames, temporary reservations and the shared allowance
return to baseline/zero. One baseline async `items()` distinct-receiver sample
allocates 3,512 extra bytes per eight invocations; raw evidence is retained and
table values are medians. No recapture or tuning.

This is an **allocation-only diagnostic**. Elapsed time was not measured. It
excludes compilation, context/receiver setup, public projection, scheduling,
process startup and host I/O. Async values complete synchronously on the measured
thread. No CPython, latency, compile-to-result or representative-job speedup is
claimed. Final sorting calls in the comparison catalog establish usage, not
whole-job importance. [Environment](environment.json), [frozen scope](measurement-scope.json)
and [source screen](source-screen.json) state the boundaries.

All 360 distinct focused Release cases pass, including 58 complete CSV/text/JSON/
file jobs. The 31 new semantic/admission/lifetime cases also pass on the baseline;
10 independent [CPython programs](goldens.json) match. Coverage includes live
views after value updates or rebinding, iteration mutation, exposed/unbound/
expanded/dynamic calls, Counter/defaultdict controls, arity effects, refusal before
publication, shared budgets, retained results and real receiver I/O/cancellation.
Existing terminal-refusal, cache/callback ownership and managed stack tests remain.
Original Stress scales, assertions and timeouts are unchanged. No public memory
contract, guest behavior or regex implementation change.

[Raw baseline](baseline.trx), [raw candidate](candidate.trx), [diagnostic](diagnostic.cs),
[comparison](comparison.json) and [validation](validation.json) preserve the
evidence. The temporary diagnostic is removed; production source was not changed
after measurement. Full Windows/Ubuntu and explicit Release package qualification
are recorded in PLAN. Stop this call family at these three exact no-argument
methods; exposed and dynamic forms keep the general path.
