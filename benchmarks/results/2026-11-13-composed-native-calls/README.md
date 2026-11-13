# Composed executable helper calls

R303 retains composed helpers in their existing final slots when callees complete
directly. It removes eager caller context creation, admitted workspace acquisition
and slot copying at an ordinary helper call. Dynamic children stop before their
operation and transfer once to the existing linear runner. Parent admission failure
unwinds every prepared descendant. No new evaluator, call arena, policy or public
option is introduced.

The qualified reference is **fc2531b8**, measured core **6b468bf1**. Initial prototype
**628ab680** has the same 571 production inputs except four runtime files. The retained
refinement **2f34a48f** changes only native call selection order relative to that
prototype: existing receiver/unary operations precede user-function selection. Both
complete source snapshots and exact binaries are preserved.

On the existing quiet Azure VM, .NET11 pinned runtime and fixed **fec79afd** worker,
the 4,096-record/127,031-byte invoice workload improves against the reference:

| Retained refinement | Elapsed change | Wider process CPU change | Measurement status |
| --- | ---: | ---: | --- |
| Warm invoice | −9.79..−8.02% | −10.34..−6.98% | Source-pair, two instances per revision, five rotating rounds |
| Compile plus one invoice run | −13.07..−7.02% | −14.44..−5.66% | Source-pair; reference control spread4.24%, so magnitude is less precise |
| Captured-text warm control | +0.09..+3.27% | −1.32..+3.70% | Unresolved residual regression risk; reference control spread3.06% |

Negative means faster than the earlier Lython reference. These are **not CPython
ratios**. Worker execution clocks include mediated I/O, result cleanup and, in the
fresh lane, compilation/compiled-owner cleanup. Fixture reset, protocol and golden
validation are outside those clocks but inside the wider process CPU samples.
Startup is excluded. Runtime overrides are absent and workstation GC is unchanged.

The initial prototype establishes **−19.51% warm sync/async allocation and −18.97%
fresh allocation** for the complete invoice script. Those allocation measurements
belong to628ab680; they are not relabeled as final-source results. Its eight scripts
pass compilation, warm sync, warm async and compile-plus-run goldens, balances and
cleanup. Desktop elapsed controls move as much as52.30%; fresh invoice and warm
report also have adverse results. They remain visible and do not establish CPU
benefit or equivalence.

Initial quiet B657 completed six jobs in250.317s, supporting invoice gains but
showing captured warm +1.72..+5.95%. That justified the dispatch-order refinement.
B658 completed three affected/control jobs in134.740s. Both collections pass loaded
image/catalog identities, every file golden, quiet-machine checks, worker joins,
fixture removal and persistent SHA-verified archival. No VM users or RAM mounts
remain. The residual captured uncertainty is accepted for the coherent delivery
checkpoint; there is no third isolated timing campaign.

Initial focused Release checks pass120internal/193public per actual .NET11/.NET10.
The retained refinement passes120internal and its four new public cases on .NET11,
including shared-budget refusal after one/two prepared descendants, independently
funded retained input, once-only closure effects, live defaults/saved receivers,
actual I/O and cancellation. Broader local checks and desktop diagnostics remain
incomplete at this evidence checkpoint; later platform qualification must be linked
separately. An original stalled .NET10 run, timed-out stack observation and mistakenly
overlapping rebuild are retained. Serial rechecks of the initial source pass.

[Initial validation](initial/validation.json),
[initial sources/binary/raw evidence](initial/source-checks.tar.xz),
[refined validation](refined/validation.json) and
[refined sources/binary/VM evidence with partial local snapshots](refined/source-checks.tar.xz)
preserve the exact boundary and every adverse condition. Current external invoice
comparisons, final allocation confirmation and full platform/package qualification
are still pending. Publication remains postponed.
