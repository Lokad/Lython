# Product computation keeps its granted admission — R57

2026-11-07, baseline525172bc (qualified R55 production) plus the archived patch.
Before/current cores990c3bf8/bd1e50f5; source inventory and exact hashes are in
[comparison identity](comparison-identity.json). Production removes15 net lines:
the void guard, its premature refund, separate post-allocation admission, duplicated
operand conversion and unused pair counters; start validation is shared.

Four native baseline failures demonstrate two problems under both APIs: a granted
working lease is discarded and requested again for the same result; cancellation
at that admission computes/commits the unneeded product before checking the token.
Two refusal controls pass baseline. Six final native cases pass, keep input aliases
and first refusal, fund retained magnitudes, refund scratch and reuse the allowance.
Public CPython3.13.2 goldens also expose empty integer starts being reboxed and bool
starts becoming ints. Both corrected baseline public cases fail; final cases agree.
An initial fixture incorrectly expected empty float identity: independent CPython
shows a fresh float. Original fixture/failures and corrected goldens remain archived.

The shared multiply operation holds conservative result/computation capacity until
the retained magnitude commits from that same lease. Check cancellation after a
substantial grant, before entering opaque CLR arithmetic. At most64-bit results
use existing minor-scalar headroom; no per-item temporary owner/admission is needed.
Empty integer/bool starts retain identity; float accumulation still returns a fresh
float. No numeric representation/public API, quota, hidden allowance, collector or
new memory abstraction. Admission thresholds may differ conservatively. Individual
CLR BigInteger operations remain non-interruptible; current iteration checks and
awaited async cleanup remain, without abandoned work.

**Focused Release:91 internal and638 public per actual .NET11/.NET10 PASS.**
This includes retained products, delayed host-backed input, validation/error shapes,
sum/aggregate state, awaited cursor cleanup, integer aliases and admission reentry.
Source/core checks match the frozen diagnostic. Full combined R57/R58 delivery CI42404834827 on e66b8f72 is GREEN: Windows/Ubuntu,
actual11/10, original Stress and installed consumers. The [cached receipt](../2026-11-07-executable-graph/delivery-ci.json)
is final; no repeated query or unchanged qualification.

One short existing-tool complete-job diagnostic: a1024-row CSV matrix-size report
with four-factor products and original ordinary CSV/text/JSON/report controls,
both APIs. New affected fixture stdout/file goldens independently frozen by CPython.
Normal GC/tiering, three isolated images sharing CoreLib/processGC/hash seed,
32warmups/image and5rotated rounds*4jobs. Complete Run/awaited RunAsync, mediated
I/O, validation and result cleanup; compile/startup/final cached teardown excluded.
All10golden/isolation/funding/fixture-cleanup gates pass. The unchanged driver's
R45 caption is historical; this catalog and source pair define the observation.

Affected script allocation−7.808%sync/−7.216%async, controls within0.045%.
Elapsed is unusably unstable: affected sync−18.52/−42.98% with42.90%old-role spread;
async+102.49/+1.49% with99.52%old spread. Other controls include adverse results
and8.84–36.54%drift. Do not claim CPU/elapsed improvement or generalize this new
affected script to the primary CPython table. Retain for demonstrated correctness,
one coherent admission lifetime and fewer temporary allocations; no VM retiming
campaign is needed to justify this correction. All raw adverse clocks remain.

[Summary](diagnostic-summary.json), [focused counts](focused.json),
[raw sources/cores/failures/tests/samples](observations.tgz),
[archive receipt](archive-receipt.json). Publication remains postponed and the
substantial whole-script performance objective remains open.
