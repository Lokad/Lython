# Native dictionary integer storage — 2026-11-11

Retain the refined source pair against fully qualified `6904093e`. Private native
dictionary entries keep exact counting results as managed integers until a Python
read materializes one immutable cached `BigInteger`. This removes intermediate
arithmetic boxes. Copies materialize before exposing aliases; shared CSV/JSON
records keep their adopted object arrays and read those arrays directly.

No new execution route, scalar side table, pool, owner or public API. Key entries
grow 32→40 bytes; capacity, snapshots and replacement overlap admit the difference.
Existing value-slot headroom funds eventual boxes. Tighter allowances may refuse
earlier. Wide integers, protocols and required growth retain ordinary execution
before effects; terminal refusal and cancellation remain authoritative.

## Results and decision

Complete-script CLR allocation falls **14.90–14.91% captured text, 16.12–16.13%
ordinary text, 3.13% CSV and 4.20% JSON-lines**, on both APIs. Report/search and
compilation allocation are effectively flat. These are separate from earlier
native helper-return reductions; percentages are not additive.

| Refined quiet workload | Elapsed change | Wider process CPU change | Limit |
| --- | ---: | ---: | --- |
| Captured text / Run | −9.06..−3.81% | −9.78..−4.35% | Reference control 5.73%; candidate 0.04% |
| Ordinary text / Run | −8.66..−2.62% | −9.24..−0.89% | Controls 1.90/4.43% |
| CSV validation / Run | −3.28..+1.17% | −2.94..0% | Inconclusive; reference control 4.32% |
| JSON-to-Markdown / RunAsync | −3.21..+2.29% | +1.91..+6.38% | Possible CPU regression debt; elapsed mixed |
| JSON-lines / Run | −2.12..−0.23% | −2.27..−2.27% | Small favorable direction; max IQR 3.26% |

Ranges are all four independent process-median pairings, not confidence intervals.
Negative means faster. CPU includes protocol/reset work outside warm timing.
Retain the substantial allocation/representation benefit and directional text
improvement; **accept report CPU uncertainty/regression debt at this milestone**.
Reassess it at the next coherent checkpoint before publication. No universal CPU
gain, neutrality, pending-I/O latency, RSS or embedding-tail-latency claim.

The initial form's quiet captured RunAsync elapsed improves 4.46–5.09%, with
supporting wider CPU. That does not qualify refined asynchronous text performance.
Initial desktop reportAsync is +17.53..+20.70% with a 2.63% unchanged control;
the quiet adverse elapsed result does not recur, cause unresolved. Refined desktop
captured sync is +4.62..+5.63% with a 0.96% control; JSON sync is +6.62..+13.63%
with a 6.57% control. These observations remain alongside all favorable results.

## Source-backed refinement and validation

The initial getter inlined an unused allocation branch into shared-record lookup:
actual desktop Tier1 code grew 422→562 bytes and local stack 48→64 bytes. Reading
the already materialized record array directly removes that branch; refined code
is 387 bytes. This is structural evidence, not attribution of any elapsed change.
Both forms, exact sources, cores and generated listings survive.

Initial focused Release checks total 3,566 across actual .NET 11/10; refined checks
total **5,092**, including broader JSON coverage. Four independent CPython 3.13.2
correctness goldens pass. Regressions cover copies/repeated-read identity, retained
old aliases, deletion/order/growth/clear/pop/setdefault, shared-record isolation,
signed bounds and exact promotion, protocol continuation, first refusal before
growth/publication, retained results, shared allowance reuse and genuine pending
I/O cancellation with joined cleanup. Full platform/stress/installed-consumer
qualification is a separate delivery step; focused checks do not establish it.

Existing Windows complete-job tooling uses the same fixed worker/adapter, three
isolated core images, 256 warmups and five rotating rounds of sixteen jobs. All
six scripts pass full outputs, balances and fixture cleanup, on both APIs with
compilation separate. Reset, mediated I/O, validation and disposal are included.
Desktop clocks remain diagnostic; raw controls and compilation results survive.

B588/B589 use the existing F4as_v7 Ubuntu quiet VM, .NET 11 RC3 and fixed `fec79`
worker. Five-second warmups and seven rotated 150 ms rounds are unchanged.
Six initial jobs finish in 168.715 s; five refined jobs in 141.342 s. Warm includes
mediated RAM I/O, complete validation and prior-result cleanup; compilation,
startup, reset, protocol and final cache teardown are excluded. No new CPython
ratios or desktop/VM clock merging.

Both collections pass identity, output, ownership, quiet and joined-user/RAM
cleanup gates. The initial service exits 1 **after** all observations/cleanup:
its archival assertion still expected five jobs. Raw observations remained in
persistent storage. A fresh exclusive-lease recovery corrects only the six-job
assertion, retains the original helper/journal/failed service status, and verifies
the archive without rerunning timings or losing RAM evidence. Refined collection
exits 0 and archives under its owned lease. Both are terminal/cached.

Staging refusals are preserved. Disk recovery uses fresh path/UID/inode/all-alias
proofs against exact canonical committed Git evidence, protecting current images;
no sole evidence is deleted. Refined preparation also preserves its original
refused inventory involving an absent payload, then selects actual duplicate
archives with canonical proofs.

[Decision](decision.json), [archive hashes](archive-identity.json),
[initial assessment](initial-assessment.json),
[initial raw observations](initial/quiet/observations.tgz),
[refined summary](refined/quiet/summary.json) and
[refined raw observations](refined/quiet/observations.tgz) preserve both source
pairs and all adverse conditions. Each raw VM archive contains its exact net11
`current-core.dll`; net10 candidates are compressed separately. Qualified baseline
images are [referenced canonically](reference-images.json), avoiding duplicate
bundles. External comparisons remain source-labelled in
[BENCHMARKS](../../../docs/BENCHMARKS.md). Publication is postponed.

First full delivery attempt `43081976930` is terminal **failed**: Ubuntu actual
.NET 11/10, package consumers and original stress pass; Windows .NET 11 passes,
but a five-second async file-callback test times out on .NET 10. Windows package/
stress steps are skipped. The [receipt](delivery-first-attempt.json) and
[original job log](windows-net10-callback-failure.log.gz) preserve that failure.
The same32focused checks pass before correction on local .NET 10. The fixture
moves to the existing nonparallel RuntimeStress collection, retaining its actual
suspension/cancellation/cleanup assertions and unchanged five-second deadlines;
32checks pass on each target. Scheduling interference remains a hypothesis.
Corrected full delivery is pending; no runtime change or performance remeasurement
is implied by this test-scheduling correction.

Subsequent corrected delivery `43087005596`, source `458be840`, is **GREEN** on
Windows/Ubuntu, actual .NET 11/10, original stress and installed Release consumers.
Its production source is exactly retained `8f8628a4`; the correction changes test
scheduling, keeping five-second deadlines and every assertion. The
[terminal receipt](delivery-ci-status.json) is cached. Original failure and
collection-time status remain preserved. This does not qualify later prototypes
or resolve the report CPU debt.
