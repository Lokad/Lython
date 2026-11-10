# Receiver-aware native method entry — R192

Committed1a83c64f is fully qualified; e5731404/coree364 reference versus
frozen ac6edb6f aboveb57bfa47. The same16-slot managed destination and shared Span
binder now take the already-evaluated receiver separately for eligible ordinary
methods and bound aliases. This removes workspace rental/return and pending-call
transport, with no prepended self array, second binder, owner/cache/arena/engine or
public API. Native proof occurs before effects. Wide, variadic, protocol-default
and dynamic body cases retain ordinary execution before admission/mutation.

The existing complete R145 text script stores Words().from_line; source inspection
verifies its native body and receiver slot. Old workspace arrays were already
reused, so allocation is effectively flat, about−0.005..−0.007%target. Compilation
allocation is flat within0.02%. This is a work/transport removal, not a substantial
allocation result. Original desktop warm target−0.80..+1.07%, completed-I/O async
−1.19..+3.29%, compile+0.30..+9.97% and all adverse/moving controls are retained.

2026-11-10, F4as_v7/Ubuntu24.04.4, actual.NET11RC3, same frozen workers/adapter0.4/
dependencies, ordinary GC/tiering. Four independent processes/job,5swarmup/role,
seven rotated400msrounds. Same scripts/fixtures/goldens per source; no CPython lane.

| Complete warm script | Elapsed change | Wider process CPU | Status |
| --- | ---: | ---: | --- |
| Bound-method text helper | −8.51..−5.18% | −7.69..−5.98% | Target improvement |
| Ordinary text control | −4.85..+0.66% | −4.55..+0.42% | Inconclusive |
| Report control | −2.06..+0.93% | −2.33..+1.47% | Inconclusive/possible small debt |

Empirical all-pairing ranges, not confidence intervals. Reference/candidate target
spreads2.09/1.45%; ordinary-text reference spread3.46%, report candidate spread2.91%.
CPU includes wider reset/protocol and coarse ticks; no general CPU or neutrality
claim. Warm includes mediated RAM-file I/O, complete stdout/file validation and
prior-result cleanup; compile/startup/staging/reset/protocol/final caches excluded.
Pending I/O, RSS and embedding tail latency are not measured. Completed-I/O async
allocation/elapsed are desktop diagnostics, not a quiet async CPU comparison.

**Decision:** retain this completion of the existing receiver convention and the
targeted CPU benefit. Accept unresolved small control/desktop compilation timing
debt, with no new compiler/storage mechanism. Ordinary methods remain exposed as
real bound values; dynamic cases retain their original behavior. This milestone
does not complete the broader whole-script performance objective.

Focused206public/128internal pass per actual.NET11/10. Eight independentCPython3.13.2
goldens cover receiver/alias/default/live metadata, class/static methods, descriptors,
closures, wide/dynamic protocols and evaluation order. Retained results across two
shared-budget executions, active pure-loop cancellation, genuine suspended host
protocol cleanup, terminal admission despite except/finally and budget reuse pass.
Temporary architecture probe was removed. Main forced solution build and53focused
public checks peractualtarget pass ([record](qualification/main-focused.json)). Windows/Ubuntu, actual.NET11/10, original Stress and installed Release consumers
pass [cachedGREEN42792541280](qualification/delivery-ci.json). Source worktree
retirement preserves all six deltas. No full local ordinary replay or unchanged
historical qualification. Publication remains postponed.

All three quiet jobs complete110.836seconds, all identity/output/owner/quiet/join/
RAM removal/archive-under-lease/localSHA checks pass. TerminalB540 cached. Initial
generated-script null interpolation, unique-stage guard and unused Windows apphost/
debug-file inventory mismatch stopped before guest work. Correct runtime inventory
matches establishedB538/B539; originals/corrections survive. Exact canonical old26d
and raw-copy retirement preserves the newly-qualified e364/current baseline; no
active/current image or sole evidence was removed.

[Prospective contract](milestone.md), [actual source facts](source-gate.json),
[Python goldens](python-goldens.json), [all quiet results](summary.json),
[raw cores/source/checks/observations](observations.tgz), [source delta](source.patch),
[main equivalence proof](source-promotion-proof.json), [development/adversity/corrections](development.tar.xz),
[retirement proof](space-recovery.json). Normal optional-adapter pins unchanged.
