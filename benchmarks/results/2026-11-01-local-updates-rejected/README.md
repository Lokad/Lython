# Local augmented updates: rejected (B72)

2026-11-01, source baseline `4f4e5615`, core source `9003d045`, ordinary adapter
`d703767c`/PythonRe 0.4.0. The single **52.877-second** trial passes its controls,
but fails the predeclared workload gate. **The entire compiler/runtime prototype
is removed.** No production improvement or updated CPython ratio is claimed.

## Hypothesis and implementation

B71 measured actual integer-loop instruction/slot transport. The normal compiler
emits four operations for `total += i` or `total += 1`. After ordinary lowering and
validation, consolidate exactly that local/local-or-integer-constant shape into
one operation in the existing engine. Exact boxed BigInteger pairs use existing
arithmetic and local-store policies without operand-stack round trips. A
non-inlined helper limits CLR temporary lifetimes. Dynamic operands are read once,
retained in admitted storage and passed to the existing synchronous/asynchronous
protocol helpers, with cancellation/refusal checked before storing.

No scalar representation, arithmetic backend, ownership subsystem, new option or
alternate interpreter is introduced. General expressions and other scope shapes
keep their existing executable instructions. The source patch and identities are
preserved for review; smaller instruction count alone was not acceptance.

## One frozen comparison

Quiet dedicated Azure VM, actual .NET 11 RC3, ordinary GC/tiering. Original,
candidate and identical original run as separate Lython images in one process,
sharing CoreLib/hash behavior and identical auxiliary bits. All four cases use
unchanged source, input and full stdout/file goldens. The pure loop uses an empty
file fixture. Warm public execution includes fixture reset, mediated I/O,
verification, result disposal and owner cleanup; compilation/startup are excluded.

256 interleaved warmups per image; two seconds settling per case; seven rotating
batches of 256 loop jobs or 32 complete script jobs. Completed cases are saved
incrementally. The owned service limit is 60 seconds. Before code, retention
required at least 12.5% loop improvement **and** 5% complete-text improvement
against both originals, CSV/JSON regression at most 3%, original drift at most
3%, relative IQR at most 10% and all batches longer than 100 ms.

| Job | Candidate elapsed change vs original | vs identical original | Fixed gate |
| --- | ---: | ---: | --- |
| Integer loop | -13.94% | -13.45% | Passes loop target |
| Complete text helpers | +2.78% | +1.25% | Fails text target |
| Complete CSV helpers | -0.77% | -1.08% | Passes regression bound |
| Complete JSON helpers | -2.28% | +0.43% | Passes regression bound |

Positive changes mean longer elapsed time. All controls pass: original drift is
0.31–2.70%, IQR 0.29–2.51%, minimum batch 203.60 ms. Both quiet gates, all full
goldens, per-job ownership baselines, final zero budgets and fixture cleanup pass.
Service exit is zero, MainPID zero, control group empty and lease free. No worker
remains. No retiming, rescue adjustment or acceptance change follows the result.

## Correctness and restoration

Focused Release checks pass on both actual .NET 11 RC3 and .NET 10.0.8:
**706 white-box and 2,977 public cases per runtime**, before and after restoration.
New independent controls cover local integer admission before assignment, original
RHS identity across real protocol suspension and rebinding, cancellation waiting
for the real user, retained funding and final zero balance. The existing reflected
numeric family now uses a local RHS, preserving its real asynchronous host work.

The first candidate run passes 663 cases and fails one layout-specific sample;
the sample is adapted only to exercise the still-existing typed instruction. That
adaptation is removed with the prototype. All four production files match HEAD.
The original layout test and existing protection policies are restored. Independent
semantic/lifetime controls remain and pass against production.

An initial rollback byte comparison mistakes CRLF working files for changed LF Git
blobs and stops before restoration. Its caller erroneously continues into a
prototype build/first native check; that session is interrupted and joined, and
its logs are explicitly excluded from restoration proof. The corrected rollback
compares normalized source, restores original bytes, then forces a fresh
nonincremental Release solution build before the actual restored checks.

Unchanged production delivery remains the cached terminal Windows/Ubuntu,
ordinary/original Stress and installed-package result 41343479748 at d703767c.
It is not requeried or repeated for this rejected runtime experiment.

[Summary and check hashes](summary.json), [candidate source](prototype.patch.gz),
[fixed plan](pre-code-plan.md), exact driver, identities and all observations are
preserved. [Raw results](raw-results.tar.gz), SHA256
`6caae77db215fcc9eaf9900390058a62f120a6ae544fb16ab4f150d5c31b6dac`.
This historical experiment does not describe a future HEAD performance level.
