# Range-iteration stage — bounded refinement

R362, 2026-11-14 UTC. Initial frozen `8056823e` against fully qualified
`3bc513b8` / frozen `ca1e2331`; three production inputs, 582 total.
**Uncommitted development prototype; no final retention/delivery claim.**

Replace generated range iteration with shared explicit state and native primitive
pulls through the existing cursor. Signed machine bounds use exact cardinality
and inline progression; wider values keep BigInteger and eager major-yield funding.
Ordinary pulls cache stable Current values; independent enumerators, errors/EOF,
early disposal and active cursor transfer remain. PyRange's CLR/Python interfaces
are unchanged. Native publication reuses already prepared integer working storage;
other shapes retain object publication. No compiler pass, unconditional working
array, frame value system, pool, owner, tracing or public option.

The [owning profile](../2026-11-14-range-iteration-cost/README.md) selects this
whole stage; its samples are not a speed forecast. 536 focused actual .NET 11/10
checks plus 2,298 generator/iterator/aggregate consumer checks pass; 90 isolated
CPython 3.13.2 controls and 18 frozen-worker goldens match. Exact machine/large/
negative bounds, aliases, reentry state, real I/O/cancellation, first refusal and
shared-budget cleanup preserve the existing ownership contracts.

## Initial complete-script comparison

Quiet F4as_v7 /Ubuntu 24.04.4 /four EPYC 9V45 /16 GiB/no swap /.NET 11 RC3,
normal GC/tiering, fixed worker/optional adapter 0.4. Four fresh processes/job,
5-second full-request warmups (12 seconds captured text), five rotating 0.5-second
batches. B744 finishes seven jobs in 267.436 seconds with every image, output/file,
budget, quiet, joined-user and verified persistent-archive gate. No users/fixtures
remain. Raw SHA256
`1c9cd9f208afcd34f3b1c8a991af6301feb8b7ff22af4522baa328134ec37084`.

| Complete script | Elapsed change | Wider CPU change |
| --- | ---: | ---: |
| 100,000-add counter, warm | −29.75..−28.49% | −30.70..−28.80% |
| Same counter, compile-to-result | −27.79..−27.42% | −26.20..−25.78% |
| 256 materializations of range(64), warm | −0.40..+1.72% | −1.01..+1.76% |
| 4,096-row CSV while validator, warm | −3.03..+1.26% | −2.88..+0.95% |
| Captured text, warm | +1.58..+7.94% | −0.64..+6.67% |
| Short/zero helpers, warm | −1.75..+1.54% | −3.20..+0.25% |
| Helpers exposing every intermediate, warm | +0.46..+2.01% | +1.21..+2.25% |

All process-median pairings, not confidence intervals. Text's 4.36% candidate
spread limits magnitude precision, but the adverse elapsed direction remains;
CSV candidate spread 3.04% is also uncertainty. Observed-helper small adverse
CPU remains. Counter warm is 2.002–2.009 versus 2.809–2.849 ms. No universal,
neutrality, current CPython ratio or cumulative/cross-collection gain claim.

Allocation (one separate fresh process per role): counter 3,215,210→15,192 B/run
(−99.53%); CSV effectively flat; materialization 977,198→989,510 B/run (+1.26%).
The small iterable adapter plus independent enumerator exposes acquisition cost.
Both runtimes use identical algorithms. Materialization is an explicit sensitivity
control, not a representative workload replacement or a newly invented framework.

Warm includes mediated RAM I/O where present, full stdout/file checking and result
cleanup; excludes compile/startup, fixture reset/protocol and final cache disposal.
Counter/materialization have no file I/O. Fresh adds compile/disposal; wider CPU
includes reset/protocol. Allocation reduction alone never establishes CPU gains.

## Selected bounded completion

Restore general cursor creation/object-pull code exactly, retaining only the new
standalone primitive pull API. Range state and the native working-storage invariant
are unchanged. Remove two extra general range probes instead of introducing more
router state. Initial text adversity justifies testing this coherent scope change;
source inspection does not establish its causal CPU share. Normal IPyIterable
acquisition adds 24 bytes per native range cursor; preserve that tradeoff.
Earlier tests/evidence remain, corrected-source focus and affected/control timing
are separate. No further unchanged replay is automatic and no permanent design ban.

Initial local archive-member preflight failed before remote work; corrected prefix
and original script survive. The launched owner was not restarted. Staging retires
only byte-proven committed superseded A4 aliases after ca1 full qualification and
exact historical raw-result copies. Current ca1/prototype, catalogs/auxiliaries/
toolchains and 254/252 MiB floors remain. Publication stays held; the broader
representative-script objective and independent regex development continue.

[Identity](initial/candidate-identity.json), [timing](initial/timing-summary.json),
[allocation](initial/allocation-summary.json), [focused checks](initial/focused-checks.json),
[consumer checks](initial/consumer-checks.json),
[sources/binary/raw/conditions](initial/sources-checks-and-observations.tar.xz).
