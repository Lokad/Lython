# One borrowed callable entry

R151/B506,2026-11-09. Qualifiedafb11d97 production/core6417B97A (doc-only baseline
c89d6914) versus the measured source freeze21CF1118 in [identity](identity.json).
Common frozen worker/optional adapter; same source/input/full golden per pair.

Extend the existing internal callable contract with borrowed short arguments.
Functions, bound methods, builtin and JSON binders consume inputs before returning
pending work. Other callables receive invocation-private arrays, cleared after their
users finish. Constructors/wide calls retain owned packets through actual cleanup.
Remove three executable-call thread-local pools and their rent/return/clearing
lifecycle, two callback type switches, and completed general/JSON async forwarding.
Restore the builtin binder hierarchy's private visibility. No new interface, dispatch
framework, pool, option, dependency or public API. Six pool-sentinel fixture regions
are gone; their independent semantic/lifetime assertions remain.

| Complete workload | Warm allocation | Quiet elapsed | Interpretation |
| --- | --- | --- | --- |
| Maintained JSON report, sync | Unchanged | −1.0–1.8% | Small observed improvement; coarse CPU −2.4–0%. |
| Maintained JSON report, async API | Unchanged | −5.7–10.6% | Supporting CPU −5.0–12.2%; baseline process spread3.7%. |
| Maintained text control, sync | Unchanged | −0.4–+8.6% | Unresolved; baseline/current process spreads3.8/5.0%. |
| Map-heavy text control, sync | Unchanged | −2.8–+5.4% | Unresolved; current process spread6.8%, coarse CPU mixed. |
| Constructor-heavy CSV control, sync | +2.5% | −0.9–+0.7% | Roughly neutral elapsed; explicit owned-array allocation cost. |

Compile allocation unchanged across all five catalog cases. Desktop ordinary CSV
allocation/timing is also retained; it has no additional quiet job. Original desktop
JSON async adversity and controls varying up to7.4% remain. Quiet B506: five jobs
141.919s, four independent processes each, five-second warmups/seven rotated150ms
rounds, normal GC/tiering. Existing warm-async protocol lane, no worker/framework
change. Included warm clock: mediated RAM-file I/O, full output checks and prior-
result cleanup; excludes startup, compilation, reset/protocol and final teardown.
External process CPU is separate and coarse. Async host calls complete immediately
in this comparison; no pending-I/O latency, universal or CPython speedup claim.

Retain the coherent boundary/pool deletion and measured JSON async benefit. Accept
constructor allocation+2.5% as the cost of explicit ownership without another pool;
do not broaden this into constructor redesign. Text elapsed/CPU uncertainty remains
regression debt for the next substantial whole-script checkpoint, not evidence of
neutrality. No unchanged second campaign or fixed-percentage veto.

706 internal/1614 public focused Release checks pass per actual.NET11/10. Existing
packet-release tests first caught five real lifetime failures; code now clears
fallback packets through pending completion/failure/cancellation, preserving the
original assertions. Receiver/order/default/variadic/invalid-call/constructor,
first-refusal, retained aliases, pending cleanup and budget-reuse coverage remains.
Five independent CPython stdout/file goldens validate the three maintained scripts
and explicit map/constructor shapes. Full platform/Stress/installed consumers batch
at delivery; focused checks do not claim that qualification is complete.

Initial VM setup refused for4.6MB of headroom before any service/root/fixture existed.
Under a fresh idle lease, verify exact hashes, single aliases, ownership and parents,
persist the pre-mutation plan, then remove only two redundant remote compressed
archives already tracked byte-for-byte locally; their raw trees/receipts remain.
Retry the identical staged source/payload with unchanged floors/limits. No timed work
restarted. All identity/output/quiet/join/RAM/archive/localSHA gates pass; terminal
cached/no users or fixtures. Original source-assumption/build/lifetime/headroom
failures are retained. Publication postponed; broader performance work remains open.

[Quiet summary](summary.json), [desktop allocation/adverse controls](local-summary.json),
[source/validation status](delivery-source.json), [independent goldens](script-goldens.json),
[space recovery](space-recovery.json), [terminal receipt](terminal.json),
[raw observations](observations.tgz), [original conditions/source/logs](preparation.tar.xz).
