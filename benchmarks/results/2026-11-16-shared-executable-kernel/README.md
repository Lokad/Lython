# Shared executable kernel: first coherent prototype

R439/B838,2026-11-16UTC. Isolated **91ca4a41** versus fully qualified
**312f5911 /0443b5cf**. The prototype is active and **not promoted**. Publication
remains held. This records a coherent simplification and adverse measurements,
not an allocation/CPU improvement or completion of the performance objective.

Hypothesis: one closed instruction loop over final managed slots can replace
native/persistent duplicate dispatch,publication/local-store/retirement handlers,
completed call selection and whole-function opcode eligibility. Both lifetimes
borrow the actual execution metadata; real protocols,suspension,return/finally
and exception routing keep the existing frame driver. No per-call heap controller,
copied PC synchronized after callbacks,arena,exception router or new policy.
The compiler opcode inventory and temporary migration gate are removed. Genuine
lexical/cell/class facts and first-cleanup admission boundary remain. Shared
storage preserves persistent clearing/queued async cleanup and bounded private
dead references; Locals still owns a real binding-loan return handle.

| Complete job | Candidate elapsed change | Wider process CPU change | Ref/candidate process-median spread |
| --- | ---: | ---: | ---: |
| Normalization warm | −2.63..+7.17% | −2.44..+5.08% | 7.82/2.09% |
| Normalization compile-to-result | −2.72..+2.16% | −2.16..+0.75% | 2.95/2.01% |
| Captured-helper text | +6.75..+8.63% | +4.00..+6.28% | 1.38/0.38% |
| Protected file search | +3.11..+5.44% | +2.96..+3.66% | 1.31/0.94% |
| CSV invoice | +3.84..+7.83% | +4.83..+6.82% | 0.08/3.75% |
| JSON helpers | +0.22..+3.32% | −1.96..+0.88% | 1.52/1.54% |

Each range is all four pairings of two independent processes per source.
Captured text/search show consistent adversity. Normalization is inconclusive
because its unchanged reference controls move7.82%; CSV candidate controls move
3.75%. JSON CPU includes mixed pairings. No causal mechanism follows from these
timings alone. Do not automatically promote,discard the whole design,or repeat
the unchanged comparison. Next perform one bounded source/CPU attribution of the
changed dispatch/call boundary,then refine only with a concrete eliminated-work
hypothesis. Preserve this source and all adverse originals. Explicitly decide
remaining regression debt at the coherent retention milestone.

Eight local full warm diagnostics (including async normalization,drain and ordinary
calls) pass all stdout/file goldens and shared-budget baseline cleanup. Allocation
changes are all below0.10% and accounted peaks are identical. This is flat allocation,
not a demonstrated reduction. Counters cover whole-process CLR allocation with
natural GC; no elapsed/CPU or physical heap/RSS claim follows.

Release calls/continuations2838internal/6402public pass eachactual.NET11/10.
After the final filename/nonincremental both-target build,focused81internal/45public
checks also pass per runtime. Earlier metadata,arithmetic,iteration,storage,
closure and value stages are preserved separately,not summed. The existing
cased-split ownership fixture adapts its private setup while retaining independent
funding/cancellation assertions.609inputs are frozen; source and candidate image
remain exact through comparison. Full platform,original stress and installed
package qualification is pending retention; unchanged qualified baseline is not
retested. No supported Python behavior or public API is intentionally removed.

The fixed catalog is identical for all processes. Quiet F4as_v7,Ubuntu24.04.4,
four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 normal tiering/workstation GC.
Warm clocks include mediated RAM I/O,stdout/file goldens and prior-result cleanup;
exclude compilation,startup,fixture reset/protocol and final cache teardown.
Fresh includes compilation and one execution with compiled-owner cleanup. Wider
process CPU includes reset/protocol. This is a Lython source pair; current external
CPython evidence remains [R437](../2026-11-16-representative-cpython-checkpoint/README.md).

All six jobs complete206.412s. Exact images/catalog,goldens,quiet before/after,
Closed/exit/join and persistent archive verification pass. All workers and RAM
fixtures are gone. B838 is terminal cached; never requery it. Only four exact
previously committed archived replicas are retired after fresh UID/path/hash/link/
all-alias verification: withdrawn e0core,B837archive/working observations and
B836working catalog. Canonical evidence,current baseline,toolchains and254/252MiB
floors remain. Profiling/retention is independent of unpublished regex capabilities.

[Raw observations](observations.tgz),[summary](summary.json),
[allocation](allocation-summary.json),[focused checks](focused-checks.json),
[frozen sources/checks](sources-and-checks.tar.xz),
[procedures/images/original conditions](procedure-and-image.tar.xz).

[Matched-source CPU attribution and bounded refinement](attribution/README.md)
are recorded separately; the first prototype remains unpromoted.
