# Completed async entry: withdrawn prototype

R433/B830,2026-11-16UTC: candidate **7c592066** versus fully qualified
**86d9344e /ecd9e956**. Candidate never committed to production or fully qualified.
The source, exact candidate core, all original failures and controls are preserved.

**Decision: withdraw the runtime change.** Plain completed prefixes and one pending
continuation avoid entering two generated async bodies for completed calls/yields,
but add83net runtime lines and retain the existing instruction/exception machine.
They remove no argument/frame storage or complete execution representation.
Whole-job allocation and accounting peaks are effectively unchanged. Lower async
normalization time alone does not justify the additional control-flow machinery.
This is a complexity/evidence decision, not a rigid improvement cutoff, failed
design or permanent prohibition. A coherent replacement with changed evidence is
still eligible. No remaining performance debt is introduced by withdrawn code.

| Complete warm job | Elapsed candidate/reference | Wider CPU | Ref/candidate median spreads |
| --- | ---: | ---: | ---: |
| Async normalization | −7.70..−1.76% | −8.12..−0.76% | 5.49/0.90% |
| Sync normalization control | −4.10..−0.48% | −4.23..+0.73% | 0.41/3.35% |
| Async protected file search | −3.98..+3.34% | −3.42..+1.59% | 2.14/5.37% |
| Sync JSON control | −0.40..+2.18% | 0..+2.72% | 1.43/1.15% |

All four process pairings, not confidence intervals. Five rotated approximately
0.5s batches after5s full-request warmup, two processes/source;140.053s and all
image/golden/quiet/Closed/exit/join/archive gates. Reference/control movement limits
precise attribution; all adverse samples retained. Local natural-GC whole-job
allocation changes are −0.0067%,−0.0030%,0%,+0.0114%; accounting peaks unchanged.
No allocation, synchronous-CPU, startup or embedding-tail improvement is claimed.

Same F4as_v7/Ubuntu24.04.4,four EPYC9V45vCPUs,16GiB/no swap,.NET11RC3
normal tiering/workstation GC. Warm includes mediated RAM I/O, full stdout/file
goldens and prior-result cleanup; excludes compilation/startup, reset/protocol and
final cache teardown. Wider process CPU includes reset/protocol. The host completes
I/O synchronously; pending operations are correctness-tested, not latency-benchmarked.
No CPython refresh, heap/RSS or contention measurement. This candidate could not
explain the published synchronous CPython gap.

Candidate focused actual.NET11/10 each128internal/495public pass, including
generators, closures/protocols, cancellation/refusal, retained owners and pending
cleanup. The initial public run failed only a controlled subprocess thread-placement
assertion: guest recursion/recovery succeeded and budget returned to zero, but an
extra fixture async wrapper let the CLR continuation hop off the owned256KiB stack.
Returning the actual held operation removes that wrapper; all original physical
owned-thread/256KiB/deadline/recovery/refund assertions remain. Three candidate
checks eachactual11/10 and onebaseline11 pass. An earlier baseline attempt was
rejected by its binary-hash guard and proves no baseline behavior; original evidence
is retained. Keep this fixture correction independently and verify it after restoring
the qualified runtime; it does not change managed stack policy.

Persistent archive hash-verified before owned RAM teardown; all users joined.
Only exact committed working replicas were retired with fresh lease/path/ownership/
hash/all-alias proof. Current images/catalogs/toolchains and254/252MiB floors kept.
Terminal cached. Publication remains held; whole-script performance work continues.

[All controls](summary.json),[raw observations](observations.tgz),
[allocation](allocation-summary.json),[candidate checks](focused-checks.json),
[complete candidate source/checks](candidate-source-and-checks.tar.xz),
[prototype/procedures/original failures](procedure-and-prototype.tar.xz).
Baseline auxiliary images are unchanged from the
[retained line-construction image](../2026-11-16-line-construction/README.md).


Restored production source verified against all586committed inputs and force-built
nonincrementally. All37stack-guard subprocess scenarios pass eachactual.NET11/10
with the minimal held-operation fixture; original assertions/deadlines retained.
Rebuilt hashes are recorded, not asserted equal to the earlier measured image.
No production change remains, so unchanged full qualification is not repeated.
[Restored validation](restored-validation.json),[source/TRX/build logs](restored-validation.tar.xz).
