# Checked native dictionary-update boundaries

2026-11-13. Retain frozen798edc1d against fully qualifiedR330/frozen965c7e0f
as a simplification, with no demonstrated CPU or allocated-byte improvement.
The broader complete-script performance objective remains open.

Native counting updates check their entry once, then complete one finite update
whose guards exclude admission and guest callbacks. Three internal polls and
two span arguments used only by those polls disappear. The fused loop drops
duplicate polls for private binding/deferred arithmetic. Managed stack admission,
long hash/compare checks and checks after observable binding/retirement and before
the next word's allocation remain. First refusal, original executable continuation,
exact promotion and finally-publication remain. Net production deletion is3lines;
no counter, mode, cache, policy, value representation or public API is added.
Cancellation can be observed at another native boundary; exact opcode/poll
coordinates are not promised. Completed effects are not transactionally rolled back.

53internal/179public focused Release cases pass per actual.NET11/10. They cover
native/cased loops, mirrored/private stores, promotion, immutable counter aliases,
required growth, first refusal through suppression/finally returns, reentrant
refund publication, genuine protocol suspension and cancellation. Only two
white-box helper entry calls lose their obsolete span arguments; assertions stay.
Full delivery passes[43520131516](delivery/README.md): both platforms/actual11/10,
original stress and installed consumers;578inputs/16summaries cached. Publication held.

## One bounded complete-script comparison

B702, quietF4as_v7 Ubuntu VM, fixed B685workeracda3ef3/optional0.4adapter, ordinary
.NET11RC3 workstation GC/tiering. Exact same text/search scripts, input/output bytes
and command-v2 clocks. Two fresh processes per source/job,12-second full-request
wall warmup budgets, five rotating one-second wall-sized batches. Guest clocks
include mediated RAM-file I/O, full stdout/file validation and prior-result cleanup;
exclude compilation/startup, fixture reset/protocol and final cache teardown.
Wider worker CPU includes reset/protocol. Ranges pair process medians, not intervals.

| Complete script | Reference elapsed | Candidate elapsed | Elapsed change | Wider CPU change |
| --- | ---: | ---: | ---: | ---: |
| Captured text /3,072lines,35,840B |1.411–1.437ms|1.411–1.420ms|−1.83..+0.64%|−1.94..+1.18%|
| File search /2,049lines,42,010B |253.5–268.8µs|258.5–262.7µs|−3.83..+3.62%|−4.00..+2.99%|

Text process spreads0.63%candidate/1.87%reference keep the small difference
inconclusive. Search reference spread6.06%/candidate1.57%is explicit uncertainty,
not a failed design or proof of harmlessness. All identity/golden/quiet/join/archive
gates pass in150.915s; actual users join and fixtures are removed/unmounted, with
observations kept persistent and the resulting archive SHA-verified. No new
CPython timing, allocation, live-heap or embedding-tail comparison is made.

Retain the coherent boundary/span cleanup without a speedup claim. Accept small
unresolved CPU debt and moving search controls; stop unchanged retiming at this
checkpoint. R325's23.65%dictionary owning-frame share includes useful work, not
predicted removable CPU. This experiment does not complete the performance goal.

The read-only staging preflight found the old B683working catalog already absent;
no files changed or collection launched. It then rejected an unlisted B686hard
link to the historical5aruntime. Both conditions remain. Exact committed archives
prove old5a/c16bytes and all aliases before retirement under a fresh idle lease.
Current965/new798images and original canonical evidence remain; VM floors are kept.

[Source/check evidence](source-checks.tar.xz), [raw observations](observations.tgz),
[summary](summary.json) and [terminal receipt](terminal-status.json) retain exact
578source inputs, all worker/adapter hashes, commands and failed conditions.
