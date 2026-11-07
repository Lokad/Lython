# Owned split-result storage — R28, withdrawn

2026-11-07 UTC. Both prototypes are withdrawn; the runtime returns to the fully
qualified R26 source c4729a9f. Independent Unicode/regex regression coverage stays.
No publication, new CPython comparison, or claimed CPU gain.

The proposed representation replaces per-word UTF-8 arrays with a newly allocated
split-result payload owned by the execution. It never borrows the file decoder's
refundable window. Word metadata refunds cannot refund the shared payload.
Deleting the hash-cache Boolean makes room for an offset without increasing the
measured **56-byte** string object on actual .NET 11/10. Zero hashes recompute.
The initial complemented offset is refined to a sign-bit/mask encoding, removing
the conditional from every byte access. Both source variants remain archived.

Exact complete-job allocation diagnostics show **6.94% less text allocation** for
both execution APIs; seven other workload controls remain flat within noise.
The final capacity estimates and admitted peaks match baseline. This establishes
allocation reduction, not lower retained memory, RSS or CPU. Initial extra header
admission adds 65,536 B to the text peak and is corrected using the existing
per-word header headroom. That adverse condition is preserved too.

## Controlled elapsed comparisons

Quiet Azure Standard_F4as_v7, Ubuntu24.04.4, .NET11RC3, ordinary GC/tiering,
unchanged b94fa98c worker/host and optional 0.4 adapter. Baseline core9e1dbc38
matches R26 production source. B375 current55805d04; B376 current81630e92.
Complete maintained scripts include mediated RAM I/O, output/file checks and
prior-result cleanup. Compilation, startup, staging and final teardown excluded.
Five-second warmups per role; seven rotated150ms rounds. Positive change means
slower. Ranges below retain all current/reference process-median pairings.

| Workload/API | B375 change | B376 mask change | B376 identical current / baseline spread |
| --- | ---: | ---: | ---: |
| Text sync | −0.32 to −0.17% | −1.93 to +1.91% | 3.61% / 0.29% |
| Text async | +1.08 to +7.59% | −4.03 to +2.82% | 0.73% / 5.98% |
| Report sync | +4.03 to +6.61% | +1.63 to +4.97% | 1.87% / 1.37% |
| Report async | +1.48 to +4.36% | +0.91 to +2.53% | 0.48% / 1.11% |
| CSV sync control | −0.76 to +0.12% | −1.96 to +1.32% | 0.23% / 3.01% |
| JSON sync control | +3.14 to +6.04% | −0.73 to +2.10% | 1.86% / 0.93% |

B375 has one baseline and two current processes; B376 adds a second baseline.
They complete in129.17s/168.49s respectively. All six jobs in each pass loaded-image,
golden, quiet, funding, Closed/exit, joined-user and fixture-removal checks. Raw
archives are verified persistently under the owned lease. Observation occurs
after the recorded owner/stop/margin bounds. Coarse process-CPU observations are
retained separately and do not establish a CPU improvement.

The mask refinement improves several observations but does not establish a text
speedup; report observations remain adverse. The allocation benefit does not
justify adding window metadata, byte-access work and ownership complexity to all
strings at this checkpoint. Withdraw the representation and both runtime paths,
rather than accumulating unresolved regression debt. This is an evidence-based
decision on this implementation, not a permanent ban on future storage designs.

## Correctness and provenance

Each final prototype passes951 internal/2,557 public focused Release checks per
actual target, including Unicode casing/expansion, whitespace/maxsplit, string
operations, regex offsets, cancellation, terminal refusal, retained aliases and
budget reuse. Explicit installed Release consumers pass both targets. Two initial
tests incorrectly equate summed word snapshots with shared backing ownership;
their failures and corrected independent funding assertions remain in history.
Prototype success is not production qualification. No duplicate full platform/
stress cycle is run for the withdrawn runtime. A nonincremental Release rebuild
of the restored source passes16 internal/one public affected checks per target.

[Allocation](allocation-summary.json), [initial elapsed](comparison-summary.json),
[mask elapsed](comparison-mask-summary.json), [withdrawn patch](withdrawn-mask.patch),
[development sources, binaries, all checks and failed conditions](development-evidence.tgz),
[B375 raw/source/gates](b375/observations.tgz),
[B376 raw/source/gates](b376/observations.tgz), [archive hashes](sha256.json).
The development archive includes exact withdrawn Linux images; they are diagnostic
evidence, not release packages. Initial staging disk refusal starts no capture;
the follow-up shares the unchanged, hash-verified persistent catalog. Independent
old B205 image backup/retirement is recorded separately in its original evidence.
