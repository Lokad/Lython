# One final-slot function binder

2026-11-13. Retain refined660a0338 against qualified557d18d4/c07279a2/frozen9d157bb2
as a source simplification, with no demonstrated CPU or allocation improvement.
The broader complete-script performance objective remains open.

The existing binder places flat positional operands into final storage directly,
then shares live-default completion with supplied keyword/variadic forms. Native
and owned entries use that same route. The independent positional binder and
framed-entry selection disappear; dynamic supplied-form parsing moves to a helper.
Net production deletion is21lines. No cache, arena, workspace, policy, forced-inline
attribute or execution representation is added. No public API changes. Existing
ownership, before-allocation admission, terminal refusal and real async transfer remain.

83internal/348public focused Release cases pass on each actual.NET11/10 target.
They preserve live/default replacement and aliases, bound receivers, argument/error
order, variadics, actual protocol suspension/cancellation, retained inputs, reentrant
refunds and budget reuse. Architecture-specific test entry calls switch to the
common binder; their independent assertions remain. Full delivery passes[43493784585](delivery/README.md): both platforms/targets,
original stress and installed consumers;576source inputs match.

## Bounded source-pair comparisons

Same complete text/search scripts and fixed B685 workers/optional0.4 adapters on
the quiet F4as_v7 VM; normal tiering/GC. Two fresh Lython processes per source,
12s full-request-wall warmup budgets and five rotating1s full-request-sized batches per job.
Warm guest clocks include mediated RAM-file I/O, complete stdout/file checking and
prior-result cleanup; exclude compile/startup, fixture reset, protocol and final
cache teardown. Wider process CPU includes reset/protocol. No new CPython
measurement, tail-latency, live-heap or allocation comparison is made.
The original prose mislabeled the warmups as guest seconds. The collectors use
wall budgets, with a possible final-batch overrun; [derived guest totals](warmup-boundaries.json)
are10.62–12.35seconds for B693/B694. Raw observations and timed medians are unchanged.

| Source / collection | Complete duration | Search elapsed change | Search wider CPU change | Text elapsed / wider CPU change |
| --- | ---: | ---: | ---: | --- |
| Firstb3b / B693 |150.467s|+1.01..+12.81%|+1.64..+10.94%|+0.31..+1.76% /−0.99..+2.32%|
| Refined660 / B694 |152.124s|−0.57..+1.05%|−1.44..+0.29%|−6.52..−0.09% /−6.75..+0.39%|

Ranges pair process medians within each collection; they are not confidence
intervals. First candidate search-process spread9.57% remains explicit. The
refinement's search spreads are1.40%candidate/.22%reference; text spreads3.75%/
2.79% and wider CPU crossing zero keep its apparent elapsed benefit diagnostic.
All identity, golden, quietness, join and archive gates pass; all actual users
finish; observations stay persistent while RAM fixtures are removed/unmounted,
then the resulting archive is SHA-verified. Historical
VM working copies are retired only after exact committed-archive verification;
current9d reference images remain. No original adverse evidence is discarded.

## Refinement reason and decision

Local Windows JIT diagnostics on the same frozen pairs observe that the first
larger binder stops inlining into native entry: reference entry5111bytes includes
it; first candidate713bytes calls a separate4453-byte binder. This is a reason to
investigate, not proof of the Linux regression's cause. Moving existing dynamic
supplied-form parsing out of common completion restores observed local inlining:
refined entry3979bytes versus5038reference in that diagnostic; common binder3388.
PGO/code sizes vary by process and do not predict CPU gains.

Retain one binding route with the smaller common completion. Search now spans a
small positive/negative difference rather than establishing a speedup; text is
inconclusive. Accept that uncertainty as explicit regression debt. Stop unchanged
retiming at this checkpoint and continue toward a larger demonstrated construction/
transport removal. The source gate is[separate](../2026-11-13-post-loan-cpu-attribution/README.md).
Source/raw/JIT observations for both forms survive. Publication remains postponed.
