# Array-backed JSON input windows — R60 withdrawn

2026-11-07. **Entire production prototype withdrawn.** Qualified core sourcee66b8f72
is restored; no view constructor/storage-start field/borrow method/hash-cache
replacement survives. Architecture-specific native tests are deleted and original
copy/denial assertions restored. Keep one independent public decoded-text/regex/
Unicode interoperability case with shared-budget reuse under both APIs.

R54's ROM design added8B to every string. This different layout combines an array,
length and storage-start state (owned/batch-retained/view), removing the separate
hash-ready flag. Atomic zero hashes remain zero and can recompute. Managed empty/
nonempty construction80/88B matches baseline, versus R54's88/96B. Full immutable
input backing/header remains funded through batch lifetime before source-shell
refunds; view metadata is admitted; other-owner/unowned input/escapes copy.
No tracing, ledger, new owner, unsafe code, quotas or forcedGC. Small views can
retain large input until batch end, allowing conservative earlier denial.

First8failures/203pass preserve old copy-size denial fixtures and mutation after
internal FromOwnedUtf8 ownership transfer. Revised unowned-input independence,
real output denial and source/view/nested-view/refusal/reentry checks preserve
funding; strengthen surviving-shell plus full-backing funding. First and refined
variants pass460internal/2496public per actual11/10. Independent CPython3.13.2
goldens combine JSON-decoded Unicode, indexing/reversal/hash/replace/repeat,
JSON output and runtime regex patterns/subjects/capture offsets. This does not
justify an API/ownership tradeoff without complete workload measurements.

## Allocation and first controlled comparison

Baseline frozenaa133dcb; first992f53fa; refined8ad25a95. Exact sources/cores are
in the archives; precommit informational versions are not a source-equivalence
proof by themselves. Normal optional0.4 adapter/worker/fixtures stay unchanged.
An existing10job desktop diagnostic, both APIs separately, passes all complete
goldens, image/hash isolation, root funding and owned-fixture cleanup. First-variant
allocation JSON−5.049/−5.041%, report−6.972/−7.056%; controls within0.007%.
Root admitted peaks decrease145,482BJSON/243,823Breport; controls unchanged.
Desktop clocks are mixed/adverse: text reaches+22.10%, old spread15.47%. No CPU gain.

Quiet F4as_v7/Ubuntu24.04.4/.NET11RC3, normalGC/tiering. Two independent baseline/
two current processes;5s warmup/role/job,7rotated150ms rounds. Full warm invocation,
mediated RAM-file I/O, complete validation and prior-result cleanup included;
compile/startup/staging/reset/transport/final cached teardown excluded. All new/old
process pairings retained; positive means slower.

| Complete script | First B416 elapsed | Refined B417 elapsed |
| --- | ---: | ---: |
| JSON-to-Markdown report |−3.28to−0.84%|−3.40to−0.87%|
| JSON-lines helper summary |+0.28to+2.63%|+1.56to+7.31%|
| CSV helper control |+2.65to+3.07%|−1.07to+0.32%|
| Text helper control |−1.32to+2.79%|+0.57to+1.98%|

B416CSV has stable old/new spreads0.17/0.24%, motivating one source refinement:
explicit zero-offset span/memory construction for owned strings restores the
baseline bounds shape. This is a source hypothesis, not JIT causal attribution.
Sharing/ownership/profile/inputs/worker stay unchanged. Refined wrapper allocation
still80/88B; full script allocation was measured only on the first variant.

B417JSON has5.18%current-role spread despite within-role IQR0.34/0.41%; retain
that process variation and adverse+7.31%, not a claim of an exact7.31%regression.
Even its best pairing is+1.56%; text is consistently+0.57to+1.98%. Report's original
roleIQR4.17% and current1.89–2.37% qualify the modest gains. External whole-worker
CPU additionally includes reset/protocol/background work and remains mixed/adverse:
refined report−0.96to+2.08%, JSON+1.80to+9.36%, text+1.32to+1.83%.
No substantial CPU improvement or broad runtime gain is established.

Withdraw the representation:5–7%first-variant allocation savings and modest report
elapsed benefit do not justify global accessor/ownership state with JSON/text
regression debt. The refinement largely removes the CSV penalty; all original
points remain. This is a measured complete-cost decision, not a fixed threshold,
permanent rejection of sharing or completion of the whole-script objective.

Both runs pass source/core/every auxiliary/actualReady, goldens, quiet endpoints,
Closed/exit0/join/emptyfixtures/unmount and persistent archive verification under
lease and locally. B416114.231s/B417114.322s;180sowner+30stop+30margin each.
All users ended, no mount remains. Terminal/cached; never service-query again.
[First raw/source](first/observations.tgz), [refined raw/source](refined/observations.tgz),
[first assessment](first/assessment.json), [refined assessment](refined/assessment.json).

## Restored implementation

All561production files match the qualified Git source. One checkout changes only
LF toCRLF versus the older working snapshot. Forced nonincremental core builds
precede456internal/2496public PASS per actual11/10; matching probe core bytes agree.
New hashes include build metadata/checkouts and are not relabeled as earlier bits.
No repeat of unchanged full platform/package qualification is needed: completed
CI42404834827 remains final for the restored runtime. Publication is postponed.
[Restoration proof](restoration.json), [decision](decision.json),
[local sources/cores/failures/probes/raw clocks](local-observations.tgz),
[archive receipt](local-archive-receipt.json), [contract](source-contract.md).
Experimental contracts describe the removed prototype, not current Lython behavior.
