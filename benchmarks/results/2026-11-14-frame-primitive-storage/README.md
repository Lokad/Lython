# Ordinary-frame primitive transport: withdrawn

2026-11-14 UTC, R360. Initial frozen `b9ad2490` versus fully qualified
`98acaa5b` / frozen `a4bfbfd8`; 17 changed production inputs / 583 total.
**Withdrawn after a profile-backed refinement.** No frame primitive storage remains
in production. The baseline matrix-helper correction and public lifetime cases
are retained. Publication remains held. Initial observations below are preserved.

The prototype extends private machine values through the existing frame owner,
keeping final object-array binding and direct call spans. One primitive array is
admitted with frame capacity before allocation and remains funded through users
and cleanup. Ordinary frames can keep this owned state across suspension; native
borrowed spans still materialize before transfer. Stable immutable identities are
cached before alias copies, argument spans, collections, captured/mirrored stores
and projection. Existing publication rechecks namespace policy after cleanup.
No private heap cells, pool, hotness policy, tracing or public numeric type.

The existing segment scan now computes inline/storage facts, removing a separate
pass. Shared primitive reads/arithmetic/materialization replace duplicate native
handling and the object-only chain wrapper. This reduces allocation but introduces
new transport costs; allocation alone does not establish CPU improvement.

## Initial complete-script result

Quiet Azure F4as_v7, Ubuntu 24.04.4, EPYC 9V45, .NET 11 RC3, normal GC/tiering,
unchanged catalog/workers/auxiliaries. Four fresh processes/job, 5-second
full-request warmups (12 seconds for captured text), five rotating 0.5-second
batches. B737 completes all eight jobs in 305.404 seconds with every image,
stdout/file, budget, quiet, joined-user and archive gate. No users/fixtures remain.
Raw SHA256 `638ff280e25bcc2f2c08e421d1d765e48ff3dd62149425e4b6b1efd478d8c295`.

| Complete script | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| 100,000-add inside-range counter | −0.17..+2.57% | −0.85..+1.67% |
| 4,096-row CSV while validator, warm | +10.34..+11.35% | +10.31% |
| Same CSV, compile-to-result | +5.82..+7.38% | +6.32..+6.34% |
| CSV fulfillment | +1.19..+4.95% | −0.23..+3.24% |
| Captured text | −1.96..+8.40% | −3.01..+8.31% |
| JSON helper summary | +6.61..+7.16% | +5.84..+7.35% |
| Short/zero helpers | −11.77..+10.68% | −12.01..+12.86% |
| Helpers exposing every intermediate | +10.86..+12.05% | +11.78..+13.00% |

All process-median pairings, not confidence intervals. Short-helper process
spreads (11.91% candidate / 10.51% reference) and captured-text spreads
(6.09% / 3.84%) are uncertainty. They do not erase stable adverse CSV/JSON/observed
directions. Counter elapsed is near flat despite 57.23% less allocation
(7,518,762 to 3,216,018 B/run). CSV allocation falls a further 4.31% relative to
A4 (approximately 3,034,435 to 2,903,596 B/run). Four separate allocation processes
reverse role order and preserve all budget/golden checks. No CPU inference.

Warm clocks include mediated RAM-file I/O, full validation and result cleanup;
exclude compile/startup, fixture reset/protocol and final cache disposal. Fresh
adds compilation/compiled-owner disposal; wider CPU includes reset/protocol.
The CSVwhile source is the identical diagnostic algorithm in both runtimes,
not a runtime gain against the different maintained for-loop algorithm. No new
CPython ratio, startup, pending-I/O, contention, heap-peak or tail-latency claim.

## One owning profile and refinement

B738 profiles that same frozen CSV source: one 5-second, 199 Hz cpu-clock/fp
capture after 8 guest seconds of warmup, with same-process JIT map/disassembly.
977 guest leaves, 60 unknown, zero ambiguous. Native RunCore owns 302 leaves;
the new ordinary wrapper owns 20. These include useful/inlined work and do not
quantify removable shares. Diagnostic JIT/perf overrides preclude a timing claim.
Every image/golden/budget/quiet/join/archive gate passes; all RAM traces are
SHA-verified persistently before unmount. Raw SHA256
`868f4cb8d1628a44a4d5698c97d11efc85eab942d6901cdbfc146d73200cc905`.

The unique 1,969-byte Tier1 primitive helper contains an unconditional checked
reference assignment for its `out object` result before actual publication. One
bounded refinement removes that handoff: return only primitive data and let the
existing opcode determine boolean/integer publication. No new packet, type, owner,
reader-order special case or configuration. The profile does not predict magnitude;
retain all original adverse data and confirm affected complete scripts/controls
before retention or withdrawal. No automatic unchanged retiming or fixed veto.

## Correctness and fixture evidence

12,890 focused checks across actual .NET 11/10 and 46 isolated CPython 3.13.2
controls pass, including aliases, overflow, scopes/closures/generators/patterns,
real asynchronous I/O/cancellation, retained results, terminal refusal, managed
stack/recursion and shared-budget reuse. Four new lifetime cases pass per runtime,
including 10,000 protected helpers with one immutable input in 512 KiB.

The initial repeated-range fixture exhausts 512 KiB in both frozen baseline and
prototype: conservative guest-object retention until command end is unchanged.
Reusing one input distinguishes relinquished working capacity without requiring
arbitrary reference reclamation. Original source/failure and both-role controls
survive. A refusal fixture initially denied any growth after a closure write, so
new frame capacity legitimately refused before its intended callback; it now
targets callback entry while preserving earlier effects, exactly one callback,
first refusal and all cleanup assertions. Constructor/nullability build failures
and original failed conditions also survive; no timeout/semantic assertion relaxed.

Independent baseline A4 checks confirm that `@=` in a simple native helper
incorrectly reports RuntimeError. The prototype also initially misroutes module
`@=`. Both now use the existing Python matrix path; new helper `@`/`@=` cases
match CPython under both APIs. Preserve this fix independently if the broader
prototype is withdrawn. The qualified A4 delivery is separate and cached.

[Initial timing](initial/timing-summary.json), [allocation](initial/allocation-summary.json),
[identity](initial/candidate-identity.json), [checks](initial/focused-checks.json),
[lifetimes](initial/lifetime-checks.json), [profile](initial/profile-attribution.json),
[all sources/binary/raw/maps/fixtures/failures](initial/sources-checks-and-observations.tar.xz).
The wider representative performance objective and independent regex track remain active.

## Refined comparison and decision

Frozen `b8687b28` removes only the out-object primitive handoff, compared with the
same qualified A4 reference, settings and measurement boundaries. B739 completes
all six jobs in 207.688 seconds, with all image/golden/budget/quiet/join/archive
gates. No VM users or RAM fixtures remain. Raw SHA256
`7d50a17337083dc9050d7ba6993966cbea033a431174d3bdab690947909ac9f7`.

| Complete script | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| 100,000-add inside-range counter | −0.77..+0.49% | −1.15..+2.00% |
| 4,096-row CSV while validator, warm | +9.28..+9.84% | +9.18..+11.46% |
| Same CSV, compile-to-result | +6.38..+12.08% | +6.75..+12.22% |
| JSON helper summary | +1.79..+8.17% | +2.96..+8.27% |
| Short/zero helpers | +4.86..+10.72% | +5.05..+12.47% |
| Helpers exposing every intermediate | +8.86..+12.33% | +7.96..+10.26% |

All process-median pairings. JSON reference spread 4.30% and short-helper candidate
spread 5.40% limit magnitude precision; the stable CSV and observed-helper adverse
directions remain. Counter allocation is still 57.23% lower and CSV allocation
4.31% lower. Neither establishes a CPU benefit. A fresh controlled collection of a
changed design is valid; comparing B737/B739 medians does not isolate a causal
effect of the handoff removal.

Withdraw the integrated representation migration: its added array, branch and
publication state do not earn their CPU/regression cost. This is an evidence-based
scope decision, not a universal ban on unboxed values or a fixed percentage veto.
Remove the shared prototype kernel and all frame scalar fields/array accounting;
restore the qualified native/ordinary representations and facts. Keep the
independent two-line matrix-routing correction, its Python-shaped error cases,
four public lifetime cases, and callback-entry refusal targeting. Restore the two
internal constructors. No dormant backend or configuration survives.

All 582 production source bytes restore before applying only that matrix guard.
Forced nonincremental Release builds and 330 focused checks pass across actual
.NET 11/10 (95 internal +70 public per target), including real suspended I/O,
cancellation, terminal refusal, aliases and budget reuse. Existing full A4
qualification stays cached; the small correction's full delivery is deferred to
the next coherent checkpoint. A withdrawal script's CRLF assumption failed after
restoration; it was corrected before any build or measurement, and is preserved.

[Refined timing](handoff/timing-summary.json), [allocation](handoff/allocation-summary.json),
[identity](handoff/candidate-identity.json), [checks](handoff/focused-checks.json),
[sources/binary/raw](handoff/sources-checks-and-observations.tar.xz),
[restoration proof and checks](withdrawn/checks-and-source-proof.tar.xz).
