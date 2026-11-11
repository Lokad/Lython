# R236 — call preparation, bounded subset retained

2026-11-11. **Withdraw the broad canonical-span API conversion.** Common
representative paths already borrow their inputs; converting the remaining native
consumers adds many preparation stages without a useful whole-script allocation
reduction. This is a complexity/value decision, not a fixed percentage veto or
permanent rejection. All original and refined observations survive.

Retained source **698f7e77** fixes factory validation before truth callbacks and
typing variance coercion order, admits/fills exception argument tuples directly,
and removes the synchronous argument-carrier/generic policy. Six production files
change; every other original production file and internal test is restored
byte-for-byte. No canonical-call API conversion, native preparation stages, ref-struct carrier,
ChainMap factory, control hierarchy or dormant alternative remains.
[Decision](decision.json), [retained source/checks](retained-source.json).

## Measurements and uncertainty

Two complete source pairs use qualified **15f457f7** sources / frozen **f5ae2355**
core as reference, with a second unchanged reference in each process. Fixed common
Windows worker **9a4272de** and adapter **d9ab54d2**; only core differs. Same six
maintained CSV/text/report/JSON scripts, inputs and independent goldens. Current
Windows development host, 28 logical processors, .NET11RC3, ordinary GC/tiering,
shared CoreLib/hashing and isolated Lython load contexts. **Desktop elapsed and
wider/coarse process CPU remain diagnostic. No CPython comparison or CPU gain.**

Each phase/image has256 warmups, then5 rotated rounds of16 jobs. Initial image
order is before/candidate/control; refined initial order is control/candidate/before.
Warm sync and awaited async are separate from compilation. The complete warm
envelope includes fixture reset, mediated file I/O, full golden checks and result
disposal; startup/compilation are excluded. Raw Run-only clocks are also recorded.
Compilation includes construction/disposal. Medians of different boundaries need
not add; no RSS, pending-I/O or embedding tail-latency qualification is implied.

| Observation | Initial | Refined |
| --- | --- | --- |
| Whole-script allocation | Essentially flat, within about0.02% | Essentially flat, within about0.02% |
| Captured text / sync elapsed vs two references | +13.19..+16.62% | −0.60..+1.26%; original adversity did not recur |
| JSON / sync elapsed | −0.35..+1.73% | +15.98..+24.80%, reference spread7.07% |
| Report compilation elapsed | +41.48..+48.92% | +7.45..+9.60% |
| Other clocks | Mixed, including adverse and unstable controls | Mixed, including adverse and unstable controls |

Ranges compare the candidate with both reference medians, not confidence intervals.
The source-justified refinement removes the obsolete sync argument-carrier layer
instead of wrapping it in a new ref struct, and wires five already-prepared native
wrappers through the existing borrowed async entry. Timing does not establish
individual causality. The refined source still provides no useful representative
allocation saving; common paths already consume arguments without the proposed
copy. No further quiet measurement is required to decline this implementation.
The adverse JSON/compilation results remain unresolved, not disproved.

[Initial warm sync](initial/warm-sync/summary.json),
[async](initial/warm-async/summary.json),
[compilation](initial/compile/summary.json);
[refined warm sync](refined/warm-sync/summary.json),
[async](refined/warm-async/summary.json),
[compilation](refined/compile/summary.json).
Every phase passes goldens, ownership balances and owned-fixture cleanup.
All raw observations, image identities, core binaries and verified complete source
bytes accompany these summaries. [Archive identities](archive-identity.json).
The first evidence commit allowed Git newline normalization. Archive attributes
restore canonical raw bytes from frozen storage, checked against the original
manifest; no measured values change.

## Validation and next boundary

Both broad prototypes build actual11/10 and pass focused semantics/cancellation/
ownership checks. Initial broader checks include4533 ordinary internal cases per
target and4097 selected public cases on11; refined preparation checks pass1376
internal/114 public per target. No full platform/package qualification is claimed
for withdrawn code. Earlier compilation errors and obsolete reflection/delegate
fixtures, with their corrections, remain in [preparation](preparation.tar.xz).

The retained subset receives a fresh nonincremental Release solution build24.39s,
zero warnings/errors, and **4534 ordinary internal +1003 selected public cases per
actual target (11074 total)**. Independent CPython3.13 goldens cover flag order,
invalid-keyword effects and descriptor-returned initializer functions. Pending
descriptor cancellation keeps aliases funded until joined cleanup; refused
exception storage is rejected before allocating an input-proportional value vector.
Full Windows/Ubuntu, original stress and installed consumers batch with the next
coherent runtime delivery. Publication remains postponed.

Next assess complete native aggregation across eligible counting iterations,
reusing current native iteration and non-admitting dictionary updates. This targets
per-element dispatch/operand work rather than argument packaging. Current short
strings already store up to8 UTF-8 bytes inline, and casing writes final fragments;
there is no presumed per-word array or intermediate cased-line copy to remove.
The whole-script performance objective remains open.
