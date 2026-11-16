# One native and CLR iteration lifetime

R450/B853,2026-11-16UTC. **Retain the coherent consolidation; [full delivery qualification passes](delivery/README.md).** Reference qualified R448 d77a8bdd/finale1204916; final candidatea44f65a8
at base3f98a725 plus exact archived source. No public API, dependency or policy change.

Delete eight ToSequence/ToSequenceAsync routing methods, CheckedSequence,
CheckedCollection, AsyncSequence, AsyncEnumerator and their source file; remove
unused GeneratorOuter enumerable fields, obsolete acquisition bridges and the
unreferenced eager Zip(object[]) overload. The sole ZipCallable takes
CallArgumentValue[] and retains the lazy Python iterator. Native slots/reductions
and CLR builders use one existing cursor, context, current value and disposal
lifetime. No second controller, owner, cache, quota, tracing or unsafe API.

C# consumers use standard foreach with a strongly typed cursor enumerator. The
initial124manual loops are removed, so the production consolidation also reduces
source rather than accumulating loop scaffolding. The existing two drain loops
remain the single capacity-growth rules for sync/async materialization. The
prototype's duplicate materializer loops/earlier-scratch-admission proposal were
withdrawn before measurement. Native VM/reduction consumers retain scalar results.

| Complete source / phase | Elapsed change | Wider process CPU change | Reference / candidate process spread |
| --- | ---: | ---: | ---: |
| scripts.text-normalization.large / compile | +1.03..+2.56% | -0.28..+4.44% | 0.35 / 1.16% |
| scripts.text-normalization.large / warm | -5.91..-1.34% | -4.07..-1.29% | 1.56 / 3.25% |
| scripts.text-normalization.large / compile-run | -6.19..-2.75% | -5.96..-3.37% | 0.54 / 3.10% |
| scripts.numeric-squared-error.large / warm | +1.11..+6.69% | -1.57..+6.35% | 3.84 / 1.62% |
| scripts.csv-invoice.large / warm | -2.81..+1.18% | -3.29..+0.14% | 1.75 / 2.32% |
| scripts.report-render.large / warm | -2.92..+5.59% | -3.64..+3.53% | 5.19 / 3.40% |
| calls.positional.large / warm | -2.76..-1.56% | -3.52..-2.36% | 0.50 / 0.73% |

Seven jobs213.084s; same five-case catalog42531737 and unchanged worker, normal
optional0.4adapter, scripts/fixtures/stdout/file hashes. Both complete32-file
images frozen after nonincremental build, only core differs. Two independent
processes per source,4s full-request warmup and five rotated~0.5s samples. Four
pairings of medians are ranges, not confidence intervals. All image/Ready/golden/
quiet/Closed/exit/join/archive gates pass. Terminals cached; no workers/RAM remain.

Warm includes mediated RAM I/O, complete stdout/file checks and prior-result
cleanup; compilation/startup, reset/protocol and final cache teardown excluded.
Fresh adds compilation/compiled-owner disposal; compilation measures construction
and disposal. Wider CPU includes reset/protocol. Quiet F4as_v7/Ubuntu24.04.4,four
EPYC9V45vCPUs,16GiB/no swap,.NET11RC3 natural workstation GC/tiering. No CPython
timing,RSS,pending-I/O or embedding-tail claim. Frozen-worker CPython verification
passes all five cases under both images.

Normalization fresh and call elapsed/CPU favor the final source. Normalization
warm has3.25%moving candidate copies; magnitude remains uncertain. Compilation
elapsed is1.0–2.6%adverse, with mixed CPU. Numeric elapsed is1.1–6.7%adverse in
this final collection, unlike the initial prototype; its reference copies move
3.84%and CPU is mixed. Accept this unresolved numeric debt at the consolidation
boundary; do not call it fixed or transfer initial numeric gains to final source.
CSV and report are inconclusive, including adverse pairings and5.19%moving report
reference. No universal gain or inferred recovery of R448's prior regression.

Local natural-GC whole-process allocation is effectively flat (all changes<0.04%),
with identical conservative execution peaks. Output/file goldens, compiled-budget
baselines and disposal to zero pass. This is a design/CPU result on named workloads,
not an allocation improvement. Full workflow/source/control limits remain explicit.

Final source builds both targets without warnings/errors;1,563internal/4,989public
focused checks pass each actual Windows .NET11/10, including current cancellation,
first refusal, real pending users, aliases, disposal and budget reuse. Initial
5755snapshot full local11 ordinary passes4,806/17,935; it does not qualify the final
refinement. [Final platform, original stress and installed-package qualification passes](delivery/README.md). Documentation-only updates after freeze describe the resulting contract;
production source parity is required at delivery, no rebuilt binary equality.

Two new cases reproduced a prototype bug (remaining2items became3): list extension
unwrapped the original list/array behind a partially consumed cursor. The fix uses
count hints without replaying input, and snapshots only an unstarted direct list.
Aliases and budget cleanup remain asserted. CLR bridge cursors are single-pass;
create a new cursor for an independent enumeration. Dictionary versions capture at
acquisition; pending cleanup retains its original subject until completion.

[Initial source/results/adversity](initial/README.md) remain separate. The final
refinement removes scaffolding as well as correcting behavior; it does not overwrite
the first comparison. Original missing-CLI-argument, overload-resolution, private
tool and known-count build errors remain. None started a discarded VM measurement.
No timeouts/assertions were weakened and no performance threshold dictated retention.

Full auxiliary hashes are recorded. Reference coree120 is in [initial cores](initial/cores-and-procedures.tar.xz);
unchanged31auxiliary files are frozen/reference in the [R448 archive](../2026-11-16-compact-iteration-result/procedure-and-checks.tar.xz).
Final candidate is archived here. Only two historical working raw JSON copies
(total4,956,519B,including a preserved failed collection) were retired after committed
archive/member equality and fresh UID/path/hash/all-alias proof under idle lease.
Adverse canonical evidence, current images/toolchains and254/252MiB floors remain.
Source/whole-script objective continues; publication/tagging remain held.

[Summary](summary.json), [allocation](allocation-summary.json), [exact sources](candidate-sources.tar.xz),
[image/source hashes](identity.json), [raw](observations.tgz),
[core/checks/procedures](cores-and-procedures.tar.xz), [decision](decision.json).
