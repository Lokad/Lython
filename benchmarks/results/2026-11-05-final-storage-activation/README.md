# Final list storage and lazy activation construction

2026-11-05. **Retain** the exact D37/D38 patch over c7500764, frozen core
**c0221b77**, versus qualified **a8e476bf/c6d71108, core402ae9f1**. Focused
checks pass on actual .NET11/10; full delivery qualification remains pending.
Publication postponed and the whole-script performance objective remains open.

Counted whitespace splitting admits payloads and final list backing together,
fills one final array and adopts existing PyList storage. Executable fixed-count
list displays likewise adopt their array instead of copying it into another one.
Remove separate list-constructor loans, per-fragment Add/growth and duplicate
literal backing. Unowned internal helpers acquire no hidden allowance.

Member-cache storage joins the existing frame working owner and is admitted
only at the first generic lookup, before descriptor/protocol effects. Direct
builtin receivers need no cache table. Remove constructor allocation/precharge.
Ordinary lexical, inlined, global and nonlocal reads use existing nonmaterializing
namespace lookup; explicit dictionary consumers retain stable real namespaces.
No context fusion, new owner, arena, representation or dispatcher framework.

Payload aliases and actual suspended users stay funded. Combined list admission
can refuse earlier; lazy cache admission can occur later in a body after earlier
effects. Both precede their allocation/lookup effects and a first refusal remains
terminal. Cancellation, closure/class/annotation semantics and cleanup remain.
These are internal changes, with no public API or options added.

## Controlled complete-script comparison

Quiet dedicated AzureF4as_v7/Ubuntu24.04.4/.NET11RC3, ordinary GC/tiering.
Three isolated source images, two unchanged baseline roles, identical auxiliaries,
CoreLib and key hashes. Six guestwarmup seconds/image, two settle seconds/job,
nine rotated approximately200ms batches: **161s owned**, all original gates pass.

| Full job | Elapsed vs baseline / control | Baseline spread | Allocation vs both |
| --- | ---: | ---: | ---: |
| Text warm |−6.11% /−1.69%|4.49%|−5.38%|
| Text compile + run |−2.29% /−0.94%|1.36%|−4.95–4.96%|
| Text async, completed I/O |−3.40% /−3.24%|0.16%|−5.38%|
| Ordinary-helper CSV warm |−1.88% /−4.20%|2.42%|−6.43–6.45%|
| Ordinary-helper CSV compile + run |−1.21% /−3.19%|2.05%|−6.25–6.27%|
| Report warm control |+0.15% /−0.66%|0.81%|unchanged|

Async text gives the clearest elapsed observation; warm text/CSV magnitudes are
limited by unchanged-role variation. Fresh text is a small favorable observation.
Fresh CSV candidateIQR1.98%, reportIQR2.89% further limit small claims. Retain the
construction simplifications and allocation reductions; no stable adverse
whole-script effect appears in this comparison. Earlier component debt and all
adverse/control-limited conditions remain recorded; this coupled change does not
isolate their causes or prove them fixed. No CPU attribution, new CPython ratio,
RSS/startup/embedding-latency or genuinely suspended-I/O performance claim.

Full public invocation, mediated RAM-file I/O, full stdout/file checks and result
cleanup are included. Fresh adds Compile/Dispose; async awaits RunAsync with
completed host I/O. Fixture reset/staging, startup, compilation in warm jobs,
hashing and final cached teardown are excluded; actual users finish before
cleanup. Process allocation includes reset/background work. Each warmed job
returns allowance to its frozen baseline; all owners end at zero. Independent
CPython3.13.16 full output/files, identity/quiet endpoints, joins, fixture removal
and free lease pass. These are source-pair observations, separate from CPython
comparisons and not additive with earlier gains.

## Correctness and evidence

Nonincremental Release36.46s, zero warnings/errors; **2,242 internal and7,909 public
checks per actual framework** pass. Coverage includes text/CSV semantics,
closures/class/nonlocal/annotation/locals/generators/protocols, real suspension,
cancellation, terminal refusal, retained owners and allowance reuse. New tests
cover nonmaterializing reads/stable explicit namespaces and cache denial before
lookup effects, with earlier funded body effects preserved. Remove obsolete
reflection assertions about the old cache field; retain semantic, pending-operand,
cleanup and balance assertions. Cache admission fixture now leaves capacity for
the small operand array and checks denial at the actual generic lookup.

Initial compiler nullable-flow errors and obsolete-field/preflight fixture failures
are preserved with their corrections, rather than hidden. No timeout, containment
assertion or user-visible behavior was relaxed. VM disk cleanup removed only
hash-verified duplicate uploads/raw traces already present in local and remote
archives; frozen workers and evidence archives remain.

[Plan](plan.md), [exact source identity](identity.json), [all metrics](assessment.json),
[source patch/snapshot, catalog, driver, raw and gates](observations.tgz),
[focused validation](validation.json), [original/corrected logs and cleanup receipts](validation-logs.tgz).
Archive SHA256 `38df04e97034706839a724b9f42fdf35ee43dc3bdf3cf0a4e4928cf51ee329f6`.
