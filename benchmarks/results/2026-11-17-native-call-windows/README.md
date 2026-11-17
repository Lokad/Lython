# Native helper calls borrow admitted caller windows

R481/B895, 2026-11-17 UTC. **Withdrawn after corrected-source measurement; initial retention superseded.** Qualified
d8611c85/frozen0315de0a versus 0ebab036 on 1390c110, six production files.
Ordinary positional calls reuse evaluated arguments as callee locals/operands.
The NativeValues fixed stack buffer and NoInlining StartNativeCall wrapper are
removed. Pattern storage, temporary ownership and unpacked outputs select a
disjoint admitted tail; other arguments retain native binding. Existing eight-
prefix eligibility bounds a fixed 128-slot tail. No growable arena, saved
activation array, offset-owner view, registry, new pool or execution route.

Borrowed locals are initialized before entry and cleared after actual users and
cleanup. Genuine suspension copies live values into the existing owned frame
before releasing the caller window. All managed stack checks remain. An invocation
with calls admits 3072 estimated extra bytes before allocation; earlier denial is
possible. Physical allocation/accounted peak are explicit costs, not hidden gains.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference / candidate controls |
| --- | --- | ---: | ---: | ---: |
| Floating JSON report | Warm | **−4.64..−3.52%** | **−6.48..−4.57%** | 0.52 / 0.66% |
| Same | Compile and execute once | −2.17..+3.42% | −4.05..+3.96% | **5.27 / 0.42%** |
| Integer CSV invoice | Warm | −1.65..+2.35% | 0.00..+1.47% | 1.85 / 2.17% |
| Helper text | Warm | **+1.01..+5.78%** | **+0.80..+7.37%** | 0.59 / **4.12%** |
| Floating report | Compilation | −1.54..−0.43% | −1.38..−0.82% | 0.63 / 0.49% |

Natural-GC allocation increases 4096 B / 0.83% for floating, 2560 B / 0.061%
for CSV, and approximately 2678 B / 0.16% for text (minor diagnostic variation).
Accounted peaks increase 3072/3072/3584 B. Compiled balances are unchanged and all
shared allowances return to baseline. No allocation reduction or overall gain claim.

Each actual Windows .NET11/10 target passes 237 public and 272 internal focused
cases. Three new observable programs cover unassigned locals, nested/deep helpers,
live pattern guards and tuple returns; independent CPython3.13.2 agrees. Existing
tests preserve genuine I/O, cancellation, first refusal, retained aliases, defaults,
keyword protocols and awaited cleanup. Seven direct white-box calls now supply
independently funded caller windows; no production compatibility wrapper survives.
The cache-refusal fixture now admits its small working window and still refuses
the intended large cache with unchanged assertions. Original removed-wrapper build
failure, initial fixture failure and corrected arithmetic golden are preserved.
Both copied 32-file images pass actual three-case catalog goldens; candidate also
passes all 13 prior representative goldens. Full delivery is not claimed.

193.46 seconds, five jobs, quiet AzureF4as_v7/Ubuntu24.04.4/.NET11 RC3, normal GC/
tiering; six-second interleaved full-request warmups and five rotated sample rounds.
Warm clocks include Run, mediated RAM I/O, output/file goldens and previous-result
cleanup; exclude compilation/startup/reset/protocol/final cache disposal. Fresh
adds compilation and compiled-owner disposal. External CPU is wider. No CPython
ratio or embedding-tail/RSS/startup/async-I/O gain is inferred.

All image/golden/quiet/Closed/exit/join/archive checks pass; RAM removed and terminal
cached. Fresh path/UID/hash/link/all-alias proofs retire only 1287962 B of exact
committed older working evidence replicas, preserving current images/toolchains/
canonicals and 254/252 MiB floors. A local early-observation guard made no remote
request; the same collection was retrieved after its deadline. Publication held.

The numeric warm result supports the hypothesis; fresh and text remain unresolved
or adverse. One unchanged-image floating-fresh/text-warm confirmation with eight-
second warmups is justified. Do not erase the first text adversity or infer that
its moving candidate is the only cause. Retain/refine/withdraw remains explicit.

[Summary](summary.json), [allocation](allocation-summary.json),
[validation](validation.json), [independent programs](cpython-semantic-goldens.json),
[raw](first-pair/observations.tgz), [identity](identity.json),
[sources](candidate-sources.tar.xz), [prototype](prototype.patch.gz),
[core](candidate-core.dll.gz), [procedures and failures](procedures-and-checks.tar.xz).

## B896 confirmation and retention decision

Same sources/images/catalog/protocol, eight-second interleaved full-request warmups;
two unresolved jobs finish 96.83 seconds. No source tuning or forced collection.

| Complete script | Phase | Elapsed change | Wider CPU change | Reference / candidate controls |
| --- | --- | ---: | ---: | ---: |
| Floating report | Compile and execute once | **−3.55..+1.71%** | **−2.42..+3.51%** | **4.67 / 0.75%** |
| Helper text | Warm | **+0.27..+0.98%** | −1.07..+1.08% | 0.54 / 0.18% |

Retain the removed call storage and demonstrated first warm numeric CPU benefit.
Fresh remains uncertain with moving reference controls; do not claim it improved,
that a particular speedup is established across all scripts, or that an inconclusive
measurement disproves the design. Accept the small confirmed text elapsed debt and
explicit capacity/allocation increase. First larger text adversity is preserved and
unexplained. R471 integer/R460 async/tuple debts remain; no additive speedups.
No more unchanged timing is needed for this bounded retention decision. The broader
performance objective continues with a separate record-construction design gate.

All guards/goldens/Closed/exit/join/archive checks pass; RAM removed, terminal cached.
The first confirmation staging guard stopped before mutation/timing because disk
headroom missed its floor by 5006 bytes. The repaired proof retires only an additional
31309-byte exact committed obsolete bytecode replica; original failure retained.
Total retirement is 521399 bytes of exact committed first working evidence, with
fresh path/UID/hash/link/all-alias checks. Current images/toolchains/canonicals and
254/252 MiB floors are preserved. No observation timeout restarted any collection.

Six production files and their semantic/ownership test adaptations are promoted;
full Windows/Ubuntu, original stress and installed Release consumers remain pending.
No public API change; earlier supplied-budget denial from admitted capacity is the
intentional compatibility tradeoff. Publication and tagging remain held.

[Confirmation](confirmation-summary.json), [raw](confirmation/observations.tgz),
[decision](decision.json), [procedures and original guard](confirmation-procedures-and-checks.tar.xz).

## First delivery failure and correction

[CI44067803913 fails](delivery-first-failure/README.md): caught binding errors can
leave partial parameter values and poison later valid calls. The corrected code
clears the disjoint parameter window before binding and after failure. Existing
default-mutation/partial-binding cases reproduce and verify it; two CPython controls
agree. Tight-budget fixtures now reach their intended later refusal after admitting
the working window, preserving effects, aliases, terminal failure and cleanup.

Actual .NET11/10 focused corrections pass 124 public/88 internal cases each.
Corrected frozen core **27ed69a6** has a fresh nonincremental build, copied full
image, exact production snapshot and actual-worker three-case goldens/allocation
checks. Source before this correction remains fully preserved as historical
prototype evidence; its timing is not a measurement of the corrected HEAD.
Corrected-source comparison and a fresh combined delivery remain pending.

[Corrected identity](corrected/identity.json), [sources](corrected/candidate-sources.tar.xz),
[correction procedures and failures](delivery-correction-procedures.tar.xz).

## B897 corrected-source result: withdraw

Four jobs in 154.89 seconds against the same qualified d8611c85/0315de0a reference.
Corrected **abd3c788 / frozen27ed69a6** initializes/unwinds parameter storage after
binding errors. The catalog adds one diagnostic from the existing positional-loop
fixture with an ordinary defaulted parameter; expected output/input stay unchanged,
source/hash change explicitly. Both actual workers and CPython validate it before
staging. This is corrected-source qualification, not unchanged preferred-sign timing.

| Corrected complete workload | Phase | Elapsed change | Wider CPU change | Reference / candidate controls |
| --- | --- | ---: | ---: | ---: |
| Floating report | Warm | −8.03..+2.91% | −8.90..+4.36% | **8.18 / 3.44%** |
| Same | Compile and execute once | **+0.98..+7.75%** | **+2.73..+9.39%** | **5.01 / 1.61%** |
| Defaulted helper diagnostic | Warm | **+3.61..+5.95%** | **+5.25..+6.98%** | 1.30 / 0.95% |
| Helper text | Warm | **+2.17..+5.69%** | **+3.07..+5.01%** | 1.73 / 1.70% |

Withdraw the caller-window footprint. Corrected numeric warm gain is not established;
stable adverse helper/text CPU and extra capacity/allocation do not justify the new
lifetime state. Possible contributors include clearing and heap-backed operand
writes; neither is individually measured. No more unchanged run is needed for this
decision, and it is not a permanent prohibition on better call representations.

Remove InvocationStorageCapacity/the 128-slot tail, caller-window selection, parameter
window helpers and the NativeCallWindow fixture. Restore the real qualified
NativeValues/StartNativeCall implementation and prior architecture test setup.
Keep three independent observable helper-local programs and the stronger structured
integer-preflight assertion. No dormant alternative, compatibility wrapper, option
or new owner remains. The binding-poisoning bug belonged to the discarded reused
window; the restored fresh stack-backed parameters preserve its correct behavior.

All gates/goldens/Closed/exit/join/archive checks pass; RAM removed, terminal cached.
The initial UID guard stopped before mutation/timing on a root-owned historical
archive. A separate fresh idle-lease proof verifies its committed exact bytes,
resolved path, UID0, single inode/alias and containment before reclaiming that one
replica. Ordinary staging then rechecks and retires only the incorrect0ebab036
working core's two aliases and committed old raw/archive replicas. Current0315,
toolchains/canonicals and 254/252 MiB floors remain intact. Both original staging
conditions and the specific retirement receipt are preserved.

The corrected prototype's completed full CI is independent historical qualification;
it cannot establish current-HEAD performance or justify retention. The restored
runtime already has its cached full delivery; focused new-test checks pass on
both actual runtimes, with no repeat unchanged platform qualification. Broader
performance work continues; publication/tagging remain held.

[Corrected summary](corrected/summary.json), [allocation](corrected/allocation-summary.json),
[raw](corrected/observations.tgz), [exact corrected source](corrected/candidate-sources.tar.xz),
[core](corrected/candidate-core.dll.gz), [procedures and original guards](corrected/procedures-and-checks.tar.xz).

Restored production matches all585 qualified source files. Fresh nonincremental builds and actual .NET11/10 each pass124public/88internal focused regressions; three independent helper programs and the structured JSON preflight assertion remain. Private editable prototype replicas are removed after exact committed/archive checks. [Restored validation](restored-validation.json), [checks](restored-runtime-checks.tar.xz).

The discarded corrected prototype also passes [full historical qualification](delivery-corrected/README.md). This does not change the withdrawal or qualify current HEAD; current production matches the earlier qualified source.
