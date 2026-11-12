# Shared positional fill — withdrawn with R268

This bounded refinement follows the paired native-search profiles. The existing
positional fill is shared between admitted arrays and transient managed slots;
ordinary eligible arguments no longer pass through general CallArgumentValue
packaging. The single prefix/live continuation design otherwise stays unchanged.
Frozen core a90a2e versus qualified da9/a552. All 569 source hashes are recorded.

Release nonincremental solution build has zero warnings/errors. 1,366 focused
checks pass on actual .NET 11/10 (226 internal +457 public per target). Six complete
scripts pass both public APIs and separate compilation, independent stdout/file
goldens, live allowance and joined cleanup. Warm allocation remains essentially
flat; compilation allocation remains −0.14..−1.34%. This is no allocation gain over
the preceding prototype. Desktop native Run −4.04..−0.42% has 3.76% control drift;
CSV Run +11.20..+12.47% and report Run +7.97..+9.82% remain preserved, as do mixed
async and compilation results. No desktop CPU improvement claim.

B620 finishes four complete jobs in 129.519 seconds, normal GC/tiering, unchanged
worker/adapter/runtime. Five-second per-process warmup, seven rotating 250ms
rounds, two reference and two candidate processes. All hashes/goldens/balances,
quiet gates, joining, RAM removal, archival and independent local SHA pass.

| Complete script / API | Elapsed change | Wider process CPU change |
| --- | --- | --- |
| Native search / Run | +22.08..+25.79% | +19.22..+23.54% |
| Guarded search / RunAsync | +16.05..+20.08% | +11.18..+16.98% |
| CSV / Run | −3.39..+5.07% | −1.79..+3.70% |
| Report / Run | −3.06..−0.86% | −3.70..+3.85% |

Ranges span all four process-median pairings, not confidence intervals. Native
reference/candidate controls differ 0.79/2.23%; guarded 1.58/1.80%. CSV controls
move 4.06/4.32%, so no CSV gain/equivalence claim. Report elapsed improves modestly,
but CPU crosses zero. Warm clocks include invocation, mediated RAM I/O and worker
validation/prior-result cleanup; compilation/startup/reset/protocol/final cache
cleanup are excluded. Wider process CPU includes reset/protocol and shared JIT/GC.
No CPython, RSS, tail-latency or large-file claim follows.

**Decision: withdraw the whole R268 production migration**, including this
refinement. Strong native/search regressions survive stable controls despite the
binding correction. Removing the old proof/evaluator saves code but replaces its
prepared input traversal with recurring generic entry and instruction dispatch.
A defensible complete migration must eliminate that work, not restore a second
native evaluator under a span facade. This result is no permanent ban and no
fixed-percentage gate. Original/refined sources, all adverse results and initial
callback/golden/collector failures remain preserved. The independent public
partial-formatting I/O/cancellation/retained-result regression is kept.

Initial launch stopped **before any worker**: unpredicted directory/hardlink
metadata left staging 12 KiB below the existing floor. Under a fresh idle lease,
one 78 KiB exact committed B617 source-inventory duplicate was SHA/path/UID/inode/
link verified and retired. Floors stayed unchanged; staged images were rechecked
and the empty RAM fixture reused. No worker or timing was replayed. The original
launch/error and corrective proof are retained. The final unit is terminal/cached;
archive SHA e1f3e9a8e3f87840e5819715340c9ffba7f307c5fb64afc31a80c38c78b06207.
Superseded unpromoted a082 aliases were archived and retired with all-alias proof;
qualified a552 reference and new frozen a90 image remain protected.
