# Benchmark experimental history

These revision-specific records preserve observations and decisions; they do not
describe HEAD or impose current experimental rules. Uncertain measurements can
justify bounded confirmation, refinement or correction, with adverse results and
original conditions retained. Current comparisons belong in
[BENCHMARKS](../docs/BENCHMARKS.md).

[R330 XML traversal/clear](results/2026-11-13-xml-traversal/README.md) adds one funded
nonrecursive tree iterator and preserves old attribute/text/child aliases through
clear. The first unconditional async pull is8–10%slower; a shared synchronous native
path with real-suspension protocol handling becomes effectively flat in B701
(elapsed−1.05..+2.57%, widerCPU−0.36..+1.98%). Native iter intentionally replaces
findall in this comparison; no engine-wide speedup is claimed. Heap savings0.41%
and settings+0.63%remain separate. Ushort depth gives no object-size benefit and is
withdrawn. Retain compatibility/working-storage benefits and explicit small debt;
4KiBspare funds wide traversal while materialization refuses/refunds in the paired
regression. Original/narrowed/final sources and all adverse observations survive.

[R329 iterative XML selection](results/2026-11-13-xml-selection/README.md) removes
frontier lists and per-candidate UTF16 tag conversion, adds namespace-aware
descendants and stops `find` early. Complete settings/report allocation falls44.83%;
same-image reversed-order B699 supports XML elapsed−35.76..−35.55% with wider CPU
support. Original search+2.99..+7.08% remains; confirmation spans−3.39..+5.08% with
6.72%candidate-process spread. Retain simplification/semantics with explicit search
uncertainty, not a universal speedup; stop unchanged retiming at this checkpoint.

[R328 XML UTF8 input](results/2026-11-13-xml-utf8-input/README.md) removes the
whole-source UTF16 cache request with a bounded managed TextReader. Complete XML
summary allocation falls10.57%; B697elapsed−13.22..−7.63% has wider CPU support,
with search controls about±2%. Process spreads and exact magnitude remain
diagnostic. B696's fixture-schema failure before Ready is preserved; corrected
manifest goldens and identical runtime images pass. No whole-runtime claim.

[R327 native-entry instruction attribution](results/2026-11-13-native-entry-instructions/README.md)
resolves all46entry samples against the same process's Tier1 code. Live-default
dictionary work leads; prolog/epilog each receive three samples. This does not
establish a dominant removable activation or packet, so another arena or calling
convention is not justified by this capture. Unknowns and mixed useful work remain.

[R326 final-slot binding](results/2026-11-13-final-slot-binding/README.md) removes
the separate positional binder and entry selection. The first form loses local
inlining and has adverse controls. Moving general supplied-form parsing out of
common completion preserves one binder; refined search timings are effectively
flat, text remains inconclusive. Retain the simplification without a speedup claim.
Both forms survive; full platform/stress/installed qualification passes43493784585.
The [warmup correction](results/2026-11-13-final-slot-binding/warmup-boundaries.json)
distinguishes full-request wall budgets from actual guest warmup totals without
changing raw observations or timed medians.

[R324 word-buffer loan](results/2026-11-13-word-buffer-loan/README.md) consolidates
private cursor/range storage and removes per-line transaction packets. Complete
captured-text allocation falls35.1%; search allocation is flat. Initial B690text
timing is adverse and search controls move. Same-image B691longer confirmation
supports modest text CPU direction; exact magnitude/search/tail latency remain
uncertain. Both original and confirmation survive. The reentrant-retirement regression
fails the old slot lifetime and passes after clear-before-dispose. No universal
tagged values, second pool or return dispatcher is introduced.

[R322 native JSON byte input](results/2026-11-13-json-byte-input/README.md) retains
compatibility/ownership improvements with no CPU gain claim. B687's unchanged str
control is adverse; cold byte decoding is separated and a duplicate str execution
check removed. B688 and same-bit B6891s confirmation retain moving controls and
uncertain small debt. All raw data, original source/binary and final diagnostic-only
delta remain explicit. This does not close the whole-script objective.

[R321 complete-script CPU attribution](results/2026-11-13-whole-script-cpu-attribution/README.md)
finds different text/search costs. Owning frames include useful work; unknowns and
allocation attribution remain unresolved. Current dictionary accumulation already
uses one probe/unboxed counters; no second representation or repeated arena is built.

[R319 positional ZIP](results/2026-11-13-positional-zip/comparison/README.md):
source976f9439/frozen97bbeff3 removes full-archive acquisition/retention on capable
opened files, sharing one parser and genuine async operations. ZIP64 order,
physical overlap bounds, foreign-header errors and name-index admission improve.
B684 keeps2partial jobs after underestimated untimed fixture costs; no invoice/final
gate. Same-image B685 sizes batches by full wall time with unchanged guest clocks
and240s owner, completes3jobs/127.465s/all gates. Large archive elapsed−79.1..−77.3%
with reference variability and wider CPU support; small elapsed+4.75..+5.31%
(roughly2.2–2.5µs), wider CPU uncertain. Invoice control is close to flat. Retain
coherent bounded storage; record small-case debt without an automatic size switch.
Initial mutable-reference mismatch and all partial/adverse conditions survive.
Original delivery43461763537 fails one stale overlapping-read expectation on both
platforms; CPython golden confirms rejection. Keep first-info/listing, append refusal
and unchanged-input assertions;120focused cases pass per11/10. Runtime bytes stay
unchanged; corrected full delivery passes43465190359 on both platforms/targets,
original stress and installed consumers. Publication remains postponed; the wider
captured-text/search performance gaps remain open.

[Qualified-source CPython refresh](results/2026-11-13-fixed-definitions-cpython-checkpoint/README.md):
B683 completes7jobs/296.496s on frozen5a184a1d. Invoice warm1.25–1.27×,
compile1.09–1.11×, compile-to-result1.21–1.24×; captured text1.60–1.63×,
search1.68–1.72×, JSON0.84–0.86× and report1.21–1.25×. Report maxIQR5.63%
and JSON3.24%remain visible. All identity/golden/quiet/join/archive gates pass;
no later ZIP source or cross-collection gain is inferred. Original space-floor
refusal and canonical byte/inode/alias retirement proofs survive. The current
guide now leads with these five warm workloads and separates compilation,
startup uncertainty, historical cases and source-pair allocation observations.
Original full guide is archived; publication remains held and the roadmap active.

[Corrected fixed-definition delivery](results/2026-11-13-builtin-core-definitions/delivery/README.md):
runtimecb8736ff/frozen5a184a1d, test correction90de765d, GREEN43450480558.
Windows/Ubuntu actual11/10, original stress and installed consumers pass;573frozen
inputs match the exact commit and16step counts/full logs are cached. The intermediate
cb Ubuntu10 process-global allocation failure remains preserved. Same-thread
observation asserts thread identity and retains the65536copy-sized bound, refusal,
no-publication and final balance. Historical7ee clocks stay source-specific; no
cold-fix CPU inference. The independent positional ZIP prototype is outside this
qualification. CPython refresh is active on the qualified frozen image, after an
unchanged disk-floor refusal; only canonical byte-proved obsolete VM working copies
were retired. Original launch conditions remain archived. Publication stays held.

[Metadata delivery correction](results/2026-11-13-builtin-core-definitions/factory-correction/README.md):
original157/7ee fails the same existing FactoryNewSlots public case on both platforms;
4681white-box pass and17500/17501public pass per platform. Subsequent gates skip.
The fourth object-new consumer, partialmethod's factory, still requires a descriptor.
Reuse canonical binding there too;86internal/802public focused11pass with the
existing assertion unchanged. Failed full logs and exact573-source proof survive.
Corrected-source delivery and CPython refresh are separate; no cold-fix timing claim.

[Fixed immutable builtin definitions retained](results/2026-11-13-builtin-core-definitions/README.md):
7ee65002 removes per-invocation object/type construction and the builtin-new wrapper,
reuses canonical inherited binding and prevents internal dataclass mutation. Views
stay caller-owned; mutable callable hierarchy caches remain per execution.94internal/
683public Release11 pass. Factory allocation10248→7680B, separate from adverse local
clocks and complete-script results. B681 is partial after my unsupported fresh lane;
all failure/RAM evidence is preserved. Corrected same-bit B682193.673s passes all
gates: modest config compile-to-result/report direction, other CPU small/uncertain.
No universal gain or external-ratio update; full delivery pending, objective continues.

[Selected native call entry retained](results/2026-11-13-selected-native-call/README.md):
485e008e removes impossible post-selection decline branches/duplicate eligibility
and the native body's duplicate logical nesting charge; managed stack validation
remains.186internal/440public Release11 pass, including the actual256KiB continuation.
B680229.223s supports modest captured-text/invoice direction with CPU. Search has
roughly7%unchanged-control movement; JSON/report are inconclusive. All adverse
controls remain; no allocation or new external-ratio claim. Committed6237552b is
[fully qualified](results/2026-11-13-selected-native-call/delivery/README.md),
with573source inputs matching cached GREEN43435357297 and16step summaries.

[Isolated dev80 bridge](results/2026-11-13-regex-dev80-bridge/README.md):
exact15b248dc packages; remove the obsolete validation-option argument from the
development bridge.88internal/534public pass per actual11/10; normal29files and
573core sources remain unchanged. Core stays decoupled, normal pin0.4; precise
producer gaps defer affected final adapter qualification only, not development.

[Byte-bound window capacity, withdrawn](results/2026-11-13-text-window-capacity/rough/README.md):
cf762db1 deletes the counted scan. Allocation remains effectively flat; search/text
timings have moving controls, JSON has a small supported gain. Peak charged root
rises137% for search and25% for JSON, including unused retained headroom; these are
allowance estimates, not heap measurements. Added window state/earlier rejection
does not justify that limited benefit. Restore qualified production and retain
observable admission/mixed-read regressions; all raw adverse results survive.

[Counted window-header capacity, not promoted](results/2026-11-13-text-window-capacity/counting/README.md):
fd258a65 passes198internal/106public Release11 checks. B676 five complete warm
jobs228.141s remains inconclusive with moving controls; no allocation/CPU gain
claim. Refine by deleting the counting scan and admitting a conservative byte-based
capacity bound; retain extra-headroom/earlier-denial tradeoff and all original
adverse conditions. No scalar credits, failed-design label or percentage veto.

[Current file-search execution gate](results/2026-11-13-file-search-source-gate/README.md):
40d92aef/91d,905guest leaves/4.59CPU s,20.0%unknown/0ambiguous; all source/output/
ownership/quiet/join/SHA-RAM-archive gates. Actual source has a live keyword-only
default. Native entry/dispatch includes real work; buffered sync reads already
avoid per-line async state. A bounded remaining-window header-capacity admission
candidate removes per-line transactions, without a call arena or inferred CPU gain.

[Frontend identifier source gate](results/2026-11-13-frontend-name-source-gate/README.md):
six current sources on40d92aef/91d, valid public compilation and all shared balances
return to baseline. Parser18–62KiB/full compilation86–243KiB; modeled duplicate
identifier strings only0.41–0.95% of full allocation, with explicit model limits.
No name cache, visitor merger, runtime prototype or CPU-share claim. Larger shared
representation work requires substantive evidence; this is not a permanent veto.

[Same-stream native integer intermediates retained](results/2026-11-13-native-integer-chains/README.md):
91d removes16384unpublished boxes/524288B per4096invoice records without shadow
slots or a new engine. Allocation−8.46..−8.48%(rich−6.55%); controlled invoice
warm−6.55..−2.91%/fresh−6.03..−3.78%, supporting CPU. Original adverse report and
longer-warmup confirmation retain moving controls: report remains unresolved debt,
no universal gain or inferred CPython ratio.135internal/216public unique focused
cases pass; initial invalid fixture retained/corrected. Source-matched40d92aef full
delivery is[cached GREEN43417375221](results/2026-11-13-native-integer-chains/delivery/README.md).

[Qualified native-return CPython refresh](results/2026-11-13-native-return-cpython-checkpoint/README.md):
B671 six jobs233.888s on85c73469/8f, no later integer prototype. Invoice warm
1.27–1.31×CPython3.13.16, compilation1.08–1.09×, fresh1.29–1.31×. Captured text
1.65–1.75×, search1.75–1.82× and JSON0.82–0.86× retain moving controls; all raw
spreads/goldens/ownership/quiet/join/archive evidence survives. No multiplied
speedup, cross-collection attribution or whole-script objective completion.

[Final tuple-result transport retained](results/2026-11-13-native-tuple-return/final/README.md):
8f69261d removes transient tuple/backing, caller scratch/shared declaration/duplicate
publisher and the caller-output span from hot state.110internal/183public focused11
pass. B669180.051s: invoice warm−5.07..−1.80%, fresh uncertain, old report regression
not repeated; small text debt explicitly retained. Final Windows allocation run
stalls; targeted B670 on quiet VM completes15.062s using the existing driver with
only its fixture-count guard relaxed. Allocation−6.408..−6.436%(rich−5.049..−5.052%),
all goldens/balances/cleanup/identity/quiet/join/archive gates pass. Earlier failures
and adverse phases preserved. Committed85c73469 is source-matched and fully
qualified by[cached GREEN43411564505](results/2026-11-13-native-tuple-return/final/delivery/README.md).
No universal CPU claim or new external ratio. Performance/independent regex continue.

[Direct caller tuple operands](results/2026-11-13-native-tuple-return/operands/README.md)
883c removes caller scratch/shared buffer/duplicate publisher;110internal/181public
plus7overlap cases and8scripts pass. Allocation−6.4%(rich−5.1%). B668180.434s gives
invoice warm−1.31..−0.43% with CPU, fresh/text uncertain, report+1.77..3.04% debt.
Final simplification removes the result span from hot native state, keeping it only
in the synchronous return wrapper; focused checks/changed-source decision pending.
No promotion, unchanged retiming or new external ratio; all adverse phases retained.

[Tuple-return scratch phase](results/2026-11-13-native-tuple-return/scratch/README.md)
activates the intended path: invoice allocation−6.4%(rich−5.1%),110internal/
181public/eight scripts pass. B667180.336s improves invoice warm0.5–2.9%/fresh
1.8–5.3% with CPU, but text regresses1–5%. Refine to already-admitted caller slots,
deleting128B scratch/shared declaration/span-publication helper. Wider/narrower
overlap and old stack/cancellation/ownership coverage precede the changed-source
control-debt decision. No promotion or inferred CPython ratio.

[Native tuple return, initial inactive guard](results/2026-11-13-native-tuple-return/initial/README.md):
23eb passes110internal/180public and eight scripts, but invoice allocation is flat.
Compiled facts prove unrelated owned-slot maxima block the intended call. B666
four jobs180.263s preserves flat warm/adverse fresh/moving controls. Refine actual
bounded input cleanup eligibility; no unchanged retiming, promotion or CPU claim.

[Current invoice source gate](results/2026-11-13-invoice-current-cpu-source-gate/README.md)
profiles qualified93 after the measured R305 change:1603leaves/8.06CPU seconds,
8.36%unknown,0ambiguous. No dominant ordinary-frame phase; useful work remains
inside native/ordinary dispatch. Tuple construction46leaves plus immediate
helper-return unpacking selects bounded direct tuple-result transport. No universal
tagged values/larger tuple headers; exact semantic/admission/cancellation and whole-
script acceptance precede retention. Raw inherited metadata correction explicit;
all output/ownership/quiet/join/persistent-SHA gates pass. No speedup claim.

[CSV shared long-field backing withdrawn](results/2026-11-13-csv-shared-fields/refined/README.md):
initial21d0 allocation−3.50%, CPU uncertain; descriptor-derived277c deletes parser
counter/reset state but adds a pass. Four-job B664169.149s shows rich warm crossing
zero/fresh+0.65..2.88% adverse. Restore qualified production, force nonincremental
build;117internal/372public CSV checks pass. Six ownership/cancellation/refusal
regressions retained; no dormant CSV alternative. Initial/adverse/incomplete
desktop evidence remains. No fixed-percentage veto or CPU claim; future work
requires a changed storage/copy premise.

[Shared immutable repetition](results/2026-11-13-immutable-repetition/README.md)
fixes reproduced unfunded/late-admitted bytes output, shares bounded copying and
removes tuple's duplicate late admission. Final93a6d134 passes192internal/339public
focused Release checks and CPython count semantics. Initial6f artifact/seven-script
diagnostics pass both APIs with flat allocation; moving/adverse desktop clocks
remain diagnostic. Incorrect initial oversized-count expectations survive; full delivery is
GREEN43390871843 for14dd45b7/93a6d134. The broader performance objective remains open.

[Captured-text CPU source gate](results/2026-11-13-captured-cpu-source-gate/README.md)
records1502guest samples on committedc5d05cff/3a2c1cf6. Compiler/source checks rule
out mirrored locals, per-line ordinary frames, repeated Unicode sizing/mapping and
nested allowance locks as proposed duplicated work. Existing dictionary/preparation
frames include useful work; no new runtime prototype or speedup is selected.
All output/ownership/quiet/join/RAM-archive gates pass; exact uncertainty survives.

[Pre-admitted integer arithmetic](results/2026-11-13-integer-admission/README.md)
consolidates four integer operations and math/Counter consumers, removes modulo
quotient/product intermediates and fixes demonstrated allocation before refusal.
Frozen3a2c1cf6 improves complete invoice warm7.2–8.2% and fresh7.2–9.3% against
qualified2e74ef4f, supporting wider CPU; allocation is flat. Text/JSON controls cross
zero.115internal/1049public focused Release checks and eight scripts/both APIs pass.
All original failed/adverse conditions survive; integer delivery is
GREEN43384587266 with cached source-matched platform/stress/package evidence.

[Managed continuation call handoff](results/2026-11-13-native-call-stack-handoff/README.md)
corrects a demonstrated R303 stack overflow. A controlled owned256KiB continuation
returns structured failure with qualified6b468bf1 but crashes2f34a48f with0xC00000FD.
Corrected2e74ef4f uses existing logical depth to hand deep calls to the linear runner;
no new guest limit/counter or native stack telemetry. Direct/mutual recovery and
finite100-depth calls pass, with66focused Release cases per actual target. Original
failed CI, inconclusive probes and fatal traces survive; full delivery is GREEN.
[Current external/source-pair checkpoint](results/2026-11-13-invoice-cpython-checkpoint/README.md) qualifies corrected2e shallow gains, with remaining external gaps.
Older R303 performance observations keep their original source labels.

[Composed executable calls](results/2026-11-13-composed-native-calls/README.md)
remove eager caller materialization for completing helper compositions. Retained
refinement2f34a48f versus qualified6b468bf1 improves complete invoice warm8.0–9.8%
and fresh7.0–13.1% with supporting wider CPU; fresh controls widen the range.
Captured warm0.1–3.3% with3.1% reference-control movement remains unresolved debt.
Initial628ab680 establishes warm−19.51%/fresh−18.97% allocation, separately labeled.
All original/adverse controls, failed iteration conditions and raw sources survive.
Retained2f34a48f also confirms warm−19.51%allocation,120internal/193public.NET11
checks and eight warm goldens/balances/cleanup. Desktop controls move up27.88%; no
desktop CPU claim. Original2f fails full qualification; corrected2e is fully
qualified, with its external/source-pair checkpoint linked above.

[Invoice helper-call source gate](results/2026-11-13-invoice-cpu-source-gate/README.md)
captures1,608samples/8.09guestCPU seconds on qualifiedfc2531b8/6b468bf1 with the
completeb649cbd8 invoice workload. Unresolved leaves8.58%; useful/inlined work is
not a removable share. Source confirms composed native-eligible calls force eager
caller frames. R303 is selected for a bounded migration/whole-script decision;
no predicted speedup or elapsed/CPython comparison. All identity, golden, quiet,
join and persistent-archive-before-RAM-unmount gates pass.

[Shared annotation demand](results/2026-11-12-annotation-demand/README.md) removes
the repeated demand walk/body dictionary from unannotated and regex scripts,
using facts from lexical preparation. 816 focused actual-target checks and five
complete scripts/compile-to-result controls pass. Two combined-source quiet
compilation rows improve elapsed1–3%; CPU crosses zero and warm controls remain
variable. Allocation savings are432–456bytes on unannotated scripts; annotated
controls add eight bytes. Desktop adversity and the corrected new test expectation
remain preserved. Retained for delivery, with no substantial whole-script claim.

[Iterative JSON admission](results/2026-11-12-json-depth/README.md) removes the
inherited 64-container decoder policy and backend error builder. Existing explicit
frames retain overlap admission, cancellation and awaited ownership. 120 focused
actual-target checks and three complete scripts/both APIs/compile pass; allocation
is flat. Desktop native-async control +4.31..+5.83% remains adverse and unresolved;
no CPU gain claim. [Exact8233c969 delivery](results/2026-11-12-json-depth/delivery/README.md)
is GREEN43208107207: Windows/Ubuntu, actual .NET11/10, original stress and installed
Release consumers. All568 frozen source hashes match with explicit checkout
normalization; terminal status is cached, not replayed.

[Ordered helper prototype](results/2026-11-12-ordered-helper-prototype/README.md)
is withdrawn. One prefix/span borrow/live export removed the duplicate native
tree and passed 1,366 focused checks/six complete scripts. Shared callback
preparation fixed +40% allocation, but B617 retained material CPU regressions.
Paired profiles justified sharing positional fill; B620 still shows native CPU
+19–24% and guarded async +11–17% with stable controls. The entire migration was
removed and all 568 qualified production hashes restored. The independent partial
formatting I/O/cancellation/ownership test is retained. Original/adverse results
and collector/staging failures survive. No permanent design ban or CPU gain claim.

[R266 delivery](results/2026-11-12-conditional-helper-bodies/delivery/README.md)
is terminal GREEN43183343974, exactda9e5066: Windows/Ubuntu, actual .NET11/10,
original stress and installed Release consumers. All568 frozen production sources
match with explicit checkout newline normalization. Terminal cached; no unchanged
qualification replay. Measurement uncertainty and accepted debts remain separate.

[Current record/search CPython checkpoint](results/2026-11-12-record-cpython-checkpoint/README.md)
finishes10jobs301.542s, all identity/output/ownership/quiet/join/RAM/archive gates.
Qualified da9e5066 warm CSV1.03–1.04×, JSON-lines0.85–0.91×, guardedsearch1.81–1.99×;
captured text1.65–1.67× absolute control is stable. Compile/result stay separate;
guarded/JSON spreads and all adversity survive. Captured source-pair causality
remains unresolved. No compounded gains or equivalence claim. Bounded operand
proof identifies actual array/suspension/effect-order obstacles without a new
runtime prototype. Publication held; substantial performance work continues.

[Current CSV/report attribution](results/2026-11-12-record-call-attribution/README.md)
closes two short captures, 11.771 seconds total execution, all identity/output/
ownership/quiet/join/archive gates. Selected leaves: CSV instruction/input/result/
comparison267/1189, binding/entry/work90; report196/1068 and17, with160 formatting/
write and material JSON work. Useful work and unknowns remain explicit; these are
not removable shares. Source excludes already-shared workspace/bulk CSV/inline
strings/final UTF-8 formatting. Ordered admission and later mutable reads prevent
speculating all fields or restarting after effects. Next is a concrete operand/
lifetime deletion proof; no extra dispatcher, arena or tagged-value prototype is
justified by leaf counts alone. No elapsed/allocation or external-baseline claim.

[Conditional helper normalization](results/2026-11-12-conditional-helper-bodies/normalized/README.md)
retains the one-file compiler form after996focused actual-target checks and five
complete scripts/bothAPIs. B614 fourjobs254.822s supports guardedRun elapsed
−28.45..−22.30%/Async−25.05..−14.58%, supporting widerCPU. Native-search CPU
+0.46..+1.54% is accepted control debt; captured text remains uncertain with
9.11% candidate spread. Compilation allocation+1.02%, warm allocation flat.
No runtime unary enum/shape/guard/evaluator additions survive; raw prototype and
same-source confirmation remain immutable. Full exact-source delivery pending;
no compounded CPython ratio or causal explanation of adverse controls.

[Positional/default binding](results/2026-11-12-positional-default-binding/README.md)
withdraws the three-file form after628focused actualtarget checks/five complete
scripts and B611sixjobs170.297s. Allocationflat; guarded elapsed modest/uncertain,
nativeRun+1.45..3.13%, capturedRun+1.43..7.05%, CPU/controls uncertain. Sharing a
loop/removing projected records keeps whole ordinary work and adds control flow;
additional debt unjustified for thisform. No fixed%veto/permanent designban.
All source/cores/desktopadversity/first space-short condition survive. Qualified
source restored with freshnonincremental build/same628checks; no full replay.

[Ordinary call/body source gate](results/2026-11-12-ordinary-call-source-gate/README.md)
retains exact983bab96 source facts and one6s guarded-search profile:1006guest
leaves/5.09CPU-s,35unknown. Binding80/entry-work91/dispatch206 top24leaf samples
include useful work, not removable shares. No duplicate frame/array exists;
ordinary helpers still prepare binding/work and dispatch. Next design must remove
a coherent call/body stage, preserving native gains and slower arena/prefix lessons.
All identity/output/ownership/quiet/join/RAM/archive gates pass; no timing claim.

[Current output/text CPython checkpoint](results/2026-11-12-output-cpython-checkpoint/README.md)
completes12jobs325.440s, allgates/join/RAM/archive pass. Retained983bab96 warm
UTF8report1.26–1.33x, UTF16report1.44–1.51x, capturedtext1.62–1.67x; ordinarytext
0.95–1.11x variable. Compile/compile-to-result separate; no singlechange
attribution or equivalence. Original flush-copy CI failures and fixture-only
correction survive. Full correctedhead qualification pending, publication held.

[Final-byte text storage/batching](results/2026-11-12-text-output-batches/README.md)
preserves the original forms and retained refinement. Whole-file staging raises UTF16
allocation6.8%/peak208KB; bounded batches instead reduce report allocation12.7%
UTF8/19.2%UTF16 and peak494KB.4574focused actualtarget checks and complete-script
bothAPI/compile goldens/balances pass. B607tenjobs277.347s allgates/join/RAM/archive
pass. CPU is mixed; additional asyncUTF8report+2..8.61% debt requires a bounded
completed-receiver refinement before promotion. Original desktop adversity,
space-short inventory and baseline codec-error defect/goldens survive. No new
externalbaseline or final delivery claim; wider roadmap continues.
The completed-operation refinement passes8616focused actual-target checks;
B608sixjobs169.344s allgates pass. Async report CPU falls4–6%UTF8/2–6%UTF16;
elapsed mostly uncertain, smallCSVelapsed+0.03..1.52% debt accepted. Retain final
encoded/bounded storage, preserving all prior adversity and separate-base debts.
Full exact796c3218 delivery is terminal GREEN43166350709/cached: Windows/Ubuntu
actual11/10, original stress and installed Release consumers. Production matches
983bab96/b2cb; first fullrun's obsolete flush-copy fixture failures and correction
are preserved in the delivery supplement. Representative B609 checkpoint above
keeps its original at-collection status; no timing uncertainty is erased.

The retained cell-write runtime `f4f44caf` is fully qualified by terminal GREEN
[43155450349](results/2026-11-12-cell-write-authority/delivery/README.md):
Windows/Ubuntu actual .NET 11/10, original stress and installed Release consumers.
The measured frozen sources match the commit. Its original pending-at-collection
records and all adverse observations remain unchanged; publication stays postponed.
Further final-encoding storage work is a separate prototype.

[Cell-write authority](results/2026-11-12-cell-write-authority/README.md) retains
common Value assignment/cachedownedslot, removing repeated mapping/ownerstore/
duplicatemirroring. Activecacheguard and necessaryresume sync remain. Independent
baselineprobes uncover nonlocalcomprehension hidden cellcorruption and mirrored
generator staleclasswrites/deletion; focused regressiongoldens bothAPIs pass.
9184actualtargetchecks,3correctedCPythonprobes and3whole scriptsbothAPIs pass.
B606fourjobs114.479s allgates/joins/RAM/archivepass, capturedRunelapsed-8.91..-3.17%,
Async-6.96..-4.75%, supportingCPU/allocationflat. AdditionalordinaryRunCPU+.77..+1.54%
accepted versus7114; reportelapsedvariable. Initialinactivecache/refreshdeletion
form and stagingmissingpath failure preserved. Full delivery pending, widerroadmap
continues; no unchangedretiming or scalarproject inferred.

The direct-call runtime7114021e is fully qualified by terminalGREEN
[43148511620](results/2026-11-12-completed-call-lifecycle/delivery/README.md):Windows/
Ubuntu actual11/10, original stress and installedReleaseconsumers. Receiptcached;
no unchanged requalification. Further cell-write work and latent lexical/generator
bug fixes are separate prototypes; their evidence cannot inherit this qualification.

[Direct completed-call lifecycle](results/2026-11-12-completed-call-lifecycle/README.md)
retains five-file state/control migration for delivery. Pendingfunction/receiver and
runner preparation are removed; actualchildren keep funded result/error staging.
12938focused actual11/10 and7complete scripts bothAPIs/compile pass. Allocationflat;
B605ninejobs251.294s allgates/joins/RAM/archivepass. GuardedAsyncelapsed-8.61..-6.75%,
nativeRun-4.62..-3.22%, ordinaryRun-3.62..-.82%, supportingCPU. GuardedRunelapsed
uncertain, captured/report/CSVvariable; all initial desktop adversity remains.
Accept additional CPUuncertainty versus82ed explicitly, separately from earlier
82ed/2ec debt. Full delivery pending; continue wider roadmap, no isolated completion.

[Completed helper-lifecycle captures](results/2026-11-12-helper-lifecycle-costs/README.md)
record986guarded leaves/4.99guestCPU seconds and898native leaves/4.55seconds.
Binding/workstate/pending transitions are real ordinary work; allocations already
nearly equal. Labels overlap useful/inlined work, not removable shares. All37raw
payloads/goldens/allowance/quiet/join/archive/RAM gates pass; terminalcached. Select
consolidating completed ordinary call preparation/publication into the existing
runner, preserving native binding and actual suspension; no new dispatcher/arena.
No unchanged capture replay or speed forecast.

The retained receiver runtime82ed92d4 is fully qualified by terminalGREEN
[43139754244](results/2026-11-12-receiver-operations/delivery/README.md):Windows/
Ubuntu, actual11/10, original stress and installed Release package consumers.
Terminal cached; no unchanged requalification or publication. The next source
investigation profiles guarded/native helper lifecycles with equal allocation.

[Primitive counting increment](results/2026-11-12-counting-increment-withdrawn/README.md)
is withdrawn.574focused checks and three complete scripts bothAPIs pass, allocation
flat, diagnostic elapsed mixed with adversity preserved. Replacing a checked
BigInteger argument with a plain long creates an implicit range precondition;
restoring enforcement recreates the check, a new wrapper adds machinery. The
constant was already unboxed outside the loop. This small contract tradeoff does
not justify retention or a quiet campaign. Exact82ed production restored; continue
with the complete ordinary helper lifecycle rather than another local threshold.

[Affected CPython checkpoint](results/2026-11-12-receiver-cpython-checkpoint/README.md)
refreshes retained82ed92d4 captured/native-search/guarded-search across compile,
warm and compile-to-result. Ninejobs/247.974s all gates/joins/RAM/archive pass.
Warm ratios1.73–1.79x,1.61–1.64x,2.30–2.33x; substantial guarded gap remains despite
independent source-pair gains. No attribution across collections. Original cutoff/
summary postcondition failures survive; raw source and observations unchanged.

[Shared receiver operations](results/2026-11-12-receiver-operations/refined/README.md)
retain one canonical selector/shape/invocation across ordinary prefixes and native
helpers, plus awaited async replacement count protocols. 4562focused checks pass;
seven complete scripts bothAPIs/compile pass full goldens and balances. Guarded
search allocation−71%, quiet elapsed−23..−30% with supporting CPU. Native searchRun
and capturedRun carry additional CPU debt versus2ec; other controls remain variable.
All first-form/refined adversity and original failures preserved. Full delivery
and new external CPython rows remain separate; continue the wider roadmap.

[Whole helper-route counterfactual](results/2026-11-12-native-route-costs/README.md)
isolates native-body removal without changing normal production or preparation.
Two complete unchanged scripts pass both APIs/goldens/balances/cleanup. Search
adds~451100B/command (~440B/call), captured text only64B/command; live peaks add
160/144B. Desktop timing mixed, captured controls15–24%; no general CPU inference.
Existing caller chain/deferred prefix already avoid generic frame construction.
Source selects shared canonical receiver operations to avoid unnecessary promotion
for nonzero-argument text calls; preserve native helpers and genuine dynamic paths.
No further counterfactual collection, public switch or VM rewrite selected.

Both [linear native proof forms](results/2026-11-12-native-proof/README.md) are
withdrawn. Refinement removes per-node packing but adds no allocation benefit;
desktop capturedRun+7.90..+9.45% with1.44%control and capturedAsync+3.08..+3.56%
with0.47%control do not justify its added compiler/runtime machinery or another
quiet collection. This is an evidence/complexity decision, not a fixed-percent
veto or design failure inferred from B600's collector error. All forms/binaries,
adversity and incomplete observations survive. Exact qualified2ec source restored;
nonincremental actual11/10 builds and15internal/140public per target pass.
[Restoration](results/2026-11-12-native-proof/restoration/README.md). No dormant
linear-proof scaffold remains and unchanged full qualification is not repeated.

[Linear native proof first form](results/2026-11-12-native-proof/first-form/README.md)
passes initial14964focused checks and refined checkpoint3958checks across actual
targets, plus complete six-script bothAPI/separatecompile goldens and balances.
Allocation flat; desktop elapsed mixed/adverse. B600stops after11warmjobs because
the collector retained an old295s cutoff despite the expanded390s/12job matrix.
Compilation and original final gate are missing; no complete qualification claim.
Partial capturedRun elapsed+5.17..+9.37%/CPU+5.06..+9.41%, searchRunelapsed+1.16..+2.13%
do not support retention. All failure/source/raw evidence is archived, users joined,
empty RAM unmounted; late quiet gate explicitly separate. One source refinement
assesses removing new per-node packing, not unchanged retiming or permanent closure.

[Current retained-source CPU captures](results/2026-11-12-current-runtime-costs/README.md)
complete two bounded windows: captured text 1,162 leaves/5.85 guest CPU seconds,
helper search 895/4.53. Histogram/preparation dominate text labels; shared dispatch,
proof and real binding are material in search. These are overlapping labels, not
removable-cost estimates or timing gains. The next source gate assesses linear
native proof over existing postorder nodes, retaining dynamic semantics and effects.
All gates/joins/persistent SHA/RAM cleanup pass; no unchanged repeat capture selected.

[Immutable regex dev73](results/2026-11-12-regex-development-73/README.md) passes
86 internal/532 public focused checks per actual target with the unchanged Validate
development bridge. Exact loaded-package and 29-file normal-state isolation proofs
pass. Three final producer capability gaps remain unchanged; normal adapter 0.4
and core dependencies are untouched. Partial first observation and resumed terminal
checks are preserved. This is development compatibility, not final backend speed,
safety or published-package certification.

The retained native storage/input migration is fully qualified on exact source
`2ec9d67f`: Windows/Ubuntu, actual .NET 11/10, original stress and installed Release
consumers all pass. [Cached delivery receipt](results/2026-11-12-native-inputs/delivery/README.md).
The [six-script CPython checkpoint](results/2026-11-12-native-inputs-cpython-checkpoint/README.md)
finishes 18 jobs in 487.070 seconds with all identity/output/ownership/quiet/join/
archive gates. Compile, warm and compile-to-result remain separate. Warm captured
text is 1.74× CPython, search 1.52–1.60× (variable), ordinary text 0.99–1.06×
(variable), CSV 1.08–1.09×, JSON-lines 0.85–0.89× (variable), report 1.26–1.31×
(variable). This does not attribute gains to one change or settle additional async
report debt. Older pending-at-collection records below remain historical.
Publication and tagging remain postponed.

The [native storage/input candidate](results/2026-11-12-native-inputs/README.md)
is retained for delivery qualification. It keeps the initial private UTF8 word
probe while deleting captured helper packing/clearing and appended-cell indexes.
Complete text allocation falls20–22%; B597ten quiet jobs279.026s support CPU gains
for captured Run and both ordinary APIs. Earlier async-search/CSV adversity is
not reproduced, without an isolated cause/recovery claim. ReportAsync additional
widerCPU0..+6.25% versus8f862 remains accepted uncertainty; its elapsed overlaps
zero. Both source forms, original adversity and the bounded paired profile survive.
Full delivery and the next CPython checkpoint remain separate; publication held.

The [native UTF-8 word prototype](results/2026-11-11-native-word-probing/README.md)
retains the ordinary call/return ABI and existing owners while avoiding duplicate
short-word objects in private histogram segments. Complete text allocation falls
20–22%; ordinary text quiet CPU improves, captured CPU remains uncertain. B595's
ten jobs finish278.387s with all identity/cleanup/archive gates. Async search
elapsed+6.97..+11.60%/widerCPU+9.32..+17.04% confirms adverse desktop observations;
CSVRun CPU remains adverse. Matching Release policy and normalized common IL
rule out an accidental shared search-body edit, not a native-code effect. This
prototype remains active and unpromoted while its control-regression gate is
investigated. All original failed fixtures/inventory and adverse results survive;
neither allocation gains nor a fixed improvement threshold decide retention.

The [native iteration storage prototype](results/2026-11-11-native-iteration-storage/README.md)
is withdrawn after three source/core forms, each9,178focused actual11/10 checks.
Eight Python references and real pending-return cleanup/alias regressions survive.
Text allocation falls26–29%, compilation flat, ordinaryRun has supporting CPU
improvement. Final quiet searchRun elapsed+10.87..+17.24%/CPU+6.62..+9.07% and
reportAsync elapsed+3.36..+4.11% remain material debt. Removing unnecessary state
writes did not close it; exact generated-code cause is unresolved. All original
adversity survives, no fixed percentage veto or permanent design ban follows.
Exact qualified8f862 production is restored; new public regressions pass after
nonincremental builds on both targets. Next source gate assesses native word
materialization/probing without widening ordinary calls/returns. Publication held.

The [native-loop admission prototype](results/2026-11-11-native-loop-admission/README.md)
is withdrawn after3,046focused checks and a bounded quiet confirmation. Allocation
is flat; captured elapsed overlaps zero, slight ordinary-text improvement lacks
consistent CPU support, and unaffected CSV also improves. Original tight-control
desktop async adversity remains unexplained. A longer admission lifetime/cleanup
flag is not justified by this evidence; exact qualified production is restored
and nonincrementally rebuilt on both targets. No permanent design veto follows.

The [current construction captures](results/2026-11-11-current-construction-costs/README.md)
reuse qualified8f862 source, existing allocation-tick collector and two complete
scripts. Zero lost events; all goldens/balances/joins/fixture cleanup pass. Type
trigger counts are not exact allocation totals or CPU attribution. Source identifies
one64B private iteration carrier per3072text lines:196608B, about17.5%of prior
complete allocation. This supports a source gate for coherent native iteration/
return storage consolidation, without a pool/cache or predicted gain. Real record
storage, host-phase uncertainty and already-deferred frames remain explicit.

The [current storage-cost profiles](results/2026-11-11-current-storage-costs/README.md)
measure retained8f8628a4 captured text/Run and report/RunAsync:1148/1020guest leaves,
71/60unknown, actual current code/maps. Word preparation, dictionary/native
iteration and report dispatch/format/write remain material associations, not
removable percentages. Source confirms shared frames/workspaces and direct UTF8
formatting already ship. The next bounded source gate assesses one native-loop
admission scope while preserving effect order/reentry/stack safety; the subsequent
prototype and withdrawal are linked above. Two short windows, all gates/join/archive pass; original failed
preparations preserved. Full delivery and publication remain separate.

The [current dictionary-storage checkpoint](results/2026-11-11-dictionary-cpython-checkpoint/README.md)
measures retained8f8628a4 against stockCPython3.13.16 with the same six scripts,
fixtures, workers and separate compile/warm/compile-result boundaries. All18jobs
finish486.404s with identities/output/ownership/quiet/join/archive checks passing.
Warm captured1.77–1.84×, ordinary1.06–1.11×, CSV1.04–1.05×, JSON-lines0.84–0.87×,
report1.28–1.35×variable, search1.54–1.57×. Previous source-labelled rows survive
verbatim in the prior guide. No individual-change attribution or report CPU-debt
resolution. Full delivery is separate; the roadmap/publication hold continues.

The [native dictionary value change](results/2026-11-11-dictionary-integer-storage/README.md)
retains exact managed counts inside already funded entries, materializing stable
immutable values on reads/copy publication. Text allocation falls14.90–16.13%,
CSV3.13% and JSON-lines4.20% on both APIs; compilation flat. Refined quiet text
Run improves directionally with supporting widerCPU. Report widerCPU+1.91..+6.38%
remains accepted debt; CSV elapsed inconclusive. Corrected full43087005596 is
GREEN/cached on both platforms, actual11/10, stress and installed consumers;
original callback-deadline failure remains preserved. Original/refined adversity, JIT structural correction and initial archival
failure/recovery survive. No universal neutrality, additive gain or new CPython
ratio claim. The performance objective continues.

The [compiler-known argument binding study](results/2026-11-11-known-call-binding/README.md)
withdraws both measured forms. Runtime allocation stays flat; compile allocation
rises0.38–0.54%. Refined quiet CSV improves slightly, synchronous search remains
adverse and asynchronous search is inconclusive. Both exact source/core pairs,
all controls, failed conditions and raw archives survive. No mapping layer or
architecture-only tests remain; useful public semantics/lifetime tests survive.
This decision does not permanently veto coherent call/frame redesign.

The [current native-call profiles](results/2026-11-11-current-native-call-costs/README.md)
use actual690 maps/code and retain unknowns. Captured1139leaves/5.74CPU seconds
and keyword-default search872/4.42 associate work with proof/binding/evaluation;
these are not additive removable percentages. The next source gate assesses
eliminating a repeated structural proof traversal while preserving complete
pre-effect validation, live mutable inputs and the existing execution engine.

The [six-script current checkpoint](results/2026-11-11-captured-helper-cpython-checkpoint/README.md)
uses retained `6904093e` versus stock CPython3.13.16, separating compile, warm and
compile-to-result. All18jobs finish in487.016s with verified outputs/identities/
ownership/quiet/join/archive. Captured warm remains1.96–1.97×, ordinary1.16–1.19×,
CSV1.06–1.09×, JSON-lines0.84–0.85× and keyword-default search1.54–1.59×. Report
1.25–1.38× has moving controls and adverse ratio versus earlier evidence; both
engines' absolute times improve, so no source regression is attributed. All
original data/source labels survive in the prior guide; no universal neutrality
or individual-change attribution. The objective continues beyond this checkpoint.

The [R243 captured native sequences](results/2026-11-11-native-closure-sequences/README.md)
are retained in `6904093e`. Existing native input storage carries live cells;
proven stores/continuations omit the complete binding/workspace/dispatch phase.
Runtime allocation stays flat. Quiet captured elapsed improves10.56–15.17% Run/
16.59–18.16% RunAsync with supporting wider CPU; ordinary text is inconclusive
and CSV +0.49..+0.89% remains debt. Initial extra-parameter adversity, refined
desktop CSV/report adversity, exact source pairs and all controls survive.
4742 focused actual11/10 checks and ten isolated Python goldens pass. Full
platform/stress/installed-consumer delivery is tracked separately. The original
19–20% native-helper-return allocation result is a different milestone.

The [R241 compiled-workspace consolidation](results/2026-11-11-mirrored-function-storage/README.md)
is retained in `5f9059fa`. Mirrored functions use the same admitted final array
and existing runner as other compiled functions, preserving real namespaces.
Complete local-record CSV allocation falls4.92% through both APIs; the original
desktop sync slowdown does not recur on the quiet VM, without explaining its
cause. Quiet affected elapsed improves; ordinary CSV control+0.97..+2.83% remains
explicit uncertainty. 4744 focused actual11/10 checks pass; subsequent full
Windows/Ubuntu, actual11/10, originalStress and installed consumers are GREEN.
All source pairs, cores, controls and preparation failures survive.

The [R242 current call-cost assessment](results/2026-11-11-current-call-costs/README.md)
preserves two short profiles of qualified5f executable source, including unknown
and inlined work. No source-pair timing or removable CPU claim. No blind binding/
work-state merger or raw-word/scratch prototype is selected. The next complete
phase selected captured helper sequences using the existing native-body
proof/evaluator; R243 above records its subsequent validation and retention.

The [R240 private-total segment](results/2026-11-11-private-counter-segments/README.md)
is retained in `b5849b04`. Whole-script text allocation falls9.04–9.72% through
both APIs. Ordinary sync improves directionally in two quiet collections;
captured/async clocks remain uncertain. Original report adversity does not recur
with fresh processes but remains unexplained and preserved. No universal CPU or
control-neutrality claim. Exact `b5849b04` passes Windows/Ubuntu, actual11/10,
original stress and installed consumers, covering R239/R240 together.

The [current native-word profile](results/2026-11-11-native-word-costs/README.md)
selects the next complete boundary after R237, retaining unknown/inlined work.
It does not forecast removable CPU or reject untimed word-transport ideas.
The [native helper refund correction](results/2026-11-11-native-helper-refund/README.md)
removes duplicate cleanup after a refund callback throws: both APIs previously
refunded 160 retained bytes twice. Committed `8314c02d` passes 960 focused actual11/10
checks; combined `b5849b04` subsequently passes full platform/stress/package delivery.

The [R237 complete counting-loop milestone](results/2026-11-11-native-counting-loops/README.md)
is retained in `fe6648b2`. Existing native iteration and arithmetic remove repeated
dispatch and argument preparation. Quiet captured-text sync improves about9–11%;
other text shapes/APIs improve directionally with wider controls. Allocation is
flat, desktop clocks remain mixed/adverse and CSV neutrality is unresolved.
All source pairs, binaries, failed fixtures and controls survive. Exact `fe6648b2`
passes full Windows/Ubuntu actual11/10, original stress and installed consumers.
The [three-script CPython refresh](results/2026-11-11-native-loops-cpython-checkpoint/README.md)
keeps compilation separate and shows a remaining captured-helper gap.

The [R236 call-preparation migration](results/2026-11-11-call-preparation/README.md)
is withdrawn after a complete prototype and source-justified refinement. Common
paths already borrow; allocation stays flat and clocks remain mixed/adverse.
The initial captured-text slowdown did not recur, without establishing causality
or CPU gain. Retain independent Python factory fixes, direct admitted exception
tuples and deletion of sync argument carriers in698f7e77;11074 retained checks pass
across actual11/10. All other prototype code is removed. Preserve both source
pairs/cores/controls and failures; no unchanged full qualification replay.

The [R235 captured-operand trial](results/2026-11-11-captured-operand-transport/README.md)
is fully withdrawn. A source-justified cold-span refinement preserves the initial
adverse conditions; runtime allocation stays flat and quiet CSV/text shows no
convincing overall gain, with captured text unstable. No fixed% veto or permanent
design rejection. All568 production files equal qualified15f; fresh restored
build23.22s/zero warnings/errors and530 focused checks pass. Keep independent
span/lifetime/cancellation/refusal coverage, remove encoding-specific assertions.
All source/cores/JIT/controls survive; no new bank/guard/span machinery remains.

The [R234/B574 quiet confirmation](results/2026-11-11-constructor-text-confirmation/README.md)
finishes5source-pair jobs/142.323s: combined15f/R232+R233 versus qualified4aa.
Named CSV sync/async warm−3.22..−5.94%/−2.22..−5.64%; keyword-only/captured text
inconclusive. Ordinary-text async+1.92..+2.55% with tight controls is accepted
explicit milestone debt; wider CPU mixed. No individual-change/CPython attribution.
All hashes/goldens/quiet/join/RAM/archive gates pass. Terminal cached; no repeat.
Original setup/worker-label/telemetry mistakes survive with corrections; local
R232/R233 worker is9a4272de, separately fixed quiet workerfec79. No raw data changed.

The [R233 constructor-binding consolidation](results/2026-11-11-constructor-binding/README.md)
removes two constructor-only restrictions, reusing the ordinary function binder
and frame runner for named/keyword-only/variadic compiled initializers. Complete
named/keyword-only CSV allocation−3.77% both APIs; other scripts/compilation flat.
494focused checks across actual11/10 and independent complete-script goldens pass.
Desktop clocks mixed/adverse with moving controls: no CPU/neutrality claim. No new
route/owner/API. Exact15f457f7 now separately passes Windows/Ubuntu actual11/10,
originalStress and installed consumers; terminal GREEN42980759461 is cached.

The [R232 default allocator/admission milestone](results/2026-11-11-default-allocator-admission/README.md)
recognizes the sealed staticmethod wrapper around object.__new__, enabling the
existing canonical constructor route for ordinary classes. Three separate attribute
admission/refund regressions fail qualified4aa and pass on both current targets;
93internal/143public focused checks per target pass. Constructor CSV allocation
−3.77% both APIs; desktop elapsed diagnostic, adverse text controls unresolved.
Both earlier field-storage variants are fully removed, with all original evidence
preserved. No new runner/metadata/cache/pool/API. Committed752d78ce is fully
qualified on Windows/Ubuntu, actual11/10, originalStress and installed consumers;
terminal CI42976124076 is cached and must never be queried again.

The [R231 synchronous-call contract source gate](results/2026-11-11-sync-call-contract-source-gate/README.md)
finds a real general-call packet copy, but canonical-span signatures alone reach
shared async helpers and array delegates that retain inputs. The partial178-file
migration is restored exactly; no working candidate or timing/CPU rejection.
Avoid ToArray adapter hierarchies that relocate packaging. A future migration must
move preparation and retained ownership together. Fresh restored build23.96s and
44public focused checks per actual target pass. Original errors/source survive.

The [R230 prepared-range prototype](results/2026-11-11-prepared-split-ranges-withdrawn/README.md)
is fully withdrawn. Retaining private split boundaries removes a repeated scan
but adds a range allocation/copy,20net lines and admitted replacement overlap.
Text allocation+8.48–9.11%, accounted execution peaks+163,840B; desktop elapsed
mixed/adverse with moving controls, no CPU claim. Candidate and exactly restored
qualified4aa each pass45internal/263public checks per actual target; restored
nonincremental build20.05s/zero warnings/errors. All original preparation failure,
raw clocks, source/cores and tests survive. No tiers/cache/pool or quiet campaign;
withdraw for the storage/construction tradeoff, not a fixed percentage veto.

The [R229 patch-generation workload](results/2026-11-11-patch-workflow/README.md)
adds a complete UTF-8 two-file unified-diff/write job to the existing catalog,
with independent exact patch goldens at0/16/1024lines.83file-workerchecks pass per
actualtarget; supervisedCPython3.13.2/Lython verify all3sizes and compile/warm/fresh
smoke, with output/file hashes and joined cleanup. Core remains qualified4aa;
no new framework or runtime edit. Four quiet comparison jobs finish in114.763s:
qualified4aa/9ee7 against CPython3.13.16, patch warm0.86–0.90×/complete0.85–0.86×,
compilation0.98–1.00×. Warm Lython controls move4.44%; all adverse observations
remain visible. All output/ownership/quiet/join/archive gates pass. The single-edit
fixture does not justify a difflib redesign or establish general diff throughput.

The [R228 dispatch completion prototype](results/2026-11-11-dispatch-completion-withdrawn/README.md)
is fully withdrawn. It removes a generic jump flag/implicit completion but adds22net
routing lines and preserves the necessary disposal protocol. Allocation flat across
six complete scripts; all adverse/variable desktop clocks retained, no CPU claim or
quiet campaign.1046internal/3483public eachtarget pass; exact qualified4aa restored,
forced rebuild and62internal/119public eachtarget pass. No partial branch rescue or
architecture-only tests remain. Continue with uncovered complete patch workloads.

The [R227 current helper prefix](results/2026-11-11-current-helper-prefix/README.md)
uses qualified4aa/9ee7 after R225:1166guest samples/4.37%unknown, unique same-process
Tier1 extent matches274prefix self samples. Zero materializingSplitCore stacks;
102private-preparation stacks. Useful scanning is inlined into the prefix; no
removable-share/elapsed claim. Compiler explicit terminators plus redundant loop
completion/jump-state motivate the coherent R228 dispatch gate. Original pre-worker
environment failure and guarded correction preserved; all final collection/cleanup
and archive gates pass, no remaining users. No new runtime or repeated qualification.

The [R226 immutable dev68 input](results/2026-11-11-regex-development-68/README.md)
pins accepted a7e985ed packages and unchanged Validate development bridge.86internal/
532public focused checks pass per actualtarget; loaded hashes match and all29normal
state files remain byte-identical. New private materialization/search changes
justify this binary qualification; no new public API closes the three final
producer gaps. Normal0.4/core dependency edges unchanged, no adapter rewrite or
performance/safety/final-publication certification.

The [R225 ordinary return consumption](results/2026-11-11-ordinary-return-consumption/README.md)
retains runtime4aa2d46d: existing frame/native entries share direct-return iteration
intent, removing per-record list/value-array/range construction. Captured text CLR
allocation−4.84%both APIs; coarse execution-allowance peak−15.06%, compilebytes+0.05–0.16%.
Quiet target elapsed−1.88..+0.34% remains inconclusive; modest supporting widerCPU,
ordinary control uncertainty and all initial/corrected desktop adversity retained.
A separate custom-casing metadata error is corrected: baseline1/4fail, prototype4/4,
correctedallpass.600internal/2653public per actualtarget; full delivery42953725339 GREEN.

The [R224 captured-helper cost gate](results/2026-11-11-captured-helper-cost/README.md)
records1166guest-thread samples/4.20%unknown on qualified4625/cb03.236stacks contain
materializing SplitCore; none contain private cased-whitespace preparation. Source
confirms iteration intent reaches closed native returns but is lost across ordinary
helper returns. Call-entry leaves5.23% do not establish the entire external gap.
One six-second capture/all ownership/archive gates; no elapsed comparison. R225
selects ordinary direct-return consumption forwarding, removing per-record transient
list/array/range construction while preserving actual guest lists and cleanup.

The [R223 current text CPython checkpoint](results/2026-11-11-text-cpython-checkpoint/README.md)
finishes six jobs164.378s/all gates at qualified4625/cb03. Ordinary text is warm
1.41–1.43× / complete1.35–1.40× CPython; captured-helper/counter text is warm
2.61–2.62× / complete2.29–2.37×. Compilation separate; CPython complete ordinary
control2.94% remains variable. Exact same source/fixtures per engine, all original
e543/R221 observations and setup failures preserved. The remaining helper gap
motivates a bounded current call/frame/operand attribution; no single-change claim.

The [R221 incoming lexical stores](results/2026-11-11-incoming-lexical-stores/README.md)
remove unnecessary context promotion, name walks and repeated cell synchronization
using existing defining owners. Complete counter variants reduce CLR allocation
41.4% CSV / 47.9% text / 27.5% report; quiet warm elapsed falls 16–17% / 19–21% /
12–15%, with supporting wider CPU. Unchanged-text neutrality remains unresolved;
all desktop adversity and the independent frontend diagnostic failure survive.
672 internal / 2,587 public checks pass per actual target. Runtime4625 is committed;
combined full delivery42942735331 is GREEN, both platforms/targets/stress/installed consumers. No new CPython or producer claim.

The [R220 offline admission-lock gate](results/2026-11-11-memory-lock-callers/README.md)
resolves the64previously unattributed Monitor leaves from R211:38aggregate split
and26UTF8 string/line admission chains, all immediate callers uniquely mapped.
No contention/duplicate resize/removable CPU inference follows. Reused stub aliases
are explicit, source proof and original failed map guard preserved. No new capture
or budget mechanism; incoming lexical writes become the next call/frame hypothesis.

The [R219 regex projection workspace](results/2026-11-11-regex-projection-storage/README.md)
consolidates working storage and cursor in the existing prepared operation. Two
per-match arrays/reservation and caller cursor plumbing disappear. Unicode iteration
allocation falls17–18% on both APIs; compile and CSV/text controls are flat. Desktop
elapsed/control adversity is retained; no CPU, CPython or final-adapter claim.
64internal/397public focused checks pass per actual target; combined full delivery42942735331 is GREEN.

The [R218 current JSON/report checkpoint](results/2026-11-11-json-cpython-checkpoint/README.md)
finishes six jobs165.178s/all gates at committed e122 runtime, all568source hashes.
Report warm1.18–1.23× CPython/complete1.09–1.13×; JSON-lines warm0.86–0.89×/
complete0.86–0.88×. Both remain variable; compilation is separate. Fixed workers,
adapter and complete scripts; no single-change attribution. Replaced8d44 rows,
all original observations and the misdirected CI dispatch remain explicit.

The [R217 JSON string decoder](results/2026-11-11-json-string-decoder/README.md)
replaces the complete backend string phase and removes156net runtime lines.
Final storage is admitted directly; Python error/EOF goldens, refusal, cancellation,
aliases and cleanup pass193internal/811public checks per target. Allocation is flat
for current short strings; original desktop adversity survives. Three quiet jobs
finish111.530s/all gates: escaped report elapsed−0.22..−1.85% with supporting wider
CPU, plain/text uncertain. Final cold error-plumbing cleanup is separately labelled;
no general CPU gain or completion of the whole-script objective follows.

The [R215 UTF-8 contract milestone](results/2026-11-10-regex-input-contract/README.md)
corrects malformed filesystem decoding and defines valid, stable provider borrows.
Immutable dev67 passes focused Validate and development-only AssumeValid checks
on both targets; normal0.4 stays unchanged. Allocation is flat; original Unicode
prefix adversity reverses after longer warmup and controls remain variable. Both
conditions survive; no general CPU gain or final adapter certification follows.

The [R214 bound native argument variant](results/2026-11-10-bound-native-arguments/README.md)
is fully withdrawn. Quiet bound-text elapsed−0.80..−1.58%/supporting CPU is useful,
but allocation flat, ordinary-text CPU+0.42..+1.68%possible debt and target-mutation/
finally routing do not simplify the still-required binder. Two jobs75.696s/all gates
pass. Original desktop adversity, failed four-case setup and initial non-native
class-report capture survive. All568 production sources restored to qualified e543;
independent semantic/alias coverage remains,155internal/224public per target pass.
No unchanged full qualification, current-HEAD speedup or publication claim.

The [R211 current text cost gate](results/2026-11-10-current-text-construction-cost/README.md)
captures1151execution-thread samples/5.91%unmapped on qualified e543/0e, after the
material split/loop/tuple changes. All gates/join/SHA pass. Dispatch/text/dictionary
work remains material but includes useful work; no complete redesign is selected
from shares alone. Before-effect sizing and array-free private consumption both
serve real contracts. No elapsed comparison or unchanged qualification replay.

The [R212 frontend/invocation source refresh](results/2026-11-10-frontend-source-refresh/README.md)
confirms exact visitor equality to R158 and existing semantic distinctions. Parser,
scope facts, declaration-only metadata and async worker reuse are already delivered.
No blind merger or repeated setup redesign. R213 narrows captured-declaration reconstruction to yielding generator headers,
so ordinary helpers need no such correction. Bound native argument construction
receives the next bounded gate, without a predicted speedup.

The [R209 committed construction checkpoint](results/2026-11-10-construction-cpython-checkpoint/README.md)
refreshes CSV/text against CPython at fully qualified e543bf15, with compilation,
warm and compile-to-result separated. Six jobs / 164.735 s; all evidence gates pass.
CSV warm 1.04–1.07× and text 1.36–1.47× retain 3.11% / 5.42% Lython control spread.
Original zero-job 24 KiB headroom refusal and its exact correction survive. This
does not attribute one change or resolve tuple CPU uncertainty.

The [R210 immutable dev66 bridge gate](results/2026-11-10-regex-development-66/README.md)
passes 86 internal / 524 public focused checks per target. Normal pin/source/output
identities remain unchanged. New validated-input options retain their Validate
default; no implicit whole-subject guarantee, final producer promotion, safety or
performance certification. Three missing native capabilities stay precise.

The [R208 completed tuple construction](results/2026-11-10-tuple-value-construction/README.md)
retains direct span constructors, removing native temporary reservations/late scalar
admission and discarded fresh-literal copies. The added factory and array-only
copy overloads disappear; canonical storage and real adoption/materialization remain.
CSV allocation−3.04%both APIs, compilation flat. Final quiet sync/control CPU is
inconclusive; async elapsed slightly lower with mixedCPU. Original stable syncCPU
adversity and all desktop samples survive. Possible small regression debt explicit;
no general speedup. Full Windows/Ubuntu, both targets, original stress and installed
Release consumer qualification is GREEN42898279287; local regressions also pass. This does not close the whole-script objective.

The [R207 lexical loop classification](results/2026-11-10-lexical-loop-cleanup/README.md)
retains a two-file compiler change: same-protected-suite transfers use ordinary
jumps, removing pending jump construction and runtime unwind scans; actual cleanup
exits keep existing routing. CSV allocation falls about 2.47% on both APIs. Quiet
sync elapsed/CPU is inconclusive; async CPU slightly lower, elapsed straddles zero.
Original desktop adversity remains. No new runtime machinery or general speedup;
full retained delivery qualification is tracked separately.

The [R206 qualified-source allocation refresh](results/2026-11-10-current-allocation/README.md)
uses two existing complete scripts and the unchanged collector against qualified
runtime dba779a5. Strings/integers, CSV records/reservations and the private text
cursor remain material sample associations. All identities, goldens, zero-loss,
stop/join and fixture cleanup pass. These are not exact per-type bytes or CPU costs;
no new runtime redesign is selected from allocation shares alone. Existing row
adoption and shared setup metadata are confirmed, not reimplemented.

The [R204 call-source prototype](results/2026-11-10-call-source-transport/README.md)
is withdrawn completely. Original sync text slows5–6% with supporting CPU; one
source-guided native-consumption refinement leaves sync/CSV uncertain and async
elapsed favorable. Warm allocation is flat; compile allocation+0.72–1.01% remains.
Mapped-source/captured-packet complexity is not justified here. All favorable and
adverse observations, two failed zero-job staging conditions and source variants
survive. Both experimental core aliases and the clean worktree are retired.
This is a debt/design decision, not a fixed threshold or permanent design veto.

The [R203 private split iteration](results/2026-11-10-transient-split-iteration/README.md)
retains the complete direct/helper producer-consumer change. Transient list/value
arrays disappear; allocation falls about5.2% and quiet sync text elapsed1–3%,
with supporting wider CPU. CSV is flat; report/async controls and original/refined
desktop adversity remain explicit. Source-guided refinement removes only two added
duplicate cursor checks. Full retained qualification is tracked separately.

The [R202 coarse host-grant prototype](results/2026-11-10-coarse-admission/README.md)
is withdrawn. Quiet synchronous workloads improve roughly2–4%, with supporting
report/JSON CPU, flat allocation and uncertain async controls. Cached capacity,
two estimate levels and broad contract migration outweigh retaining this policy;
the favorable evidence and228unclassified native failures remain. The independent
wide-enumerate preflight correction and useful ownership regressions are retained.

The [R201 bounded JSON string scan](results/2026-11-10-json-string-scan/README.md)
is withdrawn after an original six-job comparison and a justified two-job
confirmation. JSON-lines elapsed benefits narrow, CPU remains mixed, report
inconclusive and text controls variable/adverse; allocation is flat. The extra
token search removes no parsing/storage machinery. Both variants of the evidence
and the corrected offline caller attribution survive. Useful public UTF-8/control/
error-position and budget-reuse regressions remain against the existing parser.

The [R200 cold-body variants](results/2026-11-10-cold-prefix/README.md) are withdrawn.
Original/refined code and focused semantic gates pass; hot code/frame shrink while
an extra helper frame remains. Original/refined six-job comparisons preserve
modest report/generator benefits, text/search adversity and variable controls.
Allocation is flat. Added dispatch/frame and regression debt do not justify keeping
the code. Both variants survive as evidence, without a permanent design veto.

The [R199 current-text attribution](results/2026-11-10-current-text-attribution/README.md)
captures one six-second warm window,1144samples/6.21%unknown, all gates/join/SHA pass.
Instruction/operand transport, native dictionary/hash and text kernels remain
material; frames/binding are not prominent. Source gates confirm already-fused
case capacity, direct native hashing and no host resize on commit. No elapsed
gain, removed-tax estimate, source optimization or recapture is claimed.

The [R198 ordinary-method context simplification](results/2026-11-10-method-contexts/README.md)
removes the redundant owner-type restriction and reuses scope-sensitive function
promotion. Class-report allocation falls about28.5%both APIs; quiet elapsed falls
roughly7–10%sync/5–10%async, supporting widerCPU. Report controls and desktop CSV
debt remain. Original startup manifest failure had zero timed jobs; preserved and
corrected without changing code/settings. Full qualification is recorded separately.

The [R197 committed-source CPython checkpoint](results/2026-11-10-json-cpython-checkpoint/README.md)
measures postcommit8d44/E294 with complete report/text/JSON-lines across three
phases,9jobs245.513s, all gates pass. Warm report1.22–1.24×, text1.43–1.46×,
JSON-lines0.89–0.92×; adverse/control variability stays explicit. No single-change
attribution or completion of the broader performance objective follows.

The [R196 iterative decoder](results/2026-11-10-iterative-json-construction/README.md)
replaces recursive container completion and repeated leaf admission with one
managed decoder lifetime through awaited hooks. Original iteration alone was not
promoted; refinement retains modest report benefit/flat allocation, with text and
JSON-lines uncertainty. Full source qualification is GREEN; both variants and
original failures remain. Conservative admission can deny earlier near stack bounds.

The [R193 instruction attribution](results/2026-11-10-prefix-instruction-attribution/README.md)
maps all168CSV/269text prefix leaves to validated code-size/tier/native offsets.
Three common fetch/check/dispatch-entry blocks contain8.22/10.10%guest samples,
including required checks; no removable-tax or speedup estimate follows. It selects
a bounded exception-boundary/loop-bookkeeping assessment, not another call arena or
routing framework. Original parser/setup conditions and corrections remain.

The [R192 receiver-aware native entry](results/2026-11-10-receiver-native-entry/README.md)
completes the shared inline binding convention for eligible methods/bound aliases,
removing workspace rental/return and pending-call transport. Allocation is flat;
existing complete bound-method text improves5.2–8.5%quiet elapsed with supporting
CPU. Ordinary text/report controls and all desktop/setup adversity remain; full
delivery qualification follows separately. This does not close the larger roadmap.

The [R191 CPython refresh](results/2026-11-10-dictionary-cpython-checkpoint/README.md)
measures committed e5731404-equivalent frozen e364 on the same three complete
scripts across compile/warm/compile-to-result. Nine jobs246.994s, all gates pass;
warm CSV about1.07×CPython, text1.45–1.48×, keyword-default search1.50–1.52×.
Text/search variability and previous adverse/control observations remain. This
is not a single-change attribution; delivery CI is recorded separately.

The [R190 native dictionary accumulation](results/2026-11-10-dictionary-accumulation/README.md)
removes the eligible get/add/store region's second table probe and intermediate
transport in the existing engine. B538threejobs110.923s: text elapsed−10.9..14.9%,
CSV−1.1..5.1%, supporting widerCPU. Report remains uncertain/possible+3%CPUdebt,
allocationflat, compile+0.05..0.07%targetbytes. Source retained for full delivery
qualification; no general gain or publication. Original desktopadversity remains.

The [R189 current-qualified attribution](results/2026-11-10-current-call-transport-attribution/README.md)
refreshes exactly CSV/text after R184/R185,11.888captured seconds, all identity/
output/quiet/join/archive checks pass. Text binding/frame samples are absent;
VM transport, text and dictionary/subscript paths remain material. Keep all unknown/
thread/raw data; shares are not removable taxes. The next source gate is one native
dictionary accumulation pipeline, preserving actual dynamic fallbacks and ownership.

The [R188 deferred integer-local prototype](results/2026-11-10-integer-local-storage/README.md)
removes per-update boxing for eligible local counters. The original complete-script
allocation falls about14%text/18%search, but B535's112.766second quiet comparison
leaves CPU near neutral/uncertain. Original adversity and all controls survive.
A generated-code-supported cold-read refinement completes B536fourjobs148.225s:
small search gains, CSV/report debt and uncertain text. Both local-only sidecars
are withdrawn; useful semantic/cancellation tests remain,98focused cases peractual
target pass on unchanged qualified runtime. The allocation evidence informs future
end-to-end transport work; the broader performance roadmap remains active.

The [R176 contract-preserving small-integer assessment](results/2026-11-09-private-small-integers/README.md)
preserves public BigInteger/CLR ingress and reproduces6.91% text allocation savings.
Both wrapper and numeric-view refinement are withdrawn:58production files change,
without a simpler execution architecture or established CPU benefit. Desktop
adverse/control-limited observations and original failures remain.824focused
internal checks pass each variant; affected public checks are labelled by source.
Keep five public semantic cases; exact qualified production restored and48retained
checks pass eachactual11/10 after forced builds. No quiet/CPU or repeat platform
qualification claim. Machine integers remain a possible future coherent design.

The [R175 ordinary call-input batch](results/2026-11-09-call-input-preparation/README.md)
removes14CSV/9text/8report module dispatches in two private variants, but retains
reads/copies in a new loop. Shared prepared metadata removes a duplicate field.
Both quiet5job141s batches pass all gates; original async text+2.71..5.14%, refined
controls overlap with possible Unicode cost. Runtime allocation flat, compilation
+0.29..0.66%. Withdraw both variants completely, retain seven semantic cases and
all adverse/raw evidence. Exact qualified production restored;42retained checks
per actual11/10 pass. Initial full16931public11 and refined576/3886each target are
labelled by source. No CPU/equivalence claim or permanent design veto; objective open.

The [R174 private integer transport prototype](results/2026-11-09-machine-integer-transport/README.md)
demonstrates about6.91% less complete-text allocation, but violates the existing
BigInteger projection contract. Original and refined timings remain mixed/adverse;
no CPU claim.136numeric/protocol and42admission cases pass; two added public
projection cases fail the prototype and pass restored production on actual11/10.
Both runtime variants removed completely; exact evidence retained. This does not
close the performance objective or permanently veto a wider scalar design.

Combined delivery42684724223/source6d852edc is GREEN for R172/R173: Windows/Ubuntu,
actual11/10, original Stress and installed consumers, all eight gates. The
[cached terminal](results/2026-11-09-optional-string-index-metadata/ci-terminal.json)
does not change timing uncertainty; prior partial/failed qualification remains.

The [R173 optional index metadata milestone](results/2026-11-09-optional-string-index-metadata/README.md)
removes every string's separate rune-table reference while retaining direct UTF-8
and scalar-length caches. Actual56B versus64B shell;42internal/4042public each11/10
pass. Deterministic old reentry overcharge232vs184B fails; keep only one published
cache and refund unpublished admission. Desktop allocationsCSV−3.58/text−4.94/report
−2.01%, Unicode-index+1.26%. One quiet5job140.642s confirmation overlaps controls;
no CPU gain/equivalence claim. Retain the coherent field reduction with indexed cost;
broader whole-script CPU objective remains active. Original payload-carrier adversity,
fixture, disk, unused desktop-symbol/apphost and missing protocol-link failures
remain. Corrected finite owner archives all5jobs plus failed first start; localSHA/
all cleanup gates pass, no remaining users. Full new-source delivery remains separate.

The [R172 native text-key cancellation correction](results/2026-11-09-native-text-key-cancellation/README.md)
forwards the current caller through actual native dictionary equality even when
hashes are cached. One frozen-old read returns after cancellation; final27internal/
825public eachactual11/10 pass. Original comparator-boundary and missing-identity
refinements, all adversity and the overstrict proof-analysis correction remain.
Final desktop syncCSV+5.13–5.86%/asyncText+3.78–12.72% are possible costs accepted
for effective cancellation; other favorable results do not establish gains. No CPU/
equivalence claim. The next substantial checkpoint carries these controls; full
platform qualification of this new source remains separate from R167/R171 delivery.

The [R169 representative CPython checkpoint](results/2026-11-09-current-cpython-checkpoint/README.md)
refreshes five complete scripts at43350e4f-equivalent production source, separately
compile/warm/compile-to-result. Fifteen jobs410.313s, two independent processes per
engine and all output/ownership/quiet/join/archive checks pass. Text and report
controls remain variable; no individual-change gain follows. Three older main-guide
rows remain historical. Offline catalog-path correction reruns analysis only.

The [R170 immutable dev.59 adapter qualification](results/2026-11-09-regex-dev59/README.md)
passes86internal/524public eachactual11/10 with the unchanged isolated dev55 bridge.
Normal pins remain0.4; no core dependency, producer publication, safety or backend
performance claim. Remaining preparation-owner/name-enumeration/creation-token
bridges defer only affected final adapter qualification, never core development.

The [R171 concurrent cancellation fixture correction](results/2026-11-09-comparison-cancellation-fixture/README.md)
preserves the failed Windows10 Stress condition: comparison finished before any
request. Controlled original2fail/corrected2pass snapshots reproduce the scheduler
assumption without runtime changes. Normal focused7each11/10 pass; original sizes,
timeouts, joined workers and exact ownership assertions remain. Full42667400495/
e0522ef5 is GREEN across both OS/frameworks, original Stress and installed consumers.
Its cached terminal receipt qualifies R167/R171, not later R172. No performance
retiming for this fixture-only change.

The [R168 native-return source gate](results/2026-11-09-native-return-source-gate/README.md)
retains the borrowed-input/no-workspace optimization: ordinary calls already defer
contexts but need array-backed value storage. Primitive semantics/facts are shared.
Unification needs a real storage redesign; no new accessor/arena/prototype or CPU
claim follows. Prior slower arena/control work remains evidence, not a permanent
veto. The representative external-baseline checkpoint is now recorded as R169.

The [R167 native text-construction milestone](results/2026-11-09-native-text-construction/README.md)
removes per-fragment accounting/payload-result plumbing and commits complete native
split/ordinary CSV results together.386internal/824public eachactual11/10 pass;
three corrected cancellation regressions fail frozen old balances. Quiet4job115.748s
accepts possible CSV+0.76–4.23% elapsed cost for rollback correctness; text overlaps
controls, adverse async controls do not reproduce. Allocation flat; no CPU/equivalence
claim. Original source/timing/fixture/space failures remain. Full delivery42661297753
passed Ubuntu and ordinary/package Windows checks; Windows10 Stress exposed the
separate R171 fixture race. Full corrected-fixture42667400495/e0522ef5 is GREEN.

The [R166 current whole-text attribution](results/2026-11-09-current-text-runtime/README.md)
reuses the96e-equivalent freeze, one5.856s capture/5.77guestCPU/1147guest samples.
Text23.37%, dictionary8.37%, scalar6.36%; VM prefix38.71% contains useful inlining.
Existing native helper returns remove ordinary binding/frame phases from this script.
No assumed per-character string conversion, duplicate hash or row copy remains.
Select a bounded complete text-construction/dictionary pipeline design phase;
no elapsed/speedup claim, new arena/cache/tagged-value commitment or permanent veto.
All gates/raw/maps/source and initial space refusal survive; no VM users remain.

The [R165 JSON item-pair correction/delivery decision](results/2026-11-09-json-item-comparisons/README.md)
replaces the native key-only comparator. Three regressions fail frozen R164 and
match independent Python after correction. Focused198/1378actual11 and196/1372actual10
pass (Stress selection differs explicitly). Quiet three-job88.558s CSVsync−1.38to+0.21%
does not reproduce desktop slowdown; async controls7.50% apart remain inconclusive,
config within±0.71%. Retain R164/R165; no CPU/equivalence claim, original adversity
preserved, debt decision closed. Exact initial binary/space refusals survive.

The [R164 shared UTF-8 comparison milestone](results/2026-11-09-shared-string-comparisons/README.md)
consolidates contextual kernels and deletes a private loop/static rendering comparer.
Focused108internal/150public per actual11/10 pass; frozen mutation preserves JSON
guest-hook availability. Allocation flat; possible desktop CSV+2.41–5.45% debt is
explicit pending the R165 boundary. Original adversity and corrected-source raw
controls survive. No CPU claim or refresh of historical CPython rows.

The [R159 current config boundary](results/2026-11-09-config-boundary/README.md)
measures7c10322c-equivalent/cfeb, stock CPython3.13.16, one unchanged181B file edit.
Three jobs88.416s/four independent roles: compile1.06–1.07×, warm1.27–1.28×,
complete1.11–1.12×; maxIQR2.98%. Only config guide rows refresh; old source labels/
guide remain. No setup-cause or individual-gain attribution. Initial duplicate
cleanup was unnecessary; exact tracked bytes/mode/owner/group restored before
timing, both proofs retained, floors unchanged. All quiet/output/owner/cleanup/
archive gates pass; next bounded step is current on-CPU attribution, not a cache
or default-stack redesign from aggregate elapsed.

The [R158 frontend-flow source gate](results/2026-11-09-frontend-flow-gate/README.md)
reuses R128 traces; current name/abstract statement visitors are unchanged. Name
diagnostics account for0.43/1.75% of old config/CSV compile samples, distinct from
the19–29% abstract share. Their maybe-assigned/unknown/directive semantics differ;
no merger, new shared state or builtin cache is selected. CPython directly compiles
source. No new measurement/current CPU share or permanent design veto. Next material
gap: current config compilation/warm/complete boundary through existing tools.

The [R157 managed whitespace-search milestone](results/2026-11-09-managed-whitespace-search/README.md)
replaces the shared scalar ASCII run loop with managed bulk searches, deriving
fixed inventories from one classifier. Focused178internal/219public per actual11/10
and six independent Python boundary scripts pass. Allocation unchanged. One quiet
three-job B508/86.826s: textsync−4.99to+0.86%, async−3.89to+0.95%elapsed, overlapping
control spreads; coarse CPU mixed. Retain the bounded implementation without CPU
gain/equivalence claim. Original desktop adversity/different source and staleFive
receipt/setup count survive; archived actual3jobs/4roles/28samples qualify directly,
no capture restart. Full delivery deferred; broader performance objective stays open.

The [R156 same-process prefix attribution](results/2026-11-09-prefix-generated-code/README.md)
matches the14159-byte Tier1 prefix to246self samples, measured fully qualified
R151/core21cf, notHEAD78fd. Of1194guest samples, observed loop bounds/switch account
for6.28%; mixed instruction fetch/required checks3.69%. Neither is a saving estimate.
Original headroom failure, exact redundant-archive recovery, offline classifier
failure/correction and all raw/JIT/code identities survive. No capture restarted or
CPU claim. Select the shared ASCII boundary scanner as the next bounded source phase;
do not infer a VM rewrite from inclusive prefix shares.

The [R154 function-workspace capacity milestone](results/2026-11-09-workspace-capacity/README.md)
removes two byte ledgers and exact-size eviction from the existing execution-owned
idle cache. Actual capacity stays funded through smaller logical loans; no new
pool or ownership layer. A deferred-prefix reference-retirement regression failed
in both APIs before correction; original source/failures are retained. Focused
actual11/10 pass832 internal/3184 public per framework. Nested-constructor CSV
allocation−0.81%; ordinary/compile unchanged. Original async-text adversity and
corrected identical-control spread26.43% prevent a CPU claim. Both frozen source
pairs and raw results survive. Full delivery42615908221/source78fd7bb4 passes bothOS
actual11/10, original Stress both and installed consumers, including the valid
nineteen-code-length DEFLATE correction and its supplied regression. Terminal cached.

The [R27 execution source gate](results/2026-11-07-execution-source-gate/README.md)
records existing operand/call/storage mechanisms and a bounded3,000-job Windows
JIT inspection of R26. All text goldens/cleanup pass; mixed inlined semantics keep
the earlier21% prefix sample share from establishing removable dispatch cost.
No runtime prototype, CPU improvement or Linux attribution is claimed. Original
staging conditions remain explicit. Reused allocation sampling adds1024 checked
text/report jobs each, zero lost events. Text PyString/bytes~58.5%, integer boxes
~23.7%; these are sampled allocation shares, not CPU costs. The next bounded
source design uses newly owned split-result payloads, with no larger string
footprint; explicitly refunded input windows cannot be shared indiscriminately.

The [canonical formatting milestone](results/2026-11-07-canonical-formatting/README.md)
fixes a demonstrated stale type proof after admission and deletes the separate
formatted-body evaluator. Canonical instructions keep primitive fields context-free
and dynamic operations resumable. Eight-case sync/async allocation is flat.
B37487.91s/all gates: report sync−0.64% to+0.29%, async+2.80–3.17%, text+0.71–2.07%;
variable report samples/moving controls and adverse coarse CPU remain explicit.
Retain correctness/deletion with possible performance debt; no speedup claim.
Full delivery42324522730 on c4729a9f is GREEN/all eight platform/runtime/original
Stress/package gates; terminal receipt cached.

The [R25 CPython checkpoint](results/2026-11-07-current-cpython/README.md)
records exact01aedc3b/current frozen0933d540, all15jobs/312s/all gates. Warm CSV
1.20–1.22x,text1.81–1.83x,JSON1.08x CPython; fresh JSON/report/config remain
variable. Full R25 delivery42319704404 is GREEN/all eight gates, cached. This
refresh does not qualify or time later native-body corrections, explain prior
adverse source pairs or establish startup/RSS/suspended-I/O latency.

The [completed CSV values milestone](results/2026-11-07-csv-record-values/README.md)
transfers funded row arrays into canonical shared-key dictionaries after admission/
header revalidation. Native CSV allocation−7.31/−6.55%sync/async; B372109s/all gates
shows native warm−7.72–8.04%, ordinary warm−4.13–4.78%. Reader/text/compile controls,
coarse adverse ordinary CPU and the original incorrect Clear-refund test remain
explicit. No broad CPU gain, fresh/async elapsed or refreshed CPython ratio.
Focused actual11/10 and installed consumers pass; full delivery is separate.

The [current R24 CPU gate](results/2026-11-07-current-script-cpu/README.md)
profiles exactec134e8c text/report for about5guestCPU seconds: ordinary calls~2%,
text prefix21%including inlined work, report JSON~10%. Unknowns, original export
failure and observer-delay deviation remain explicit; no speedup/CPU cause follows.
Source inspection identifies a distinct post-R20 CSV opportunity: adopt completed
DictReader row values under shared keys. Ownership/header-reentry proof comes
before promotion or timing. Whole-script/text objectives remain open.

The [validated-program reuse milestone](results/2026-11-06-regex-program-reuse/README.md)
removes duplicate frontend/runtime compilation under the existing compiled owner.
B36988.55s/all gates: extraction warm−8.34–9.64%, fresh−6.47–6.53%, with moving
text/compilation controls explicit. Programs remain resident longer; the simple
compiled baseline grows39,397→105,481B. Guest-visible names remain run-owned
after a concurrency/ownership review. No general CPU or refreshed CPython claim.
Core remains backend-independent; normal optional0.4 and current tokens retained.

The [R22 builtin initializer](results/2026-11-06-builtin-initialization/README.md)
prepares fixed functions/signatures once while preserving run-private mutable
types/namespaces. It removes2,688B of factory allocation per invocation. B368
completes88s/all gates: config warm−1.13–2.44%, fresh−1.43% to+0.13%; unchanged
compilation also improves and text has4.10% identical-role spread. No broad CPU
gain or closure of text debt is established. Actual11/10 focused checks pass; R22 delivery42310347889 is GREEN/all eight
platform/runtime/Stress/installed-package gates on41c25958, cached beside evidence.

The [R20/R21 CPython refresh](results/2026-11-06-current-cpython/README.md)
records all fifteen compile/warm/fresh jobs in312s. Warm text remains1.88–1.90×
CPython, JSON1.08–1.09×. Identical-role variation and separate clock boundaries
remain explicit. It updates the primary guide without attributing each change's
CPU benefit. Original Ubuntu allocation-test failure and its corrected preflight
measurement boundary are preserved; updated delivery qualification is separate.

The [R21 frame-boundary simplification](results/2026-11-06-frame-resume-boundaries/README.md)
routes pending exceptions only on resumption and deletes the duplicate return
flag. All instruction guards and cleanup ordering remain. B366 completes109s/all
gates: warm text−3.00–5.59%, fresh−0.79% to+0.02%; unchanged compilation also moves,
limiting precision. Allocation is flat; no general CPU improvement is established.
Full local actual11/10 passes; combined platform/Stress/package is separate.
Failed diagnostic preflights/post-processing and the original limited profile
remain explicit; no recapture manufactures missing shutdown/quiet proof.

The [R20 record-storage milestone](results/2026-11-06-record-layout/README.md)
splits canonical keys/values and shares immutable JSON/CSV metadata, deleting the
separate sorted JSON hint. B361 completes the quiet five-job source pair in140s:
JSON warm−10.9–12.5%, report warm−5.4–8.8%/fresh−8.5–10.5%. Separate full-job
allocation falls about15% JSON/9.4% report. CSV elapsed improves modestly while
coarse CPU is adverse. Text has5.28% identical-build variation; a bounded59s
confirmation observes−1.21% to+5.53%, preserving possible regression as accepted
debt. Retain the coherent redesign for record gains/allocations; the whole-text
objective stays open. All original failed launch/catalog conditions and partial
samples are retained; no runtime error or adverse observation is filtered out.
Full ordinary checks pass both runtimes. Runtime f2ce53db delivery is GREEN as
CI42294911136 on1fdca591: Windows/Ubuntu, actual11/10, original Stress and installed
consumers. Terminal outcome is cached; no unchanged qualification repeat.

The [R19 current CPU gate](results/2026-11-06-current-cpu-records/README.md)
captures 14.41 s of text/report guest work. Ordinary calls/binding are under 2%
of observed leaves; text prefix plus inlined work remains 20.6%, and report JSON
reading 11.9%. These are attribution, not predicted savings. Select a canonical
immutable record-key storage redesign with explicit ordinary-dictionary regression
debt and deletion of the separate JSON hint; R20 implements that selected design.
Original archive-permission failure and verified recovery are retained.

The [capture ownership milestone](results/2026-11-06-regex-capture-ownership/README.md)
removes provider carriers/arrays and eager retained groups. Same-producer direct
cursor allocation drops 20.34%; complete regex extraction improves 3.6–5.2% warm
and 4.6–6.4% fresh in an undisturbed 87 s confirmation, with 2.28% control variation.
Optional-adapter regex remains roughly 7× CPython. Earlier observed/archive-failed
conditions and SDK launcher failures remain separate; no process-CPU/RSS claim.
Full ordinary actual 11/10 checks pass; b542b601 platform/Stress/package delivery
is GREEN as CI42279409059. The cached outcome closes this runtime milestone.

The [shared-target forwarding assessment](results/2026-11-06-shared-target-assessment/README.md)
withdraws both prototypes after complete113s source pairs. Initial text−4–5%
did not recur in the refinement; refined JSON improves4.7–5.4%, CSV stays mixed
with coarse adverse CPU, and compilation controls are adverse. Added forwarding/
branch complexity is not justified. Both candidates and restored source pass
114internal/232public per actual11/10; normalized production source is unchanged.
Raw adverse conditions, bounded refinement and setup corrections remain archived.


The [b6650f09 delivery comparison](results/2026-11-06-delivery-cpython/README.md)
refreshes compile/warm/fresh against CPython for four large scripts and the existing
small configuration edit.15jobs/311.43s, two identical current roles, all goldens,
quiet/funding/cleanup/archive gates pass. Fresh ratios1.31/1.75–1.83/1.12/1.44–1.46/
1.21–1.22x; text fresh/report/warm CSV remain variable. Separate Windows/Ubuntu,
both-runtime/original Stress/package qualification is GREEN as CI42258608463.
The current guide uses this source; earlier adverse source pairs remain separate.


[Native cursor working admission](results/2026-11-06-native-cursor-admission/README.md)
qualifies a private immutabledev44 adapter against b6650f09:84internal/427public
per actual11/10, real interruption/refusal/retained ownership and256KiB bounded
matching. One existing reservation replaces the cursor subject-size heuristic;
conservative live high-water capacity ends at cursor Close. Direct working peak
32,880→2,064B; physical allocation+128B/cursor, busy-host elapsed adverse/diagnostic
+9.1%. Normal0.4 pin/core dependency independence remain unchanged. The earlier
exact-working-balance fixture failure and all measurements are preserved.

The [Excel number-format correction](results/2026-11-06-openpyxl-number-formats/README.md)
preserves numeric colors/formula caches with a direct first-section classifier;
CPython/openpyxl reference plus focused actual11/10 checks pass. No timing claim.


The [text-provenance milestone](results/2026-11-06-text-operation-provenance/README.md)
deletes per-string birth spans, the unused governing interface and hidden
constructor funding flag. Actual shells shrink64→56B on both runtimes; whole-job
allocation drops4.28% text and roughly2.4–2.7% CSV on both APIs. B351140.60s
retains text elapsed+1.6–2.2% as debt, with other controls mixed; no broad CPU gain.
Focused checks and full public.NET11 pass; the corrected native ownership fixture
and original failures remain. Combined R17/P01 delivery is qualified at b6650f09.

[Immutable dev44 adapter development](results/2026-11-06-adapter-dev44/README.md)
passes84internal/421public per actual runtime against62141d4d using the selected
range/token bridge. Normal0.4 pin/core decoupling remain. The next cursor working-
allowance migration has a precise owner/refusal contract; absent one-shot owner
overrides defer only that affected part, not independent work or core releases.

The [explicit dictionary-context milestone](results/2026-11-06-dictionary-context/README.md)
uses current CPU evidence to remove ambient installation/rediscovery from native
queries and funded writes, preserving structural/protocol provenance. Focused
365internal/1,281public per actual runtime pass. B350's140.32s complete-script
source pair favors fresh CSV/text/JSON; warm CSV is inconclusive and report has
6.92% identical-role divergence. All adverse evidence remains; no broad CPU or
allocation claim. Combined C+R14 runtime62141d4d is fully qualified by cached
terminal CI42243450458, GREEN/all8 platform/runtime/package/original-Stress gates.

The [prepared lexical-load milestone](results/2026-11-06-prepared-lexical-loads/README.md)
uses compiler binding facts to retire general lexical searches for ordinary
function globals, and corrects generic type-parameter shadowing in static facts.
Focused checks pass on both actual runtimes. The 113.56-second CSV comparison is
inconclusive: identical-current roles straddle the baseline, as does the text
control. Retained for routing simplification and correctness; no speedup claim.
Combined F02/F03 delivery is fully qualified on ae3a5a5b, cached42155751891/all8;
later F04/F05 is fully qualified on1134d6e8, cached42158371981/all8. These terminal
qualifications are closed; subsequent CSV template work was withdrawn.

The [current CSV construction source gate](results/2026-11-06-csv-construction-source-gate/README.md)
records existing native-return helpers and one stable ordinary CSV frame size.
One owned capture supplies1,540 guest-thread samples/7.75CPU seconds, with10.06%
unknown and the partial symbol map preserved. It does not establish frame-only
dominance or an elapsed/allocation gain. No pool/cache/arena prototype was added;
record staging and lookup/transport remain separate redesign candidates.

The [canonical key-storage milestone](results/2026-11-06-canonical-key-storage/README.md)
removes dict/set shadows, the shared side index, BCL set backing and per-numeric
bookkeeping. Nine Python goldens correct custom hashes over unhashable fields;
full4207internal/16072public pass per actual11/10. Original B307 adverse CSV/text
results remain. B308 single-query refinement supports set−47%/dataclass−30–31%;
B309328.85s/twelve jobs refreshes four complete scripts across compile/warm/fresh.
Fresh CPython ratios~1.44CSV/1.8text/1.12JSON/1.34–1.40report. Warm CSV/text debt
remains; no broad speedup/CPU/allocation claim. K01 source dd76df36 is fully
qualified: cached terminal 42145148043 GREEN/all eight Windows/Ubuntu runtime,
original stress and installed-package gates. It does not qualify later F02.

The [prepared-pattern milestone](results/2026-11-06-prepared-patterns/README.md)
replaces raw pattern evaluation/capture dictionaries and the lowered literal CWT
with compilation-owned work and admitted frame slots. Focus3388public/76internal
per actual11/10 passes. B306111.20s/four jobs/all gates: record warm−27–29%,
compile-to-result−25–30%; compile/text controls inconclusive. No allocation or broad
whole-script gain claim. Original failed launcher retained; P01 delivery db3d1908 is cached42135195574 GREEN/all8.

The [mapping-extraction milestone](results/2026-11-06-mapping-extraction/README.md)
corrects length/key/value phases, duplicates/rest and immediate native field failure,
using admitted private working storage and existing cleanup. Focus770public/78internal
per actual11/10 passes. B30585.11s/three jobs/all gates: native pattern elapsed
+17.54%/+15.30% is retained regression debt; dataclass/text controls inconclusive.
Next milestone prepares existing compiled match artifacts rather than undoing
semantics/admission. Original failures and fresh-process hash-layout evidence remain;
combined replacement delivery9d40a6db is fully qualified: cached42131217218 GREEN/all8.

The [dictionary-view milestone](results/2026-11-05-dictionary-view-relations/README.md)
consolidates24 relation bodies, removes one raw equality set/two membership scans
and native items-to-items tuple records. Focus372public/132internal per actual11/10
passes. B30359.07s/B30459.02s all gates; final native-view elapsed−80.17%/−79.28%,
current spread4.49%, still5.76× CPython. Complete text mixed; no broad CPU/allocation
claim. The reflected-dispatch and terminal contains failures stay preserved.
Mapping extraction/duplicate/rest defects are corrected in the subsequent milestone;
combined replacement delivery9d40a6db passed all8 platform/stress/package gates after the stale-golden correction.

The [Counter key-traversal milestone](results/2026-11-05-counter-key-traversal/README.md)
removes four temporary key HashSets/three unions and corrects Python effects,
ordering, aliases and source mutation with funded live traversal. Focused187public/
104internal per actual11/10 pass. Final59.12s source pair: native-key Counter
elapsed−10.62%/−12.10%, current spread1.65%; complete CSV+0.84%/−0.55% mixed.
Original59.03s prototype and failed conditions preserved; no broad CPU/allocation
claim. Counter delivery is now fully qualified: cached42119563175 GREEN/all8. E3b storage
coupling stays open.

The [contextual nested-hash milestone](results/2026-11-05-nested-hash-protocols/README.md)
corrects slice/defining-dataclass hash protocols, admits field snapshots before
callbacks and shares managed admission checks. Full4168internal/16033public per
actual11/10 pass; combined Windows/Ubuntu actual11/10, Stress and installed
consumers GREEN/all8 (cached42113971859). B29986.16s and justified
B30059.23s pass all gates. Native grouping+8.19/7.54% initially,+2.07/2.68% after
depth-only classification refinement remains debt. Text signs conflict across
blocks/unchanged reference drift; no general CPU/allocation/latency claim. E3
structural dataclass storage coupling stays open with its actual callers recorded.

The [shared set-query milestone](results/2026-11-05-set-query-resolution/README.md)
removes candidate temporary containers/sorting and three independent query loops,
fixes empty/native queries and admits metadata before publication. Focus1400public/
464internal per actual11/10 pass; combined delivery GREEN/all8, cached42113971859. B29785.76s
and source-justified B29858.83s pass all gates. Synthetic query regression+5.05/
4.39% initially, +2.30/1.80% after refinement remains explicit debt. Native complete
controls are small/variable; retain correctness and deletion without a broad
CPU/allocation/latency claim. Original/adverse/control/failed conditions preserved.

The [keyed-query/validation correction](results/2026-11-05-keyed-validation/README.md)
restores required hash callbacks/errors and native-copy behavior, preserves nested
failure origins and removes repeated subtree hashing. Full4154internal/16015public
per actual11/10 pass. B296140.41s/five jobs/all gates: native compound-key controls
improve; complete CSV/text/JSON do not establish a broad speedup, with small CSV
and conflicting JSON debt preserved. Actual-hash candidate authority and nested
protocol hashing remain demonstrated follow-ups. No allocation/causalCPU claim.

The [current complete-text cost gate](results/2026-11-05-current-text-cost/README.md)
captures afc3189e/084aaa38 in28s:1332guest samples/6.7CPU seconds; unknown7.21%
and one truncated map retained. All quiet/golden/identity/Closed/finalzero/joins/
fixture gates pass. Self costs include real work; no CPUgain/allocation/latency
claim or resolution of elapsed disagreement. Source assessment proceeds to
keyed mutation/duplicate hash enforcement, with protocol/error/admission proof
required before code. No new profiling campaign or assumption of dominance.

The [ordinary-result transport milestone](results/2026-11-05-ordinary-result-transport/README.md)
replaces the dedicated augmented opcode with one result/binding invariant across
ordinary values and iteration. Same instruction fields; source spans and pending
cleanup keep their actual store continuation. Original B293193.43s and refined
B294193.25s sevenjob blocks pass all gates. Final basicloop−2.45/3.02%,JSON−.93/
1.98%; possible small CSV debt, text control disagreement and variable report
remain. Initial larger gains are not generalized. No allocation/causalCPU claim;
full local/platform delivery is recorded separately in that evidence.

The [complete local augmented-update pipeline](results/2026-11-05-local-augmented-update/README.md)
removes one intermediate result/store transfer using the existing compiler pass
and five metadata ints. B291166.96s/six complete jobs and one justified same-binary
B29252.01s CSV confirmation pass all gates. Basicloop elapsed−16.05/16.54%;
CSV confirmation+0.77/+0.99% possible debt, initial adverse/control discrepancy
and noisy report retained. No whole-script/causalCPU/allocation claim. Full local
4144internal/16004public per actual11/10 pass; a63708a7 delivery42081657838
GREEN/all8, terminal receipt cached.

The [retained-runtime CPython refresh](results/2026-11-05-runtime-cpython-refresh/README.md)
measures13d69be6/ec4b versus qualified9c1d/6cb, CPython3.13.16 and independent
current control:440.25s/16jobs/all gates. Fresh ordinaryCSV1.43–1.44×,text1.89–1.96×,
JSON1.07–1.08×,report1.45–1.47×. Separate compile and warm tables/IO-cleanup boundary
in current guide. Warm source-group CSV−6.53/8.82%,text−9.07/9.50%,freshJSON−7.04/
8.42%; fresh report+.65/1.72% adverse/variable, config/compile limited. No individual
attribution/causalCPU/allocation/latency claim or closure of D55 Unicode/older debts.
Full local4143/16004 per actual target pass; retained13d platform delivery
42074585232 GREEN/all8, terminal receipt cached.

The [basic-loop cost gate](results/2026-11-05-basic-loop-cost/README.md)
closes one25s/912guest-sample capture, unknown4.71%/one truncated map line retained.
Prefix41.78%,store14.14%,addition8.33%,rangeMoveNext7.68% include actual work,
not removable fractions. Preserve lifetime fences; no broad scalar dominance
claim. Source review exposes terminal-entry frame restoration missing cleanup,
now a bounded correctness/consolidation milestone. All quiet/golden/identity/
Closed/finalzero/worker-profiler joins/fixture gates pass. No elapsed/allocation/
causalCPU/embedding-latency claim or general profile replay.

The [protected instruction-loop layout](results/2026-11-05-instruction-loop-boundary/README.md)
is withdrawn. Entry failure stays outside routing, same instruction ref/pending
cleanup, no phase flags/new strategy. B287139.70s/all gates: text−4.24/4.71%,
JSON−1.13/2.67%,CSV conflicting,report+2.60/3.63% variable,basic+.59/.92%. One
justified same-binary B28897.10s/all gates reverses report−1.31/3.64%,basic−.67/4.33%
with substantial dispersion. Preserve uncertainty, favorable/adverse conditions;
no causal gain/failure claim. Extra outward branch/cold tail lacks sufficiently
consistent benefit: source restored/nonincremental build, six independent routing/
origin/effect/ownership regressions retained. No percentage veto/permanent ban.

The [compiler-bounded operand-retirement milestone](results/2026-11-05-compiler-bounded-retirement/README.md)
uses one conservative slot bound from the existing shape pass, removing ordinary
guest-value classification above it while retaining full clearing/reverse/failed/
awaited cleanup. No extra pass/array/registry/owner/mode. Focus223/786 per actual
target pass. B286140.24s/all gates: text−7.29/9.29%,CSV−.75/2.01%,JSON−1.13/1.75%,
report−3.55/6.36% variable. Compilation inconclusive−.89/1.83%; preserve dispersion.
Retain coherent cleanup simplification; no allocation/causalCPU/general-speedup
claim. Larger runtime/frontend/regex roadmap and D55 Unicode debt remain open.

The [bounded whitespace cancellation milestone](results/2026-11-05-whitespace-cancellation-chunks/README.md)
removes per-scalar shared traversal updates in three UTF8 scans, using <=64scalar
chunk checks with unchanged owners/semantics. Focus150/237 per actual target pass.
B28486.34s/all gates: standard−.16/1.48%,unfused−1.99/2.40%; Unicode+.03/5.87%
with5.83% identical-current spread. One justified same-binary B28551.94s/all gates
gives Unicode+.20/2.18%, still possible small regression/inconclusive. Retain the
bounded simplification; no Unicode/general/causalCPU/allocation/latency gain claim.
Original ENOSPC/setup/maintenance conditions and both raw blocks preserved.

The [execution-owned function workspace](results/2026-11-05-owned-function-workspace/README.md)
replaces three ThreadStatic tiny pools/size branches with one actual cleared,
funded exact-capacity array per execution. No new owner/arena/window/credit/retry;
small buffers now funded and idle retention can deny earlier. B282139.91s/all gates
does not show a general CPU gain: warmCSV+.72/.94%, fresh/async control-limited.
Separate B28338s/all gates process allocation−6.67/6.69% (~393KB/job), paired elapsed
−.92/3.34%/2.51% original spread; current/prior-result cleanup boundaries differ.
Retain ownership consolidation/allocation saving, preserve adverse elapsed.
Focus790/2413 and full4130/16004 per actual target pass; bce54732 delivery
42061776662 GREEN/all8 Windows/Ubuntu actual11/10, original stress and installed
consumers, terminal cached. No unchanged replay.

The [bulk cased range/capacity milestone](results/2026-11-05-text-range-capacity/README.md)
retains final construction: ordinary scalar scans unchanged, existing unbounded
cased split derives its exact conservative estimate during range discovery;
per-word capacity rescans and unused coupling disappear. Initial140.32s is control
limited; refined113.00s exposes actual unfused+2.76/3.69% despite favorable Unicode.
Final86.42s/all gates removes ordinary per-scalar work: standardtext−2.61/3.98%,
unfused−1.64/3.19%, mixedUnicode conflicting−4.03/+1.69% with5.96% current spread.
Preserve all three blocks; no final Unicode/general/causalCPU/allocation claim.
Focus233/193 per actual target pass; final1fa4773e delivery42051400891 GREEN/all8,
Windows/Ubuntu actual11/10, original stress and installed consumers. Terminal
cached; performance objective stays open.

The [current runtime-cost gate](results/2026-11-05-runtime-cost-gate/README.md)
on6c776d0d/66263582 closes39s/723text/1017ordinaryCSV samples, preserving unknowns,
truncated maps and collector conditions. Instruction prefix21.72/12.29% contains
actual work; split construction19.64% inclusive is not a removable fraction.
Source confirms compact instructions/shared locals already delivered. Select
one-pass word ranges plus conservative casing capacity, removing a real repeated
traversal and parameters while retaining Unicode writers and standalone sizing.
No fresh elapsed/allocation/CPU-improvement claim or new frame/budget arena.

The [executable-temporary contract](results/2026-11-05-executable-temporary-contract/README.md)
replaces one marker interface with one empty base for the same four private classes;
no additional instances/storage/modes. D49 offline attribution identifies actual
RetireTo callers in the existing D43 profile, without claiming removable CPU shares.
B276140.27s/five jobs/all gates has3–4% identical-current differences. One justified
unchanged-binary B277114.57s/three-job confirmation supports warm text4.00–5.53%
lower;CSV.96–2.89% lower varies. Async+0.58–0.99% conflicts with initial−4.24–8.08%:
preserved unresolved debt. Retain the nominal contract; no allocation/CPU/general
speedup claim. Focus223/514 per actual target pass; full4125/16004 per actual
target and CI42044419339 GREEN/all8 on6c776d0d with the independent regex diagnostic
correction. Terminal cached; the overall performance objective remains open.

The [bounded string-metadata correction](results/2026-11-05-bounded-string-metadata/README.md)
retains shared scalar traversal/current len+projection tokens and removes two
unbounded loops, with unchanged storage/ownership. Both bulk algorithms withdrawn:
initial mixedUnicode+306%, refined+36.6/37.1%, despite ASCII~51% lower. Preserve
both146.77s/five-job blocks/all controls. Final91.80s/three-job diagnostic/all gates
shows no gross count-script regression (roles within~1%); no speedup/CPU/allocation
claim. Focus529/2037 and full4124/16001 per actual target pass; final platform/stress/
installed delivery42038712714 GREEN/all8 on2183bf18, terminal cached. Bounded checks
are distinct from measured in-flight latency.

The [retained-source CPython checkpoint](results/2026-11-05-current-cpython-checkpoint/README.md)
measures9c1d/6cb05f8e after executable targets and CSV input/EOF delivery; D46 is
absent.440.59s/16jobs/all gates, all final reservations zero. Fresh nativeCSV
1.32–1.33×,ordinaryCSV1.50–1.55×,text2.00–2.02×,JSON1.12–1.15×,report1.47×,
config1.03–1.04×,longCSV.14×CPython. Compilation1.27–1.29/1.32–1.38×.
Warmtext4.45% spread/freshJSON conflicting source signs remain inconclusive;
longCSVwarm17.1–18.4% lower versusqualified7dcc. No component/CPU/allocation debt
closure claim. Full retained-source74de3388/42029990429 GREEN/all8/cached;
publication postponed, whole performance objective still active.

The [complete call-operand transport prototype](results/2026-11-05-call-operand-transport/README.md)
is withdrawn. Five staging fields removed using immutable paused instructions
and admitted operands, preserving both descriptor phases and awaited cleanup;
two extra subscript scratch slots.139.98s/five jobs/all gates: ordinaryCSVwarm
2.9–5.3% slower in both current roles, fresh+.45/.73%,async+.80/1.42%; text and
JSON control-limited. No established whole-script gain or measured allocation
benefit. Full4113internal/16001public per actual target pass, then all three source
files restored byte-for-byte. Evidence supports withdrawal of this tradeoff,
not a permanent design ban, narrower rescue or completion of the performance goal.

The [owned native CSV input transport](results/2026-11-05-csv-owned-input/README.md)
removes an unexposed line string/copy/admission and, after independent Python
corrections, the ready-row queue/backlink/capacity machinery.143.12s/five jobs/all
gates: longCSVwarm18.32/18.59% lower/.33% current spread; ordinaryCSV smaller
timings are control-limited. Text+3.46/−1.18% with4.48% spread is inconclusive;
the adverse observation remains. No allocation/causalCPU claim. Original source
rollback assumption and baseline failures are preserved with the corrected
Python record lifetime. Separate non-strict quoted-EOF mismatch is a follow-up.

The [current complete-script CPU/source gate](results/2026-11-05-current-script-cpu/README.md)
closes39s/two cases on qualified7dcc/a4ce:783text/1022ordinaryCSV guest samples,
unknown5.11/8.51%, one truncated map line each and collector conditions preserved.
Initial6% pre-gate failure has no guest samples and remains alongside one settled
correction. Type/dispatch and storage callers remain visible; inclusive costs are
not removable overhead. Fields/row construction is already bulk-admitted. Select
owned native CSV input transport to remove the hidden line string/copy/admission,
with explicit in-place, cursor, failure and pending-owner proofs before code.
No allocation/speedup claim or additional profiling campaign.

The [complete target-instruction family](results/2026-11-05-executable-targets/README.md)
retains measuredd3735439 over qualified1f59. Remove9 operation records, retained
delete syntax, generic target routing and always-async target/delete dispatch;
reuse executable operands and preserve spans, evaluation order and awaited owners.
4111internal/7360public per actual11/10 pass.139.94s/five process-isolated jobs/all
gates: fresh text7.27–7.31% lower/.05% current spread, async5.60–5.67% lower/.07%,
ordinaryCSV2.04–2.36% lower. Warmtext controls differ2.99%; reportIQR up to7.47%
is inconclusive. Allocation/CPU/real-I/O latency unmeasured. Full7dcc/42014687629
delivery GREEN/all8/cached, publication held; the complete performance objective remains active.

The [qualified dictionary/construction CPython checkpoint](results/2026-11-05-dictionary-cpython-checkpoint/README.md)
compares exact final1f59/e958 with previous402, an identical-current process and
CPython3.13.16.466.52s/17jobs/all gates; all final worker reservations zero.
Fresh nativeCSV1.37×,ordinaryCSV1.57×,text2.16–2.22×CPython. Coupled ordinaryCSV
warm/fresh5–7% lower, report5–8% lower; freshtext+1.14/−1.60% remains inconclusive.
Cache-drainCSV9.3–9.5% lower/.25% current spread, without individual D40 attribution
or proof of the earlier drift's cause. Current guide refreshed; original adverse
points/debt remain. Exacte958 full42005719439 is GREEN/all8/cached; publication held.

The [complete native dictionary backing](results/2026-11-05-native-dictionary-backing/README.md)
retains one managed entry/bucket array and single-probe admitted insertion,
removing the BCL carrier/separate buckets. Initial161s/all gates has favorable
CSV/JSON but reportallocation+5.13%; preserve it. One JSON-construction refinement,
160s/all gates: CSVwarm4.2–8.5% lower/fresh5.6–7.1% lower, JSON4.1–4.3% lower;
allocationCSV−3.3–5.2%,JSON−6.9%,report−4.49%. Report/text elapsed control-limited.
Final source1f5919c0 also completes bucket-preserving ordered removal, eliminating
a full-capacity/last-deletion cliff before promotion.4111internal/4655public per
actual11/10 pass. Final54s/two jobs/all gates: allocationnativeCSV−5.2%,cache-drain
CSV−17.38%; elapsedinconclusive with6.62/9.16% unchanged spread. Prior construction
timings do not measure that final binary. Retain complete storage/semantic gains,
accept custom-table maintenance and workload-dependent large-map space costs;
no further local tuning. Full delivery and the process-isolated milestone are
now closed by the cached qualification/checkpoint above; original uncertainties remain.

The [final list storage and lazy activation replacement](results/2026-11-05-final-storage-activation/README.md)
retains c0221b77 versus qualified402. Remove constructor loans/per-field insertion,
duplicate list-literal backing, eager member-cache tables and missed-read namespace
construction.161s/six jobs/all gates: allocation text−5.0–5.4%,ordinaryCSV−6.3–6.5%;
async text elapsed−3.2–3.4% with.16% unchanged spread. Warm text−1.7–6.1%/4.49%
spread,ordinaryCSV−1.9–4.2%/2.42%spread limit magnitude; reportnearflat. No new
CPU/CPython or component-debt closure claim.2242internal/7909public peractual11/10
pass. Original nullable/obsolete architecture-fixture failures preserved. Full
41990811055 attempt2 is GREEN/all8gates on unchanged e5017ce4; only failed Windows
job rerun, Ubuntu gates reused. Original funding-fixture failure and later Windows
5s comparator timeout remain; their test-only corrections/local checks do not
explain the timeout. Whole-script objective remains open.

The [complete-script allocation assessment](results/2026-11-05-activation-allocation/README.md)
closes one32s external AllocationTick capture on qualified402, two complete scripts,
zero loss and all cleanup/identity/quiet gates. Text strings/bytes54.16%, temporary
reservations11.01%; ordinary interpreter/context26.26% sampled bytes, with genuine
escaping contexts. These are neither exact counts nor CPU/live-heap evidence.
Select final list-array construction and lazy cache/nonmaterializing namespace
reads; no context fusion, tagged values, heap tracing or new runtime framework.
Source replacements require their own focused and controlled validation.

The [retained CSV/list CPython checkpoint](results/2026-11-05-runtime-cpython-csv-checkpoint/README.md)
completes439.56s/16jobs/all identity/golden/quiet/join gates on a8e476bf/core402ae9f1.
Fresh nativeCSV/ordinaryCSV/text/config1.43/1.68/2.15/1.05×CPython3.13.16;
JSON1.19–1.24×/report1.48–1.57× retain3.65/5.54%controls. Compile1.28/1.36×.
Coupled text−2–3% versus0997 cannot isolate D34's accepted~1%component debt.
WarmJSON selected+5.50% versus0997/identical-currentcontrol−0.01% stays unresolved.
Final D34 delivery41977587599 is GREEN/all8gates on c6d71108, cached;
no new CPU/RSS/startup claim. Whole-script
objective stays open, with complete activation/transport and actual allocations
requiring evidence before another coherent redesign.

The [private CSV record construction](results/2026-11-05-private-csv-records/README.md)
retains final arrays with explicit public adoption/private relinquishment, removing
intermediate row lists, per-field insertion and the draft's extra async forwarding
layer. Each source134s/all gates: allocation−4.7–7.6%, publicreaderelapsed−4.2–5.3%;
DictReader warm timings control-limited. Finaltext+1.9–5.1% adverse remains; one
unchanged-bit41s reversed/longer-warmup confirmation+0.8–1.0% with0.20%spread.
Accept roughly1% warmtext debt, no general CPU/CPython/RSS claim.116internal/431public
per actualtarget pass; full delivery41977587599 GREEN/cached. Preserve failed3.96% quiet pre-gate,
both sources, all controls and real pending-user/refusal cleanup coverage.

The [known/empty list storage policy](results/2026-11-05-known-list-storage/README.md)
removes the8slot constructor floor, special first expansion and allocating clear
replacement. Thirteen net production lines removed; sort uses one managed empty
marker.108s/all gates: textallocation−8.61%/elapsed−2.4–2.9%,CSVallocation−2.1–3.3%,
report favorable. InitialnativeCSV+0.7–2.6%elapsed/controlspread1.88% remains; one
unchanged-bit41s reversed/longer-warmup confirmation is−0.5–1.0%/spread.49%.
No general nativeCSV/CPU claim or automatic retiming. Accounted initial peaks
CSV−19%/text−43% are not RSS.1561internal/3696public per actual11/10 pass; old
replacement-refusal/floor assumptions corrected without relaxing failure checks.
Full41974563982 deliveryGREEN on7efe581b,all8gates,cached; publication stays postponed.

The [ordered native formatted-body replacement](results/2026-11-05-ordered-native-format/README.md)
removes runtime builder shapes and recursive append-chain traversal. Initial135s
reportwarm−3.2–3.5%/async−4.4–5.0%,buttext+1.4–2.9%adverse. One135s source
refinement restores the original tiny shared entry guard: warmreport−3.5–5.4%,
async−5.6–7.2%,allocationflat. Possibletext~1%/compile~2%cost and freshuncertainty
explicitly accepted; neither inlining nor generalCPUcause established. Both source/
adverse conditions and275internal/1167public per actualtarget/revision preserved.
Full41974563982 deliveryGREEN on7efe581b,all8gates,cached; whole-script objective stays open.

The [construction CPython checkpoint](results/2026-11-05-runtime-cpython-construction-checkpoint/README.md)
measures exact0997baa8/core68a9f3f8 in385.81s/14jobs with independent current control
and previouscf3 source. All normalruntime/identity/golden/quiet/join gates pass.
Fresh CSV/JSON/report/config1.49/1.19/1.57/1.05×CPython3.13.16; freshtext2.20–2.28×
and warmtext2.32–2.47×retain3.32%/5.85%control variation. Compile1.30/1.35×.
Initial B250 worker staging loaded81d instead of68a; identity gate stopped before
any job timing. Original failure/source/Ready/journal preserved; corrected B251
copies identical worker executable/deps/runtimeconfig beside intended core.
No runtime/parameters changed, no discarded adverse timing or causal CPU claim.

The [shared formatted-string construction](results/2026-11-05-formatted-construction/README.md)
is retained after a 165 s six-job source pair. Report allocation decreases 10.3%
warm/async and 10.0% fresh; elapsed favors the new source by 5.8–9.0% across
those boundaries. Compile and numeric control are near flat; text control moves
favorably and has no attributed gain. Capacity/admission tradeoffs stay explicit;
focused 275 internal/1,167 public tests pass per actual target. Full delivery
is GREEN on0997baa8/41963810520, all8gates cached. This remains separate from external CPython ratios and CPU attribution.

The [current prepared-runtime CPU attribution](results/2026-11-05-prepared-script-cpu/README.md)
has1108text/1613report samples on8475a8dd/81deb3be,36sowned/all gates. Actual declaring
owners separate parameters from callers; unknown/incomplete maps remain. Budgets
already share one gate; mandatory funding is not a duplicate policy. Select whole
fstring construction capacity planning, keeping native ranges, actual read order,
same governor and bounded pre-allocation/cancellation/rollback proof. No new speedup.

The [prepared native-body replacement](results/2026-11-05-prepared-native-bodies/README.md)
is fully qualified on8475a8dd/41958212610,all8logicalgates GREEN cached.
Ordinary instructions40->32B;16B prepared values
only for eligible helpers replace runtime structure/arity/method/fusion classifiers.
Fifteen net production lines removed.268s/all gates: textwarm−4.7–8.0%,async−5.3–7.3%,
compileallocation−2.3%,warm allocation flat. Same-bit81s CSV/JSON confirmation narrows
old7.7%/3.7%control spreads; possibleCSV~1% debt and fresh/report uncertainty remain.
1744internal/5171public plus overlapping40native/50internal+90public stack checks
per actual target pass. Preserve UTF8 fixture-writer failure/correction and all
adverse signs. No CPU/RSS/external-ratio claim; whole objective remains open.

The [whole native-helper source gate](results/2026-11-05-native-preparation-source-gate/README.md)
selects a complete preparation/deletion contract after six maintained-source
inventories. Hypothetical328–776B retained savings are not measurements; repeated
structural classification must actually disappear. No prototype/speedup. Preserve
B207's graph/readonly lessons, dynamic snapshots and existing native optimizations.

The [JSON byte-run assessment](results/2026-11-05-json-byte-runs/README.md) is
withdrawn: modest report/JSON signs and flat allocation do not justify actual
1024-record output regressions of4.7–7.2%encode/write and1.4–2.0%streaming.
Preserve encoder-only162s scope error, corrected161s source pair and unchanged-bit
83s output qualification, all quiet/identity/golden/balance/join gates. Retain
semantic input-window/strictUTF8/plain-cancellation controls; both production
scan changes are removed. No causal CPU claim or permanent design veto.

The [mapping-RHS source gate](results/2026-11-05-mapping-rhs-source-gate/README.md)
finds direct dictionary calls already borrow operands: no argument array/frame to
remove. No extra guarded opcode/evaluator selected from dispatch counts alone.
Preserve the RHS/LHS/callback/lifetime proof obligations; whole-script work continues.

The [qualified pipeline CPython checkpoint](results/2026-11-05-runtime-cpython-pipeline-checkpoint/README.md)
uses exactcf3dc9cd/43fdf658 after full eight-gate deliveryGREEN. Original14jobs
384.95s/all gates plus one unchanged-binary55.34s fresh-text confirmation preserve
5–6.5% control variation: fresh text is a visible2.29–2.50x range, warm2.32x;
CSV/JSON/report/configfresh1.46/1.24/1.74/1.04xCPython. Compilation1.32/1.34x.
No discarded adverse data/additive CPU claim; substantial gaps and whole objective open.

The [immutable dev29 adapter qualification](results/2026-11-05-adapter-dev29/README.md)
passes84internal/421public per actual11/10 with exact package/test/fixture hashes,
six lifetime/three actual in-flight cancellation controls included. Main declared
PythonRe surface unchanged, normal0.4 pin/output intact, core backend-free. Three
precise final adapter capability gaps remain; no released promotion or producer
performance/safety claim. A corrected reflection artifact path required no retest.

The [final-word casing pipeline](results/2026-11-05-cased-whitespace-pipeline/README.md)
retains elimination of a full cased-line intermediate using the existing native
graph, Unicode mapping and whitespace constructor. Six-job161s pair establishes
text allocation−14.1%warm/async,−13.2%fresh; elapsedfresh−5.2–6.6%,async−4.9–5.2%.
Warm7%control spread and JSONcontrol+0.9–1.1% debt remain visible.1280internal/
4772public plus overlapping66pipeline cases per actual11/10 pass. No new opcode,
descriptor/owner/ASCII shortcut. Full delivery41944972624 GREEN on cf3dc9cd,
all eight gates cached; whole objective open.

The [precedence consolidation reassessment](results/2026-11-05-precedence-consolidation/README.md)
retains thirty net parser-line deletion after a current-source136s pair. Compile
elapsed CSV/text−6.7–8.4%,report−8.5–11.4%; allocation effectively flat. Fresh config
−1.1–1.3%; warm text's+0.6–3.8% adverse signs remain control-limited. Original B46
rigid-threshold rejection and19% fresh-config drift are preserved.656internal/
3499public per actual11/10 and overlapping27 boundary/cancellation cases pass;
full retained-source delivery41940441052 GREEN on f7fed42c, all eight gates
cached. Whole-script work remains open.

The [current compiler source gate](results/2026-11-05-frontend-source-gate/README.md)
has774text/728report compiler-thread samples with quiet/goldens/owned cleanup.
Parsing28/34% and static binding25/20% are inclusive; LoweredScript itself~3%.
Retain unknown maps and diagnostic corrections. Select one current-source
precedence consolidation reassessment: earlier6–8% compileCPU/30line deletion
missed a rigid threshold, while fresh-config controls were inconclusive. No
automatic promotion or new CPU/speedup claim; whole-script objective remains open.

The [native JSON construction consolidation](results/2026-11-05-json-container-construction/README.md)
retains combined shell/first-table admission and a shared next-token loop. Final
six-job165s collection establishes allocation −3.79% JSON/−2.24% report; async
report elapsed −4.3–4.7%, with other timing limits visible. Fresh config +0.8–1.3%
(~2–4µs) is accepted debt. Both source variants/adverse controls remain;242internal/
782public checks per actual11/10 pass. Full delivery41938239957 GREEN on ab487dfa;
all eight platform/framework/original Stress/installed-consumer gates cached.
Whole objective remains open.

The [checked operand-read assessment](results/2026-11-05-operand-read-assessment/README.md)
is withdrawn after three coherent source shapes and bounded controls. Compact218s
has text+4%; getter-only28s remains inconclusive. Direct spans54s initially give
warmtext−2.4%, but final217s exposes asynctext/report+3–5%, compile+4–6%/allocation
~5%, with larger bytecode. All focused semantics pass; no CPU cause is inferred.
Preserve every original/adverse result, restore production and retain seven public
failure-order/span/cleanup regressions. Whole-script work continues at JSON native
construction; no permanent design veto or obsolete runtime scaffolding remains.

The [qualified instruction-prefix source gate](results/2026-11-04-instruction-prefix-source-gate/README.md)
closes after17s/6.13s guestCPU:1,218samples,201prefix samples mapped to the exact
Tier1 listing. Loop/guard/dispatch includes useful work, not predicted savings.
Existing argument/local sharing and escaped/pending-owner constraints are explicit;
next map whole compiler operand lifetimes before selecting a redesign. No prototype
or new speedup/ownership-balance claim; unknowns and raw code remain preserved.

The [qualified f6e51d1c CPython checkpoint](results/2026-11-04-runtime-cpython-declaration-checkpoint/README.md)
completes14jobs385.53s. Fresh CSV/JSON/text/report/config ratios1.44/1.28/2.43/1.70/1.07;
long-field CSV0.17×. Full identities/goldens/quiet/joins pass; report remains variable
and independent-source deltas are not additive. Current guide now uses this source.

The [completed prepared-target prototype](results/2026-11-04-completed-targets-withdrawn/README.md)
is withdrawn: six jobs165s, async text+3.2–4.6% despite async CSV−1.7%, allocation
flat. Added68lines of manual continuation machinery do not justify mixed results.
Sync controls remain uncertain. All focused checks pass; source restored before
continuing. Preserve stale descriptive labels/harness correction and actual hashes.

The [prepared declaration/default cleanup](results/2026-11-04-prepared-declarations/README.md)
removes executable lowered-body retention, duplicate default maps/clones and three
construction routes.744internal/1677public plus14hosting cases per actual target
pass. Eight-case221s collection passes all gates: fresh text−1.7–3.4%, warm text
control-limited, report compile+1.2–1.3% (~0.7µs) retained as explicit debt. Tiny
allocation changes, no general CPU/RSS claim. Full41919086629/f6e51d1c delivery
now passes all eight gates; CPU roadmap remains open.

The [final borrowed-layout revision](results/2026-11-04-function-layout-borrow-withdrawn/README.md)
is also withdrawn after110s: fresh text−1.5–1.9%, warm text control-limited, config
control+9–10% unexplained. Tiny allocation benefit does not justify unresolved
overall debt. Every production prototype is removed; the public semantic regression
remains. Continue prepared declaration simplification; no permanent design veto.

The [compiler parameter-count/layout revision](results/2026-11-04-function-layout-reuse-withdrawn/README.md)
also remains withdrawn: six-case164s, text warm+2.9–3.2%, old spread0.27%; tiny
allocation benefit. Restoring separate plan/function objects did not remove the
adverse text result. A final simpler design deletes the new code-object metadata
and compiler pass; no CPU cause or speedup is claimed, all evidence preserved.

The [combined function/binding representation](results/2026-11-04-function-construction-withdrawn/README.md)
is withdrawn. Nine-case244s collection exposes9.18%warm-text old-role spread;
one bounded57s confirmation establishes text+2.6–3.1% with0.52%old spread.
Whole-script allocation savings~0.01–0.03%, no CPU attribution. Preserve all
original/adverse conditions. A narrower compiler-layout reuse revision restores
separate plan/function objects and requires its own source qualification.

The [native dictionary ownership refinement](results/2026-11-04-dictionary-owner-operations/README.md)
retains baseline data dispatch, removes view capacity/release stubs and fixes
refund-before-rejection for live aliases.770internal/2258public per actual target
pass. Final-source four-case111s collection passes all gates: text/asyncJSON near
unchanged, syncCSV−5.0–5.1%, fresh config control-limited, allocation flat. Initial
warm setup peaks are not fresh-run/RSS peaks. No broad CPU claim; full delivery
41904808069/ee8020f0 is now terminal GREEN on all eight gates. The complete self-dispatch deletion remains withdrawn, not silently promoted.

The [dictionary self-dispatch prototype](results/2026-11-04-dictionary-dispatch-withdrawn/README.md)
is withdrawn:276s/all gates, sync text+3.8–4.4% and asyncJSON+1.2–1.8% beyond stable
old-role spreads, allocation flat. Warm VM JIT already inlines typed lookups; no
blind hint series. Preserve adverse/moving controls and original failed diagnostic
conditions. A pre-existing live-view rollback refund bug is independently RED on
the committed baseline/GREEN corrected; the narrower owner-interface refinement
retains that fix and needs its own source qualification.

Delivery update: **eff471b3** completed full **41896187873** successfully; all eight
platform/framework/original Stress/installed consumer gates pass. [Cached result](results/2026-11-04-completed-json-prefix/delivery-ci-status.json).
Historical pending states below retain their observation time.

The [shared completed JSON loop](results/2026-11-04-completed-json-prefix/README.md)
retains the existing grammar/storage and CLR nesting, entering continuation state
only for actual suspension.181 internal/815 public per actual target pass. Eight
whole-script jobs222s: JSON elapsed−2.1–3.8%, sync report−2.0–2.7%, allocation and
admitted peaks unchanged. Text controls move5.6–6.5% and remain inconclusive; no
broad CPU or pending-I/O performance claim. Full new-source qualification pending.

Current delivery update: **5c827e68** is fully qualified; **41889236498** completed
successfully with all eight platform/framework/original Stress/installed consumer
gates. [Cached result](results/2026-11-04-csv-boundary-state/delivery-ci-status.json).
The [current ordinary text/report CPU gate](results/2026-11-04-current-text-report-cpu/README.md)
selects a pending-only JSON continuation proposal; inclusive useful work is not
claimed as removable overhead. Earlier pending status below records its observation.

The [5c827e68 external checkpoint](results/2026-11-04-csv-runtime-cpython-checkpoint/README.md)
completes14 jobs384.53s with all loaded identities/goldens/quiet gates/joins.
Long-field CSV fresh0.17×/warm0.10×CPython; usual freshCSV/JSON/text/report/config
1.48/1.30/2.40/1.79/1.09×. Current/reference shortCSVwarm straddles−2.0..+0.8%;
original possible+1..3.5% source-pair debt remains preserved. Text/report gaps and
variable rows remain. Full retained64KiB field/digest controls pass per API/target.
Runtime qualification41889236498 is independent/running; publication held.

The [CSV boundary/state parser](results/2026-11-04-csv-boundary-state/README.md)
removes20 net production lines, four overlapping flags and duplicate option/matcher
plumbing; preserves admitted batched record construction. Two skipped-space bugs
are fixed. All101 internal/347 public per target and84 CPython Unicode cases per
API/target pass. Combined newline/parser long-field script−86.99%, allocation
−2.54%; typical CSV warm remains possible+1–3.5% debt amid dispersion, fresh
slightly better and ragged near unchanged. All193s gates pass, adverse data retained.
Full combined runtime delivery qualification pending; publication held.

The [shared bulk newline scan](results/2026-11-04-bulk-newline-search/README.md)
replaces scalar mode/terminator branches with managed span search,17 net lines
deleted. Complete long-line scripts take23–27% less elapsed; allocation unchanged.
PossibleCSVwarm+1.2–2.8% debt accepted; text remains control-limited/inconclusive.
All focused actual11/10 checks and155s source-pair gates pass. New helper full
qualification is pending the next runtime checkpoint; no broad CPU or new CPython
ratio claimed. Earliera00 delivery remains fully qualified/terminal green.

The [a00fe76d current CPython checkpoint](results/2026-11-04-utf8-runtime-cpython-checkpoint/README.md)
completes12 jobs in328.59s with exact loaded cores, quiet gates and joined cleanup.
FreshCSV/JSON/text/report/config ratios1.46/1.25/2.45/1.70/1.09; warm
1.49/1.28/2.52/1.70/1.05. CompilationCSV/report both1.48×. The substantial
text/report gap remains. A failed executable-symlink identity gate before timing
is preserved; corrected application directories load exact frozen1dcca6f2 and
qualified0d452f95. Original source-pair adverse debt is retained, never offset by
adding percentages. Full41874093956 is GREEN on a00fe76d: all eight platform/Stress/package gates
and terminal success cached; no repeat of unchanged qualification.

The [shared UTF8 search window](results/2026-11-04-utf8-search-window/README.md)
removes59 net production lines and corrects out-of-range empty searches/index-hook
ordering.13,608 CPython combinations match through both APIs/targets. Complete
long-line scripts improve22–23%; reversed-role confirmation preserves that result.
PossibleCSVwarm0.6–3.6% debt remains control-limited; config direction reverses and
compile regression is unconfirmed. All original/adverse data retained; no broad
CPU gain. Focused checks pass; combined runtime41874093956 is GREEN on a00fe76d, all eight
gates/terminal success cached.

The [native string-existence correction](results/2026-11-04-utf8-existence/README.md)
removes scalar-count traversals from truth/text EOF and unhinted readlines. Complete
1MiB chunk scans take49–53% less elapsed, line filtering32–34%, allocation unchanged.
Existing small text helpers move adversely0.9–1.4%, near dispersion; preserve this
accepted uncertainty. All goldens/owners/quiet gates pass;213s owned collection.
Focused actual11/10 checks pass; combined runtime41874093956 GREEN on a00fe76d.

The [module lexical consolidation](results/2026-11-04-module-lexical-facts/README.md)
deletes57 net frontend lines: duplicate module-super traversal, lowered-to-syntax
reconstruction and nullable builder facts. Exact metadata matches across27 scripts/
71 code objects and focused checks pass on both runtimes. Compilation allocation
falls4.25–4.83%; text/report compilation elapsed improves4–7%, while CSV controls
shift and fresh scripts remain near unchanged. All observations remain; this is
not a broad CPU or updated CPython claim. Full41863346055 is GREEN on97bc43b2,
all eight Windows/Ubuntu11/10 ordinary/Stress/installed-consumer gates cached.

The [binding-state cache reassessment](results/2026-11-04-resolution-cache/README.md)
removes recursive record hashing and redundant versions from the existing cache.
Current-source compilation elapsed improves3.9–6.5% CSV,8.4% text and3.1% report;
fresh/warm execution effects remain small or control-limited. Original B87 fixed-
threshold rejection and drifting config controls remain intact. All focused checks
pass; full delivery41853526333 is GREEN on123f9ff1, all eight gates cached. A direct older454
comparison finds CSV compilation near unchanged, text5.5–5.8%/report1.2–1.8%
lower elapsed, with net3.3–5.0% more compiler allocation accepted explicitly.
This does not close the whole-script objective or establish a broad CPU gain.

The [dev.26 optional-adapter qualification](results/2026-11-04-adapter-dev26/README.md)
passes existing84 internal/421 public controls per actual11/10, including real
backend in-flight compile/match/cursor cancellation and owner cleanup. Exact new
immutable producer images and private fixture identities are verified; normal0.4.0
pin/core decoupling remain. This is Windows development compatibility, not a
released promotion, producer safety certification or performance result.

The [ordinary-call construction gate](results/2026-11-04-call-construction-gate/README.md)
finds about 544 extra allocated bytes per guarded helper call, including its
additional comparison/branch work. Complete-text CPU attribution confirms real
construction and dispatch costs without establishing a dominant removable frame
mechanism. General frame redesign is deferred pending a concrete deletion and
lifetime design; bulk storage/text assessment comes next. This is diagnostic
evidence, with no runtime change or speedup claimed.

The [executable-storage CPython checkpoint](results/2026-11-04-executable-cpython-checkpoint/README.md)
measures0ffc9ac6 with current controls and a qualified454 reference. Fresh ratios
remain1.13–2.46x; warm1.05–2.53x. Some source pairs improve modestly, while CSV and
report variability and possible compilation cost limit claims. All12 jobs finish
328.72s with goldens/gates/joins. Later copy-context correctness work is separate;
the substantial performance objective remains open.

The [executable storage consolidation](results/2026-11-04-executable-storage/README.md)
removes dynamic operand transport, the separate native-return graph and duplicate
frame arrays. Warm allocation is flat; initial compilation costs remain visible.
Block consolidation deletes duplicate compiler remapping before delivery. A confirmed
async-report prototype regression is corrected by managed readonly instruction
borrowing; the final focused comparison is close to its qualified baseline.
Original adverse timings, allocation-tool failure and failed disk-full profile
remain visible. c6ac2ae5 corrects copy provenance and old fixtures, then passes
all eight delivery gates41838965356. The whole-script objective is open.

The [earlier text-storage source gate](results/2026-11-04-text-storage-source-gate/README.md)
uses one short allocation profile of ae9b82b7. Split byte arrays are8.06% of sampled
guest-thread allocation; string objects remain necessary. A compact-payload/window
proposal was selected for investigation. Later windows were withdrawn and only
[cache-accounting simplification](results/2026-11-04-string-storage-assessment/README.md)
was retained. This source gate itself is no runtime optimization or CPU claim.

The [direct-list representation](results/2026-11-04-direct-list-backing/README.md)
removes366 net runtime lines and separate storage wrappers. CSV/text allocation
falls1.8-1.9%/2.95%. An initial config allocation/peak regression is corrected
by keeping established first-growth sizing; all adverse evidence remains.
Synchronous text1.2-2.9% elapsed cost is explicit accepted debt; async text remains
control-limited. Config elapsed improves in this comparison, not a general CPU
claim. Exactae9b82b7 passes all eight delivery gates in cached41793317194;
whole-script performance work continues.

The [native formatted-helper milestone](results/2026-11-04-native-formatted-returns/README.md)
extends the existing return proof and removes full invocation construction from
complete report rendering. Allocation falls26.1% on both APIs; report elapsed is
7-12% lower warm and8-10% lower fresh, with approximately2% more compilation cost.
Other helper controls and all adverse observations remain separate. The shared
JSON grammar's95-line deletion is retained: earlier async-report cost is not
reproduced; async-text remains accepted uncertainty. Exactffc01134 passes all
eight delivery gates in cached run41788586642.

The [owner-capacity admission assessment](results/2026-11-04-owner-capacity-assessment/README.md)
is shelved with production restored. Several complete scripts improve, but a
13–16% text regression does not reproduce in the bounded five-image confirmation.
Allocation stays flat; substantial-capacity fixture migration remains incomplete.
Sources, failed checks and adverse conditions are preserved separately from the
qualified runtime. A public result-lifetime test correction is retained.

Historical records preserved on 2026-10-12. Revision-specific findings below
must not be read as measurements of current HEAD. See [BENCHMARKS.md](../docs/BENCHMARKS.md)
for the authoritative comparison and [COMPARISON.md](COMPARISON.md) for methods.
The original narratives and evidence links follow unchanged. Collector-first
ownership experiments below are historical and superseded by governed memory;
their reproduction sources are evidence and are outside the supported runtime.

The [combined storage/CPython checkpoint](results/2026-11-03-combined-storage-cpython-checkpoint/README.md)
directly compares7a3a8afe with pre-loop B166 and a same-bits control. Synchronous
report elapsed is net2.7–4.5% lower warm /3.7–4.3% fresh, offsetting the earlier
individual report debt. CSV remains1–2% slower and fresh JSON1.8–1.9% slower;
those are accepted current tradeoffs, not erased by allocation results. Fresh
text straddles old roles (10% unchanged-control spread). Current CPython ratios
and all limits are in the authoritative performance guide; no async debt is closed.

The [reader-owned window milestone](results/2026-11-03-reader-owned-windows/README.md)
borrows unchanged UTF-8 directly until window retirement, removing an intermediate
string/copy and reader rune-offset cache. Whole-script allocation falls1–2.75%,
block-read allocation10–15%. Elapsed is mixed, some async identical-source controls
differ53%, and sync CSV/JSON changes remain provisional regression debt. All
observations remain; the combined storage/CPython checkpoint decides the tradeoffs.

The [direct casing storage milestone](results/2026-11-03-direct-casing-storage/README.md)
replaces mutable casing builders with one admitted immutable backing and static
UTF-8 expansions. Exact two-pass mapping initially ran48–55% slower in the long
Unicode diagnostic; a byte-only conservative sizing refinement preserves text
allocation−5.92% and useful complete-text elapsed results. Separate-worker Unicode
confirmation is near unchanged. Long-ASCII identical-source controls differ38–63%
and remain inconclusive. Original sources, adverse samples, ownership tradeoffs
and measurement boundaries are retained; no general CPU gain is asserted.

The [shared UTF-8 casing pipeline](results/2026-11-02-casing-pipeline/README.md)
removes three duplicate loops and eager final-sigma state,43 net production lines.
A justified refinement removes per-rune context-window construction after an
initial uppercase CPU regression. Final controlled complete text-helper CPU is
6.8-10.3% lower, elapsed5.8-7.9% lower, allocation unchanged; uppercase CPU matches
controls. All initial adverse samples/conditions remain, no new external ratio.

The [revised native CSV constructor](results/2026-11-02-native-csv-records/README.md)
retains the useful part of B74 after selective review and controlled confirmation.
Whole CSV allocation falls13.7-15.1%, CPU7.2-11.1%. Helper elapsed improves5.9-7.5%;
plain elapsed is unchanged in one block and improves6.7-7.0% in another. Text
controls and all adverse/noisy points remain visible. Native short/extra rows
share the constructor; dynamic headers keep their semantic path.

The [controlled formatting confirmation](results/2026-11-02-formatting-confirmation/README.md)
retains direct UTF-8 output storage after two independent counterbalanced blocks.
Complete-report allocation is8.59% lower on the current-source pair (8.67% on
its original revision); elapsed/CPU are5-8% lower against both originals, and
unchanged JSON controls stay close. The historical adverse phase result remains
unexplained and preserved; it is not reproduced by either aligned clock here.

The [native function-return milestone](results/2026-11-02-native-function-returns/README.md)
generalizes the existing guarded simple-return operation, preserving exact-integer
addition and admitting native conversions/dictionary reads without resumable
interpreter storage. No arena, pool, new backend or owner layer is introduced.
One controlled whole-script collection reduces CSV/JSON allocation by 19.9/19.3%,
with unchanged text-control allocation and all ownership/quiet gates passing.
Adverse text elapsed points and all raw observations remain; no CPython speedup
or embedding-process latency claim follows.

The [shared compiler-fact correction](results/2026-11-02-shared-compiler-facts/README.md)
replaces recursive container/callback queries with one iterative, temporary graph
inspection. Shared tuple payloads and captured states are visited once per query;
the checks preserve inference, observe active cancellation and prioritize prior
admission refusal. One bounded initial-source comparison demonstrates an exponential
compilation cliff. Complete public semantics and allowance reuse pass; this does
not establish warm-execution or representative CPython improvements.

The [expression-route cleanup](results/2026-11-01-expression-route/README.md)
reuses ordinary one-value unpacking for starred annotations, fixes independent
Python mismatches and deletes the generic expression opcode/storage/dispatch.
Its fixed CSV control retains a possible +2.38%/+3.34% CPU cost with essentially
unchanged elapsed time. Correctness/deletion is retained without retiming or a
speedup claim.

The [compiler cancellation correction](results/2026-11-01-compiler-cancellation/README.md)
fixes ordinary parsing and later-phase transitions with deterministic regressions.
One fixed cost check shows no clear compilation slowdown; an unchanged warm
runtime's CPU difference has no elapsed improvement and is not a speedup claim.
The measured and final coverage patches remain distinct.

The [precedence-parser prototype](results/2026-11-01-precedence-parser-rejected/README.md)
misses its fixed compilation-CPU target; fresh-config controls also drift beyond
their bound. The original parser is restored, independent semantic cases and
all adverse observations remain. No acceptance relaxation or retiming follows.

The [current frontend capture](results/2026-11-01-current-frontend-cpu/README.md)
records 32,768 fresh compilations after Python tokenization. Parsing and static
analysis remain prominent; scope facts and UTF-8 CSV conversion already have
their delivered paths. A bounded precedence-parser consolidation is selected
with separate semantic and controlled-comparison criteria; no gain is inferred.

The [current main-loop attribution](results/2026-11-01-main-loop-attribution/README.md)
maps all 165 main-method self samples to the unique emitted Tier1 byte range.
Source correlation identifies the shared service/guard/state forwarding chain;
it does not establish a removable CPU fraction. One 6.980-second capture, no
recapture or speedup claim. The subsequent
[shared-state consolidation](results/2026-11-01-execution-state-consolidation/README.md)
passes its fixed process-CPU criterion on CSV, with smaller uncertain elapsed
changes. Source/semantic and comparison criteria were frozen before code.

The B42 source decision finds that no-region functions already omit block-depth
storage; protected code needs it for abrupt handler restoration. There is no
justified deletion or new capture. Two misleading control-flow-validation
comments are corrected; executable statements and protection remain unchanged.

The B41 source-only boundary review at 9e99f640 finds ordinary flush/close and
matched fixture reset/full-output checks. Payload/code retirement and host path
authorization differ between integrations; the [methods](COMPARISON.md) now spell
out those existing boundaries. No new capture, timing correction, runtime change
or explanation of the warm CPU gap is inferred. The review stops here.

The [ordered native-key correction](results/2026-11-01-native-key-resolution/README.md)
shares funded resolution, deletes duplicate scans and removes string-only async
lookup routing. Original runtime fails 45/84 new semantic goldens. The fixed
native-only control flags a possible CPU cost (+5.99% / +1.37%, control +4.56%);
it measures the first candidate before the subsequent routing correction. Both
patches and all adverse/raw callback-count observations remain, without retiming
or a final-source speedup claim.

The [native-hit ordering probes](results/2026-11-01-native-hit-protocol-order/README.md)
demonstrate four remaining custom-key callback mismatches after B38. They are
unfixed correctness findings, without a performance claim; native-only lookup
and reentrant mixed-key ordering need a coherent correction.

The [current JSON/configuration CPU assessment](results/2026-11-01-current-json-cpu/README.md)
records 1c369000, two complete scripts and 6.093 seconds capture. JSON costs are
distributed; configuration has too few guest samples for a dominant-cost finding.
Unknown/native origins and background JIT remain visible. One bounded source
decision selects no parser/value rewrite; no recapture or speedup claim follows.

The [mixed-key correctness checkpoint](results/2026-11-01-mixed-dictionary-keys/README.md)
fixes native writes bypassing equal custom keys and consolidates defaultdict
population/copying. Independent Python goldens, cancellation/refusal and copy
rollback checks remain. A short correct copy control lowers the governed peak
estimate; this is accounting evidence, without a timing or physical-heap claim.

The [dictionary-write consolidation](results/2026-11-01-dictionary-writes-rejected/README.md)
misses its fixed complete-script CPU target; text's identical control also drifts
beyond the allowed bound. The prototype is removed with fresh binary identities
verified. Six processes / 47.49 seconds are preserved without retiming. Independent
semantic coverage remains; the subsequent mixed-key correctness fix is separate.

The [on-demand sigma-context experiment](results/2026-11-01-sigma-on-demand-inconclusive/README.md)
has an invalid baseline: a timestamp-preserving restore left B35's rejected
binary in incremental outputs. Exact identities expose the mismatch. Runtime
restored and forcibly rebuilt, fresh output copies verified, independent goldens
retained. Six processes / 46.59 seconds are preserved without retiming or a
performance claim; the earlier B35 local restoration claims are corrected.

The [shared iteration-cursor prototype](results/2026-11-01-iteration-cursor-rejected/README.md)
removes the separate checked enumerator but misses its fixed text CPU/allocation
targets. Allocation falls 3.56%; text CPU is +2.58% / -2.80% against the two
baselines, whose CPU drift also exceeds the fixed bound. Runtime restored with
no retiming; independent mutation/exhaustion/generator coverage remains. Six
processes / 47.85 seconds, with all adverse wall/control observations preserved.

The [current allocation-origin diagnostic](results/2026-11-01-current-allocation-origins/README.md)
records ae22392d, two complete scripts and 2.065 seconds capture. Frame/context
objects account for approximately 26.13% text / 18.85% CSV sampled guest bytes.
This is allocation attribution, not a removable CPU fraction. The bounded source
and lifetime review identifies no justified deletion prototype; no frame pool,
context fusion, tagged-value project, recapture or performance claim follows.

The [bulk whitespace-construction prototype](results/2026-11-01-bulk-whitespace-rejected/README.md)
consolidates UTF-8 scans and per-word reservations but misses its fixed complete-
text targets: CPU -6.84% / -4.78% and allocation -2.375% against the two baselines.
Both targets required -5% against both. Runtime restored without retiming; three
independent mixed UTF-8/control/maxsplit boundary cases remain. Six processes /
47.12 seconds; adverse wall/control observations are retained.

The [final-sigma property-index prototype](results/2026-11-01-sigma-properties-rejected/README.md)
passes exhaustive flag/casing checks but misses its fixed whole-text CPU target:
-6.85% against the first baseline, -4.15% against the final identical control,
requiring -5% against both. Runtime restored without retiming. Six processes /
46.59 seconds; metadata and adverse wall/control observations remain visible.

The [current synchronous CPU assessment](results/2026-11-01-current-text-cpu/README.md)
records 830c6251, two fixed complete scripts and 15.676 seconds capture. Unicode
sigma-property searches occupy 10.7% of text guest samples, zero in the CSV
control. It identifies a bounded property-representation hypothesis, not a
predicted speedup or justification to reopen the rejected bulk-ASCII path.

The [bounded async CPU attribution](results/2026-11-01-async-cpu/README.md)
records two fixed complete scripts, 8.65 s total capture, explicit guest threads
and ordinary worker tiering. Existing native paths are used; async operation-
helper self samples are small and do not justify a wholesale engine rewrite.
Distributed native costs, unknown leaves and background JIT remain visible.

The [complete-script float-entry prototype](results/2026-11-01-json-float-entry-rejected/README.md)
passes semantic checks but its fixed nine-process CPU comparison is inconclusive:
the unchanged baseline shifts 36.7%, and candidate equals the final control.
The runtime patch is removed without retiming. Independent Python rounding,
exponent and retained-result coverage remains; no whole-script gain is claimed.

The [bounded UTF-8 float-entry assessment](results/2026-11-01-float-entry-assessment/README.md)
checks 2,137 CPython binary64 goldens and complete consumption before a fixed
local component comparison. Both BCL APIs match; the alternative passes the
component gate. No production change or complete-script performance gain is
claimed; separate semantic/ownership/target and whole-job acceptance remains.

The [fixed short-comparison conditioning experiment](results/2026-11-01-micro-conditioning-rejected/README.md)
uses identical-worker controls to reproduce early-batch nonstationarity. Six jobs /
43.73 s pass full goldens; observable conditioning narrows spread but misses its
pre-set ordered-sample bound and is removed. No timing is trimmed or repeated;
existing elapsed conclusions remain unqualified, independently of allocation gains.

The [native UTF-8 error/newline pipeline](results/2026-11-01-native-utf8-errors/README.md)
removes per-rune CLR strings, UTF-16 builder scratch and re-encoding. Complete
robust search/float-report scripts allocate 49.8%/71.5% less; strict controls stay
unchanged. Twelve fixed jobs/94.74 s pass all goldens but retain severe timing
variance and increases. No elapsed gain or primary CPython-table update is claimed.

The [2026-10-31 current attribution](results/2026-10-31-architecture-profiles/README.md)
separates compilation, large-script execution, host I/O and cleanup at 07c150a6.
Its [activation-reuse prototype](results/2026-10-31-activation-reuse/README.md)
misses the fixed material-gain criterion in two representative observations and
is removed. The [byte-range contract diagnostic](results/2026-10-31-regex-ranges/README.md)
separately retains an inconclusive adapter integration check. These do not update
the primary CPython table or imply current-HEAD performance.

The [2026-10-31 native CSV pipeline](results/2026-10-31-native-csv/README.md)
removes line decoding and field re-encoding. Two six-column shapes allocate about
14% less; fixed complete-script observations show modest warm changes, with noisy
fresh compile/run rows preserved. The remaining interpreter gap stays open.

The [shared native regex shaping](results/2026-10-31-native-regex-shaping/README.md)
removes backend split/template APIs and the adapter replacement cache. Split
allocates less, while template sub and expand allocate more with the 0.4 bridge;
all regressions and the unchanged text-file control remain visible. Contract
correctness is separate from final backend performance; no speedup is claimed.

The [compiler traversal redesign](results/2026-10-31-compiler-traversal/README.md)
removes ordinary per-node iterator objects without a syntax cache or pass rewrite.
Complete public compilation allocates 35–41% less across six existing scripts.
Two fixed VM observations show lower compilation point medians, with noisy rows
and inconclusive complete-script timings retained. Warm execution gaps remain.

The [bounded executable CPU attribution](results/2026-10-31-executable-cpu-attribution/README.md)
maps current instruction-loop samples to JIT blocks and obtains useful caller
chains from the same fixed external capture. It separates checkpoint/fetch work,
native admission callers and text Unicode-property lookup, without removing
cancellation/admission checks or assuming boxing explains the whole gap.

The [bulk ASCII casing prototype](results/2026-10-31-bulk-casing/README.md)
misses its fixed complete-script acceptance criterion and is removed. Allocation
is unchanged; helper text improves below target while plain text regresses with
noisy observations retained. Its independent final-growth cancellation fix and
Python/ownership regressions remain, separately committed.

The [native JSON string-token prototype](results/2026-10-31-native-json-strings/README.md)
removes backend rewriting and error translation, but reduces complete-script
allocation by 7.0%/5.0%, below its fixed 10% criterion. It is removed before VM
timing; all raw allocation observations and the exact patch remain. Four
independent cancellation/ownership controls pass the restored decoder on both
targets. No runtime improvement or new CPython comparison is claimed.

The [compact instruction representation](results/2026-10-31-compact-instructions/README.md)
removes duplicate operand tags/operator fields and halves measured instruction
array elements (48 to 24 bytes). Complete compilation allocates 3.4–3.8% less;
the fixed sixteen-job comparison has no repeatable material script regression,
with noisy/reversing compilation and control rows retained. This is no qualified
overall elapsed improvement or update to the primary CPython table.

The [numeric-report aggregation redesign](results/2026-10-31-numeric-sum/README.md)
keeps fixed numeric prefixes in scalar fields, eliminating per-item result boxes
inside Sum. Complete integer/float JSON reports allocate about 20.9% less, and
native sum allocation falls to fixed overhead/results. The fixed fourteen-job
comparison has extremely noisy numeric rows, including increases; it proves no
elapsed speedup or host-latency improvement. Existing controls and all outcomes
are retained separately from the primary CPython table.

The [numeric-report CPU capture](results/2026-10-31-numeric-cpu/README.md)
uses one 10.66-second fixed collection after unboxing. Number paths dominate
both shapes; integer-only limb reservations cause repeated shared-budget work
before non-allocating small parses. Unknown leaves and overlapping caller counts
remain explicit. The evidence selects allocation-coupled integer parsing, not
stack-policy removal, a parser rewrite or an elapsed-performance claim.

The [allocation-coupled JSON integer path](results/2026-10-31-integer-admission/README.md)
removes temporary limb reservations before bounded small-number parsing. Complete
integer reports allocate 26.5% less; float, CSV and text allocation controls are
unchanged. All fourteen fixed VM jobs finish in 111.10 seconds. Large timing
spreads and increased float/control rows remain visible; no elapsed speedup is
claimed. Large-magnitude preflights, hooks and ownership regressions remain.

The [development range adapter](results/2026-10-31-development-range-adapter/README.md)
uses an exact unpublished producer pin in an isolated build. Existing semantic,
ownership and real in-flight cancellation checks pass; split/sub allocation falls
6.4%/10.8% against the same producer. The fixed eight-job comparison includes a
401% split increase and very noisy/reversing rows: no speedup or promotion claim.
Direct EffectiveOptions mapping fails existing Python flag-object goldens and is
rejected. The independent native UTF-8 prefix change ships on normal 0.4 builds;
final released-adapter qualification remains separate.

The [funded development adapter](results/2026-10-31-funded-development-adapter/README.md)
replaces fixed program/cursor estimates with producer admission through existing
Lython owners and reuses borrowed current-capture storage. Semantic, first-refusal,
retained-program, independent-borrow and actual cancellation checks pass. The
fixed allocation comparison costs 0.7–1.3% for split/sub and 0.2% for expand,
with text unchanged; no timing gain is claimed or VM comparison repeated. One-shot
detailed matching retains conservative funding because its API cannot select a
working allowance. The retained-program fixture now creates distinct programs.

The [native streaming text windows](results/2026-11-01-native-text-windows/README.md)
remove UTF-16 roundtrips from ordinary UTF-8/Latin-1 reads using shared admitted
codec results and an explicit copy of borrowed raw windows. Complete float JSON
reports allocate 15.19% less, integer reports 9.96% less, search 6.60% less; other
complete controls also fall slightly. Both targets pass focused semantics and
ownership checks. Twelve fixed VM jobs/96.20 s pass quiet/goldens, with large
numeric/search spreads and a small config increase retained. No qualified elapsed
or primary CPython-table update is claimed.

The [2026-10-29 foundation baseline](results/2026-10-29-foundation/README.md)
records 27 fixed CPython/release/development comparisons and separate CSV CPU,
allocation and GC diagnostics. All outputs pass; short script rows vary heavily.
Actual on-CPU sampling contradicts the apparent lock/GC domination in managed
sample leaves. The profile is a lead for frame/dispatch attribution, not proof
that memory admission causes the gap or that .NET 11 regresses the interpreter.

The [2026-10-29 development delivery comparison](results/2026-10-29-delivery011/README.md)
records six unchanged complete scripts at da0ff811 against CPython and the actual
published 0.10.0 core. Every output and quiet gate passes in 49.79 seconds, but
short rows remain unstable, including higher development medians. No qualified
overall elapsed improvement is claimed. This is the source of the current guide's
table; the older foundation/profile records retain their own identities/boundaries.

The [2026-10-29 runtime-boundary diagnostic](results/2026-10-29-runtime-boundaries/README.md)
separates actual setup/execution/host/projection/cleanup with unchanged scripts,
then compares the public APIs with completed and genuinely delayed host work.
The fixed 8.31-second collection passes 576 complete jobs and ownership cleanup.
Aggregation cost remains inside execution; setup/projection are small. Instrumented
timings and unstable CSV are explicitly unqualified, with no new CPython ratio,
governor attribution or favorable retiming.

The [2026-10-29 prepared-scope consolidation](results/2026-10-29-prepared-scope-facts/README.md)
reuses lexical metadata across compilation and class creation, replacing separate
class-cell flags. Five representative sources allocate 1.7–3.7% less during
compilation. Its sole nine-job timing collection has mixed compilation medians
and very noisy complete scripts; no elapsed speedup is established. Existing
lexical/diagnostic checks pass on both platforms/TFMs. No class-engine migration,
new cache policy or favorable remeasurement follows.

The [2026-10-29 class-subscription wrapper prototype](results/2026-10-29-subscription-wrapper-rejected/README.md)
passes focused checks but its single unchanged Linux fixture comparison barely
changes the slow descriptor witness (18.193/18.055 seconds). The added completed/
pending helpers are rejected and removed. CI fixture headroom is addressed with
one default-depth rejection and genuine protocol recovery, without a runtime
performance claim, changed stack bounds, deadline or original stress scale.

The [2026-10-29 executable class-suite prototype](results/2026-10-29-class-execution-rejected/README.md)
is rejected before timing. Focused namespace/generator/ownership checks pass, but
the completed full public run catches earlier deep-method admission and a fatal
stack overflow in a genuinely resumed recursive hash callback. The patch is
preserved outside production, and current execution sources/stack protections are
restored. Call-frame and exception-unwind safety must precede this convergence.

The [2026-10-20 native predicate cancellation work](results/2026-10-20-predicate-cancellation/README.md)
retains its rejected per-rune prototype and final chunked observations. The
prototype interrupted native scans but showed higher uninterrupted times in one
unqualified capture. The final code checks existing execution guards between
small UTF-8 chunks without per-rune bookkeeping. Both APIs interrupt funded work
and return ownership after cleanup. Raw costs and uncertainty remain visible;
there is no qualified throughput gain, CPython ratio or general latency claim.

The [2026-10-20 complete text-helper profile](results/2026-10-20-text-profile/README.md)
uses the existing worker, script, fixture and complete goldens for 384 fresh
compile/run jobs. Managed thread-time samples include substantial waits and CLR
helper leaves. They identify paths without establishing avoidable cost or the
cause of the historical CPython gap. The profile is retained as inconclusive;
no runtime edit, relaxed admission or repeated workload collection follows.

The [2026-10-20 identifier/title follow-up](results/2026-10-20-identifier-title-cancellation/README.md)
closes two demonstrated context-forwarding/cancellation gaps and removes a whole
UTF-16 identifier copy. Twenty-four independent CPython goldens preserve Unicode
semantics. One short before/after observation shows both APIs interrupting funded
pending scans, with baseline/root-zero cleanup. The uninterrupted title samples
are nominally higher. All timing remains unqualified, with no general latency,
throughput or CPython-ratio claim. No per-rune counter or new policy is introduced.

The [2026-10-19 exact-add async-entry prototype](results/2026-10-19-native-add-rejected/README.md)
is rejected without merging. 176 focused checks pass, but one warm-async nested-
comprehension capture gives 1.064/1.141 ms, CPython 0.104 ms. The additional forwarding
boundary has no demonstrated benefit. Timing is unqualified; no regression claim,
retiming or wider operator campaign follows.

The [2026-10-19 list capacity consolidation](results/2026-10-19-list-capacity/README.md)
deletes per-value numeric admission/refund bookkeeping in favor of conservative
retained capacity. Focused ownership/refusal/cancellation and complete-script
checks pass. One 7.827-second nested-comprehension capture gives 0.995/0.920 ms,
versus CPython 0.133 ms; baseline spread is large. No performance claim or retiming.

The [2026-10-19 guarded list-display prototype](results/2026-10-19-list-display-rejected/README.md)
is rejected and removed. Its protocol, ownership and complete-script checks pass,
but one 7.833-second nested-comprehension capture shows no established benefit for
the added receiver/stack dispatch: 0.942/0.966 ms before/after, with 4%/31% spreads.
This is inconclusive timing, not a proven regression. Keep the simpler dispatcher;
no revival, retiming or alternative dispatch framework follows.

The [2026-10-19 native prepared-operation routing](results/2026-10-19-native-prepared-routing/README.md)
removes async routing for six native operations without changing their bodies.
Focused white-box, public protocol/ownership and complete-script checks pass.
One formatted-report kernel capture at `b481e3fd`/`bdf777d6` gives 0.679/0.626 ms,
versus CPython 0.191 ms. The lower median remains unqualified; no general speedup,
allocation claim or retiming follows.

The [2026-10-19 native JSON value entry cleanup](results/2026-10-19-json-value-entry/README.md)
removes native/container async forwarding while preserving one awaited callback
tail. 155 white-box, 518 public JSON and 123 representative-script checks pass,
plus a resumed-stack replacement-depth control. The sole complete config job at
`e69e5fc8`/`b481e3fd` takes 2.169/2.153 ms with 26%/27% spreads. No speedup,
allocation claim or retiming follows.

The [2026-10-19 compiled function body cleanup](results/2026-10-19-compiled-function-bodies/README.md)
removes redundant lowered-body retention and runtime docstring-tree inspection.
87 white-box and 345 public checks pass; eight docstring controls match CPython.
The sole complete text-job capture at `eb3f674c`/`e42d2d8f` is inconclusive, with
221%/151% spreads. No speedup, regression, heap/allocation claim or retiming follows.

The [2026-10-19 generator definition route removal](results/2026-10-19-generator-definition-route/README.md)
deletes the metadata-driven lowered-definition detour and namespace copy. All70
white/435public focused checks and8CPython semantic controls pass. One7.596s
annotated variant of the maintained generator job gives0.289/0.308ms at
`b2c91ba6`/`eb3f674c`, versus CPython0.043ms; large spreads make change inconclusive.
No speedup, allocation claim or retiming follows.

The [2026-10-19 ordinary byte-sum call check](results/2026-10-19-bytes-sum-call/README.md)
removes argument repackaging for one-positional `sum(bytes)` and shares immutable
sum metadata. One complete file-job comparison at `13375e82`/`b2c91ba6` takes
4.294/4.616 ms with 21.43%/16.63% spreads. CPython context is 4.661 ms. No speedup
or qualified regression is established; no retiming is planned.

The [2026-10-19 JSON scalar completion check](results/2026-10-19-json-scalar-completion/README.md)
compares immutable `3d1ec461` and `13375e82` on one complete fresh JSON helper
job, with CPython context. The 9.298-second Windows capture is inconclusive:
10.577/10.697 ms Lython medians and 10.45% candidate spread. Removing redundant
async forwarding establishes no measured speedup; no retiming is planned.

The [2026-10-19 regex boundary check](results/2026-10-19-regex-boundary/README.md)
records one bounded before/after/control comparison of the maintained reuse
workload. It reports added allocation and unqualified timing, with actual owned
result cleanup. It is an integration diagnostic, not a CPython comparison.

The [2026-10-19 native field conversion](results/2026-10-19-native-field/README.md)
separates immediate native formatting from the unchanged async instance protocol
body. One pair retains22 allocation arrays and472 exact boundaries;native faults
save1,808/1,256B per sync/async call. All831 focused checks and58 jobs pass;12
CPython programs match both APIs. This does not prove a successful-call speedup.

The [2026-10-19 Unicode formatting delivery](results/2026-10-19-unicode-format/README.md)
removes inert OwnJoinItem/21 calls separately from fixing Unicode character units.
All739 focused cases and58 jobs pass;31 CPython programs match both APIs. Native
allocation/work arrays remain unchanged;correct output needs1-2B more retained
funding in measured cases. Initial baseline failures and both comparisons remain
visible. No elapsed speedup claim or new ownership/formatting framework.

The [2026-10-18 recursive repr correction](results/2026-10-18-repr-stack-admission/README.md)
supersedes unsafe intermediate8f00a1c3:its exact Release package overflows the CLR
stack on a bounded acyclic-container witness despite green ordinary CI. Moving
the existing managed guard to recursive descent fixes the witness and preserves
the64B scalar saving/all34 allocation and funding arrays. Container fuel gains
explicit safety checks;336 focused cases/58 jobs pass. Full fault traces retained.

The [2026-10-18 repr cycle-storage change](results/2026-10-18-repr-cycle-storage/README.md)
defers the existing per-call set until a container needs it. One frozen pair
saves64B per affected rendering call;14 container arrays,funding and368 boundary
records match.327 focused checks and58 complete jobs pass;six CPython programs
match both APIs. No new cache/dispatcher/owner mechanism or elapsed claim.

The [2026-10-18 single-part f-string change](results/2026-10-18-single-part-fstring/README.md)
removes a redundant executable join builder and copy. One frozen pair saves
248-528 B/call; six control arrays and 352 boundary records match. All 318 focused
checks and 58 complete jobs pass. Seven CPython programs match both APIs; owners,
actual formatter I/O/cancellation and multi-part refusal remain covered. No timing.

The [2026-10-18 str keyword/default correction](results/2026-10-18-str-keywords/README.md)
reuses argument presence and builtin metadata,removing suffix inference and an
input copy.24/41 baseline regressions become successes;all440 focused checks and
58 complete jobs pass.37 CPython programs match through both APIs. One unchanged
builtin-subclass boundary remains explicit. A short allocation pair preserves
native calls and252 boundary records;general-call array costs are documented.

The [2026-10-18 ordinary native str calls](results/2026-10-18-native-str-call/README.md)
reuse existing call frames and rendering,removing a bound array per affected
call. One frozen allocation pair saves32B;14 controls and252 boundary records
match.366 focused checks and58 complete jobs pass. Eight CPython programs match;
two object-keyword failures reproduce on the immutable baseline and are queued
separately. No elapsed measurement or new binder/rendering/ownership machinery.

The [2026-10-18 finalized slot-map handoff](results/2026-10-18-slot-map-handoff/README.md)
removes two compiler dictionary copies. One native/public compilation pair saves
816-984 B per representative source, retaining roughly 168 B more cell payload
for text/file under the existing owner. All ten warm controls match; 418 focused
checks and 58 complete jobs pass. No elapsed measurement or new memory mechanism.

The [2026-10-18 binary operator consolidation](results/2026-10-18-binary-operator-kind/README.md)
removes a duplicate enum, two maps and a forwarder. One allocation-only pair has
18 identical sample arrays and 96 matching boundary records; 572 focused cases
and 58 complete jobs pass. No timing or allocation-reduction claim. Compilation
is native frontend/lowering/executable construction, not public compile-to-result.

The [2026-10-18 complete manifest helper checkpoint](results/2026-10-18-manifest-checkpoint/README.md)
compares immutable `3ea431fd` with CPython 3.13.16 using two identical Lython
controls. Fresh complete jobs measure 2.027–2.251 ms versus 0.597 ms, nominal
3.40–3.77×; 30.37%/31.64% Lython spreads and 11.02% control difference leave
the result inconclusive. All 39 responses/3,670 invocations and complete file
goldens match; cleanup/lease release pass. One 7.649-second capture, no retry.
The previous leading comparison table is preserved behind its evidence link.
No runtime or benchmark implementation change, historical trend or fix attribution.

The [2026-10-18 sum cursor/acquisition change](results/2026-10-18-sum-cursor/README.md)
fixes a demonstrated Python ordering bug and removes general async sum's two
forwarding objects. One frozen allocation pair saves 96 B per affected call;
sync/bytes controls and 126 valid-path fuel/error/span records match. Eight initial
public failures become successes against eight CPython programs; 328 focused
checks and 58 complete jobs pass. Nine native cleanup cases preserve funding and
stored refusal through cleanup. An internal/public-boundary fixture correction
is explicit. No elapsed measurement, recapture or new memory/cursor machinery.

The [2026-10-18 JSON dictionary-helper capture removal](results/2026-10-18-json-dictionary-capture/README.md)
moves one captured local function into a static helper with explicit inputs.
The same helper body, awaits, cycle tracking and funding remain; 80 B per value
disappears, saving 5,200 B for 64 scalars or 15,440 B for 64 small dictionaries.
All 465 focused checks, 58 complete jobs, six CPython goldens and two new baseline/
candidate lifetime cases pass. Semantic guard fields and controls match; affected
refusal allocations drop 80 B. Initial fixture errors and one raw allocation
outlier remain documented. No elapsed measurement, recapture or new memory machinery.

The [2026-10-18 dictionary view calls](results/2026-10-18-dict-view-call/README.md)
reuse existing receiver operations and view admission. One allocation-only pair
saves 6,656/7,168 B per 64 distinct-dictionary calls sync/async. Raw controls and
576 fuel/error/span records match; one baseline allocation outlier stays visible.
All 360 focused checks and 58 complete jobs pass; 31 new rows pass before/after,
with 10 independent CPython goldens. No elapsed measurement or new memory machinery.

The [2026-10-18 reused function scope facts](results/2026-10-18-scope-facts/README.md)
replace repeated runtime/compilation collection with the existing facts stored on
the lowered definition. One frozen pair saves 108,544B per 64 class-method creations;
ordinary controls and all native step arrays match. Elapsed is inconclusive,
including 47% control IQR and 12–16% slower fresh controls; no recapture. All 423
focused checks and 58 complete jobs pass. Upfront metadata retention is explicit;
no new cache/analyzer/owner or architecture-only tests.

The [2026-10-18 executable generic functions](results/2026-10-18-generic-functions/README.md)
remove unconditional definition/body fallbacks while retaining existing TypeScope,
captured cells and metadata order. Shadowing is pinned before edits. Source/lifetime
checks precede one short native/public-fresh pair: about 55 KB less per 128-call job,
stable ordinary allocation controls and unqualified elapsed observations. All 423
focused checks and 58 complete jobs pass. Generic steps change 2,204→2,460, explicitly
documented; recursion/work/cancellation/admission remain bounded. Existing class/
generator/contextual routes remain. No new closure/owner machinery or retiming.

The [2026-10-18 async any/all cursor change](results/2026-10-18-any-all-cursor/README.md)
consumes the existing cursor without two outer wrappers or per-pull forwarding.
One pair saves 96 bytes per affected native call; all 68 exact behavior/fuel/error/
admission records and control arrays match. All 354 focused checks and 58 complete
jobs pass, including paused/funded native cleanup, actual iterator/truth suspension,
cancellation and resumable guest iterators. Initial cleanup exception expectations
were corrected before edits. No new cursor/owner/framework or elapsed claim.

The [2026-10-18 general default-map capacity change](results/2026-10-18-default-map/README.md)
removes intermediate dictionary growth in the two general builders. One native
pair saves 552/912 bytes for 8/32 defaults; empty/single/executable controls and
evaluation/order/alias records match. All 306 focused checks and 58 complete jobs
pass with existing preflights/owners unchanged. Initial diagnostic fixture mistakes
precede runtime edits and remain visible. No shared defaults or elapsed claim.

The [2026-10-18 binding-plan constructor](results/2026-10-18-binding-plan/README.md)
removes three working lists/backings/copies, retaining per-function defaults and
conservative admission. One native pair saves 96–304 bytes per plan; context
controls, metadata and 27 admission/rollback records are identical. All 306 focused
checks and 58 complete jobs pass. Existing CPython binding-error wording differences
remain explicit. No layout cache, new owner, elapsed or CPython speedup claim.

The [2026-10-18 immutable sorted definition](results/2026-10-18-sorted-definition/README.md)
reuses the existing fixed-metadata mechanism for one consumer. One allocation pair
saves160 bytes per warm fresh context; child samples are identical. 1014 focused
checks and 58 complete jobs pass, including readonly metadata and an overlapping
sort callback/cancellation/retained-result owner witness. Initial host-array fixture
and diagnostic-hash metadata mistakes were corrected before runtime edits with
receipts retained. No new owner/cache, timing capture or CPython speedup claim.

The [2026-10-19 complete JSON helper refresh](results/2026-10-19-json-checkpoint/README.md)
uses qualified immutable a22c11b8 and identical Lython controls against CPython
3.13.16. Fresh compile/run takes 9.261–9.541 ms versus 4.461 ms: a diagnostic
2.076–2.139× ratio, unqualified with 18.70%/6.52% Lython spreads and 2.48% CPython.
All 35 responses / 424 invocations match complete goldens. Exact source/binary/
fixture identities, all worker/fixture cleanup and lease release are verified.
One 8.660-second capture; no rerun, new framework, profile or ten-minute lane.
Earlier CI/AU/CC stay revision-specific; no historical trend or individual-fix,
finite-budget or embedding-latency claim.

The [2026-10-18 complete JSON helper comparison](results/2026-10-18-json-checkpoint/README.md)
uses immutable 6584ddd7 and identical Lython controls against CPython 3.13.16.
Fresh compile/run takes 9.466–9.607 ms versus 4.441 ms; nominal 2.132–2.163×
remains unqualified with 13.11% spread in one control. All 35 responses and 432
invocations match complete goldens; source/input identities and all worker,
fixture and lease cleanup are verified. One 8.659-second capture, no runtime or
framework change, retiming, profile or ten-minute lane. Historical AU/CC remain
separate; no individual-fix gain is inferred.

The [2026-10-18 ordinary join call path](results/2026-10-18-join-call/README.md)
uses existing receiver/operand machinery and shared corrected join operations.
One qualified native pair saves 352 B/sync call and 320 B/async; exposed, expanded
and custom arrays and all 448 behavior/fuel records remain unchanged. Initial
fixture mistakes were corrected before runtime edits with receipts preserved.
All 537 focused cases, 11 CPython goldens and 58 complete jobs pass. No new
ownership/iteration framework, retiming, elapsed or CPython speedup claim.

The [2026-10-18 join drain correction](results/2026-10-18-join-drain/README.md)
fixes seven synchronous effect/error-order failures and two builder-scratch
cleanup failures. Existing leased drains keep materialized input funded through
output admission. One native capture pair costs an extra416B/sync and40B/async
call with16Unicode parts; this is a correctness cost, not a performance win.
528 focused checks,8CPython goldens and58complete jobs pass. No new owner or
iteration framework, retiming, elapsed or CPython speedup claim.

The [2026-10-18 small JSON result conversion](results/2026-10-18-json-result-copy/README.md)
shares eager and iterator finalization, replacing eligible UTF-16 heap copies with
fixed managed control spans and the existing funded UTF-8 factory. One capture
pair saves 40–280 bytes per call; large controls and all 560 exact work records
remain unchanged. 788 focused checks pass, including 58 complete jobs. Tiny
admission thresholds can change; retained-result funding and large-output
accounting remain. No elapsed or CPython speedup claim.

The [2026-10-18 complete configuration comparison](results/2026-10-18-config-checkpoint/README.md)
uses immutable 348998dc, two identical Lython controls and CPython 3.13.16. Fresh
compile/run takes 2.584–2.769 ms versus 1.399 ms; nominal 1.847–1.980× remains diagnostic
with 6.69% control divergence and 16.63% spread in one control. All 39 responses/
2,456 invocations match independent complete goldens; source/inputs, all workers/
fixtures and shared lease are verified. One 8.074-second capture, no runtime or
framework change, repeated timing, profile or ten-minute lane. Historical AF stays
separate; no individual optimization gain is inferred.

The [2026-10-18 module/builtin separation](results/2026-10-18-builtin-namespace/README.md)
corrects six public runtime probe failures across both APIs and removes duplicate
builtin storage and identity-based alias compensation. Fresh/child context allocation
falls by 2,936/24 bytes in one fixed capture pair; 1,491 distinct focused cases pass.
Explicit aliases fund retained binding slots; no new owner or namespace framework.
No elapsed, complete-job or CPython speedup claim.

The [2026-10-18 subscript forwarding experiment](results/2026-10-18-subscript-forward-rejected/README.md)
is rejected and restored. Its normalized async/control ratio improved by 3.7%,
below the fixed 5% gate; allocations and all 320 behavior/fuel records are unchanged.
The rebuilt original sources pass 147 focused checks. No retiming, qualified
elapsed improvement, complete-job or CPython speedup is claimed.

The [2026-10-18 two-positional split call path](results/2026-10-18-split-call-count/README.md)
removes eligible method/delegate/argument packaging on the corrected count
semantics. One fixed native capture saves 336 bytes/sync call and 304 bytes/async;
zero/one-argument and exposed controls are unchanged. All 240 work/error records,
1,014 final focused cases and 31 CPython split goldens pass. No elapsed or
complete-job/CPython speedup claim is made.

The [2026-10-18 split count correction](results/2026-10-18-split-count/README.md)
normalizes counts before separator validation, supports large signed 64-bit counts
and awaits type-index descriptors/callbacks. Baseline 18 failures include 10 actual
async-host cases; all 1,011 final focused cases and 30 CPython split goldens pass.
Existing validators/kernels/frontend dispatch remain unchanged. No performance
measurement or two-positional native call change is included.

The [2026-10-18 int/float signature input reuse](results/2026-10-18-builtin-signatures/README.md)
removes two fixed name arrays and repeated interning lookups during invocation
setup. Native warm fresh-context allocation falls from 19,312 to 19,176 bytes;
child control stays at 264 bytes. Mutable type wrappers remain per execution,
and all 846 focused cases pass. Existing layouts were already cached; no new
registry/cache or elapsed/complete-job/CPython performance claim is introduced.

The [2026-10-18 native absolute-value call path](results/2026-10-18-native-abs/README.md)
removes bound argument-array creation for ordinary exact-builtin scalar calls.
One fixed allocation/control comparison saves 32 bytes/call on both APIs; expanded
and synthetic protocol controls are unchanged. All 132 work/identity/error/span
records match; 1,078 final focused cases and 37 CPython goldens pass. Numeric kernel,
call-frame/owner implementations and core dependencies are unchanged. No elapsed
or complete-job/CPython speedup is claimed.

The [2026-10-18 operator unary correction](results/2026-10-18-operator-unary/README.md)
routes neg/pos/invert through the existing unary dispatcher and corrects shared
type-slot lookup, including awaited descriptor binding. Baseline regressions and
the registration-only failure are preserved; 1,071 final focused cases and 36
CPython source goldens pass. Numeric kernels/signatures are unchanged. No new
dispatcher/owner or performance measurement is included.

The [2026-10-18 absolute-value correction](results/2026-10-18-abs-async/README.md)
makes abs and operator.abs await guest __abs__ hooks through the existing unary
dispatcher. Two global-abs async-host regressions fail before the correction;
all 18 protocol controls and 965 final focused cases pass afterward. The numeric
kernel/preflights are unchanged, and 26 programs match isolated CPython.
No performance measurement or primitive dispatch change is included.

The [2026-10-18 type-alias shortcut](results/2026-10-18-alias-capture-rejected/README.md)
was rejected: removing fallback-driven mirroring before replacing lazy captures
loses outer names in three escaped factory cases. Baseline and restored source
pass six CPython-matched programs through both APIs; 129 restored focused checks
pass. All production bytes and the qualified6912bd8f library are restored.
Useful public regressions remain. No timing, retry, relaxed gate or broader
capture redesign; future lowering must migrate lazy evaluation/captures together.

The [2026-10-18 native text dispatch change](results/2026-10-18-native-text/README.md)
extends the existing synchronous native operand gate to exact builtin strings
under async hosting. One predicate changes; no operator/storage/protocol body or
execution framework is added. The fixed native gate passes: async-string median
2.7052 to 2.0524 ms, allocations unchanged, controls within their limits. All 44
frozen work matrices and 694 final focused cases pass. This is an unqualified
native observation; no whole-job or CPython timing claim or repeated capture.

The [2026-10-18 native separator split change](results/2026-10-18-string-split/README.md)
removes ordinary split(sep) method/argument packaging and the split-only
zero-argument delegate, sharing existing validation and UTF-8 storage code.
Native calls save 264 bytes synchronously and 232 bytes through the async API;
omitted/exposed controls are unchanged. All 88 exact work records, 1,020 final
focused cases and 16 isolated CPython goldens pass. No elapsed, whole-job or
CPython timing improvement is claimed; no repeat comparison or long lane.

The [2026-10-18 complete text comparison](results/2026-10-18-text-milestone/README.md)
closes one short capture at immutable `4acd532f`, using unchanged runtime73deb8dc
and two identical Lython controls. Fresh compile/run takes7.354–7.451ms versus
CPython1.527ms; nominal4.818–4.881× remains unqualified with16.27% spread in one
control. All35responses/1,074invocations match complete goldens; immutable source,
inputs, all three worker/fixture cleanups and shared lease are verified. No
runtime/framework change, repeat timing, trace or ten-minute lane.

The [2026-10-18 ASCII casing shortcut](results/2026-10-18-ascii-casing-rejected/README.md)
was rejected: ASCII improved in one native capture, but the Greek-only lower
control rose from 1.913 to 6.124 ms and failed the fixed gate. Allocations and four
work/fuel/refund records were unchanged. All 195 prototype checks passed; source
and test additions were restored and 185 original checks passed afterward.
No repeated timing, relaxed gate, JIT attribution or whole-job/CPython claim.

The [2026-10-18 complete CSV comparison](results/2026-10-18-csv-milestone/README.md)
closes two short captures at immutable `34d4da35`: fresh compile/run 8.549 ms vs
CPython 3.962 ms, warm RunAsync 9.321 ms vs 3.759 ms. Ratios 2.158×/2.480× remain
diagnostic. Earlier-Lython nominal reductions 6.92%/5.66% are separate and
unqualified. All 70 responses / 957 invocations preserve full goldens; immutable
sources, input hashes, all six worker/fixture cleanups and shared lease are
verified. No runtime/framework change, repeat timing, trace or long lane.

The [2026-10-18 lowered-module lifetime change](results/2026-10-18-lowered-root/README.md)
removes unconditional whole-module lowering retention from executable programs
and runners. Three native lifetime failures on4d3c90f9 become passing cases;
ten final sync/async/annotation/lowered controls and543focused checks pass.
A temporary diagnostic confirms lowered runner fields1→0 and executable root
retentionYes→No while original syntax remains. Needed lowered subtrees and
conservative compilation charges remain. The shared async body is equivalent
apart from syntax/statement input substitution. Diagnostic removed;no time/byte/
RSS claim,profile,comparison lane or runtime reclamation mechanism is added.

The [2026-10-18 bridge CPU attribution](results/2026-10-18-bridge-profile/README.md)
is closed without a performance change:3,296samples,zero lost/missing stacks,
81unresolved leaves.546canonical source files align to432b11c0 while the reused
worker retains22648c5a build metadata. JIT31.10%,fixtureReset19.69%,protocolRead
27.28% and RunAsync16.02% are overlapping inclusive CPU figures,not savings or
wall-clock fractions. Whole-process sampling includes protocol outside timed
batches and ordinary JIT;no fixed builtin/setup mechanism was established.
3,559responses/7,116invocations match goldens,inputs/source stay unchanged and
worker/tracer/fixtures are gone. One actual trace only;preflight transport and
Windows git-archive newline corrections are preserved separately. A small
unused prepared-definition owner parameter was removed with admission unchanged;
133existing focused checks pass. No new test/benchmark or runtime speedup claim.

The [2026-10-18 default JSON encoding](results/2026-10-18-json-default-dumps/README.md)
omits ordinary default-call binding/constructor/member packaging and reuses the
existing serializer with stateless builtin metadata. Native scalar counts save
1,840 bytes per default call and 64 bytes per general control through both APIs.
Eight frozen fuel/error/effect cases and 645 focused checks pass. One7.631s bridge
file-job capture records1.514→1.571ms vs CPython0.16484ms;3.80%higher lies within
spread,with no qualified gain/regression.41responses/10,839invocations match full
goldens;input/frozen-file hashes and cleanup are verified. Temporary diagnostic
removed;no repeat capture/profile/full lane. The first launch preflight used a
catalog hash before Windows newline conversion;corrected to frozen file bytes
before any measured run started. That failure remains in private receipts.

The [2026-10-18 governed text reads](results/2026-10-18-text-read/README.md)
admit accumulated working storage before growth and correct cross-window
readline character limits. Empty ownership hooks, duplicate UTF-16 storage/cursor
traversal and CLR accumulation/re-encoding are removed. One 9.309 s capture
records 11.042 → 10.854 ms versus CPython 4.283 ms; the 1.71% decrease is within
spread, with no qualified speedup. All 35 responses / 441 invocations match
goldens; input/frozen-file hashes and cleanup are verified. Ten failed baseline
witnesses, isolated CPython controls and 428 passing focused checks are retained.
No repeat capture or broad codec/ownership investigation ran.

The [2026-10-17 key identity/string comparison](results/2026-10-17-string-key-identity/README.md)
accepts identical keys and omits general equality/suppression for builtin strings.
Independent probes exposed an existing NaN-key bug in the immutable aa9fc0ea
package; the final source fixes it while preserving ordinary NaN inequality.
The first string-only prototype's native/timing/failure evidence is retained
separately. One required final-source capture records 11.700 → 10.953 ms versus
CPython 4.259 ms: nominal 6.4% lower, unqualified. Its 9.317 s run preserves all
36 responses / 456 invocations, inputs, frozen files and cleanup. Native fixed-
operand timings permit optimizer folding; zero allocations and outcome controls
are useful, not their apparent per-call speedups. No further capture or trace ran.

The [2026-10-17 ordinary JSON loads](results/2026-10-17-json-default-call/README.md)
removes generic binding storage from a single-positional default call. A preceding
ten-second JSON-helper profile has 7,952 samples, no lost events/missing stacks
and source aligned to 6c9a51df; inclusive costs select the boundary, not a gain
prediction. Native128-call counts save 11,264 bytes through both APIs. One 9.662 s
whole-job capture records 11.893 → 11.631 ms versus CPython 4.245 ms: nominal
2.2% lower, unqualified. All 37 responses / 469 invocations match goldens; inputs,
frozen binaries and cleanup are verified. No repeat trace/capture or new framework.

The [2026-10-17 JSON numeric scratch placement](results/2026-10-17-json-number-scratch/README.md)
keeps token-sized scratch only around the allocating integer fallback. Separate
native counts remove 32 bytes per ordinary conversion/hook, with the large-integer
control unchanged. One 9.473 s complete-job comparison records 12.283 → 12.438 ms
versus CPython 4.288 ms; the 1.26% higher median is within 1.57%/5.45% spread.
All 37 responses / 471 invocations match goldens, twelve inputs and 55 baseline
files remain unchanged, workers/fixtures are gone and the lease is released.
No latency gain, repeated capture or new benchmark framework is claimed.

The [2026-10-17 JSON helper controls](results/2026-10-17-json-helper-warm-async/README.md)
measure identical f728636a binaries through warm completed-host RunAsync:
12.223–12.340 ms versus CPython 3.13.16 4.309 ms, unqualified. One 9.495 s run,
37 responses / 466 invocations, independent full goldens and cleanup are retained.
The separate [shared-defaults candidate](results/2026-10-17-json-default-options/README.md)
removes 11,264 → 0 bytes per 128 native default bindings. Its one 9.945 s complete
job records 13.007 → 12.993 ms versus CPython 4.520 ms, spreads 2.39%/2.23%:
no elapsed gain established. All 37 responses / 442 invocations match goldens;
twelve inputs and frozen sources unchanged, workers/fixtures gone. Native probes
are removed; no recapture or variant campaign.

The [2026-10-17 CSV pull consolidation](results/2026-10-17-csv-pull/README.md)
deletes one single-use async forwarding method. The sole affected complete-job
capture records 10.007 → 10.194 ms versus CPython 3.783 ms; 4.31%/2.43% spreads
leave the 1.87% higher median inconclusive. BW retains the working file/patch,
all 36 responses / 510 invocations, unchanged inputs and terminal cleanup. A
preceding ten-second all-thread profile of immutable 1b6a1531 supplies attribution,
not a gain prediction. Two staging failures occurred before timing; evidence is
preserved. No repeated timing or profiling campaign ran.

The [2026-10-17 complete CSV helper RunAsync snapshot](results/2026-10-17-csv-helper-warm-async/README.md)
compares clean VM Release builds 5b546b2e → 1b6a1531 with CPython 3.13.16.
Warm medians are 10.379/10.346/3.756 ms; the nominal 2.755× external ratio is
unqualified and the 0.31% earlier-Lython change is within spread. All 37 responses /
519 invocations preserve exact stdout/file goldens. The sole capture took 9.320 s;
input hashes, clean sources, exited workers/fixtures and released lease are
recorded. BV includes completed-host RunAsync setup and file work; compilation,
startup, real suspending I/O, finite-budget/latency claims remain outside it.

The [2026-10-17 declared-builtin operand experiment](results/2026-10-17-builtin-operands-rejected/README.md)
is rejected. Both isolated variants preserve 4,686 binding records and save no
allocations; synchronous sorting medians are worse, with some wide distributions.
The interface, binder branches and prototype assertions were removed. Four public
delayed-I/O callback/cancellation/refund regressions remain. Raw data and both
patches are archived; no job-level/CPython/host-latency benefit is established.

The [annotation-only local CSV diagnostic](results/2026-10-17-annotation-only-locals/README.md)
compares 3f799746 and one guard deletion with CPython 3.13.2 on Windows.
Fresh-job medians 21.677/19.745/17.014 ms have 38.0%/48.9%/13.5% spread;
no direction or ratio is qualified. One 11.228 s capture, 36 full-output/file responses,
100 unchanged frozen files and recorded worker cleanup are preserved. It is a
source-level executable-coverage correction, with no measured elapsed claim.

The [2026-10-17 native dictionary-read diagnostic](results/2026-10-17-native-dictionary-dispatch/README.md)
compares 2cb4a786 with the archived guard-only patch: allocation stays320bytes
per128reads in all four native cases. Async dictionary median falls while its
unchanged sync control rises20.2%; concurrent boundary tests and local timing
leave elapsed benefit unqualified. This capture precedes the associated stored-key
async correction and forwarder deletion; it supplies no complete-job/CPython ratio.
Source and raw samples are preserved; temporary test source is removed, with no
retuning or recapture after the later correctness change.

The [2026-10-17 complete CSV helper snapshot](results/2026-10-17-csv-helper-fresh/README.md)
adds the fresh compile/run boundary at efccd974:17.358ms, CPython 3.13.2 14.348ms,
spreads45.85%/29.76%. Earlier Lython a5a2fc0f measures14.818ms with48.73% spread;
no direction or ratio is qualified. One10.119s capture,275correctness responses,
100frozen files and seven removed workers/fixtures are recorded. BS is separate
from the original CSV job and its quiet-VM history. Four helper variants now have
maintained output, batch ownership, retention and cancellation regressions; the
original scripts and their larger/stress fixtures remain unchanged.

The [2026-10-17 complete text fresh snapshot](results/2026-10-17-text-fresh/README.md)
fills compile-to-result at 877f20e2 with identical Lython controls: 16.539/14.994ms,
CPython 3.13.2 11.188ms; spreads48.87%/106.79%/39.21%. The9.34% control difference
and very large spread leave this local Windows result inconclusive. One8.788s
capture; independent three-size stdout/report goldens,35micro responses and
both50-file manifests pass; seven workers/fixtures gone. No optimization/VM-warm
comparison, qualified ratio or repeat capture. BR adds the fresh row; the first
comparison table now leads with complete jobs, retaining every prior warm/core
row in a separate visible table.

The [2026-10-17 bounded integer parser checkpoint](results/2026-10-17-utf8-small-integer/README.md)
reuses the complete fresh CSV job at e01d45c6→27a61681:8.181→7.503ms, CPython
3.849ms; spreads7.54%/6.44%/1.14%. The nominal8.3% lower median remains unqualified
and inconclusive. One8.197s capture; independent three-size goldens and both604-file
source manifests pass, seven workers/fixtures gone and lease free. Separate native
128-call allocation is11,576→4,408bytes through both APIs/text/bytes.86CPython
cases and80frozen traces pass; temporary probe removed. No elapsed gain is inferred
from allocations. BP refreshes the fresh CSV row; BO remains historical.

The [2026-10-17 native integer-text call diagnostic](results/2026-10-17-integer-text-calls/README.md)
compares8505c40d with production sources committed asdada7c9c. For128fixed native
calls, allocation is20,792→11,576bytes synchronously and32,056→11,576bytes through
asynchronous hosting, for both text and bytes. Four warmups/eight samples per case,
80exact fuel/span traces and pending-input ownership controls preserve the boundary.
The temporary diagnostic is removed; raw evidence/source are archived. Complete
CSV timing and CPython performance remain unmeasured for this change.

The [2026-10-17 fresh CSV snapshot](results/2026-10-17-csv-fresh/README.md)
fills compile-to-result for the complete 4,096-record file job at 0f1cff1f.
Two identical Lython controls observed 8.114/8.133 ms, CPython 3.8777 ms,
with 14.79%/7.15%/0.89% spread: one diagnostic, no qualified ratio.
Independent three-size file/stdout goldens pass; 1,954 frozen files match;
five workers/fixtures are gone and the lease is free. Separate native context
setup allocation is 27,568 → 26,088 bytes; the CSV controls do not time that change.

The [2026-10-17 replacement consolidation](results/2026-10-17-string-replacement/README.md)
removes the duplicate bounded algorithm and per-scalar intermediates. A separate
bounded Unicode member-call witness allocates 115,032 → 4,392 bytes; its 32 KiB
both-API working-set script now passes. Existing unlimited Unicode pipeline:
3.621 → 2.981 ms, CPython 0.5404 ms; spreads 80.79%/12.69%/0.52%. The 17.7%
lower median is inconclusive within baseline spread, with no elapsed benefit or
qualified ratio. One 7.759 s capture; no retry/retuning/new producer. Frozen
sources/independent three-size goldens match; seven workers/fixtures gone,
services inactive, lease free. BN replaces the rejected AZ prototype in the
leading table; AZ's original evidence and historical identities remain here.

The [2026-10-16 cursor dispatch change](results/2026-10-16-cursor-sync-dispatch/README.md)
removes async forwarding for synchronous builtin pulls and existing user/async
iterator tasks. One unchanged branch loop observed 5.979 → 5.889 ms, CPython
1.0203 ms; spreads 4.74%/0.70%/0.37%. The 1.5% lower median is within baseline
spread: inconclusive, no speedup/qualified ratio. One 7.257 s capture, no retry,
retuning or new producer. Frozen manifests and independent three-size arithmetic
goldens match; seven workers/fixtures gone, services inactive, lease free. BM
refreshes the same branch-loop row; BG remains historical. No later HEAD claim.

The [2026-10-16 JSON forwarding deletion](results/2026-10-16-json-container-forwarding/README.md)
removes container forwarding awaits and the redundant prefix decoder/result
record. One unchanged JSON transform observed 2.626 → 2.691 ms, CPython 0.4439 ms;
spreads 8.42%/12.68%/0.33%. The 2.5% higher median is within spread, inconclusive.
One 7.516 s capture, no retry/retuning/new producer; frozen sources and independent
three-size goldens match, seven workers/fixtures gone, services inactive, lease
free. BL refreshes the same in-memory row; BK remains historical. The later
integer-admission correction is allocation/containment work, excluded from the
measured source pair and with no elapsed-time claim.

The [2026-10-16 JSON literal change](results/2026-10-16-json-literal-dispatch/README.md)
removes true/false/null from the generic async token route, preserving the existing
scalar admission and actual callback suspension. Both roles include the preceding
parser-error correction. One unchanged JSON parse/edit/dump case observed
2.365 → 2.380 ms, CPython 0.4414 ms; spreads 17.11%/22.15%/0.15%. The 0.6% change
is within spread: no benefit or qualified ratio established. One 7.385 s capture,
no retry/retuning/new producer. Independent three-size row goldens and frozen
sources match; seven workers/fixtures gone, services inactive, lease free. BK keeps
this in-memory case separate from historical complete file jobs and later HEAD.

The [2026-10-16 invocation-owned locals change](results/2026-10-16-owned-function-locals/README.md)
consolidates executable parameter/local storage and admits unpooled capacity
before binding. One unchanged starred-list helper observed26.602 →27.550ms,
CPython2.9739ms; spreads2.29%/2.36%/2.13%. The3.6%higher median demonstrates no
performance benefit. One8.503s unqualified capture, no retry or retuning. Both
roles pass three-size goldens, frozen manifests match, seven workers/fixtures are
gone and the lease is free. BJ preserves this source pair; no HEAD timing claim.

The [2026-10-16 direct split change](results/2026-10-16-direct-string-split/README.md)
removes per-receiver callable/binder work from ordinary zero-argument exact-string
split and reuses the governed kernel. One unchanged complete text job observed
7.018 → 6.405 ms, CPython 1.3426 ms; spreads10.70%/10.08%/0.87%. The 8.7%
change is within spread: no speedup or qualified ratio established. One8.094s
capture after corrected catalog-profile setup; original failure occurred before
timing. Three-size independent file/stdout goldens and frozen manifests match;
seven workers/fixtures gone, services inactive and lease free. AY refreshes the
leading text snapshot; AN remains separate history. No repeat/tuning/new producer.

The [2026-10-16 synchronous advancement change](results/2026-10-16-generator-sync-advance/README.md)
removes the async advancement wrapper from ordinary synchronous generator pulls,
sharing managed admissions, frame entry/exit and result reading. One unchanged
warm reduction observed 4.118 → 3.912 ms (5.0% lower median), CPython 0.5586 ms;
spreads 3.67%/1.55%/0.39%. This remains one unqualified diagnostic. Both roles
pass three-size goldens; frozen sources match, seven workers/fixtures are gone,
services inactive and lease free. AX refreshes the leading snapshot; AW and AV
remain separate historical source pairs. No repeat/tuning/new producer/full lane.

The [2026-10-16 executable generator migration](results/2026-10-16-executable-generators/README.md)
uses genuine child instructions and existing frames/cells; ordinary lowered
creation/overlay and fallback-only parent mirroring are removed. One unchanged
warm numeric reduction observed 4.330 → 4.252 ms, CPython 0.5611 ms; spread
1.45%/3.83%/0.26%. The 1.8% change is inconclusive: no established speedup or
qualified ratio. One 7.219 s capture, no repeat/tuning/new producer. Both roles
pass three-size independent goldens, frozen manifests match, seven workers and
fixtures are gone, services inactive and lease free. AW preserved that revision’s
numeric snapshot; AV and its earlier binding result remain historical evidence.

The [2026-10-16 generator-binding correction](results/2026-10-16-generator-bindings/README.md)
removes per-yield binding scans, preserving walrus writes through rejected filters,
errors and suspended captured cells. One unchanged warm numeric reduction observed
4.919 → 4.077 ms (17.1% lower median), CPython 0.5695 ms; spreads
0.64%/1.11%/0.22%. This is an unqualified single diagnostic; generator-expression
bodies remain lowered. Frozen sources match, seven workers/fixtures are gone,
services inactive and lease free. Failed CRLF shell setup stopped before timing;
its evidence is retained. No repeat, retuning, new producer or full lane.

The [2026-10-16 terminal-admission correction](results/2026-10-16-terminal-admission/README.md)
ends the command after refused admission while preserving native cleanup,
retained funding and later commands. One unchanged complete JSON aggregation
observed 9.713 → 9.492 ms, CPython 4.175 ms. The 2.3% difference is within baseline
spread; no speed change, qualified ratio or host-latency result. One 9.533 s owned
capture, no repeat or retuning; frozen sources match and all workers exited. The
initial catalog-profile preparation failure occurred before timing and is preserved.

The [2026-10-16 builtin setup change](results/2026-10-16-builtin-setup/README.md)
shares three fixed plain definitions, retaining private type graphs and current
caller module resolution. One existing empty warm invocation control observed
23.318 -> 23.445 µs, CPython 1.506 µs; spread 3.7%/2.9%/1.0%. The 0.5% higher
median is inconclusive. No improvement, useful-workload ratio or gap attribution
is established. Source removes three wrappers, two temporary parameter arrays
and three catalog lock/lookups per root execution. One 7.186 s owned capture;
no repeat/tuning/new producer. All frozen sources match, seven workers exited,
services inactive and lease free. Catalog selection and line-ending proof
corrections preserve their original evidence; no measured input changed.

The [2026-10-16 fresh JSON boundary](results/2026-10-16-json-fresh/README.md)
measures fresh compile/run of the unchanged complete aggregation job inside warm
hosts. Two identical Lython binaries at 1ad18b79 observed 9.8803/10.0244 ms,
CPython 4.3585 ms; spread 21.3%/8.7%/0.7%. These are equivalent controls and an
unqualified diagnostic, not a runtime change, confidence interval or pure compile
cost. One 9.364 s capture, no other lane/repeat/tuning/new framework. Independent
three-size goldens per control, all 524 production sources and shared tooling
verified, seven workers/fixtures gone and lease free. AU adds the fresh boundary;
AT and all warm data remain separate. No historical warm subtraction or current
overall ratio follows.

The [2026-10-16 JSON number delivery](results/2026-10-16-json-utf8-numbers/README.md)
removes unconditional CLR numeric text conversion and shares raw-hook preparation
and existing magnitude admission. One unchanged complete job observed 9.8138 ->
9.5872 ms, CPython 4.4011 ms, spread 17.6%/4.1%/0.3%. The 2.3% lower median is
inconclusive; no improvement established. Two frozen sources, exact loaded versions,
all three golden sizes per role, normal policies, one 9.626 s timing command, seven
workers/fixtures removed and lease free. No repeat, tuning or new producer. AT
replaces AS's warm row; the earlier string-token evidence remains:

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Complete JSON-lines aggregation [AS], warm | 4,096 records / 110,592 UTF-8 bytes | 10.548 ms | 4.651 ms | — | Short diagnostic at b85fdde9; candidate median 2.9% higher, within 17.2% baseline spread; inconclusive |

| Label | Date | Measured Lython revision | Evidence |
| --- | --- | --- | --- |
| AS | 2026-10-16 | b85fdde9 on f3e05ad9; one frozen parser source; unchanged JSON aggregation | [Owned UTF-8 tokens, preserved decoding and inconclusive timing](results/2026-10-16-json-utf8-tokens/README.md) |

The [2026-10-16 JSON token delivery](results/2026-10-16-json-utf8-tokens/README.md)
removes the CLR string/re-encoding round trip for well-formed unescaped values
and keys, reuses governed copied storage and deletes the single-use helper.
Escaped/invalid paths and scratch charges stay. One unchanged complete JSON job
observed 10.2500 -> 10.5481 ms, CPython 4.6510 ms, spread 17.2%/3.9%/0.5%.
The 2.9% higher candidate median is inconclusive; no improvement or slowdown is
established. One frozen source, correct loaded revisions, all three golden sizes
per role, normal policies, one 10.076 s timing command, seven workers/fixtures gone
and lease free. No repeat or tuning. AS replaces AC's warm row; original evidence:

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Complete JSON-lines aggregation [AC], warm | 4,096 records / 110,592 UTF-8 bytes | 9.692 ms | 4.260 ms | — | Historical warm diagnostic; elapsed change within spread |

| Label | Date | Measured Lython revision | Evidence |
| --- | --- | --- | --- |
| AC | 2026-10-15 | Working JSON value-forwarding candidate on 50583327; one frozen production source; unchanged script/tooling | [Complete JSON file job and callback/ownership checks](results/2026-10-15-json-value-dispatch/README.md) |

The [2026-10-16 prefix-call delivery](results/2026-10-16-string-prefix-calls/README.md)
shares existing receiver/argument slots and the Unicode/tuple kernel for ordinary
one-argument exact-string startswith. Both roles include keyword/static tuple
correctness fixes. One unchanged complete search observed 5.6667 -> 3.2231 ms,
CPython 0.2845 ms, spread 57.4%/63.7%/1.3%. The 43.1% lower median is inconclusive;
no improvement is established. Six frozen sources, correct loaded versions, all
three golden sizes per role, normal policies, one 8.736 s timing command, seven
workers/fixtures removed and lease free. No repeat/retuning or new producer.
AR replaces P's earlier warm row only; P's fresh compile/run row and identity stay.
The prior warm evidence remains:

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| File search with helper and protected early exit [P], warm | 2,049 lines / 42,010 UTF-8 bytes | 4.249 ms | 0.265 ms | — | One candidate diagnostic; inconclusive (56.4% spread) |

The [2026-10-16 subscript-augmented delivery](results/2026-10-16-augmented-subscript-stores/README.md)
retains receiver/key in existing frame slots and deletes their prepared read/store
arms. One unchanged complete configuration job observed 3.3377 -> 3.3093 ms,
CPython 1.6950 ms, spread 6.9%/10.4%/4.9%. The 0.9% change is inconclusive;
no performance benefit is established. Three frozen production differences,
role-specific loaded revisions, all three golden sizes per role, normal policies,
one 11.668 s command, seven exited workers/removed fixtures and a free VM lease.
AQ adds a warm configuration snapshot alongside AF's separately marked fresh
compile/run measurement. No repeat, retuning or current overall ratio claim.

The [2026-10-16 member-augmented delivery](results/2026-10-16-augmented-member-stores/README.md)
uses existing executable instructions and a shared store, removing the ordinary
fallback and prepared member-read arm. One unchanged 2,048-call method job observed
1.1127 -> 1.1439 ms, CPython 0.11515 ms, spread 1.2%/2.6%/0.2%. The candidate median
is 2.8% higher; no performance benefit is established. Source consolidation remains;
no retuning or repeat. Three frozen differences, correct loaded revisions, normal
policies, one 7.455 s command, seven exited workers and a free VM lease. AP supplies
the leading method snapshot; AO's earlier sort row and identity remain archived.
No current overall ratio or compile-to-result claim follows.

The [2026-10-16 loop-target delivery](results/2026-10-16-loop-target-stores/README.md)
shares executable target operations for ordinary for loops and eager
comprehensions, preserving the existing inlined binding scope. One unchanged
2,048-tuple stable-sort job observed 1.6082 ->1.5503 ms, CPython0.3033 ms,
spread3.5%/0.9%/0.5%. The observed3.6% change is comparable to baseline spread and
is inconclusive. Three frozen compiler differences, normal policies, one7.703s
command, seven workers exited and VM lease available; no repeat/retuning or new
producer. AO previously replaced the leading sort snapshot; AL's time and revision remain
archived below. No current overall ratio or compile-to-result claim follows.

The [2026-10-16 executable subscript-store delivery](results/2026-10-16-executable-subscript-stores/README.md)
compiles ordinary target reads in the existing frame and removes their general
syntax/lowered-evaluation binding. One complete text job observed 6.7951 ->
6.4143 ms, CPython 1.2393 ms, spread 12.4%/4.8%/1.9%. The timing improvement is
inconclusive; source consolidation remains. Both roles include the type-slot
mutation correctness prerequisite, which is unmeasured. No repeat or retuning.
The leading text snapshot AN replaces AM; AM's original row and identity remain:

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Complete text-summary file job [AM], warm | 3,072 lines / 35,840 UTF-8 bytes | 6.831 ms | 1.322 ms | — | Short baseline diagnostic at 72856c8c; 5.7% spread; wrapper candidate inconclusive and removed |

| Label | Date | Measured Lython revision | Evidence |
| --- | --- | --- | --- |
| AM | 2026-10-16 | Baseline 72856c8c; final production sources restored exactly; rejected lower-call candidate excluded from the leading time | [Complete text job, unchanged fixtures and rejected prototype](results/2026-10-16-string-lower-call/README.md) |

The [2026-10-16 lower-call wrapper experiment](results/2026-10-16-string-lower-call/README.md)
was rejected and all production changes removed. One complete text job observed
6.8308 -> 6.7464 ms, CPython 1.3224 ms, spread 5.7%/4.8%/0.9%: a 1.2% change
within variability. No repeat/retuning. The historical representative text row AM
uses the 72856c8c **baseline**; historical AA stays archived below. Twelve
useful public regressions remain, with no required discarded implementation.

The [2026-10-16 native-index candidate](results/2026-10-16-async-native-index/README.md)
shares synchronous observed transfer for exact integer reads on sealed native
sequences. One existing ASCII indexed-text diagnostic observed 0.4388 -> 0.3918 ms,
CPython 0.05844 ms; spread 2.0%/4.0%/0.2%, 10.7% lower unqualified median. Both
roles already contain the shared type-level index correctness prerequisite, so
that fix is unmeasured. One production difference, identical tooling; no repeat,
retuning or new workload. Custom protocols still suspend and cancel.

The [2026-10-16 exact-unary dispatch candidate](results/2026-10-16-async-integer-unary/README.md)
shares the synchronous arithmetic/value-transfer handler for exact integer operands
under RunAsync. One dynamic loop diagnostic observed 1.5519 -> 1.1854 ms,
CPython 0.2455 ms; spread 1.1%/1.4%/0.4%. The observed 23.6% lower Lython median
is unqualified. Both roles use the same fixture/tooling; one production file
differs. Guest callbacks still suspend/cancel. No repeat or retuning.

The [2026-10-16 eager-comprehension delivery](results/2026-10-16-inline-comprehensions/README.md)
uses ordinary instructions in the caller frame with the existing binding scope.
One declared per-item helper-call diagnostic observed 0.6107 -> 0.5851 ms,
CPython 0.1253 ms; Lython spread 1.1%/0.9%, CPython spread 38.3%. The 4.2%
lower Lython median is unqualified and the external ratio is inconclusive. No
retuning, repeat or retired arithmetic producer. Lazy bodies/nested lambda
capture remain dynamic; duplicate generator-family dispatch was removed.

The [2026-10-15 lambda delivery](results/2026-10-15-executable-lambdas/README.md)
deletes the separate callable, reuses shared binding/metadata and then compiles
eligible bodies through the named-function builder/cells. One unchanged stable-sort
case observed 1.778 -> 1.595 ms, CPython 0.3020 ms, spread 2.4%/3.5%/0.7%:
10.2% lower median, unqualified. Both roles already contain runtime consolidation;
the class deletion is unmeasured. No new fixture or repeat capture.

The [2026-10-15 expanded-call delivery](results/2026-10-15-executable-expanded-calls/README.md)
shares generator call lowering and first fixes argument evaluation for non-callable
targets. One fixed forwarding-helper diagnostic observed 2.412 -> 2.077 ms,
CPython 0.2443 ms, spread 0.6%/2.2%/0.9%; observed 13.9% lower median, unqualified.
No new binder or owner; an obsolete starred-call fallback assertion was removed.

The [2026-10-15 f-string delivery](results/2026-10-15-executable-fstrings/README.md)
reuses existing generator field/specifier/join operations. One fixed helper
diagnostic observed 2.454 -> 2.125 ms, CPython 0.4201 ms, with 1.3%/2.5%/2.5%
spread. The observed 13.4% lower median is unqualified. Shared async formatting
helpers remain; this closes a syntax fallback without a new runtime subsystem.

The [2026-10-15 expanded set/dictionary delivery](results/2026-10-15-expanded-set-dict-executable/README.md)
shares existing generator operations and corrects own-value-before-key-validation
order first. One helper diagnostic observed5.444 ->4.963ms, CPython0.6326ms;
spread0.7%/1.3%/1.4%, without a qualified multiplier or retuning. Small hash-trace
differences were reclassified using primary compiler source and a16-entry probe;
no CPython-threshold staging system was added.

The [2026-10-15 ordinary starred-tuple lowering](results/2026-10-15-starred-tuple-executable/README.md)
shares existing generator display/immutable-finish operations. Qualification fixed
empty tuple identity in both roles first. One helper diagnostic gave2.659 ->2.629ms,
CPython0.3477ms, within1.1%/5.6%/0.6% spread: no elapsed benefit or retuning.
Conservative working-list/result overlap and fuel changes are explicit contracts.

The [2026-10-15 ordinary starred-list lowering](results/2026-10-15-starred-list-executable/README.md)
reuses existing generator display operations, removing one real expression
fallback and associated local mirroring without runtime changes. One shared
helper diagnostic observed2.664 ->2.179ms, CPython0.3541ms; spread1.3%/0.7%/0.3%.
No qualified multiplier, complete-job claim or rerun of rejected comprehensions.

The [2026-10-15 fresh configuration job](results/2026-10-15-config-fresh/README.md)
compiles once and edits/publishes one JSON file per invocation in an already-running
host. Two identical654b4f5a Lython controls gave2.817-3.142ms, CPython1.572ms;
their11.5% discrepancy and4.7%/5.7%/9.5% spread remain visible. No qualified ratio,
earlier/later optimization claim or retuning. Same-path fixture replay preserves
fresh inputs and independently checked output bytes.

The [2026-10-15 async exact-integer operator family](results/2026-10-15-async-integer-operators/README.md)
extends the existing synchronous handler gate beyond addition. Yielding controls
first exposed and fixed synchronous invocation of other augmented user slots.
One unchanged branch-loop diagnostic observed 9.846 ->6.321ms, CPython0.9599ms;
spread3.0%/10.4%/0.5%. No qualified multiplier, complete-job claim or retuning.

The [2026-10-15 exact-byte accumulator](results/2026-10-15-bytes-sum/README.md)
removes byte iteration adapters, per-item integer boxes and repeated arithmetic
under existing native ownership. Final magnitude admission precedes allocation;
dynamic starts still suspend. One complete manifest job is6.394 ->6.080ms,
CPython0.4382ms, within6.7%/11.7% spread; no qualified elapsed claim or retuning.

The [2026-10-15 JSON value-dispatch cleanup](results/2026-10-15-json-value-dispatch/README.md)
removes one generated async forwarding state machine without another parser.
One complete JSON file job is 9.425 ->9.692ms, CPython4.260ms; the change is within
14.3%/7.3% spread. No elapsed benefit is established; no repeat or retuning.

The [2026-10-15 UTF-8 uppercase delivery](results/2026-10-15-utf8-upper/README.md)
removes ordinary per-scalar CLR strings and shares existing expansions with legacy
casing helpers. One new complete report job is identical in both roles:
2.610 ->2.651ms, CPython0.2444ms, spread9.5%/10.8%. No elapsed benefit established;
keep allocation removal and independent report/legacy casing checks, without retuning.

The [2026-10-15 UTF-8 lowercase delivery](results/2026-10-15-utf8-lower/README.md)
removes ordinary per-scalar CLR strings while preserving expansions, final sigma,
governed capacity and cancellation cleanup. One complete file-job diagnostic is
7.107 ->6.553ms, CPython1.231ms; the 7.8% difference is near baseline spread7.5%,
so timing remains inconclusive. Keep allocation removal; no retry/retuning.

The [2026-10-15 compiled-layout prototype](results/2026-10-15-parameter-layout-rejected/README.md)
is rejected and removed: fresh default-helper compile/run is 185.643 ->293.721us,
CPython37.302us. Preserve the single capture and source patch, without retuning
or attribution to a particular component. Only an unused binding map/field deletion
and public isolation/admission regressions remain; that final cleanup is unmeasured.

The [2026-10-15 async startup reuse](results/2026-10-15-async-worker-reuse/README.md)
removes per-invocation thread construction and the async-void relay. One empty
setup diagnostic predates a required structural ambient correctness fix; preserve
its exact prototype identity and noise. No final-source or useful-script timing
claim follows. Async ambient scopes are narrowed to synchronous work after pulls.

The [2026-10-15 executable call-cache deletion](results/2026-10-15-call-cache-deletion/README.md)
removes an identity-only record, unused async frame arrays and compiler cache
metadata. One synchronous keyword-call diagnostic is within spread; no elapsed
claim or retry. General invocation and member resolution remain intact.

The [2026-10-15 positional-call delivery](results/2026-10-15-positional-calls/README.md)
removes packaged arguments and argument-factory closures from eligible compiled
calls while sharing canonical frame/body cleanup. One async helper diagnostic
observes a 13.7% lower median; baseline spread is 9.4%, so no qualified multiplier.
Bound-values/frame copying and receiver-independent method dispatch remain open.

The [2026-10-15 live-defaults delivery](results/2026-10-15-function-defaults/README.md)
removes disconnected binding metadata and persistent default-map fees, admits
layout/default capacity and awaits custom-key lookup. One async default-call
diagnostic has a 4.2% higher median; no speedup or qualified regression is claimed.
The CPython dictionary-lifetime edge and original-scale accounting boundaries are
documented explicitly. Direct calls and immutable layout sharing remain open.

The [2026-10-15 method-exposure delivery](results/2026-10-15-method-exposure/README.md)
corrects exposed builtin method identity while keeping immediate-call caching.
One dictionary-update diagnostic is within spread; no timing claim or retry.
Receiver-independent dispatch and direct argument binding remain separate work.

The [2026-10-15 async integer-addition delivery](results/2026-10-15-async-integer-dispatch/README.md)
shares the synchronous observed-value handler for exact integers. One async-loop
diagnostic observes a 14.4% lower median with 6.0% candidate spread. A genuinely
yielding in-place addition witness also exposed and fixes synchronous user-slot
dispatch. Other augmented operators remain separate work.

The [2026-10-15 async-function delivery](results/2026-10-15-async-compiled-functions/README.md)
shares compiled function construction and real async bound-slot/cell execution.
One completed-host async diagnostic observes a 40.3% lower median; the sync
control is inconclusive. The import refresh bug and an initial client-validator
failure are preserved separately from the valid timing samples. Broader async
dispatch and mutable function metadata remain open.

The [2026-10-15 list-comprehension investigation](results/2026-10-15-list-comprehensions-rejected/README.md)
rejects two executable-list producers despite passing semantic/containment gates.
A direct iterator branch removes added boolean/dispatch work but does not show
a benefit over the existing evaluator. Public regressions and raw evidence remain;
no dormant runtime implementation or architecture-specific tests are retained.

The [2026-10-15 protected-loop checkpoint](results/2026-10-15-protected-loop-jumps/README.md)
removes ordinary protected-loop compilation rejection and preserves abrupt-target
regions during jump normalization. Separate fresh compile/run and warm file-search
diagnostics remain inconclusive; structural and cleanup correctness justify the
smaller compiler. Async function and comprehension parity remain separate work.

The [2026-10-14 complete file comparison](results/2026-10-14-representative-file/README.md)
records identical Lython controls against CPython. Background JIT and fixture
costs limit CPU attribution; no speculative runtime branch or timing repeat.

The [2026-10-14 complete text comparison](results/2026-10-14-representative-text/README.md)
records one final-sigma lookup candidate and rejects its inconclusive timing.
The exhaustive ASCII context regression remains; the runtime shortcut was removed.

The [2026-10-14 list and enumeration checkpoint](results/2026-10-14-list-and-enumeration/README.md)
records published list construction and explicit sequence wrapper state. Separate
list+sum and generator/drain diagnostics remain inconclusive; the combined
correctness gates do not establish GC-independent reclamation or host latency.

The [2026-10-14 native state checkpoint](results/2026-10-14-native-working-state/README.md)
combines native buffer descriptors, active caller links and stored sum state.
It preserves bounded streaming and rejection controls; complete ownership and
forced-GC independence remain open. One short generated-sum comparison is
inconclusive and is not a qualified speed or embedding-latency claim.

The [2026-10-13 native activation array](results/2026-10-13-native-activation-rejected/README.md)
passes its focused root/streaming gates but remains incomplete and adds recurring
bookkeeping before a GC-independent rule exists. Its one short call measurement
is inconclusive (+6.79% median,6.76% candidate spread). The standalone foundation
is rejected and reverted; neither current-HEAD timing nor proven regression is claimed.

The [2026-10-13 ownership inventory](results/2026-10-13-ownership-inventory/README.md)
identifies native state, delegate and frontend-capture obligations beyond the
34 snapshot-owner types. Its finite static audit and36 public root checks are
correctness/design evidence; no collector, logical refunds or performance claim
is introduced. Forced-GC replacement remains open.

The [2026-10-13 ordinary-call transfer](results/2026-10-13-call-transfer/README.md)
preserves inputs and results in the existing operand stack at call boundaries,
without universal instruction retention. Nine before/after probes validate this
prerequisite; forced-GC independence remains open. The one short keyword-call
diagnostic has uncertain recurring cost and supplies no qualified performance claim.

The [2026-10-13 frame-transfer experiment](results/2026-10-13-frame-transfer-rejected/README.md)
passed candidate correctness gates but added about 10% to the short integer-loop
diagnostic. The universal retained extent/completion rule was rejected and
reverted. These candidate timings are historical; they do not describe HEAD.

# Lython and CPython comparison contract

Specification version **6**, used by the declared milestone collections. The catalog
and explicit correctness check are available. Persistent worker primitives are
available, including supervised correctness/timer smokes, paired collection,
machine checks and raw-evidence report rendering.
The [explicit body-bytecode round](results/2026-10-11-method-body-bytecode/README.md)
rejects a flat instruction-loop compiler: method time changes -2.12%/+5.99%,
so its target gain does not repeat. Both producers pass 9,526 matching Debug and
Release tests and seven canonical CPython cases each. Fourteen paired groups
take about eight seconds, separate allocation/native groups about four seconds,
all under 30-second caps. All controls and native tiers remain visible. Eleven
independent control-flow/host boundary cases are added; an additional post-gate
lifetime check passes separately on both producers. Production remains unchanged,
and cold compiler costs and broader design work stay open. No full timing lane
or favorable recollection ran.

The [typed body-plan round](results/2026-10-11-method-body-plan/README.md)
rejects the isolated compiler prototype: method time changes +1.36%/-8.68%,
while keyword calls regress in both passes. Both producers pass 9,503 matching
Debug and Release tests and seven canonical CPython cases each. Fourteen paired
groups take about eight seconds; separate allocation/native groups take about
four seconds, all under 30-second caps. Every control and native tier is retained.
Production stays unchanged; broader compiler/design work remains open. No full
timing lane or favorable recollection ran.

The [lowered operation-selection round](results/2026-10-11-lowered-kind-dispatch/README.md)
rejects preselected node dispatch: method time changes +3.2%/-3.4%, with the
second gain inside its 3.9% spread. Fourteen paired micros take 8.04–8.05s
including cleanup; separate allocation/native groups take about four seconds.
Both producers pass 9,466 matching Windows Debug and Linux Release tests plus
seven complete canonical CPython cases each. Eight existing fixed checkpoint
fixtures now exercise both shared and direct function dispatch. Every control
and native tier remains visible; production stays unchanged and the broader
method-body compiler remains pending. No full timing lane or recollection ran.

The [prepared augmented-target round](results/2026-10-11-prepared-augmented-target/README.md)
rejected; the prototype remains isolated. It compares identical slice-protocol/governance corrections
on both producers, with 42 independent pre-prototype checks. Fourteen paired
micros take 8.04–8.05s including cleanup; separate allocation/native
checks and all controls remain visible. No full lane or favorable recollection
is added; the next milestone remains deferred.
Initial delivery CI exposes a Linux `/proc` read race in an existing test helper.
Two actual-worker checks reproduce it; the corrected supervisor suite passes
all 31 cases on both platforms without changing production or timing evidence.

The [corrected function-scoped dispatch round](results/2026-10-11-scoped-dispatch-corrected/README.md)
accepts direct lowered dispatch only inside active synchronous function bodies.
Method jobs take 4.4% / 10.1% less time and allocate 1.3% / 4.4% less in two passes,
with no repeated control regression beyond the frozen bounds. Fourteen paired
micros take about eight seconds each; separate allocation and native-code groups
take about four seconds. Both producers pass 9,399 Windows Debug tests and 9,399
Linux Release tests, plus seven complete canonical CPython cases each. All controls and emitted native tiers
remain visible. No full lane, recollection or milestone-qualified ratio is added.
The original delivery failed Linux CI; both new producers include the same stack
publication and exception-propagation corrections. Its original measurements,
failed qualification and diagnostic trials remain visible in the reports.

The [current class-method design assessment](results/2026-10-11-class-method-design/README.md)
adds two ten-second CPU traces (14–17s including cleanup) and 23 independent
checks on unchanged production. Attribute lookup, assignment and method invocation
remain substantial sampled costs. Real class-factory checks pin fuel/funding/cell
boundaries and genuine async suspension with same-run reentry. Executable
class-method bodies remain pending; no new timing ratio or full lane is added.
Initial Windows CI exposed an existing concurrency test's dependency on caller
ThreadPool scheduling. A 10.5s isolated diagnostic reproduces that dependence;
dedicated test callers preserve the deadlines and pass under a one-thread pool.
Production and original capture evidence remain unchanged.

The [2026-10-09 bounded run](results/2026-10-09-quick/README.md) completed all four
lanes within ten minutes each. Its raw-evidence review found no qualified
workload ratios; its exclusions remain unchanged.
The [focused basic-loop investigation](results/2026-10-09-core-loop/README.md)
completed in 2m13s. Its separate CPU profile identifies interpreter dispatch
as the leading design investigation; strict stability checks still withhold
qualified comparative multipliers.

The [first synchronous dispatch improvement](results/2026-10-09-sync-dispatch/README.md)
used two short collections of about seven seconds each. Their diagnostic medians
show 13.4–14.0% less time on the 16,384-iteration loop, with correctness checks
passing and milestone qualification deferred.

The [subsequent short experiments](results/2026-10-09-integer-addition/README.md)
accepted guarded exact integer addition after 10.7–14.1% less loop time in two
seven-second collections, and rejected direct loop-name storage after inconsistent
results. Every collection is retained. A separate operator-fallback correctness
fix preserves the measured integer guard.

The [bound loop-name compiler change](results/2026-10-09-bound-loop-names/README.md)
showed 20.2–22.4% less loop time in two collections of about seven seconds each
against a baseline with the same independently tested function-scope fix.
Simple targets reuse existing bound stores; complete outputs and checkpoints
remain checked. Both receipts are retained, with milestone qualification pending.

Ordinary improvement rounds use the short `micro` command below. Full lanes
are reserved for occasional declared milestones, with their existing ten-minute
cap and qualification rules. Microbenchmarks provide diagnostic feedback in
seconds; they do not run idle gates, establish a qualified speedup or replace
correctness checks.

The [first consolidated milestone](results/2026-10-09-first-milestone/README.md)
collected the original runtime and current runtime with an identical v6 harness
and fixed 14-case warm profile. Its lanes lasted 5m58s and 6m28s. Both controls
qualified, but no workload qualified on both producers. All original exclusions
remain visible. The current ASCII pipeline and zlib application qualified;
the basic-loop comparison still withholds a certified multiplier.

The [second consolidated milestone](results/2026-10-10-second-milestone/README.md)
completed two warm lanes in 6m00s and 6m17s. The current basic loop, keyword
calls, stable sort, JSON and zlib qualify against CPython. The basic loop takes
about 2.6–2.8 times CPython's time. The reference tiny control failed spread,
so no old/new improvement ratio qualifies. The current ASCII pipeline falls
below the unchanged invocation floor. Every exclusion remains final; ordinary
rounds keep using seconds-long micros.

The [string reclamation round](results/2026-10-09-string-reclamation/README.md)
used five short decomposition jobs and a separate 15-second CPU trace, followed
by two repetitions each of the pipeline and split/count. Direct string ownership
registrations reduced pipeline medians by 65–67% and split/count by 63–66% in
7–8-second collections. These remain diagnostic results; the next full milestone
is deferred.

The [module completion experiment](results/2026-10-09-module-completion/README.md)
used six collections of about seven seconds each. Direct synchronous completion
helped empty and tiny controls modestly, but the integer loop took slightly
longer in both repetitions, so that candidate remains isolated. An independent
writer publication cleanup correction is retained; no full lane was collected.

The [lazy string cache experiment](results/2026-10-10-string-cache-metadata/README.md)
used ten micros of 6.4–7.0 seconds and separate fixed-count allocation checks.
It reduced pipeline managed allocation by 5.11% but gave no repeatable target
latency improvement, so the candidate remains isolated. No full lane ran.

The [string ownership investigation](results/2026-10-10-string-ownership/README.md)
uses two boundary diagnostics of about 6.5 seconds and six public micros.
It identifies adoption as a target and rejects a direct string guard after
slower pipeline results. A structural ownership proposal remains pending.

## Short improvement rounds

The [call-frame investigation](results/2026-10-10-call-frames/README.md) uses two
fourteen-second CPU captures on existing positional/keyword jobs. A lazy function
namespace candidate remains isolated after a full Debug streaming-memory failure;
its timing micros are held. The report retains the failure and focused negative
controls. Production is unchanged, and the existing streaming test gains
failure-only allocation/window diagnostics for further investigation.

The [text-emission rollback correction](results/2026-10-10-streaming-emission-ownership/README.md)
fixes charges stranded when an unpublished text result's reclamation registration
is denied. Eight deterministic baseline probes fail; the fix passes all ten new
checks and the full Debug suite. Retained values stay charged. This separate
correctness prerequisite does not establish the original streaming failure's
cause or a speed improvement; it runs no timing micros or full lanes.

The [stable-sort decomposition](results/2026-10-10-stable-sort-decomposition/README.md)
uses twelve baseline variant micros, a separate bounded CPU capture and eight
old/new micros. Tuple-owned reclamation metadata reduces construction time by
about 25% in both replicas and isolated constructor/registration allocation by
18.7%. The change is integrated; high-variance sort results and the remaining
CPython gap stay visible. All micros take 6.4–7.5 seconds; no full lanes run.

The [exact integer sort-key round](results/2026-10-10-integer-sort-keys/README.md)
refreshes current construction/sort CPU leads with two bounded captures, then
accepts a guarded integer comparison after ten 6.4–7.3-second micros. Scalar-sort
time falls about 28% in both replicas; full sort/output falls 22–26%, retaining
its spread. Mixed keys retain Python dispatch, and all 9,140 Debug checks pass.
Full lanes remain deferred to a declared consolidated milestone.

The [small immutable tuple round](results/2026-10-10-small-tuple-adoption/README.md)
removes temporary adoption collections for tuples of up to two items while
preserving logical charges and rollback. Eight 6.4–7.3-second micros show 24–26%
less construction time; complete sort observations remain noisy. Separate retained
constructor/registration diagnostics show about 67% less allocation for scalar
pairs, with the larger-tuple control essentially unchanged. All 9,152 Debug checks
pass. No full benchmark lanes run.

The [streaming reservation-progress correction](results/2026-10-10-streaming-reservation-progress/README.md)
reproduces the unchanged 64 KiB streaming fixture under a controlled collection
schedule, including the original frozen namespace source. A successful scratch
reservation now lets the following allocation collect remaining garbage. Three
new regressions and all 9,155 Debug checks pass; pinned denials remain bounded.
Every untraced diagnostic process takes less than a second. There are no normal
timing samples, full lanes or speed claims; namespace qualification remains open.

The [new combined lazy-namespace round](results/2026-10-10-function-frame-namespaces/README.md)
passes its own full 9,161-check Debug gate before ten 6.4–7.3-second micros.
Complete-call managed allocation falls about 11%; the change is integrated as an
allocation improvement. Call timing changes remain below spread or mixed, so no
reliable throughput gain is claimed. The original failed namespace receipt stays
preserved. No full lanes run; future milestones keep their ten-minute cap.

The [direct instruction-array round](results/2026-10-10-instruction-arrays/README.md)
uses compiler-owned arrays directly in dispatch, with readonly synchronous reads.
Twelve 6.4–7.2-second micros show 13–15% less loop time, 7–8% less positional-call
time and 9–12% less keyword-call time. Empty/pipeline controls have small mixed
changes; sort remains too noisy for a reliable claim. All 9,161 Debug checks pass
before timing. No full lanes run; qualification waits for a declared milestone.

The [execution-check cold-path round](results/2026-10-10-checkpoint-cold-paths/README.md)
keeps sweep/exception work separate from an inlined checkpoint body. Twelve
6.4–7.1-second micros show 11% less loop time, 6–9% less positional-call time and
3–4% less keyword-call time. Sort medians rise with substantial spread and remain
visible as a limitation; empty/pipeline changes are small and mixed. All 9,164
Debug checks pass before timing. No full lanes run.

The [embedded operand-stack round](results/2026-10-10-frame-stack-storage/README.md)
places stack state inside each interpreter and borrows it by reference. Separate
fixed-count diagnostics show about 3.7% less managed call allocation; the change
is integrated as a storage improvement. Twelve 6.4–7.1-second micros retain mixed
call timings, so no reliable throughput gain is established. All 9,164 Debug checks
pass before collection. Retained helper-launcher/declaration errors precede any
allocation worker; the successful helper build is rehashed and reused. No full lanes run.

The [shared frame-owner round](results/2026-10-10-shared-frame-owner/README.md)
combines interpreter and slot state, removing a separate object and duplicate
references. Two fixed-count passes show about 6.3–6.4% less managed call allocation.
The change is integrated for storage savings; timing varies between replicas, so
no reliable throughput gain is established. Twelve paired micros stop in about
eight seconds each, and separate allocation groups in about four seconds, all
under 30-second caps. All 9,164 Debug checks pass before collection. The empty
control's small allocation increase and first-sort variation remain visible.
No full lanes run; milestone qualification stays deferred.

The [context/namespace storage round](results/2026-10-10-context-namespace-owner/README.md)
places namespace-frame storage inside its execution context. Both fixed-count
passes show about 4.1% less managed call allocation; the change is integrated for
storage savings. Call timings are small or mixed, with no reliable throughput
gain. Twelve paired micros stop in about eight seconds each; separate allocation
groups in about four seconds, all under 30-second caps. All 9,165 Debug checks
pass before collection, including concurrent namespace aliases through both views.
Every observation is retained. No full lanes run; qualification remains deferred.

The [shared locals/stack-buffer experiment](results/2026-10-10-shared-frame-buffer/README.md)
remains isolated. It reduces complete-call managed allocation by 4.18–4.29%,
but both replicas show slower positional calls (2.64%/1.99%), keyword calls
(3.55%/1.46%) and integer loops (3.74%/1.35%). Fourteen paired micros include a
prospective stack-growth control and stop in about eight seconds each. Separate
allocation groups stop in about four seconds; all have 30-second caps.
All 9,169 candidate Debug checks and complete CPython outputs pass before timing.
The runtime, tests and harness retain their preceding delivered versions.
No full lanes run; all observations and the rejection remain visible.

The [direct binder-array experiment](results/2026-10-10-binding-plan-arrays/README.md)
remains isolated despite smaller compiled binder methods. Twelve paired micros
show slower positional calls in both replicas and mixed keyword results, with
effectively unchanged call allocation. All 9,165 frozen Debug checks pass first;
normal groups take about eight seconds and allocation groups four, under 30-second
caps. A separate ten-second current-loop CPU capture then identifies dispatch,
stack transfer and integer operations as broader leads; its group stops in
14.08 seconds. Sampling is diagnostic, with no qualified CPU or speed ratio.
Production, tests and harness remain unchanged. No full lanes run.

The [direct local-dispatch round](results/2026-10-10-direct-local-dispatch/README.md)
integrates LoadLocal/StoreLocal into the synchronous main switch with their
original helpers and instruction boundaries. Both basic-loop replicas improve
7.07%/8.40%; call changes are small, other controls mixed and allocation effectively
unchanged. Generated Tier1 main-method code grows from 1,774 to 2,675 bytes;
all native bodies and tiers remain visible. Twelve paired micros take about
eight seconds each, with separate allocation/native groups about four seconds,
all under 30-second caps. All 9,165 frozen Debug checks pass before collection.
No full lanes run; milestone qualification remains pending.

The [plain-jump dispatch experiment](results/2026-10-10-direct-jump-dispatch/README.md)
remains isolated: loop changes -0.76%/-2.38% are inconclusive against their spreads,
positional calls reverse -5.21%/+5.04%, and emitted Tier1 main code grows another
185 bytes. All 9,165 Debug checks pass; twelve paired micros take about eight
seconds each, with separate allocation/native groups about four seconds under
30-second caps. A separate ten-second accepted-runtime CPU capture then records
dispatch, iteration and scalar-value/observation leads; its group stops in 14.08
seconds. No qualified CPU/speed claim, full lane or production change is made.

The [integer-observation round](results/2026-10-10-integer-observation/README.md)
keeps the exact signed-byte memory preflight while routing boxed integers before
generic value checks. Loop time falls 6.53%/5.37% and positional calls 8.59%/9.26%
in two short repeats; other controls are mixed and allocations effectively
unchanged. Thirteen budget regressions pass on the original runtime first, and
all 9,178 Debug checks pass before collection. Twelve micros take about eight
seconds each, with separate allocation/native groups about four seconds under
30-second caps. The final interpreter body stays 2,675 bytes; observation code
grows 103 bytes. Every result remains visible. No full lanes run; qualification
waits for a declared milestone.

The [checked-range iteration round](results/2026-10-10-checked-range-iteration/README.md)
combines range advancement and checked pulls in one active enumerator, preserving
the original checkpoint/ownership boundaries and materializer source shape.
Loop time falls 3.53%/4.50% in two repeats against roughly 1% spreads; call decreases
are small, sort is mixed and pipeline changes are small. Loop managed allocation
increases 8–22 bytes per complete job; final interpreter code grows 9 native bytes.
Eighteen exact boundary regressions pass the unchanged runtime first; all 9,196
Debug checks pass before collection. Twelve micros take about eight seconds each,
with separate allocation/native groups about four seconds under 30-second caps.
Every result and native body/tier remains visible. No full lanes run.

The [direct iterator-dispatch round](results/2026-10-10-direct-for-next-dispatch/README.md)
routes synchronous ForNext through its original operations from the main switch.
Loop time falls 3.50%/3.73% in two repeats against 0.5–0.9% spreads; calls and
pipeline reverse, and full-sort increases remain within their retained spreads.
Allocation is effectively unchanged. Final Tier1 main code grows 24 bytes;
early OSR grows 1,231 bytes. Every body/tier remains visible. A separate
ten-second baseline CPU capture supplies fresh leads, without a before/after
CPU claim. All 9,196 Debug checks pass before twelve micros of about eight
seconds, with separate allocation/native groups about four seconds, each under
a 30-second cap. No full lanes run; milestone qualification remains pending.

The [simple compiled-return improvement](results/2026-10-10-simple-return-functions/README.md)
removes transient interpreter and array allocations from guarded return-only
functions, while retaining their actual context, binder, checkpoints and funding.
Two paired short replicas show 16.6–18.1% less positional-call time and 13.0–20.1%
less keyword-call time, with about 53% less managed job allocation. All controls
remain visible, including an unexplained first-loop increase of 7.61% that does
not repeat in replica two (-0.29%). Native code grows with different emitted
profiles; every body and tier is retained. Twelve collections stop in about eight
seconds each; separate allocation/native groups take about four seconds, all
under 30-second process-group caps including cleanup. No full lane or recollection.
Full frozen Debug passes 9,232 checks and VM Release 204 white/296 public, with
six complete CPython goldens before timing. Milestone qualification remains pending.

The [inline operand-stack experiment](results/2026-10-10-inline-operand-stack/README.md)
remains isolated. It saves about 9.86–10.01% of managed call allocation, but both
repetitions show slower loops (11.99%/15.69%), positional calls (10.19%/6.93%)
and keyword calls (2.12%/8.33%), beyond their spreads. Fifteen independent
growth/reference/disposal checks pass on unchanged production first and are
delivered; all 9,211 candidate Debug checks pass before collection. Two separate
current call profiles supply frame/allocation leads without a savings forecast.
Twelve micros take about eight seconds each, allocation/native groups four,
under 30-second caps. All observations and native bodies/tiers remain visible.
No full lane runs; production keeps the preceding implementation.

The [context-parent consolidation](results/2026-10-10-context-parent-link/README.md)
derives ancestry from the existing frame link while retaining real function
contexts, namespaces, lexical services and containment checks. Nine independent
constructor/namespace/nonlocal checks pass original production first. Complete-call
managed allocation falls about 3.0–3.1%; keyword time falls 2.5–5.6% in both short
replicas, while positional time stays within its spreads. The loop +3.91%/-1.43%
reversal and all sort/pipeline controls remain visible without speedup claims.
The checked getter grows from 5 to 39 native bytes; every body/tier is retained.
Full Debug passes 9,241 checks and VM Release 213 white/296 public with six full
CPython goldens before timing. Twelve collections take about eight seconds each,
with separate allocation/native groups about four seconds under 30-second caps.
No full lanes run. The report also clarifies unresolved keyword allocation types
in the preceding CPU trace; complete-job byte measurements support its saving.

The [module-function discovery improvement](results/2026-10-10-module-super-discovery/README.md)
skips implicit-super receiver/class-cell discovery for unowned module-root functions,
retaining owner binding and nested/live class-cell barriers. Eighteen independent
checks pass unchanged production first. Keyword calls take 8.8–10.2% less time in
both short replicas, beyond their spreads; the first positional result overlaps
its spread, so no repeatable positional gain is claimed. All sort/control changes
and the first-pass sort allocation increase remain visible. Full Debug passes
9,259 tests; VM Release 228 white/329 public and six complete CPython cases pass
before timing. Twelve micros take about eight seconds each; separate allocation/
native groups about four, all under 30-second caps. No full lane or qualified
Python multiplier; all emitted native bodies/tiers stay retained.

The [unused super-anchor experiment](results/2026-10-10-super-anchor-state/README.md)
keeps its production change isolated. Positional/keyword allocation falls about
3.1%, but owned-method allocation falls only 0.46%, below the prospective 0.5%
floor required across all three call cases. Positional timing improves 2.8–3.3%;
keyword/method timing has no repeated benefit. Every control remains visible,
including opposite sort changes. Thirteen independent funding/parity checks are
retained; all 58 selected checks pass original production first. The candidate
passes 9,272 Debug tests, VM Release 239 white/331 public and seven canonical
CPython outputs before fourteen micros taking 8.04–8.06 seconds each. Separate
allocation/native groups take about four seconds, all under 30-second caps.
No full lanes run and no observations are recollected to chase acceptance.

The [method receiver lease experiment](results/2026-10-10-method-receiver-lease/README.md)
also stays isolated. Method allocation savings are only 35.76 / 180.16 bytes per
complete job, below the declared 0.5% floor; method timing changes +0.72% / -5.62%,
while positional calls regress +2.82% / +3.43% beyond both retained spreads.
Twenty-nine regression checks remain integrated: 87 selected checks pass original
production first, followed by 9,301 candidate Debug tests, VM Release 256 white /
343 public and seven complete CPython outputs. Fourteen micros take about eight
seconds each, with separate allocation/native groups and no full lanes. Follow-up
accepted-runtime method traces take about sixteen seconds each and identify
attribute handling and shared async lowered dispatch as further design leads.
Their overlapping inclusive stacks do not predict removable costs.

The [lowered-dispatch compatibility baseline](results/2026-10-10-lowered-dispatch-baseline/README.md)
fixes false static rejections of local lazy type aliases and later reads of
captured collections. It retains 34 regression checks, passes 9,335 Debug tests
and matches twenty trusted CPython snippets in both run modes.

The [direct synchronous lowered-dispatch experiment](results/2026-10-10-lowered-sync-dispatch/README.md)
passes 9,335 Debug tests and fresh Release/CPython gates for both producers.
Fourteen paired micros take about eight seconds each. Method jobs improve
4.8–6.6%, but the loop control repeatedly regresses 2.6–3.2%, failing the frozen
acceptance rule. The candidate stays isolated. Separate allocation/native
diagnostics retain all observations; no full lane or recollection runs.

The [default-object attribute compatibility baseline](results/2026-10-10-default-object-attribute-baseline/README.md)
corrects member-assignment order, lowered async target stores and descriptor
binding for mutation slots. Its 33 independent checks reproduce nine original
runtime failures. Fixed baseline and isolated direct-slot candidate each pass
9,368 Debug tests and 58 CPython comparisons. At that baseline's delivery, fresh
Release gates and separately declared short micros were pending; no timing or
full lanes ran.

The [exact default object-slot round](results/2026-10-10-default-object-attribute-slots/README.md)
integrates direct descriptor-aware handling after method jobs improve 31.0% /
28.3% and process-wide complete-job allocation drops 48.4% in both passes.
Both fresh Release producers pass 276 white/390 public checks and seven canonical
CPython comparisons before fourteen paired micros taking 8.04–8.06 seconds each.
All controls remain visible: positional calls change +4.39%/-3.67%, while full
sort/output changes +6.62%/+3.09% within 12.1%/18.9% retained maximum spreads.
The unchanged prospective repeated-regression rule passes. Separate allocation
and native-code groups take about four seconds; all collections have 30-second
external caps. No full lanes, recollection or milestone qualification.

The [current method CPU refresh and scoped-dispatch baseline](results/2026-10-11-function-scoped-dispatch-baseline/README.md)
uses two 10-second captures on accepted attribute-slot production, with whole
groups taking 14.07/16.08 seconds. Shared async statement dispatch remains in
33.5–35.6% of inclusive sampled stacks; these overlapping shares are design leads,
not predicted savings. Twenty-seven independent invocation checks precede an
isolated function-body strategy. Baseline and candidate each pass 9,395 Debug
tests, 181 selected checks and 58 CPython comparisons. Fresh Release gates and
short candidate micros remain pending; no gain or integration is claimed.

The [stored-root and value-transfer checkpoint](results/2026-10-13-value-transfers/README.md)
records the combined frame/code descriptors and operand publication at native
callbacks, full Release/stress/package checks and one inconclusive short cost
comparison. No logical reclamation or forced-GC removal is enabled. The
[immutable scalar.44 assessment](results/2026-10-13-regex-scalar-44/README.md)
records the remaining managed-only backend blocker separately.

The [zip construction and row continuation](results/2026-10-14-zip-state/README.md)
records the demonstrated cursor admission gap, focused publication/denial/alias
controls, removal of a redundant row copy, and one inconclusive cost comparison.
The higher candidate median is retained; no gain or regression is established.
Full/platform/package qualification is deferred to the next coherent checkpoint.


## Governed worker ownership and complete CSV field runs (2026-10-14)

Worker lifetime cleanup is accepted at f95fb734: a shared unbounded allowance,
previous-result/per-call compilation disposal, final response borrowing and cached
compilation cleanup are part of the recorded boundary. 190 focused controls pass,
with 12 local and five VM representative cases matching isolated UTF-8 CPython.

One ten-second kernel CPU profile locates dictionary CSV iteration/parser work;
one 8.725s paired diagnostic tests unquoted field runs. The candidate preserves
capacity admission and Python results, but its nominal 2.2% lower median is smaller
than 15.4% baseline spread. It is rejected and its runtime branch is removed.
The useful funded-capacity denial/resumption regression remains. This bounded
experiment stops here, without replacement samples, a full lane or a speed claim.
[Complete evidence, caveats and rejected patch](results/2026-10-14-representative-csv/README.md)
keeps CPython as the primary external baseline.


## Complete JSON loads: remove temporary decoder machinery (2026-10-14)

The existing option binder now returns scanner options for both explicit decoders
and exact builtin load/loads. Module loads avoid temporary decoder attributes,
constant callable and bound method/argument binding; custom classes still construct
and dispatch. The same parser, validation, hooks and safety guards remain.

One CPU profile supplies the construction/binding lead. One short complete-job
comparison is inconclusive (3.7% nominal change, 11.8% candidate spread). A separate
reused managed-allocation helper measures 13.790MB versus9.923MB per exact job,
28.0% lower, with ordinary CLR GC and deterministic shared-budget cleanup. Retain
for measured allocation reduction and simpler runtime work; make no elapsed-time
or embedding-latency claim. No timing repeat or new parser/cache/framework.
[Identities, all results and qualification](results/2026-10-14-representative-json/README.md)
preserve the distinct CPython and earlier-Lython comparisons.


## Diagnostic comparisons by revision

- [Complete CSV helper checkpoint](results/2026-10-17-csv-checkpoint/README.md): exact Release runtime 463b7a7a on the dedicated VM, fresh compile/run. Lython 8.581 ms versus CPython 3.13.16 at 3.718 ms, nominally 2.308× in one unqualified diagnostic. Earlier Lython at 8.432 ms is a separate control; the 1.77% median difference is within observed spread. Complete stdout/file checks and cleanup pass; no byte-kernel attribution or repeated capture.

- [Ordinary function operand transport](results/2026-10-16-ordinary-function-transport/README.md): keyword/variadic Python calls consume existing operands through the shared binder, removing temporary transport and the async preparation closure. Mutable defaults and actual protocol suspension keep invocation-owned slots. Sixteen frozen guard/effect matrices match. One unchanged keyword loop has a 1.8% lower median within 13.3% baseline spread; inconclusive, no speedup established.

- [Direct ordinary builtin len](results/2026-10-16-direct-length-call/README.md): existing operand storage bypasses pooled transport and a copied parameter array, sharing the string-entry guard. One existing constructed-text strip/length loop has a 26.4% lower median with 18.8% baseline spread; noisy and unqualified. Seven CPython controls, frozen guards, suspended input owners and terminal refusal pass. No timing repeat or complete-file-job claim.

- [Ordinary async-host control flow](results/2026-10-16-async-control-flow/README.md): synchronous handlers replace generic async forwarding for ordinary jumps/returns/exception routing and non-instance conditions. Existing branch loop median is 8.2% lower in one unqualified capture; original guard matrices, real truth-slot suspension and cleanup before finally are covered. Initial catalog-profile setup correction occurred before any timing.

- [Bulk string/bytes hashing](results/2026-10-16-bulk-byte-hash/README.md): two per-byte loops replaced with the safe managed bulk API. Equal-key, custom-key and retained ownership controls pass. One complete-text diagnostic has a 30.5% lower median alongside 30.0% baseline spread; inconclusive, no speedup established.

- [Shared zero-argument string entry](results/2026-10-16-noarg-string-consolidation/README.md): three marker/helper families replaced by one guarded entry and cached static operations. Source shrinks by 30 lines; existing semantic/guard/ownership controls pass. The one complete-text diagnostic is inconclusive (1.6% higher median within 9.0% spread); no speedup or regression established.

- [Direct immediate string lower](results/2026-10-16-direct-string-lower/README.md): existing receiver dispatch and shared casing/ownership operation remove per-receiver callables. The unchanged complete text job has a 7.6% lower median within 10.5% baseline spread, inconclusive. One declared capture; frozen guards, rejection-origin controls and retained/cancelled users preserved.

- [General method receiver binding](results/2026-10-16-general-method-binding/README.md): existing binder consumes the receiver separately for ordinary Python methods, including keywords/variadics/lowered callers. One short allocation diagnostic shows 72 excess bytes per entry become zero; no elapsed or CPython performance claim. Original guards, live defaults and suspended ownership remain.

- [Direct ordinary method transport](results/2026-10-16-lowered-method-transport/README.md): caller/prepended-self packaging removed for positional lowered methods, retaining original bodies and guards. The unchanged 2,048-call warm job has a 7.4% lower Lython median in one short, unqualified diagnostic. This is separate from AP and the rejected AZ class compiler.

- [JSON numeric/callback forwarding](results/2026-10-16-json-number-forwarding/README.md): two forwarding state machines removed with caller-owned frame/scratch unchanged; complete job median 6.1% higher within 14.2% baseline spread, no performance benefit established. One declared new capture; callback-overhead magnitude unmeasured.

- [Direct JSON string-token dispatch](results/2026-10-16-json-string-dispatch/README.md): unchanged complete JSON aggregation job; 8.1% lower median within 10.2% baseline spread, inconclusive. Same decoder, managed admission, ownership and frozen checkpoints; one short capture with declared case/source/size/lane verification.

- [Prepared ordinary class methods](results/2026-10-16-compiled-class-methods/README.md): fresh compile/run; candidate median 23.3% higher, unqualified. Actual 16,384-call input differs from the original medium-case declaration; mismatch and original receipt retained, no repeated capture. The prototype is rejected after an Ubuntu public-test-host stack overflow; its compiler and admission-policy changes are withdrawn. Independent fixes survive.

Preserved from BENCHMARKS.md before reducing its leading table. Revision and
measurement-status labels retain their original meaning; see the
[measurement identities](HISTORY.md#which-code-and-machine-were-measured).

| Workload | Input size | Lython time | CPython time | Lython / CPython | Measurement status |
| --- | --- | ---: | ---: | ---: | --- |
| Stable sort with lambda key [AO], warm `Run` | 2,048 tuples; build, stable-sort and print | 1.550 ms | 0.3033 ms | — | Short diagnostic at 155843d0; target stores compiled; timing change inconclusive (baseline spread 3.5%) |
| Stable sort with lambda key [AL], warm `Run` | 2,048 tuples; build, stable-sort and print | 1.595 ms | 0.3020 ms | — | One diagnostic; observed 10.2% lower Lython median; executable lambda bodies; comprehension unchanged; precedes the final name-label fix |
| Expanded-call forwarding helper [AK], warm `Run` | 2,048 calls; tuple and keyword mapping | 2.077 ms | 0.2443 ms | — | One diagnostic; observed 13.9% lower Lython median; fallback/mirroring removed; general binder remains |
| Formatted report helper [AJ], warm `Run` | 2,048 calls; padded row and computed next value | 2.125 ms | 0.4201 ms | — | One diagnostic; observed 13.4% lower Lython median; ordinary f-string fallback removed |
| Expanded set/dictionary helper [AI], warm `Run` | 2,048 calls; mixed literal/expanded entries | 4.963 ms | 0.6326 ms | — | One diagnostic; observed 8.8% lower Lython median; fallbacks/mirroring removed |
| Ordinary starred-tuple helper [AH], warm `Run` | 2,048 calls; six-element immutable results | 2.629 ms | 0.3477 ms | — | One diagnostic; elapsed change within spread; fallback removed, conservative overlap funded |
| Ordinary starred-list helper [AG], warm `Run` | 2,048 calls; six-element merged lists | 2.179 ms | 0.3541 ms | — | One diagnostic; observed 18.2% lower Lython median; fallback/mirroring removed |
| JSON configuration read/edit/write [AF], fresh compile/run | 16 existing paths / 181 UTF-8 input bytes | 2.817-3.142 ms | 1.572 ms | — | Two identical Lython controls differ by 11.5%; one diagnostic; no qualified ratio |
| Branch/integer loop [AE], warm `RunAsync` | 16,384 iterations; modulo/comparison/add/subtract | 6.321 ms | 0.9599 ms | — | One diagnostic; observed 35.8% lower Lython median; remaining gap substantial |
| Complete copy/checksum/manifest job [AD], warm | Two files / 4,107 input bytes | 6.080 ms | 0.4382 ms | — | One diagnostic; difference within spread; byte-allocation removal only |
| Complete JSON-lines aggregation [AC], warm | 4,096 records / 110,592 UTF-8 bytes | 9.692 ms | 4.260 ms | — | One diagnostic; elapsed change within spread; redundant wrapper removed |
| Complete uppercase-report file job [AB], warm | 1,024 lines / 23,552 UTF-8 bytes | 2.651 ms | 0.2444 ms | — | One diagnostic; elapsed change within spread; allocation removal only |
| Complete text-summary file job [AA], warm | 3,072 lines / 35,840 UTF-8 bytes | 6.553 ms | 1.231 ms | — | One diagnostic; observed improvement near baseline spread; no qualified ratio |
| Default-argument helper [Z], fresh compile/run | 256 calls | 0.186 ms | 0.0373 ms | — | Historical diagnostic at 3b34fad2; sharing prototype was slower and removed; final cleanup unmeasured |
| Split lines, strip and sum lengths [X], warm `RunAsync` | 32,768 lines / 425,984 UTF-8 bytes | 23.464 ms | 3.132 ms | — | One default-strip diagnostic on 88e0992b; inconclusive, baseline/candidate spread 16.5%/23.3% |
| Unicode string-key word counts [W], warm `RunAsync` | 16,384 updates / four keys; completed host operations | 15.146 ms | 1.313 ms | — | One direct-get diagnostic on 7e2c1f27; observed 16.0% lower Lython median; remaining gap substantial |
| Append to distinct lists and total values [N], warm `RunAsync` | 16,384 lists/appends; completed host operations | 7.819 ms | 1.132 ms | — | One direct-append diagnostic on 99e8701f; observed 21.8% lower Lython median; no qualified speedup |
| Full-body parameter-update helper [B], warm `RunAsync` | 16,384 calls; completed host operations | 10.372 ms | 1.287 ms | — | One matching-local-storage diagnostic on f93e9258; observed 3.6% lower Lython median; no qualified speedup |
| Keyword helper [K], warm `Run` | 16,384 calls | 4.262 ms | 1.174 ms | — | One call-cache deletion diagnostic on 6262ea94; difference within spread; no timing claim |
| Helper using positional/keyword defaults [V], warm `RunAsync` | 16,384 calls; completed host operations | 9.555 ms | 1.258 ms | — | One correctness/admission diagnostic on 96596466; candidate median 4.2% higher; spreads 3.0%/3.9% |
| Tuple-key dictionary updates [D], warm `Run` | 16,384 updates | 30.573 ms | 2.418 ms | — | One method-identity diagnostic on 0eb587a9; difference from earlier Lython is within spread |
| Integer reduction loop [I], warm `RunAsync` | 16,384 iterations; completed host operations | 2.660 ms | 0.579 ms | — | One diagnostic on 88f78106; 14.4% lower median than earlier async Lython; candidate spread 6.0% |
| Integer helper calls [E], warm `Run` | 16,384 calls | 3.289 ms | 0.965 ms | — | One candidate diagnostic on 015184e6; sync change within spread |
| Same helper calls [R], warm `RunAsync` | Same input; completed host operations | 5.378 ms | 1.001 ms | — | One positional-transport diagnostic on 1f2ef306; 13.7% lower Lython median; baseline spread 9.4% |
| Helper with nested filtered list comprehension [H] | 1,024 inputs / 1,364 results | 0.816–0.825 ms | 0.0793–0.0990 ms | — | Warm diagnostics at 3cad4157; both executable candidates rejected; CPython spread 23.7–38.2% |
| File search with helper and protected early exit [P], warm | 2,049 lines / 42,010 UTF-8 bytes | 4.249 ms | 0.265 ms | — | One candidate diagnostic; inconclusive (56.4% spread) |
| Same file search [P], fresh compile and run | Same input; stop after line 1,025 | 6.041 ms | 0.395 ms | — | Fresh code per job; inconclusive (42.4% spread); startup excluded |
| Complete file copy/checksum manifest [F] | 2 files / 4,107 bytes | 6.103–6.254 ms | 0.442 ms | — | One warm diagnostic; two identical Lython controls, up to15.1% spread |
| Complete Unicode text summary [T] | 6,144 words / 35,840 UTF-8 bytes | 8.018 ms | 1.258 ms | — | One warm diagnostic at ca432c22; noisy ASCII candidate rejected |
| Complete JSON-lines summary [J] | 4,096 records / 110,592 UTF-8 bytes | 11.537 ms | 4.225 ms | — | One warm candidate diagnostic; timing inconclusive (11.8% spread), allocation reduction measured |
| Complete CSV summary [C] | 4,096 records / 57,361 UTF-8 bytes | 7.530 ms | 3.483 ms | — | One warm diagnostic at f95fb734; inconclusive baseline spread (15.4%) |
| CSV parse and discard [G] | 16 data rows | 0.0780 ms | 0.0285 ms | — | One short governed-memory diagnostic; uncommitted source, unqualified |
| Sum generated squares [A] | 20,000 items | 3.947 ms | 0.499 ms | — | One short diagnostic at 09d81bb1 source; no qualified multiplier |
| Integer reduction loop [M] | 16,384 iterations | 1.162 ms | 0.617 ms | — | Short diagnostic at b43664e0; one run, no qualified multiplier |
| Integer reduction loop [L] | 16,384 iterations | 3.58–3.72 ms | 0.578–0.666 ms | — | Historical diagnostic; failed control and cross-session stability |
| Keyword calls [Q] | 2,048 calls | 1.378–1.404 ms | 0.137–0.145 ms | — | Historical diagnostic; failed invocation control |
| Stable sort and output [Q] | 2,048 items | 5.150–7.889 ms | 0.306–0.313 ms | — | Historical diagnostic; spread/order checks also failed |
| ASCII text pipeline [Q] | 1,024 eleven-character blocks | 0.618 ms | 0.0415 ms | — | Historical diagnostic; only one session before idle gate failed |
| JSON transform and round trip [Q] | 128 records | 0.674–0.741 ms | 0.0610–0.0618 ms | — | Historical diagnostic; control floor/spread/interval checks failed |
| CSV parse and retain [Q] | 128 data rows | 1.037–1.185 ms | 0.102–0.108 ms | — | Historical diagnostic; control/spread/order/interval checks failed |
| Other CSV summary sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |
| Other Unicode text summary sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |
| Other JSON-lines summary sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |
| Other file copy/checksum manifest sizes [S] | 0 / 16 fixture repeats | — | — | — | Output correctness verified; timing pending |


### Archived qualification notes

**Ratio convention:** Lython elapsed time divided by CPython elapsed time;
above 1 means Lython takes longer. A dash means unavailable or unqualified,
never zero. The historical generated reports use the inverse convention;
their ratios must not be copied into this column. Ranges above are the smallest
and largest retained **session medians**, rounded for readability, not confidence
intervals. The ASCII row has just one retained median.

The historical measurements show substantial performance gaps on these jobs,
including a loop whose cost grows with its iteration count. They support
investigating interpreter dispatch and application workloads. **No workload
multiplier qualified in either campaign. These timings do not describe today's
HEAD:** subsequent interpreter and containment changes have been integrated.
The newer AL, AK, AJ, AI, AH, AG, AF, AE, AD, AC, AB, AA, Z, X, W, N, B, K, R, V, D, I, E, H, P, F, T, J, C, G, A and M rows are separate short diagnostics at their named revisions; they do
not qualify a current overall or complete-script multiplier.
The F, T, J and C rows supply complete-script comparisons; their timing evidence
remains inconclusive. F is a range of medians from two identical Lython control
processes, not two revisions. No complete-script multiplier or overall speedup is qualified.


### Archived measurement identities

| Label | Date | Measured Lython revision | Evidence |
| --- | --- | --- | --- |
| AO | 2026-10-16 | 155843d0 on 722f4bab; three frozen compiler files; unchanged input comprehension, lambda sort and output | [Shared loop-target stores and inconclusive warm diagnostic](results/2026-10-16-loop-target-stores/README.md) |
| AI | 2026-10-15 | Working expanded set/dict candidate on28c41f1d; two frozen compiler sources; both roles contain entry-order fix | [Executable displays, mapping suspension and order qualification](results/2026-10-15-expanded-set-dict-executable/README.md) |
| AJ | 2026-10-15 | Working f-string candidate on 0c367762; one frozen compiler file, shared report fixture | [Executable fields, async protocols and ownership qualification](results/2026-10-15-executable-fstrings/README.md) |
| AK | 2026-10-15 | Working expanded-call candidate on 42dfd774; two frozen compiler files; both roles contain argument-order fix | [Executable expansion, argument effects and ownership qualification](results/2026-10-15-executable-expanded-calls/README.md) |
| AL | 2026-10-15 | Working lambda-body candidate on 24e6442a; eleven frozen production files; both roles share function runtime | [Executable bodies, deleted callable and scope/lifetime qualification](results/2026-10-15-executable-lambdas/README.md) |
| AH | 2026-10-15 | Working tuple-lowering candidate on5cf03da9; one frozen compiler source; both roles include empty-tuple fix | [Immutable conversion, admission and identity checks](results/2026-10-15-starred-tuple-executable/README.md) |
| AG | 2026-10-15 | Working starred-list lowering candidate on8f290b57; two frozen compiler sources; identical helper/tooling | [Executable displays, ordered effects and ownership checks](results/2026-10-15-starred-list-executable/README.md) |
| AF | 2026-10-15 | 654b4f5a; two identical Lython controls, freshly compiled configuration job | [Compile-to-result boundary, controls and independent file goldens](results/2026-10-15-config-fresh/README.md) |
| AE | 2026-10-15 | Working exact-integer dispatch candidate on 60277f65; one frozen interpreter source; unchanged case/tooling | [Async integer operators, suspension and boundary checks](results/2026-10-15-async-integer-operators/README.md) |
| AD | 2026-10-15 | Working exact-bytes sum candidate on 64fecd53; one frozen source; unchanged script/tooling | [Complete manifest job, exact sums and ownership/preflight checks](results/2026-10-15-bytes-sum/README.md) |
| AC | 2026-10-15 | Working JSON value-forwarding candidate on 50583327; one frozen production source; unchanged script/tooling | [Complete JSON file job and callback/ownership checks](results/2026-10-15-json-value-dispatch/README.md) |
| AB | 2026-10-15 | Working UTF-8 upper candidate on 29a1862e; one frozen source; identical new report script/catalog in both roles | [Complete report, noise and expansion/ownership checks](results/2026-10-15-utf8-upper/README.md) |
| AA | 2026-10-15 | Working UTF-8 lower candidate on d083c8cf; two frozen production files; unchanged script/tooling | [Complete text job, noise and allocation/containment checks](results/2026-10-15-utf8-lower/README.md) |
| Z | 2026-10-15 | Baseline 3b34fad2; ten-source compiled-layout candidate rejected and removed | [Fresh helper timing, rejection and retained cleanup](results/2026-10-15-parameter-layout-rejected/README.md) |
| X | 2026-10-15 | Working direct-strip/trim-identity candidate on 88e0992b; seven performance files frozen; parser fix shared by both roles | [Inconclusive text timing, allocation removal and Unicode diagnostics](results/2026-10-15-direct-string-strip/README.md) |
| W | 2026-10-15 | Working direct-get candidate on 7e2c1f27; seven production files frozen by hashes; identical new word-count case for both roles | [Direct lookup, callback lifetimes and short diagnostic](results/2026-10-15-direct-dict-get/README.md) |
| N | 2026-10-15 | Working direct-list-append candidate on 99e8701f; nine production files frozen by hashes; identical new distinct-list case for both roles | [Direct call transport, keyword correction and validation](results/2026-10-15-direct-append/README.md) |
| B | 2026-10-15 | Working matching-local-storage candidate on f93e9258; three production files frozen by hashes; identical new full-body case for both roles | [Local-array removal and lifetime validation](results/2026-10-15-bound-local-reuse/README.md) |
| K | 2026-10-15 | Working call-cache deletion on 6262ea94; eight production files frozen by hashes; comparison tooling unchanged | [Mechanism removal and keyword-call diagnostic](results/2026-10-15-call-cache-deletion/README.md) |
| R | 2026-10-15 | Working direct-positional candidate on 1f2ef306; eight production files frozen by hashes; comparison tooling unchanged | [Operand transport, boundary tests and short diagnostic](results/2026-10-15-positional-calls/README.md) |
| V | 2026-10-15 | Working live-defaults candidate on 96596466; thirteen production files frozen by hashes; one catalog case shared by both roles | [Live defaults, ownership and CPython lifetime edge](results/2026-10-15-function-defaults/README.md) |
| D | 2026-10-15 | Working method-exposure candidate on 0eb587a9; seven production files frozen by hashes | [Correct method identity and retained immediate-call cache](results/2026-10-15-method-exposure/README.md) |
| I | 2026-10-15 | Working async integer-addition candidate on 88f78106; two production files frozen by hashes | [Async loop diagnostic and yielding-protocol correction](results/2026-10-15-async-integer-dispatch/README.md) |
| E | 2026-10-15 | Working async-function candidate on 015184e6; six production files frozen by hashes | [Separate `Run` / `RunAsync` diagnostics and qualification](results/2026-10-15-async-compiled-functions/README.md) |
| H | 2026-10-15 | 3cad4157 existing runtime; both uncommitted executable-list candidates rejected | [Helper comparison and retained prototypes](results/2026-10-15-list-comprehensions-rejected/README.md) |
| P | 2026-10-15 | Working protected-loop candidate on b2235259; two frontend source changes frozen by hashes | [Separate warm and compile/run diagnostics](results/2026-10-15-protected-loop-jumps/README.md), [source declaration](results/2026-10-15-protected-loop-jumps/comparison-declaration.json) |
| F | 2026-10-14 | ca432c22, two identical worker/binary controls; runtime tree matches7736b6c0 | [Complete file diagnostic](results/2026-10-14-representative-file/README.md), [loaded identities](results/2026-10-14-representative-file/micro.json) |
| T | 2026-10-14 | ca432c22, governed runtime and worker ownership; ASCII candidate rejected | [Complete text diagnostic](results/2026-10-14-representative-text/README.md), [loaded identities](results/2026-10-14-representative-text/micro.json) |
| J | 2026-10-14 | Working direct-decoder candidate on 64ec1fd4, with identical f95fb734 worker | [Complete JSON diagnostic](results/2026-10-14-representative-json/README.md), [timing identities](results/2026-10-14-representative-json/micro.json) |
| C | 2026-10-14 | f95fb734, governed runtime and worker ownership; parser candidate rejected | [Complete CSV diagnostic](results/2026-10-14-representative-csv/README.md), [loaded identities](results/2026-10-14-representative-csv/micro.json) |
| G | 2026-10-14 | Working migration on `9fc7e37e`, before local-import simplification | [Governed-memory diagnostic](results/2026-10-14-governed-memory/README.md), [loaded identities](results/2026-10-14-governed-memory/micro.json) |
| A | 2026-10-14 | `09d81bb153c006f3c248d8ba0dfdfe9cdbc70d63` source, built before commit | [Native state qualification](results/2026-10-14-native-working-state/README.md), [all attempts and loaded identities](results/2026-10-14-native-working-state/micro.json) |
| M | 2026-10-13 | `b43664e0f60aa78f3cbd638578e0ced28d926194` | [Shared frames and transfer cost](results/2026-10-13-shared-async-frames/README.md), [all attempts](results/2026-10-13-shared-async-frames/micro.json) |
| Q | 2026-10-09 | `9358c4e72f1a29e55703eee02b28ea404d01bc0a` | [Four-lane run](results/2026-10-09-quick/README.md), [warm medians and exclusions](results/2026-10-09-quick/warm.md) |
| L | 2026-10-09 | `56d4346d960b58f037da7d4590eb66c87ffec29e` | [Focused loop run](results/2026-10-09-core-loop/README.md), [all loop samples](results/2026-10-09-core-loop/warm.md) |
| S | 2026-10-12 | `87c1a94c347a88bc94927ea603a138cec4376170` (correctness, no timings) | [Complete-script validation](results/2026-10-12-script-negative-cases/README.md), [fixture and host contract](results/2026-10-12-containment-scope/README.md) |

### Additional machine and control identities

A ran on the shared Windows 11 x64 development workstation: i7-14700KF,
28 logical processors, 64 GiB RAM; SDK 10.0.300-preview.0.26177.108,
.NET 10.0.13 and CPython 3.13.2, ordinary GIL/GC, launched with `-I -S`.
Its 7.893-second owned group uses the existing generated-sum workload with
shared printed output. The candidate library's informational metadata names
the preceding commit; frozen hashes identify the added sum implementation.
Compilation and startup are excluded. This diagnostic does not qualify a
CPython multiplier or an improvement over earlier Lython.
M ran on that dedicated four-vCPU Ubuntu VM under .NET 10.0.12 with the same
CPython 3.13.16 reference. Its portable Windows Release libraries were built
using SDK **10.0.300-preview.0.26177.108**; its identical benchmark observer and
DLL hashes are frozen separately. Seven rounds in one 8.24-second owned group
provide diagnostic context, with compilation/startup outside the warm timer.

S verifies exact stdout and output-file bytes through Lython's public API in
both synchronous and genuinely yielded asynchronous execution, plus independent
CPython 3.13.2 checks on Windows. Its fixture repeats expand into different record/byte counts for
each script; they are not a shared row count. Correctness tests are not timing
measurements. The [original scripts](scripts) remain ordinary Python.

G uses the same shared Windows workstation/toolchain as A and CPython 3.13.2.
Its warm CSV comparison excludes compilation/startup and is a single7.320-second
diagnostic; later local-import changes are unmeasured.


## Embedding-impact qualification before governed ownership

The following original qualification narrative predates the cooperative
ownership migration. Its open collector/reachability questions are historical,
not current runtime requirements. Links preserve the original source receipts.

The next useful comparison is the four complete CSV, text, JSON and file scripts,
with identical input/output bytes and a declared file-host boundary. Their
correctness is established. The [file-worker delivery](results/2026-10-12-file-workers/README.md)
at `ed987087` verifies all 12 cases against CPython 3.13.2 on Windows, in persistent
and fresh processes, with matched disk staging/reset and output checks. Quiet-VM
timing and qualification remain pending. The older retained-CSV
job is not evidence for bounded streaming or host latency.

Process-wide forced GC remains a containment concern. The separate
[embedding-impact experiment](results/2026-10-12-embedding-impact/README.md)
records host request tails, heartbeat stalls and collection counts; fewer full
collections alone do not establish better host latency. A separate
[Windows observer pilot](results/2026-10-12-embedding-observer-v2/README.md)
passes its fixed setup/output gates for async CSV and file jobs; shared-machine
data does not qualify host latency.
The [Linux async comparison at `4cdfd6b9`](results/2026-10-12-embedding-async/README.md)
qualifies all four complete jobs at that revision within its finite host model, while GC-independent
accounting and broader host-impact qualification remain open. The
[Linux sync/retained-pressure model at `f67419a8`](results/2026-10-13-embedding-sync-pressure/README.md)
meets the same latency targets in 30 original samples: no Gen2 in the four complete
streaming jobs, one Gen2 per fresh retained-pressure execution. It is a finite
embedding model; Windows impact and GC-independent reclamation remain open.
The [bound/exposed/retained-error model at `7f5fbf70`](results/2026-10-13-embedding-exception-pressure/README.md)
also meets those latency targets in 24 original samples, but incurs 128 Gen2
collections per completed job versus one for unbound catches. These cases
complete 12–14 jobs per nominal four-second window versus 1,250–1,267 for unbound catches;
each window finishes its last complete job.
The report also records failed memory recovery after reference withdrawal.

The [compiled operator-operand attempt](results/2026-11-03-operand-transport-withdrawn/README.md)
was withdrawn after bounded source-backed refinement. Steadier controls confirm a
roughly14–15% range-loop gain, but representative scripts remain flat/adverse and
allocation unchanged. The compiler/runtime plan layer is removed; independent
capture, suspension, cancellation and refusal regressions remain. All variants,
controls and failed initial conditions are preserved; operand work is not banned.

The [invocation setup gate](results/2026-11-03-invocation-setup/README.md) records
stable fixed allocation and noisy elapsed observations, including a larger-batch
correction. It does not establish builtin sharing as the next dominant whole-script
optimization. The live class-proxy identity correction is retained independently.
The [immutable dev.14 producer assessment](results/2026-11-03-development-producer-dev14/README.md)
records actual semantic, funding and in-flight cancellation checks separately from
unchanged public API, ordinary released pin and unqualified backend performance.

The [immutable UTF8 fragment prototypes](results/2026-11-03-utf8-fragments-withdrawn/README.md)
are withdrawn after a bounded representation refinement. Text allocation falls
about3%, but general allocation and representative elapsed costs remain adverse.
Both source variants, original CRLF-golden failure and all controls remain; independent
ownership regressions survive. Global string sharing is not permanently ruled out.

The [CSV record admission migration](results/2026-11-03-csv-record-admission/README.md)
is retained after a bounded same-source confirmation. It reduces allocation4.5–13.6%
and improves variable-width CSV elapsed/CPU about10%. Canonical timings vary; a
single wide record demonstrates additional working-memory headroom. Both tradeoffs
and all adverse data remain explicit, separately from CPython comparisons.

The [B151 bulk text source pair](results/2026-11-03-bulk-text-operations/README.md)
retains only managed bulk casing. Both whitespace scanners are removed after
short-ASCII/Unicode regressions; all three variants, controls and original failure
conditions remain. Allocation is unchanged; canonical/long elapsed gains are modest.

The [B153 owned JSON-name set](results/2026-11-03-json-owned-name-set-withdrawn/README.md)
reduces unique-name allocation/elapsed but harms repeated-schema report timing.
Both initial and native-span refinements are removed; all controls and source
variants remain. The prior unique-name debt is unresolved.

The [B154 iterator-owned JSON working storage](results/2026-11-03-json-iterator-working-storage/README.md)
removes per-chunk builder construction and fixes reentrant item loss. Allocation
confirmation supports config/copy reductions; elapsed remains unproven. Retained
largest-buffer capacity is explicit; initial drift and failed tests remain.

The [B155 file trace](results/2026-11-03-config-file-boundary/README.md) and
[B156 RAM-backed comparison](results/2026-11-03-ram-file-checkpoint/README.md)
separate observed truncate/storage sensitivity from interpreter attribution.
The original filesystem ratios remain; RAM config improves13%warm/10%fresh with
controls. Both conditions, copy's adverse fresh point and the initial diagnostic
controller failure are preserved.

The [B157 native producer/stream transport](results/2026-11-03-native-stream-transport/README.md)
shares the existing guarded write with JSON/CSV. Native packets disappear while
custom JSON lookup and captured CSV callables remain. A bounded larger-batch
confirmation resolves initial config control instability; all initial outcomes
are preserved. RAM config improves24%warm/17%fresh and allocation44%; whole CSV
rewrite improves about5%. Accounted peaks are unchanged, physical RSS unmeasured.

- [Native producer UTF-8 handoff, B158](results/2026-11-03-native-utf8-handoff/README.md):
  retained b3f1e9c3; one encoder/codec, unpublished producer strings removed.
  Config warm/fresh −19.6/−16.3%, allocation −37.8%; CSV allocation −10.2%, elapsed
  flat. Preserve control drift, small allocation uncertainty and the successful
  VM service followed by a trailing-CR controller error. Full delivery41625654805 GREEN.

- [Current call-cost source gate, B159](results/2026-11-03-current-call-costs/README.md):
  exact B158 source, two complete-script CPU captures. Binding-only storage
  removal is not shown dominant; no prototype promoted. Complete activation and
  formatting remain the design target; proposal/evidence stay available.

- [Prepared constant format layouts, B160](results/2026-11-03-prepared-format-layouts/README.md):
  retained873ae625, shared parser/formatter and compilation-owned metadata.
  Report allocation−10.47%, warm/fresh elapsed−6.4/−4.9%, compilation flat.
  Initial CSV+2.26%/control−0.85% remains unresolved; shared-hash diagnostic does
  not consistently reproduce it. All adverse conditions retained; no speculative
  helper-parameter refinement. Full delivery41632382342 GREEN.

- [Run versus RunAsync on identical runtime, B161](results/2026-11-03-async-execution-strategy/README.md):
  four complete scripts, synchronous-completion host, shared CLR hash and exact
  B160 source. Async elapsed+21–23% CSV/text/JSON and+11%report; allocation nearly
  unchanged except CSV+6.6%. No source prototype or CPython-gap attribution.
  Select shared native instruction dispatch and real-suspension continuation.

- [Shared instruction driver, B162](results/2026-11-03-shared-instruction-driver/README.md):
  b087c7b8 removes duplicate opcode loops and shares existing cache semantics.
  Completed operations remain outside the async outer loop; real suspension and
  cleanup await their users. All source variants/controls remain; allocation and
  accounted peaks unchanged. Async JSON benefits; synchronous text/JSON about2.3%
  adverse against control remains explicit pre-release debt. No general speedup claim.

- [Current b087 CPython checkpoint, B163](results/2026-11-03-shared-driver-cpython-checkpoint/README.md):
  four complete RAM file scripts, warm/fresh and two compilation-only cases.
  Fresh CSV/JSON1.61/1.37×, text/report3.02/2.39×. Identical-current control drift
  remains visible; startup excluded and no source-pair gain inferred.

- [Current complete call transport, B164](results/2026-11-03-call-transport-profile/README.md):
  four bounded CPU captures through existing workers, both APIs. Prefix14–20%leaf;
  binding-only work about1%, async value wrapper about2% is not a substantial-cost
  proof. Source identifies duplicated target reading/native selection/transfer;
  next coherent migration removes the complete duplicated route, not only a wrapper.

- [Complete ordinary-call transport, B165](results/2026-11-03-ordinary-call-transport/README.md):
  d3261a39 deletes duplicate selectors/second target read and net77 production lines.
  Four-image BOTH-API comparison closes the provisional B162 sync blocker without
  erasing adverse data; async1.7–5.7%lower versus B162. Allocation/peaks unchanged.
  Original Windows direct-Sum allocation outlier preserved; test-only76725065
  isolates/warms the control with the same8192 items/4096B bound. Full B167 delivery41663811073 GREEN; no production retiming for the fixture-only change.

- [Completed-fault driver, B167](results/2026-11-03-completed-fault-driver/README.md):
  433f7e36 consumes all completed outcomes in the native prefix; genuine pending
  operations still await. Original Ubuntu25s copyhook failure retained. Unchanged
  three-rejection fixture11.06→8.97s in one quiet-VM pair; normal control uncertainty
  and flat allocation/peaks retained. No deadline/fixture/stack-policy relaxation.

  Full41663811073 GREEN at exact433f7e36: both platforms/frameworks, installed
  package consumers and original stress scales; terminal receipt cached.

- [Immutable previous-record JSON keys, B166](results/2026-11-03-json-record-key-snapshots/README.md):
  retained 2323cceb; execution-wide name dictionary/comparer/capacity machinery
  removed. Ordered hint was withdrawn after reordered +37–40% elapsed/+46.6%
  allocation; original bits/data retained. Sorted refinement preserves reuse across
  order: unique elapsed −31.7–35.1%, allocation −14.64%, accounted peak −834,737 B;
  unknown/reordered near controls, config timing variable. Both APIs, 767 public/
  181 internal per framework PASS. Full 41672373384 GREEN at exact 2323cceb; terminal receipt cached. Changed-key snapshot
  metadata stays batch-owned; no new owner graph or interning guarantee.

- [Native JSON object staging, B169](results/2026-11-03-json-object-staging/README.md):
  WITHDRAWN. Final capacity once from member count adds a pair representation;
  report allocation +5.02%, duplicate-heavy +10–11% elapsed/+18.98% allocation,
  accounted peak +655,360 B. Unique/unknown allocation −2–3%, CPU inconclusive.
  Both APIs, controls/goldens/gates/balances/joins retained; no contract promotion,
  extra retime or full qualification. Restore B166 and assess complete activation/
  operand storage rather than another staging or name-index variation.

- [Current activation/storage allocation gate, B170](results/2026-11-03-activation-storage-profile/README.md):
  exact retained B166; two existing complete text/report scripts, 512 invocations
  each. Sampled allocation: text cursor/enumerator 12.33%; report interpreter/
  execution context 17.84%, with operand arrays additional. Not CPU shares or live
  storage; no runtime prototype/speedup claim. Select private exact-sequence loop
  transport in existing operand workspace; keep complete activation as separate
  substantial work, without reviving slower call-control designs.

- [Private native sequence iteration, B171](results/2026-11-03-native-sequence-operands/README.md):
  retained b7a0f40a. Source + admitted per-slot positions replace repeated private
  native-loop cursor/enumerator objects, no pool/owner layer. Text allocation
  −12.58%, sync CPU −4.05% /control +0.36%; other controls retain their drift.
  SOURCE-ROLE-SWAPPED report confirmation reproduces sync elapsed +1.33–1.36%,
  CPU +1.37–1.62%, allocation +0.33%. Explicit accepted tradeoff/debt for later
  complete activation work, no claim every script improves. Both-framework focused
  802 public/214 internal PASS; full 41682308042 GREEN at exact b7a0f40a; terminal receipt cached. All original observations,
  confirmation, semantics, balances, gates and joined users retained.

- [Current native-sequence CPython checkpoint, B172](results/2026-11-03-native-sequence-cpython-checkpoint/README.md):
  exact b7a0f40a; four complete RAM-file scripts warm/fresh and CSV/report compile-
  only. Fresh CSV/JSON1.57/1.39×, text/report2.71/2.31×CPython. Fresh text control
  +2.7%, report3.8%IQR remain visible. All30 workers/goldens/gates/joins pass in
  166.214s owned service. No source-pair gain or subtraction of phase medians.

- [Terminal UTF-8 ownership, B174](results/2026-11-03-terminal-utf8-storage/README.md):
  transfer partial-capacity arrays into immutable strings, retaining full capacity.
  Report allocation −5.68%, elapsed −4.1–6.6% with controls +1.2/−1.7%. Managed
  object size remains 72 B on the measured runtime. One same-bit role-swapped
  CSV/JSON confirmation establishes roughly 1–4% CSV elapsed debt; JSON uncertain.
  Retain with explicit peak/timing tradeoffs; no third unchanged retime. All adverse
  controls remain. Next gate uses current CPU evidence for coherent activation/
  transport work, without replaying slower call-control/arena designs.

- [Current execution CPU gate, B175](results/2026-11-03-current-execution-cpu/README.md):
  exact B174 text/report, two complete 2048-invocation captures, all gates/goldens/
  joins pass, owned39.288 s. Keep unknown/inlined work; entry selector ~1% does not
  prove activation dominance. JSON parsing and reservation-associated locking stay
  visible. Select one shared token-driven JSON reader between actual suspensions,
  retiring recursive async container readers; no second parser or staged member
  representation. Whole-script outcome unmeasured, no runtime prototype yet.

- [Shared JSON reader, B176](results/2026-11-03-json-reader-withdrawn/README.md):
  747 public/172 internal per actual target pass. One loop/lazy admitted parent
  array replaces recursive async containers; initial complete five-script pair
  inconclusive. One justified same-bit role-swapped target confirmation: syncJSON
  −1–2%, asyncJSON+2–3%, report inconsistent, allocation flat. WITHDRAW added state/
  parent storage without useful complete-script benefit; restore original source,
  no permanent design ban or third unchanged retime. All adverse controls retained.

- [Standalone allowance gate, B177](results/2026-11-03-allowance-gate-withdrawn/README.md):
  object→managed Lock only, both-target focused164 pass. Complete five-script pair
  mixed with identical controls+8.30%CSV/+5.19%async text. One direct resize diagnostic
  26.977 nsold/26.101 nsnew/28.303 nsoldcontrol, no timed allocation. No useful
  whole-script improvement established: withdraw runtime swap, retain independent
  concurrent growth/shrink test. Micro machine snapshots lost after RAM staging
  vanished; samples/main complete-job evidence retained, cause unproven. Persistent
  evidence storage adopted separately from RAM fixtures, no VM policy change.


- [Direct arithmetic operands, B185](results/2026-11-03-arithmetic-operands-withdrawn/README.md):
  WITHDRAWN. Adjacent pure fetches become arithmetic source metadata; existing
  operator/await/native-return semantics shared. Complete five-script pair both
  APIs plus compilation: sync text+5% versus both old roles, async report+6–9%,
  allocation flat. Other controls move5–13%; preserve every observation. Added
  source/read/proof machinery is not justified. Qualified runtime restored with
  nonincremental rebuild; retain independent native arithmetic-forwarding control.
  No full prototype delivery qualification, new CPU attribution or permanent ban.


- [Native dictionary ownership, B186](results/2026-11-03-native-dictionary-ownership/README.md):
  RETAINED978b51b5, full GREEN41730446899. PyDict owns native table/capacity, removes separate
  wrapper/static factory/unused Clone; live namespace/environment bridges remain.
  Both-target206 internal/2,021 public pass. Complete CSV/JSON/report allocation
  −1.7–2.4% /−1.1%, local constructor−24B. Initial adverse compile+14–32% retained;
  same-bit role-swapped confirmation report sync−4–5% /async−1.6–2.1%, compile
  near/slightly above controls, config async variable. No general CPU gain, fresh
  debt closure or repeated CPython checkpoint. Exact-source full delivery cached.


- [Shared analysis eligibility, B187](results/2026-11-03-analysis-eligibility-withdrawn/README.md):
  WITHDRAWN. Initial annotation-pass skip loses existing LA2010 analysis in three
  targeted cases despite main tests passing; corrected source preserves annotation
  binding work and matches eight baseline contracts. Corrected compilation allocation
  −0.2–0.6%, but fresh text+3–4.5% and report+2% versus both controls; warm controls
  straddle. Metadata/eligibility state not justified. Preserve both full variants,
  all favorable/adverse timings and new capability/diagnostic-order regressions.
  No permanent analysis-design ban, full prototype qualification or debt closure.

- [Admitted numeric UTF-8 output, B188](results/2026-11-04-admitted-numeric-formatting/README.md):
  RETAINED9ce21533, full GREEN41742152264. Delete integer CLR digit/sign/group strings and
  per-digit DivRem; share integer/base/percent and fixed/scientific span output.
  Demonstrated denied construction allocated162–170KB decimal/~17MB hex before
  admission; focused checks now preflight. Independent golden exposed old zero-fill
  grouping errors; preserve failed run, correct shared output and add parity cases.
  Both actual targets130 internal/1,031 public PASS. Complete report allocation−6.1%,
  numeric−18–87%. Initial async float+26–31% remains adverse; longer same-bit role-
  swapped confirmation gives6–8% improvement. Report sync inconclusive, async elapsed
  about−3% with near-flat CPU. No general CPU/fresh debt claim; whole objective OPEN.


- [Coupled source / CPython checkpoint, B189](results/2026-11-04-coupled-cpython-checkpoint/README.md):
  COMPLETE on9ce21533. Fresh CSV/JSON1.55/1.37×CPython3.13.16, text/report2.69/2.20×;
  compilation1.58/1.57×, warm/fresh and actual included I/O/cleanup distinct.
  Direct pair report−5.4–8.9% versus pre-loopB166; CSV near flat, text control
  spread4.6–22.3% retains uncertainty. JSON initial warm+2%/fresh flat conflicts
  with same-bit role-swapped confirmation warm flat/fresh+2.1–4.9%,2.7% new control.
  Fresh-JSON debt remains accepted/open, provisional2–5%; preserve every point.
  No source change, CPython-speedup attribution or completion of whole objective.


- [Shared numeric layout / UTF-8 complex body, B190](results/2026-11-04-shared-numeric-layout/README.md):
  RETAINED334c7cc7, full GREEN41748908560. Seven legacy grouping failures corrected; native and
  real CLR text use one final admitted window/layout. Remove grouped CLR assembly,
  orphan F/E text bridge and helpers;71 fewer production lines. Prototypes allocate
  general/Decimal−11.3%, complex−16.9%, with native/complex elapsed adverse2–5% and
  report variable/slightly adverse; all original points retained. One readonly-layout
  refinement preserves costs, no broad CPU gain. Final boundary correction fixes
  six short-width failures inherited from B188; not separately timed. Final per-target
  focused136 internal/1,051 public checks; full delivery GREEN41748908560. Whole objective OPEN.


- [Current CPU / call transport source gate, B191](results/2026-11-04-current-cpu-call-transport/README.md):
  COMPLETE diagnostic gate on334c7cc7/frozen80e9a32d. Text prefix17.5–19.2%,
  call/native/binding leaf buckets9.5–11.1%; report monitors10.4%/JSON string4.5%.
  Unknown/inlined work remains, no causal speedup. Failed CRLF/disk conditions and
  partial first text retained; final report/text captures pass full gates/normal
  close and raw-data verification. Select removal of transient bound storage/
  ownership-record transport from existing closed helpers; no new engine/arena.
  Reader views deferred by lifetime/metadata costs, not permanently rejected.

- [Borrowed native arguments, B192](results/2026-11-04-borrowed-native-arguments/README.md):
  RETAINED: existing guarded native helpers consume caller operands before binding;
  native evaluation carries a readonly span. Remove eligible rent/copy/presence/
  clear work; real suspension and dynamic binding retain owned inputs. Initial
  report+2–4% is preserved. One refinement removes a repeated failed guard; report
  then lies within control variation, helpers improve6.6–10%, async text~7%.
  Sync text remains uncertain; allocation is flat because small arrays were pooled.
  Both actual targets27 internal/148 public pass; full delivery41754575737 GREEN
  all eight gates on9a4af031, terminal receipt cached. Fresh-JSON
  debt and the whole-script goal stay OPEN. No general CPU or CPython-ratio claim.

- [Activation source gate / fixed UTF-8 construction, B193/B194](results/2026-11-04-fixed-text-construction/README.md):
  RETAINED complete constructor family. Remove fixed reservation heap carriers and
  duplicate slice-copy admission; preserve actual growing/shared/async transactions.
  Complete-script allocation CSV−3%,text−7.3%,JSON−10.7%,report−4.2%,config−12.4%.
  One same-bit long-warm confirmation gives sync text−7–8%; async text has accepted
  possible1–2% debt and asyncJSON direction stays uncertain. All adverse/partial
  source/controls preserved. Both actual targets213 internal/557 public pass; new
  cross-owner copy coverage and full delivery41763540856 GREEN, all eight gates
  on repaired-source5d8c7239 (runtime2d46 unchanged). Initial Windows fixture timeout
  retained. Activation merging remains
  precisely deferred, not permanently banned. Later B195 closes measured
  fresh-JSON debt; whole objective OPEN.

- [Immutable dev.20 adapter qualification](results/2026-11-04-adapter-dev20/README.md):
  Exact supplied0.5.0-dev.20 images, producerfe169a8f;84 internal/421 public per
  actual runtime pass, six real private-artifact in-flight checks across targets.
  Lifetime cases are INCLUDED in421, not additional. Preserve initial isolated
  fixture-layout failures; corrected staging only. Main declared PythonRe surface
  unchanged versusdev.14. Development evidence only: normal pin0.4.0, nonpackable
  adapter, no new core dependency/backend performance/safety/released-package claim.

- [Current coupled CPython checkpoint, B195](results/2026-11-04-current-cpython-checkpoint/README.md):
  COMPLETE on2d46dc8e production/frozenba48576a. Original12 blocks end with a
  failed6.47%idle-CPU gate and stay UNQUALIFIED. One longer-warm/swap-role/post-cleanup
  confirmation passes original thresholds/goldens/joins;366.565s capture/367.180s
  owned service. Fresh CSV/JSON1.50/1.30×, text/report2.54/1.92×, config1.11×CPython;
  separate compilation1.58×. Coupled freshJSON−5.6–7.3% closes earlier provisional
  debt under these conditions; all earlier points retained. Text/report paired gains
  are substantial, CSV/config magnitude still control-limited. Async debt remains
  separate; whole objective OPEN. Writer-fixture repair qualifies runtime separately.

- [Typed integer creation, B196](results/2026-11-04-integer-creation-withdrawn/README.md):
  BOTH PROTOTYPES WITHDRAWN. One fresh-value boundary and fixed immutable constants
  reduce CSV/JSON allocation 2.6–4.1%, but miss-heavy counters cost 3–6%. One managed
  index/inlining refinement retains that cost against stable controls and does not
  establish a useful general script improvement. Original and refined sources,
  four-image observations, adverse points and ownership/cancellation checks remain.
  No table, policy or truncating lookup remains in production. Independent public
  alias/value/budget regression passes both actual runtimes on restored source.
  Continue coherent frontend binding consolidation; whole-script objective OPEN.

- [Ordered lexical binding consolidation, B197](results/2026-11-04-binding-consolidation/README.md):
  RETAINED, 127 net production lines removed. One scope walk and explicit binding
  order replace duplicate collectors; module lifetimes and canonical local snapshots
  preserve semantics. Initial namespace/order failures and pre-order timing remain.
  Final 776 internal/3,871 public cases per actual target pass. Compilation allocation
  +0.6–0.9% on representative scripts is accepted; small possible compile/fresh cost
  and favorable but variable warm report remain explicit. No general CPU claim.
  Full delivery41771244039 is GREEN on ee2b4529, all eight gates; terminal receipt
  cached. Whole-script performance work remains OPEN.

- [String storage and cache accounting, B205](results/2026-11-04-string-storage-assessment/README.md):
  General shared windows withdrawn after two correct implementations measured
  adverse whole-script costs. Retain the smaller cache-counter deletion and
  allocation rollback, actual metadata72->64B, whole-job allocation−1.5–4.1%.
  Initial same-bits text drift justified one longer-warmup confirmation: sync
  text+3.5–5.8% remains explicit accepted debt, async−2.9–5.9%; no broad CPU gain.
  Preserve offset-copy failures, corrected semantics and all original measurements.
  Focused444 internal/2667 public per actual.NET11/10 pass; full eight-gate
  delivery GREEN41807227386 on exactbf0d38f3, terminal receipt cached.
  No dormant windows remain. Continue the coherent operand/call redesign objective.

- [Flat operand replacement design, B206](results/2026-11-04-flat-operand-design/README.md):
  COMPLETE bounded design gate on bf0d38f3. Managed actual11/10: current24B,
  five-int model32B, native node24B. Current report render already native; naive
  SSA frame growth3–4x is rejected. Select one flat representation/reused ordinary
  slots/contiguous call packets/native graph reuse, with explicit snapshot/span
  fences and deletion milestone. Optimistic payload bounds are not a compiler,
  semantic validation or speedup. B207 implementation and timing remain open.

- [Unified invocations, B312/B313/B314](results/2026-11-06-unified-invocations/README.md):
  remove the separate interpreter object and detach relinquished workspace from
  retained lexical contexts. Initial CSV allocation -1.77/-1.79%; final method
  return diagnostic improves versus that prototype, not a broad CSV claim.
- [Nonlocal ownership facts, B315/B316](results/2026-11-06-nonlocal-owners/README.md):
  remove per-call owner maps. Focused Release passes both actual runtimes; affected
  nested CSV elapsed -3.25/-3.94%, ordinary CSV flat/slightly adverse. Original
  invalid final quiet gate and all samples preserved; one unchanged confirmation.

- [Call/frame CPython checkpoint, B320](results/2026-11-06-call-frame-cpython/README.md):
  committed1134d6e8 runtime source, frozen520b3eb0, twelve complete compile/warm/fresh
  CSV/text/JSON/report jobs328.12s/all gates. Primary guide refreshed. Fresh text
  +1.75-2.01% against F04 is accepted regression debt, report variable; no broad
  speedup claim. Ordinary method allocation/elapsed improvements stay separate.

- [CSV header template assessment, B321](results/2026-11-06-csv-template-assessment/README.md):
  production prototype withdrawn after four complete jobs113.39s/all gates. Ordinary
  warm CSV essentially flat; variable/fresh gains modest, no allocation gain proved.
  New cache/lifetime machinery did not justify its cost. All source variants and
  observations preserved; four independent Python regressions retained. Production
  restored to qualified1134d6e8, nonincremental rebuild and44 canonical CSV cases
  pass both actual runtimes. No dormant template or alternate dictionary format.

- [Complete-text cost profile, B322/B323/B324](results/2026-11-06-text-key-cost-profile/README.md):
  current qualified1134d6e8 source,6.77guestCPU/1346samples, all251 prefix samples
  uniquely mapped. Required loop/guard/dispatch is not a removable percentage;
  dictionary query work is a material cluster. Original diagnostic guard failures
  precede sampling and remain preserved. Select one contextual key-preparation
  walk plus independently demonstrated Python hash-order corrections; no CPU gain
  predicted, no new cache or storage format. Whole-script objective remains open.

- [One contextual key preparation, B325/B326](results/2026-11-06-key-preparation/README.md):
  RETAINED: remove recursive tuple prevalidation and separate contextual classify/
  hash sweeps, correcting Python callback/error order. Complete compound-key
  aggregation −15.5%/−20.5%, CSV roughly flat. Original text +9.09%/+1.36% has
  unstable identical roles; unchanged longer-warmup confirmation does not reproduce
  that large adverse result, with modest text cost/variation still explicit.
  All original observations retained; no allocation/broad CPU claim. Full local
  Release 4,214 internal/16,101 public per actual runtime pass; final delivery
  qualification remains separate. Whole-script objective stays active.

- [Asynchronous key protocol contract, Q02](results/2026-11-06-key-suspension-contract/README.md):
  bounded public qualification on bf741ec2, both actual runtimes. Ordinary async
  host control genuinely suspends; public hash and compound dictionary get reject
  the same host callback before work starts. No abandoned users; shared funding
  returns to compilation baseline/zero. Document the required continuation/cycle/
  ownership boundary without claiming a wrapper fixes it or adding permanent
  rejection tests. No runtime change or performance claim. Independent whole-script
  work continues; lazy function namespaces/direct local transport already exist.

- [Remaining runtime cost gates, B327/B328](results/2026-11-06-runtime-cost-gates/README.md):
  COMPLETE: 9.19 guest CPU seconds total. Method-CSV does not justify new frame
  machinery; remaining compound-key work selects one active structural identity
  path. Actual unknown/inlined work stays explicit; profiling predicts no gain.
- [Active structural path, B329](results/2026-11-06-structural-active-path/README.md):
  RETAINED: remove two cycle hash tables, identity comparer/hashing, opposite-pair
  probes and duplicate scope fields. Immediate cycles, mixed kinds, managed bounds
  and explicit async unwind remain. Complete compound-key elapsed −14.8–17.1%
  versus Q01, CSV roughly flat, text control-limited. Full local Release 4,215
  internal/16,103 public per actual runtime pass; final delivery batches separately.
  O(depth) membership tradeoff is explicit; no allocation/broad CPU claim.

- [Executable tuple backing, B330](results/2026-11-06-executable-tuple-backing/README.md):
  RETAINED: reuse existing private-array ownership transfer, removing the second
  backing/admission/overlap. Complete compound-key elapsed −6.17–12.29% versus
  R02; CSV control spread2.81% leaves its result inconclusive. Full local Release
  4,215 internal/16,103 public per actual runtime pass. Final qualification batches
  with R02; no measured allocation percentage or broad CPU claim.

- [Construction CPython checkpoint, B331](results/2026-11-06-construction-cpython/README.md):
  COMPLETE on committed794e552b/frozen4011fcad, twelve compile/warm/fresh jobs
  327.85s/all gates. Primary guide refreshed; no broad primary speedup. Fresh
  CSV+2.18–4.41% and text+3.11–7.95% versus R02 remain explicit possible regression
  debt; text identical-role spread4.69%. Affected compound-key gains stay separate.
  Installed consumers pass, cached terminal CI42170841788 GREEN/all eight gates
  on794e552b. Excludes later R03; whole objective active.

- [Common UTF-8 word discovery, B332/B333](results/2026-11-06-word-discovery/README.md):
  RETAIN the refined common ordinary/cased planner and direct ASCII category runs;
  delete duplicated discovery and per-ASCII Rune decoding. Withdraw both native
  search tables after original warm text+2.69–3.69%. Refined warm roles straddle
  reference (+1.59/-0.93%); fresh-1.39/-0.85%, unchanged CSV roughly flat.
  Simplification retained without a warm/CPU/allocation claim. Both~86s collections
  pass all gates; failed initial disk checks and every adverse observation remain.
  Focused checks pass both actual runtimes; combined delivery51db4209 is cached
  terminal42174873217 GREEN/all eight. Excludes later Q02.

- [Builtin class-pattern correction, R05](results/2026-11-06-builtin-class-patterns/README.md):
  use actual evaluated types and Python whole-subject positional matching; remove
  source-name dispatch and artificial sequence/workspace preparation. Six CPython
  goldens and focused3,056 public/320 internal per actual runtime pass. Combined
  R03/R05 source51db4209 passes full local.NET11 (4,215/16,112) and both installed
  consumers. Cached terminal42174873217 GREEN/all eight; later Q02 excluded.
  Original extraction/value-protocol defects
  remain separate. No R05 speedup claim or unchanged benchmark replay.

- [Async hash/read boundary, Q02/B334/B335](results/2026-11-06-async-key-reads/README.md):
  retain genuine pending descriptors/hash bodies/dataclass field reads in the
  stated native read boundary. Delete sync query detours, lookup closures and
  four redundant completion helpers. Correct explicit tuple and empty-set hash
  order. Focused1,012 public/429 internal per actual runtime,21 CPython goldens,
  full local.NET11(4,227/16,137) and additional depth/ownership checks pass.
  Both86.01s collections pass all gates; consolidated async text+3.72/+5.13%
  remains accepted performance debt, CSV uncertain. All adverse/control/failure
  conditions preserved. No CPU/allocation gain or mutation/cache support claim.
  Q02 source2adf73f3 is fully qualified, cached terminal42180013420 GREEN/all
  eight platform/runtime/originalStress/package gates. Excludes later R07.

- [Shared native read proof, R07/B336](results/2026-11-06-native-read-prefix/README.md):
  reuse the existing subscript proof across native get/prepared lookup and consume
  completed result adaptation directly. Focused1,018 public/431 internal per
  actual runtime and three independent Python argument/receiver goldens pass.
  Complete async text−6.40–6.47% versus Q02, identical-role spread0.071%; CSV
  inconclusive and synchronous text+1.51–2.11% explicitly adverse.86.02s/all
  three jobs/all gates; no allocation/causalCPU gain or compounded percentages.
  Failed disk setup/recovery and all controls preserved. Coherent primary refresh
  and final qualification remain separate; whole-script objective stays active.

- [Final native-read CPython checkpoint, B338](results/2026-11-06-native-read-cpython/README.md):
  final4ace68ff/frozena45b5c5d, fourteen compile/warm/fresh/async jobs381.56s/all
  gates. Async text−6.91–7.14% versusQ02; synchronous warm text flat, fresh
  text+1.63–2.27% explicit debt. Report variable, CSV mostly control-limited.
  No broad CPU/allocation/RSS claim. Primary guide refreshed to this source.
  B337's loaded-core identity failure before timings, original ENOSPC and setup
  corrections preserved. Main worker location corrected and actual loaded core
  verified through Ready before sole B338 collection. R07 cached terminal
  42185646656 GREEN/all eight platform/runtime/originalStress/package gates;
  both local installed consumers pass. Later R06 excluded; objective remains open.

- [One class-pattern extraction phase, R06/B339](results/2026-11-06-class-pattern-extraction/README.md):
  delete full metadata copy/decoding, URL duplicate set and separate interleaved
  matchers; preserve descriptors, Python extraction/error order, namedtuple field
  metadata and contextual value equality.16 isolated Python goldens, focused
  3,057 public/320 internal per actual runtime and full local.NET11(4,239/16,160)
  pass, with cancellation/refusal/retained-alias/awaited-cleanup controls.86.64s
  complete comparison/all gates: class records+6.37–6.71% warm/+3.38–4.09% fresh;
  unchanged CSV+2.12–2.29% also adverse. Retain correctness and shared extraction
  with explicit debt; no allocation/CPU/speedup claim or semantic bypass to
  recover timing. Final platform/stress/package qualification pending.

- [Remaining runtime cost gate, R08/B340](results/2026-11-06-remaining-runtime-cost/README.md):
  exacta2a61290/frozenf940bd54, two existing complete text/class-record captures,
  8.51guestCPU seconds/1,690actual guest-TID samples. All236prefix samples map to
  unique exact Tier1 listings;125unknown samples/partial maps retained. Full
  goldens/quiet/funding/Closed/joins/fixture cleanup pass; no live VM work.
  Text is distributed, not proof for a universal value/frame/text redesign.
  Select the distinct constructor operand phase: general class-call array,
  class-prepend copy, bound initializer/compiled-receiver copy and duplicate
  sync/async construction. Existing ordinary function/direct-local work stays.
  Correctness/source/cost gates precede any prototype; no predicted gain.

- [Shared constructor sequence/operands, R09/B341](results/2026-11-06-constructor-operands/README.md):
  remove ordinary class-call/allocator/compiled initializer repackaging and duplicate
  construction drivers, preserving shared managed call admission. Four Python
  initializer-selection/binding gaps corrected.10isolated Python goldens, focused
  5,698public/915internal per actual runtime and final local.NET11(4,249/16,176)
  pass. Four original zero-headroom fixture failures retained; admitted argument
  copies now get headroom below one instance shell, preserving placeholder detection.
  B341113.58s/four whole-script jobs/all gates: class warm−2.35/−3.08%, fresh
  −2.04/−1.65%; unchangedCSV similarly improves, textstraddles reference. Retain
  simplification/correctness; no causalCPU/allocation/broad speedup or compounded
  debt-repayment claim. Final delivery qualification pending; objective open.

## Canonical constructor frames (2026-11-06)

[R10 evidence](results/2026-11-06-constructor-frames/README.md) retains B342 and source-justified B343 refinement, both adverse. One existing frame runner replaces R09 type operand/dual input transport; dynamic retained packets are owned. Final class warm+2.75–3.47%, fresh+1.91–3.44% versus933 are accepted milestone debt, with CSV/text variation explicit. No allocation/CPU/broad speedup claim; whole-script objective remains active.

## Private native cursor ranges (2026-11-06)

[Development adapter evidence](results/2026-11-06-development-cursor-ranges/README.md): exact immutabledev40, both-runtime84 internal/421 public per variant, including real in-flight cancellation. Direct token/range prototype allocated+2.29%; exact producer links creation/advance tokens. Selected private range-only refinement retains the existing local token bridge and reduces direct cursor allocations22.81% on the same producer. Busy Windows elapsed is diagnostic; no whole-script/backend release claim. Normal0.4/core dependency graph unchanged.

## Current allocation/lifecycle gate (2026-11-06)

[R12/B345 origins](results/2026-11-06-current-allocation/README.md): two complete maintained scripts,256 jobs each/1.886s capture, all source/quiet/golden/funding/cleanup gates. Current CSV context allocations remain visible (sampled424B objects); text is dominated by token storage rather than contexts. Approximate triggering-type attribution is not CPU/exact/live evidence. Original B344 CRLF launch failure preserved. Select shared executable work-state/context-on-demand source proof; do not replay slower caller records, arenas or shared CSV-key machinery.

## Shared executable work state (2026-11-06)

[R13 A foundation](results/2026-11-06-executable-work-state/README.md) removes the redundant frame base/pass-through constructors and groups mutable execution state without a new heap object or route. Distinct slot/lexical local reads preserve the old hidden-method semantics. Focused1,593 internal/4,200 public per actual runtime pass. No native prefix or performance claim; context-on-demand implementation/qualification remains active.

## Deferred contexts and publication transport (2026-11-06)

[R13 B](results/2026-11-06-deferred-contexts/README.md) uses one shared instruction
engine and adopts current locals/operands. B346's adverse observations and Slice
failure remain visible. Corrected B347 improves CSV elapsed about6% in both roles;
warm text remains adverse, JSON mixed and report variable. Runtime9a152ee0 is
fully qualified by terminal42228235593/all eight platform/runtime/stress/package
gates and both local installed consumers. This also qualifies the A foundation.

[R13 C](results/2026-11-06-deferred-contexts/transport/README.md) removes the
duplicate defining-scope parameter from publication, using existing owned services.
Focused526internal/1,508public per actual runtime and B348/all five gates pass.
CSV flat, text/JSON favorable, report adverse/uncertain. Retain the deletion;
batch full C delivery at the next meaningful runtime boundary. No compound debt
repayment, broad CPU or exact-allocation claim. The whole-script objective stays open.

## Owned split-result storage (2026-11-07)

[R28 owned split-storage evidence](results/2026-11-07-owned-split-storage/README.md)
preserves two withdrawn result-window implementations. Text allocation falls6.94%
without a larger string object, but controlled text timings remain inconclusive
and report rendering remains adverse. Both runtime variants are removed; independent
Unicode regressions remain. Allocation reduction is not claimed as CPU improvement.

## Canonical execution transport source gate (2026-11-07)

[R29/B377](results/2026-11-07-execution-transport/README.md) maps233 current-process
prefix samples out of1,095guest leaves, retaining all code/addresses/unknowns.
The21%prefix includes required checks and useful work; it is not a removable
dispatch fraction. Select relocation of existing completed native helper calls
before pending-call construction, deleting the displaced branch. No speedup,
new full qualification or broad representation rewrite follows from profiling.

## Completed helper calls (2026-11-07)

[R29/B378](results/2026-11-07-execution-transport/implementation.md) relocates the
existing closed native helper attempt into call dispatch and deletes its pending
borrow branch. Text warm elapsed improves4.25–7.61%sync/7.63–8.64%async against
R26 across two baseline/two current roles. Allocation and admitted peaks stay flat.
Report varies, CSV retains up to3.04%adverse versus differing baseline roles;
JSON improves. All168s/six-job gates pass. Focused439internal/1,958public per actual
target and installed consumers pass; full delivery remains pending. Preserve
R26's possible debt and all controls; no broad CPU or compounded gain claim.

## Completed-call delivery / CPython checkpoint (2026-11-07)

[B380](results/2026-11-07-completed-call-cpython/README.md) records exact81036f25/
frozen32dcf141, all15jobs/312s/all gates. Warm text1.71–1.75xCPython/fresh
1.62–1.66x remain variable; CSV warm1.22x/JSON1.05–1.06x, report1.38–1.42x
and variable. Compilation is separate; no source-pair compounding/debt-parity claim.
R29 delivery42333421800 is terminal GREEN/all eight gates, cached; excludes R30.
B379 fails before workers due stale generated roots, preserving journal/source
and unchanged original B373 archive. Its overwritten expanded lifecycle markers
are explicitly disqualified; immutable archived observations remain authoritative.

## Immutable regex development compatibility (2026-11-07)

[Dev48](results/2026-11-07-regex-dev48-compatibility/README.md) consumes exact
producerf6b2175e packages in isolation:85internal/488public checks per actual
runtime pass. Normal0.4 pin/adapter unchanged. The documented API is unchanged;
working-owner/name-enumeration/cursor-token limitations remain. Preparation and
stale-restore failures are preserved. This is compatibility, not backend CPU,
release promotion or full runtime-prototype qualification.

## Prepared class lexical facts (2026-11-07)

[R30/B382](results/2026-11-07-prepared-class-facts/README.md) moves the class-own-cell
syntax walk into existing prepared facts. Focused 382 internal/1,921 public per
actual target and independent Python probes pass. Class-factory allocation falls
7.75% warm; compile adds 1,048 B. Quiet warm elapsed improves 5.83–6.31%, fresh
3.65–5.33%, with compile +0.70–1.30%. Desktop text adverse remains unexplained but
is not reproduced in the unchanged quiet control. B381 metadata failure and all
original conditions remain archived. Retain; batch full delivery at the next
coherent boundary. No broad CPU gain or performance-objective completion claim.

## Prepared function-call layout (2026-11-07)

[R31/B384](results/2026-11-07-prepared-call-layout/README.md) removes repeated
immutable parameter-table construction from function objects. Focused1,080internal/
4,616public per actual target and independent Python probes pass. Class-factory
warm allocation−10.65%, fresh−7.35%, compilation+1,728B. Quiet class warm elapsed
−4.33–7.01%, fresh−3.52–4.38%, compile+0.26–1.40%; config flat. Text/CSV identical
current roles differ4.4–5.0%, retaining adverse/inconclusive debt. Desktop adverse
and B383 pre-worker collector-count failure are preserved. Retain the simplification;
no broad CPU/CPython claim. Batch full qualification with compiler-prefix completion.

## Canonical compiler parameter prefix (2026-11-07)

[R32](results/2026-11-07-canonical-parameter-prefix/README.md) deletes the compiler's
separate parameter-order algorithm and consumes R31 metadata directly. Focused
406internal/1,962public per actual target plus independent generator Python probes
pass. Compilation removes32B per ordinary definition; an empty generator layout
adds152B. Warm allocation flat; no quiet timing or CPU claim for this exact image.
Full platform/Stress/installed consumers batch the retained R30–R32 delivery.

## Native hash cancellation source gate (2026-11-07)

[R33 forwarding probe](results/2026-11-07-native-hash-cancellation-gate/README.md)
confirms the synchronous cold native-string hash route ignores its context token;
async entry rejects, while actual scan forwarding remains missing by source.
Explicit-token overload works and final funding is zero. This is not public
in-flight qualification or a CPU claim. A bounded native-hash correction is next,
independent of the active R30–R32 platform delivery.

## Current-token native hashing (2026-11-07)

[R33 correction](results/2026-11-07-native-hash-cancellation/README.md) removes
context-dropping native hash routing and reuses4096-byte scans for bytes. Actual
native-call interruption/cache/budget tests pass on both runtimes. Broad676internal/
2,680public precedes final shortcut refinement127internal/660public; exact sources
and images retained. One short native primitive comparison preserves allocations,
hashes and cleanup; desktop clocks establish no CPU gain. Full qualification batches
with the canonical-parameter stress-fixture correction; broader roadmap stays open.

## Shared-layout stress contract correction (2026-11-07)

[Delivery correction](results/2026-11-07-canonical-parameter-prefix/delivery-correction.md)
retains terminal failed42339537256 and its obsolete per-function layout denial
threshold. Preserve5000-definition scales:6MiB refusal/20MiB success, replacing
old20MiB refusal/64MiB success. Compile/result cleanup now share one root;8cases
pass per actual target, including real refusal/reuse. Full corrected delivery with
R33 remains pending; no partial green gates are called complete qualification.

## Current complete-report cost gate (2026-11-07)

[R34/B385](results/2026-11-07-current-report-cost/README.md) records916actual guest
samples/4.62sCPU from committed2f1f2715/frozen07a6530e. Full identity/golden/quiet/
funding/Closed/join/fixture/archive gates pass;63unknown leaves remain. Formatting
routing241inclusive/37self and JSON220inclusive/25self compete; large helper-call
totals include actual work rather than proving binding dominance. No timing,
allocation or CPU gain, new runtime prototype or unchanged qualification follows.
Select direct formatted instructions in the existing engine, deleting their
generic prepared-operation detour and obsolete wrappers; preserve actual dynamic
promotion, ordering, awaited cleanup, cancellation and admission. Initial launcher
self-match refusal and corrected same-payload transport remain archived. The
whole-script objective stays active; no automatic second profile is selected.

## R30–R33 delivery checkpoint (2026-11-07)

[Terminal GREEN42342955936](results/2026-11-07-native-hash-cancellation/delivery-ci.json)
qualifies exact2f1f2715 on Windows/Ubuntu, ordinary actual.NET11/10, original Stress
on both runtimes and installed-package consumers. Includes class facts, shared
parameter layout/canonical prefix, current-token native hashing and the corrected
5000-definition default-storage fixtures. Earlier failed42339537256/logs remain
historical; no terminal run is polled again. Later R35 is outside this receipt.
The performance objective and independent regex work remain active; publication
and tagging postponed.

## Canonical formatted-instruction prototype (2026-11-07)

[R35/B386](results/2026-11-07-canonical-format-instructions/README.md) removes
formatting's generic prepared-operation detour and four wrappers. Native dynamic
specs avoid unnecessary contexts; focused203internal/1,141public per target and
independent probes/complete goldens pass. Dynamic complete-report allocation
−15.35%warm/−15.05%fresh; static/control warm flat. Quiet141.89s/five jobs/all gates:
dynamic elapsed−12.17–15.13%, static warm mixed, fresh−1.28–4.39%, text+1.11–3.00%,
CSV mixed. Preserve all adverse/failed conditions, checked source/images and raw
controls. One source-justified refinement moves guest temporaries out of the
shared prefix, following its existing lifetime fences. The cause of text's
adverse timing remains a hypothesis. Final retention/delivery pending; no broad
CPU/embedding claim or completion of the whole-script objective.

## Retained formatted-instruction lifetime refinement (2026-11-07)

[R35/B387](results/2026-11-07-canonical-format-instructions/README.md#retained-lifetime-helper-refinement--b387)
keeps formatting temporaries in an ordinary helper entered only by formatted
opcodes. Generic classification/wrappers stay deleted; focused203internal/
1,141public per actual11/10 and independent probes pass. Warm dynamic allocation
remains about−15.35% on both APIs. The one changed-source comparison completes
142.36s/all five jobs/gates: dynamic elapsed−10.09–13.19%, static warm−1.57–4.68%,
fresh/text variable. Retain deletion/context savings/lifetime fence and explicitly
accept CSV+0.13–2.36% possible debt. Prior text's cause is not established or
algebraically repaid; all original adverse results/source/images remain. No third
unchanged retiming or broad CPU claim; full delivery and the wider objective remain
active. Frozen raw archives are unchanged; sidecar hashes are corrected for Git
newline conversion, with producer hashes preserved and future byte policy explicit.


## R35 delivery and representative CPython refresh — B388

Exact42101cd5 is terminal GREEN42347124351: Windows/Ubuntu ordinary actual11/10,
original Stress and installed-package consumers. No later source is qualified.
The [B388 checkpoint](results/2026-11-07-formatted-cpython/README.md) completes
all15jobs in311.96s/all gates. Warm text1.67–1.75x/report1.40–1.42x CPython;
most cases remain variable, startup/RSS/embedding tails unmeasured. This refresh
replaces the primary guide snapshot, not prior source-pair evidence or debt.
No repeated terminal queries, publication, compounded CPU claim or goal completion.


## R36 setup source gate and root-identity correction

The [R36 gate](results/2026-11-07-builtin-root-identity/README.md) finds fixed
functions/signatures already shared; no current evidence selects global type-graph
sharing. It corrects a demonstrated display-name identity bug without that redesign.
Focused986internal/2,765public per actual target and eight independent Python
goldens pass. Original masked probe/unsupported fixture failures are preserved.
No timing or performance claim; R35's full receipt excludes this later source.


## R37 call-operand gate / R38 annotation demand

[R38](results/2026-11-07-annotation-demand/README.md) removes irrelevant annotation
binding suffix work while preserving full regex conservative analysis and control
exit facts.919internal/2,015public per actual target and524exact differential cases
pass; compile allocation−4.38–8.94%, regex+304B/+0.117%, warm essentially flat.
Initial3semantic failures, source/images and diagnostic desktop adverse conditions
remain explicit. Quiet elapsed and full delivery are separate/pending. R37 static
census finds native call slices already borrowed; no further transport selected.


R38/B389 completes140.29s/all5gates: report compile−6.51–8.02%, text compile
−8.25–9.13%, config fresh−0.54–2.42%. Warm text−5.46 to+1.74% with4.94%
baseline spread remains inconclusive; regex compile+0.014–1.66% is accepted possible
overhead. Retain compilation simplification/allocation gain, no warm CPU/parity/
compound claim or unchanged retiming. Terminal capture archived/verified, all users
joined. Full R36/R38 delivery42351006316 onb0889a46 remains independent/pending.


## R39 ordinary-helper CSV cost gate

[B391](results/2026-11-07-ordinary-csv-cost/README.md) records1,127guest samples/
5.68CPU seconds on b0889a46 production;58unknown leaves retained. BeginPendingCall
550inclusive/32self includes useful helper work; work-state26/18, binding22/18,
resume11/11 do not establish a dominant removable frame phase. CSV parsing/record
construction also competes. No general frame/call-arena prototype or repeat profile
selected. B390 failed before worker creation on a tuple environment key; original
condition archived/verified before one correction. B391 all gates pass, terminal/
users joined/RAM evidence archived/removed. No source-pair performance claim.


R36/R38 full delivery42351006316 on exactb0889a46 is terminal GREEN: Windows/
Ubuntu ordinary actual.NET11/10, original Stress both targets and installed-package
consumers. Receipt committed beside annotation-demand evidence, observed07:48:45UTC;
never query the run again. This qualifies the root-identity and annotation-demand
changes, excluding the later R40 cursor prototype. No repeated qualification,
publication or performance-goal completion follows.


## R40 Unicode capture scalar cursor — B392

[R40](results/2026-11-07-regex-scalar-cursor/README.md) replaces unconditional
origin restarts for nearby lookbehind with the existing bidirectional scalar
cursor, preserving origin when nearer and all leases/owners/validation. B392
completes143.75s/all5gates: Unicode1,024−31.28–34.50%,4,096−65.54–68.53%,
8,192−78.99–79.47% elapsed versusb0889a46 with normal0.4 unchanged. These complete
growing-input scripts include backend/guest work, not isolated projection CPU or
the primary CPython table. ASCII−0.16 to+6.31%/4.78%current spread and forward
−3.38 to+2.96% remain variable; possible ASCII debt accepted, no parity/cause claim.
Allocation essentially flat/peaks identical; desktop non-monotonic clocks retained.
853internal/1,224public per actual target plus one active cancellation/owner test
each and six supervised .NET/CPython cases pass. Full R40 delivery is batched later;
the green R36/R38 receipt excludes it. No retiming, publication or goal completion.

## R41 shared receiver-method implementations — B393/B394

[R41](results/2026-11-07-receiver-methods/README.md) removes ten opaque method
markers, their invocation chain, migrated receiver-capturing factories and ten
orphaned guard helpers. Regex and existing ordinary native method families share
immutable implementations; immediate calls borrow operands, exposed methods keep
the binder/metadata. Native no-argument/helper paths and real dynamic wrappers stay.
Complete regex allocation falls about29.47% on both APIs; controls within0.04% and
admitted peaks identical. Desktop timings conflict, including sync regex+31–81%.
B393 quiet regex−7.56–9.19% accompanies adverse CSV+2.05–3.41% and variable JSON.
Source inspection justifies removing the added one-caller resolver/no-argument
name checks. B394 changes that work:regex−8.64–10.80%, JSON−4.10 to+2.65%, text
−1.34 to+1.05%, CSV−1.84 to+2.35%, with differing identical-role controls. Retain
the simplification/allocation and measured regex gain; control parity, isolated CPU
and repayment of original CSV debt remain unproved. No unchanged retiming selected.
Both four-job collections finish114s/all gates, are terminal/archived/verified with
no workers/fixtures. Original setups, source pairs and all adverse data survive.
Refined121internal/506public per actual11/10 pass; full platform/Stress/package
delivery is pending. This does not complete the whole-script performance objective.

R40/R41 full delivery is now terminal GREEN42357660230 on exactfba865ef:
Windows/Ubuntu ordinary actual.NET11/10, original Stress both targets and installed
core-package consumers. [Receipt](results/2026-11-07-receiver-methods/delivery-ci.json)
is cached; no repeated queries/qualification. Publication remains postponed.

## Representative CPython refresh — B395

[B395](results/2026-11-07-receiver-cpython/README.md) qualifies the current primary
five-script comparison on fba865ef/frozen5d2ca8e1 with matching production source
and precommit metadata recorded separately.15jobs311.66s/all gates. Warm CSV
1.19–1.23×,text1.75–1.79×,JSON1.04×,report1.36–1.38×,config1.25× CPython3.13.16;
compile42–68µs and fresh boundaries separate. Preserve report warm IQR8.69%, text
warm control2.22%, CSV warm3.22%, fresh JSON6.07% and all other variable results.
RAM I/O/full validation/prior-result cleanup included; startup/reset/final teardown
excluded. No per-change causal CPU attribution or repayment of earlier adverse debt.
docs/BENCHMARKS.md now leads with this qualified source; B388 stays historical.

## R42 complete text cost gate — B396

[R42](results/2026-11-07-text-cost/README.md) records1089guest samples/5.49CPU seconds,
44unknown retained. ExecuteInstructionPrefix229self/1056inclusive supports a coherent
operation-input/retirement boundary; self includes inlined checks/slot work, not all
removable routing. ScalarAdd41self alone does not justify universal tags. Source
census shows85module instructions,22local/constant loads,9operand capacity; the
helper is already native. Preserve R37 snapshot/span/capacity lessons. No second
capture, new arena, stack-policy change, prototype or speedup claim follows yet.
All raw profiles/maps/JIT/code/gates are archived under lease before RAM removal.
Local Windows extraction skips only zero-byte Linux FIFO metadata; original archive
and failed condition stay intact, without a service query or collection restart.

## R43 ordinary-operation operand transport — B397/B398

[R43](results/2026-11-07-operand-transport/README.md) extends the existing indexed
inputs and folds before one physical-layout pass. Initialized-local tracking,
invalidation inventories and duplicate value/store selectors disappear. Checked
reads retain original spans; native helpers and real snapshots remain. An initial
40-byte instruction is refined back to32 by reusing redundant store metadata.
The prototype frame snapshot cache/cleanup hooks/64-byte charge are removed;
raw type getters reuse existing funded scratch. Text85→77instructions, CSV helper
24→17, report helper19→13; immutable payload smaller, call-dominated workspace flat.
Initial compile allocation+2.8–3.5% becomes+0.20–0.87%. Original conflicting desktop
timings, including async text+20.88%, stay separate from controlled VM results.

B397 completes warm text then rejects my invalid `fresh` protocol lane. Original
stderr/partial observations and failure-archive helper syntax error survive, with
no workers/fixtures. B398 corrects the lane to `compile-run` with identical frozen
images;141.38s/all five gates, terminal/archived/hash-verified. Text warm−1.37to+1.11%,
fresh−2.23to−0.15%, CSV−1.57to+0.76%, JSON−2.37to+6.81% with6.66%identical-current
spread, report−2.19to−0.73%/IQRup3.92%. Retain consolidation with small compilation
cost and elapsed/JSON uncertainty; no substantial CPU gain, parity, debt repayment
or primary CPython refresh. No unchanged retiming selected. Initial/refined1,167
internal/2,665public per target, cache deletion128/676, final checked-arithmetic
cleanup/nonincremental delivery70/47 per target pass. Full delivery pending.
Continue the whole-script objective; next assess duplicate completed-instruction
checks without replacement counters, fuel or weaker containment.

## R44 checked call-entry consolidation

[R44](results/2026-11-07-call-entry-checks/README.md) deletes twelve immediate
CheckExecution duplicates after already-checked managed frame entry. No guest,
admission or await intervenes. Frame/stack bounds, exception translation/finally,
per-instruction/long-operation/resume checks and the constructor's post-allocation
check remain. No new policy/counter/abstraction or exact-checkpoint tests.
103 internal/862 public Release checks pass on actual11; full10/platform/Stress/
package checks are batched at the next runtime boundary. One short existing
text/CSV/JSON/report diagnostic passes goldens/balances/isolation/fixture cleanup;
allocations essentially flat/all admitted peaks identical, desktop clocks variable
including adverse report. This is source simplification, not a CPU gain or full
performance-objective completion. No quiet-VM campaign or unchanged retiming.

R43 full delivery is terminal GREEN42365045704 on exact5fb25354: Windows/Ubuntu
ordinary actual11/10, original Stress both and installed core-package consumers.
[Cached receipt](results/2026-11-07-operand-transport/delivery-ci.json) excludes
later R44; never query the terminal run again. This qualification does not turn
the variable B398 elapsed observations into a substantial CPU improvement.

## R45 native general integer parsing — B399

[R45](results/2026-11-07-integer-parsing/README.md) deletes general conversion's
decoded CLR copy/estimate and old UTF-16 per-character BigInteger loop. Native
unsigned accumulation and exact chunked promotion use existing ownership and
three-magnitude preflight; small results need no major reservation. Unicode
decimal strings now work, bytes retain ASCII syntax, and long conversion polls
the active token. Two baseline cancellation regressions and five CPython Unicode
mismatches are corrected.189independent grammar/boundary cases supplement existing
coverage. No new representation/framework/policy/ledger/unsafe path.

Initial160internal/776public per actual target pass after replacing one stale
decoded-copy refusal fixture with actual-magnitude denial (input50000→4096,
origin/alias/cleanup assertions preserved). Final entry-check deletion41/280 per
target and in-flight Stress both pass. Padded CSV allocation−14.70/−13.35%,
numeric-string JSON−62.99/−62.98%sync/async; ordinary controls/compile flat.
Desktop favorable/adverse/moving controls stay separate. B399196.49s/all seven
gates: padded CSV warm−12.49–13.41%, fresh−13.14–20.72% with6.75%baseline spread;
numeric-string JSON warm−41.43–41.98%, fresh−40.99–42.47%. Ordinary CSV−0.46to+0.89%,
JSON−0.31to+1.29%, text−5.65to+1.61%/4.83%identical-current spread remain uncertain.
Retain coherent redesign/correctness/affected gains, without general CPU/parity,
retained-memory or embedding-tail claims. Terminal/archive3f988c52 verified;
no workers/fixtures or repeated service queries. Full R44/R45 delivery and primary
CPython refresh pending; publication/producer qualification stay independent.

## Representative CPython checkpoint after R45 (2026-11-07)

B400 records21 compile/warm/compile-run jobs on8c9fd0d7, five ordinary scripts and
two explicit affected integer inputs. All gates/archive verification pass; variable
controls remain visible. Full combined delivery is independent and pending.
[Evidence and boundaries](results/2026-11-07-integer-cpython/README.md).

## JSON integer construction source gate (2026-11-07)

R46 is withdrawn: shared radix chunks raise whole-job allocation10.5% for40-digit
JSON values and about28.5times the total allocation for4096-digit values. Focused
semantics/ownership pass, clocks remain diagnostic/adverse; CLR decimal parsing
keeps its current path. No speculative runtime code survives.
[Evidence, failed fixture and source restoration](results/2026-11-07-json-integer-source-gate/README.md).

Combined retained R44/R45 delivery42371564043 is terminal GREEN on8c9fd0d7:
both platforms, actual11/10, original Stress and installed consumers. No qualification
is claimed for the withdrawn R46 code. [Receipt](results/2026-11-07-integer-parsing/delivery-ci.json).

## Maintained file-workflow comparison (2026-11-07)

R47/B401 adds six compile/warm/compile-run jobs for protected search and the small
two-file copy/checksum/manifest on qualified8c9fd0d7. No runtime change, new
framework or unchanged seven-case retiming. Full gates/archive verification pass.
[Complete evidence and limits](results/2026-11-07-file-workflows/README.md).

## Protected-search helper-entry cost gate (2026-11-07)

R48/B403 observes645guest samples/3.28CPU seconds; entry-method leaves do not
establish dominant removable admission work. B402 failed warmup and exact
batch-size/caption correction are preserved. No runtime change; source/census
instead identifies missing executable module declaration slots for a bounded
compiler-layout investigation. [Evidence and limits](results/2026-11-07-helper-entry-cost/README.md).

## Actual executable declaration bindings (2026-11-07)

R49 reuses module slots and shared import facts, retaining namespace publication
after frame cleanup. Initial29cross-frame failures and their correction remain;
1008internal/2819public per actual11/10 pass. Allocation effectively flat, root
capacity+48–96B; B404 observes search−4.28–5.10%, text−2.10–5.66% with3.26%
current spread, JSON−5.49–7.67%. Desktop30–50%text gains are diagnostic and not
transferred. [Evidence and qualification status](results/2026-11-07-declaration-bindings/README.md).

## Managed native integer width (2026-11-07)

R50 uses one UInt128 accumulator/DivRem guard, retains arbitrary chunks and funds
native>64bit output before conversion.102internal/420public per actual11/10 plus
real cancellation pass. Allocation numeric strings−12.492/12.487%; B405warm
−10.68–12.50%, padded CSV+0.99–1.16% accepted explicitly. Initial/refined adverse
desktop/control data remain. No universal CPU/async/scaling claim or JSON-parser
change. [Evidence and qualification status](results/2026-11-07-native-integer-width/README.md).

## Native UTF-8 wildcard translation (2026-11-07)

R51 removes the shared UTF-16 translation pipeline, glob segment copies and
per-class builders. Two complete report scripts allocate 25.0%/31.8% less; controls
are essentially flat. Initial async glob+8% and refined unstable clocks remain
inconclusive. B407 supports fnmatch−12.33–13.34% and glob−15.55–16.92%
warm elapsed, while ordinary JSON+4.16–7.43% remains accepted unresolved debt.
B406's23completed CPython jobs lack a final gate after fixture loss; failure and
partial evidence remain, with no primary-table promotion. Both targets pass semantics, refusal, retained
funding and real in-flight cancellation. One initial-capacity refinement and the
original Windows newline fixture failure are preserved. Full delivery42383291690
passes both platforms/actual11/10/Stress/installed consumers. B408 ENOSPC leaves
truncated raw JSON; no primary-table promotion. Verified offload of old test build
outputs restores headroom; corrected collection uses atomic snapshots and owned
tmpfs fixtures. [Evidence and measurement status](results/2026-11-07-wildcard-translation/README.md).

## Qualified retained-source comparison and report phase (2026-11-07)

B409 closes the coherent55e510ff CPython refresh after preserved B406/B408
failures and explicit storage/fixture corrections.23jobs/477.00s/all gates,
separate compile/warm/compile-to-result; text/report/file gaps and variable cases
remain. [Current comparison](results/2026-11-07-retained-checkpoint/README.md).

R52/B410 attributes complete report execution:927guest samples/4.67CPU seconds,
59unknown leaves. JSON/formatting and admitted storage are material; binding13self,
workspace rent3self and frame advance3self do not establish dominant repackaging.
Inclusive stacks overlap. No runtime prototype or saved-CPU forecast. Same-governor
admission reentry is a source hypothesis requiring a regression, not a measured
finding. [Full profile and limits](results/2026-11-07-report-execution-cost/README.md).

## Pending admission publication (2026-11-07)

R53 confirms two baseline funding regressions under native admission reentry:
retained allocation or scratch refund can erase the outer pending charge and let
a shared root overcommit. Pending publication, scoped resize guarding and own-charge
rollback correct it; unchanged refunds no longer invoke another host callback.
Final1401internal/1652public per actual11/10 pass, including10small ownership cases.
Normal five-script allocation is flat; desktop elapsed includes adverse CSV/search
and remains diagnostic. No performance gain claim or new memory framework.
[Source, failures, contract tradeoff and qualified delivery](results/2026-11-07-admission-publication/README.md).
Full CI42393422539 on d04024ee is terminal GREEN: Windows/Ubuntu actual11/10,
original Stress and installed core consumers. Cached receipt; no repeat polling.

## Immutable JSON input windows withdrawn (2026-11-07)

[R54](results/2026-11-07-json-input-windows/README.md) removes per-token array copies
inside the same execution owner, with complete backing/header funding preserved.
Prototype979internal/4778public per actual target pass. Report allocation about5%
lower, JSON under1%, but ordinary controls+3–4.5% from eight extra wrapper bytes.
B41286.75s/all gates: report−5.00to+2.14% with5.05%old control spread, text−0.49to
+7.80%/6.23%current spread. No substantial CPU/elapsed improvement established.
Withdraw representation/ownership state; exact source/tests restored, forced
rebuild and975internal/4778public per actual11/10 pass. Original8test failures,
desktop72%drift and B411 pre-sampling symlink/core mismatch remain archived.
B412 fixes staging only; no permanent rejection of sharing or fixed percent veto.
No prototype, view-specific tests or dormant alternative remains. Objective open.

## One lexical nesting pass (2026-11-07)

[R55](results/2026-11-07-lexical-nesting/README.md) removes96lines: whole-source
nesting scanner and diagnostic factory. Existing tokenizer depth enforces512;
parser64/unary256/f-string/managed-stack protection remains. Both quote-reusing
f-string literal cases fail baseline, then agree with isolatedCPython3.13.2 through
compile/Run/RunAsync.802internal/1555public plus14parser-hardening checks with fresh
matching probes per actual11/10 pass. Existing15job compile/fresh/warm diagnostic gates pass,
allocation flat, elapsed mixed/adverse/unstable; warm text+8.27to+20.07% with9.82%
baseline drift and fresh report+3.43to+14.86% remain. Retain coherent deletion and
correctness, no CPU claim or broad-objective completion.
Full CI42397553014 on7df9b465 is terminal GREEN: Windows/Ubuntu Release actual11/10,
original Stress and installed core consumers. Receipt cached; no repeated polling.

## Complete configuration phase attribution (2026-11-07)

[R56/B414](results/2026-11-07-invocation-setup/README.md): source7df9b465,
551 guest samples/2.91 CPU seconds, 33 unknown leaves. Builtin creation17 and
context29 inclusive do not justify lazy namespace machinery; host open269 and
resolution152 overlap. Current shared functions/workers remain. No prototype,
speedup or repeated delivery. Preserve the timed host-validation asymmetry and
CPU-versus-wall/inlining limits. B413's root-closure setup failure and corrected
B414 identities, full goldens, joined users, removed fixtures and verified archives
remain; both are terminal. Whole-script performance objective remains open.

## Product working-admission lifetime (2026-11-07)

[R57](results/2026-11-07-product-admission/README.md) confirms4native baseline
failures and2public CPython-golden failures. One held computation/result grant
replaces a prematurely disposed guard and second admission; empty integer/bool
starts keep identity. Opaque CLR cancellation limitation remains.91internal/
638public per actual11/10 pass; full delivery batches at the next boundary.
One existing10job desktop diagnostic passes goldens/funding/cleanup; affected
matrix-product allocation−7.808/−7.216%, controls within0.045%. Elapsed is unstable,
including+102.49%async and99.52%old drift; no CPU claim. Original wrong float-
identity fixture, corrected independent Python result and failures remain.

## Dense executable graph normalization (2026-11-07)

[R58/B415](results/2026-11-07-executable-graph/README.md) reassesses the original
inconclusive graph replacement. One target table/dense blocks replace repeated
walks/HashSets/sort/map; cycle and protected targets stay exact.429internal/
5143public per actual11/10 pass, with explicit non-Stress filters after preserving
an accidentally broad stopped selection. B415fivejobs124.997s/allgates/archive
907f8751 verified: compile CSV/text/report−1.66to−2.97%, fresh config−0.62to−1.61%.
Warm text−2.84to+1.49%/4.26%old spread remains uncertain/adverse; externalCPU mixed.
Separate compilation allocation−3.89to−5.89%; desktop clocks/control drift retained.
Retain coherent phase replacement and modest compile benefit; no CPU/warm gain,
whole-objective completion or immediate unchanged retiming. Combined delivery
with R57 is pending; no producer or publication dependency.

## Immutable development regex snapshot (2026-11-07)

[R59](results/2026-11-07-regex-dev54/README.md): exact0.5.0-dev.54/553acbdf,
unchanged existing development cursor bridge, isolated outputs/feed/locks;
86internal/514public per actual11/10 pass. Native working-owner cursor remains
usable; DetailedData owner, native name enumeration and creation/current-token
separation still need bridges. Normal0.4pin/core unchanged; no final released
qualification, backend performance or safety certification inferred. Producer
publication does not block core work. No sibling source edits/builds.

Combined R57/R58 delivery **CI42404834827 on e66b8f72 GREEN**: Windows/Ubuntu
Release actual11/10, original Stress and installed core consumers.
[Cached receipt](results/2026-11-07-executable-graph/delivery-ci.json); no further query
or unchanged qualification. R59 development compatibility is independently scoped.

## Array-backed JSON windows withdrawn (2026-11-07)

[R60](results/2026-11-07-array-json-windows-withdrawn/README.md) avoids R54's8B
wrapper tax; first JSON/report allocation−5%/−7%, controls flat. Both variants
pass460internal/2496public per actual11/10, full backing/shell funding and tokens.
B416114.231s/allgates shows stable CSV+2.65to+3.07%; explicit owned zero-offset
refinement B417114.322s/allgates removes most CSV debt. Report−3.40to−0.87%,
JSON+1.56to+7.31%/5.18%current spread and text+0.57to+1.98% remain, workerCPU
mixed/adverse. Withdraw whole representation/architecture-only tests; retain
independent decoded-text interoperability. Exact qualified Git production restored,
forced core builds and456internal/2496public per actual11/10 pass. No prototype,
hash flag change, owner state or view helper remains. Preserve initial8failures,
all first/refined raw/source/cores and adverse points; no permanent ban/CPU claim
or objective completion. Terminal archivesa5b030a1/f47c89fa locally verified.

## Native JSON construction source gate (2026-11-07)

[R61](results/2026-11-07-native-json-construction-gate/README.md) finds zero
escaped tokens in current JSON-lines/report/config fixtures. Escaped-buffer
copy elimination is not a candidate for those measured gaps. Completed native
loops/shared record keys/direct ordered-slot publication already exist. No new
parser/funding machinery, timing or qualification; gate records no justified
complete removable stage and continues the broader roadmap.

## Demand-driven syntax validation (2026-11-07)

[R62/B418](results/2026-11-07-syntax-validation/README.md) uses the existing
validity walk to select generator/type/future validators, and removes empty scope
sets and unchanged type-name clones. Distinct scope rules and diagnostic ordering
remain; no combined visitor/cache.559internal/1492public per actual11/10 and716
exact diagnostic differentials pass. Five-script compile allocation−3.15–4.07%;
controlled compile elapsed−2.94–5.88%, fresh config−1.32–1.45%. Warm text control
−0.51/−0.42% does not establish a runtime gain. Quantized external CPU and adverse,
unstable desktop observations remain separate. Initial staging headroom failure
was corrected with identical owned hard links before any measurement; original
conditions/logs retained.125.261s, all archive/ownership/quiet/cleanup gates pass.
Full delivery CI 42412531882 on 70267889 is GREEN (Windows/Ubuntu, actual11/10,
original Stress and installed consumers); cached receipt, no further query.
This does not complete the whole-script objective.

## Current text execution attribution (2026-11-07)

[R63/B419](results/2026-11-07-text-cpu-current/README.md) refreshes text CPU
attribution after operand/call changes: 1,086 guest samples / 5.47 CPU seconds,
65 unknown leaves, all identity/golden/quiet/cleanup/archive gates. Diagnostic
PerfMap/JIT overrides are not normal-settings timing. Dispatch/input/value
publication remain material; inclusive helper/split shares overlap. Binding/frame
construction is not established as dominant. Native lower/split, cheap counter
admission and deferred contexts already exist. Assess the complete value/store/
cleanup-continuation boundary next, preserving observable assignment and funded
async users; no tiny carrier rewrite, new arena, prototype or speedup claim.

R64 [source follow-up](results/2026-11-07-text-cpu-current/publication-source-gate.md)
finds common local-result publication already skips adjacent-store dispatch and
the temporary write. Relocating its remaining target/span/async-continuation state
does not establish a useful deletion. No prototype or timing selected; proceed
with prepared binding/class-cell consolidation assessment and the broader roadmap.

## Unused module class-cell analysis removed (2026-11-07)

[R65](results/2026-11-07-module-class-facts/README.md) deletes the trailing module
class-cell demand walk and its standalone fallback calculation. Module execution
never consumes this demand; real capture queries and scope semantics remain.
591 internal / 1,793 public checks per actual11/10, 716 exact diagnostic comparisons
and 32 public CPython probes pass. Compile allocation −3.53–4.64%, fresh roughly
−0.13–1.46%; warm essentially unchanged. Adverse report elapsed and drifting desktop
controls preserved; no CPU/warm gain claim or new VM campaign. Full delivery batches
at the next coherent checkpoint; R62's complete green delivery excludes this change.

## Buffered-line execution consolidated (2026-11-07)

[R66/B420](results/2026-11-07-buffered-lines/README.md) replaces duplicated line
algorithms with one scanner and buffered synchronous prefixes; a sole iterative
continuation retains private storage only for pending refills. Native destinations
remain caller-owned; forwarding async state machines disappear (14 net production
lines deleted). 637 internal / 3,584 public per actual11/10 pass, including twelve
two-byte-window pending-read/close cases also green on baseline. Controlled six-job,
four-process pair completes in 197.560s with every semantic/ownership/archive gate.
Async text elapsed −2.32–8.26%, JSON −1.46–3.96%; synchronous and worker CPU results
remain mixed with between-process spreads. Preserve adverse report CPU +6.94%,
sync CSV elapsed +0.99% and original noisy desktop CSV +7.45/+13.94%. Allocation
unchanged; no general CPU/sync gain or objective completion claimed. Retain coherent
deletion and bounded async elapsed evidence; combined delivery qualification pending.

## R67 retained checkpoint / R68 immutable development adapter

[R67/B421](results/2026-11-07-buffered-checkpoint/README.md): source824fc6bb,
core15c04e2a,23CPython jobs/478.32s, all goldens/identity/quiet/Closed/join/fixture
gates and local/remote archive verification pass. Current guide separates
compilation/warm/fresh and preserves source55e510ff tables historically. Variable
roles and CPython IQR stay explicit; no source-pair CPU or debt repayment claim.
Full combined source CI42418518578 remains pending at measurement close.

[R68](results/2026-11-07-regex-dev55/README.md): exact immutable unpublished
0.5.0-dev.55/d6067925, existing bridge reused byte-for-byte in isolated outputs,
assets/feed/locks;86internal/514public per actual11/10 pass. Documented names
unchanged from dev54; DetailedData owner, native name enumeration and independent
creation/current tokens still require bridges. Normal0.4pin/core unchanged;
development semantic compatibility is not final released/safety/backend-performance
qualification. No sibling source edit/build or producer CI replay.

Combined R65/R66 source824fc6bb full delivery CI42418518578 is GREEN:
Windows/Ubuntu Release actual11/10, original Stress both targets and installed
core consumers. [Terminal receipt](results/2026-11-07-buffered-lines/delivery-ci.json)
is cached; no further service query or unchanged qualification.

## Complete returned-record workload / unpack transport attribution

[R69/B422](results/2026-11-07-returned-record-calls/README.md): new maintained CSV
validation/aggregation/report with ordinary keyword calls, two early exits and
tuple results.75public file-worker checks per actual11/10 and3independent CPython
cases pass. Current qualified production unchanged;1,121samples/5.64CPU seconds,
108unknown, all source/golden/quiet/cleanup/archive gates pass. Inclusive calls
include useful guest work, not removable CPU.21Monitor leaves identify actual
unpack materialization; exact tuples currently copy into leased lists/iterators.
Select complete immutable tuple unpack transport removal first; larger activation
consolidation stays separate. No speedup, primary CPython refresh or qualification
replay. Initial smoke path error retained; no measured collection restart.

## Immutable tuple unpack transport

[R70/B423](results/2026-11-07-tuple-unpack/README.md) removes exact tuple
unpacking's iterator/list/backing/reservation phase, retaining mutable/protocol
snapshots and funded starred storage.824internal/1,106public union per actual11/10,
matching probes and24public baseline regressions pass. Existing five-script
diagnostic: returned-record allocation−13.57%sync/−14.40%async, controls flat;
desktop text+9–10%sync/+5–8%async retained. Four-process VM pair114.59s/allgates:
affected elapsed−6.45–7.45%sync/−7.01–9.34%async; ordinary CSV+0.06–0.28%,
text−0.15–0.96%, coarseCPU mixed/controlCSV+4.07%. Retain complete-phase removal,
allocation and controlled affected elapsed; no universalCPU/CPython/debt claim.
New source full delivery pending; publication postponed. B423 terminal archived/
verified; no restart or additional collection. Larger activation roadmap stays open.

## Linear keyword/variadic executable activation

R70 full delivery CI42422556519 is GREEN: Windows/Ubuntu Release actual .NET 11/10,
original Stress both and installed core consumers. Its cached receipt is final.

[R71/B424–B425](results/2026-11-08-linear-keyword-calls/README.md) removes the
generic recursive driver route for ordinary nonmirrored keyword/variadic helpers,
sharing final argument storage, binder and linear frame runner. Generic mirrored/
lowered callbacks retain real callers. Final measured source: 1,366 internal /
239 targeted public per actual 11/10; earlier initial/context-only source has
1,366/3,118 per target, not claimed for the final binding cleanup. New actual
default suspension/terminal refusal/retained input/reuse coverage and existing
cancellation/deep keyword recursion pass. All evidence gates pass.

Initial B424 adverse sync/desktop/control results remain. Refined B425 takes
114.478s: allocation −13.5% sync/−12.6% async, controlled sync affected flat and
async elapsed −1.57–4.37%. Ordinary CSV is adverse +0.07–5.09% elapsed and
+6.25–11.70% coarse CPU. Retain the complete route deletion/allocation benefit
with explicit ordinary-CSV regression debt, not a general CPU claim. Prioritize
its bounded source/generated-code assessment before another runtime optimization.
No repeated unchanged retiming, performance-objective completion or publication.
New-source full delivery pending; B424/B425 terminal, archived and verified.

## R71 ordinary-CSV transport correction

[B426/B427](results/2026-11-08-call-transport/README.md): bounded paired CPU
attribution does not establish a dominant new call cost. Actual Linux Tier1 proves
the split entry adds a 48-byte outgoing descriptor copy, elided in Windows Tier1.
Managed readonly borrowing removes that transport at all three optimized entries;
no new storage/policy/unsafe mechanism or cancellation deletion. 1,366 internal /
244 public per actual11/10 pass; five new sequence identity/pending-I/O/ownership
cases first pass with production construction unchanged. Allocation remains
−13.5% sync/−12.6% async. Controlled four-job pair114.398s: affected sync flat,
async−1.21–6.31%; ordinary CSV−1.74to+0.43% elapsed/coarseCPUflat. Retain concrete
correction; large B425 CSV concern not reproduced, no full causal explanation.
Text+2.10%, affected sync+1.17%, original B424/B425 and adverse desktop retained.
All source/golden/quiet/join/fixture/archive gates pass. Full corrected delivery
pending; publication postponed. Resume complete construction/frame milestone.

## Intrinsic sequence construction without lexical promotion

[R72/B428](results/2026-11-08-sequence-construction/README.md) moves plain list/
tuple construction into the shared prefix using existing admitted builders,
deleting the old cases/context promotion; tuple backing gains post-admission
cancellation. Two net production lines deleted; no new shape/builder/cache/policy.
Twenty public cases first pass on corrected R71;1,729internal/597public per actual
11/10 then pass with matching probes. Real suspension/cancellation/retained aliases,
first refusal origin/stopped publication/finally return and budget reuse remain.
Allocation −23.48% sync/−21.69% async; complete four-job pair114.025s, all gates:
affected elapsed−1.92–4.79% sync/−2.76–6.49% async; CSV−0.27to+0.86%, text mixed.
Adverse desktop CSV/report and coarse process CPU text+6.67% preserved, no general
CPU gain or objective-completion claim. Retain complete-phase deletion and bounded
affected benefit. Combined correction+construction full delivery pending. Verified
B426 redundant profiles reclaimed with immutable/local archives retained; receipt
records exact path/free bytes. B428 terminal/archived/verified; publication postponed.

## Returned-record external baseline at retained checkpoint

[R73/B429](results/2026-11-08-returned-record-cpython/README.md), source68d5bffe/
corea0aef9ef, fivejobs/107.441s: returned-record CSV compile1.07–1.10×, warm
1.32–1.35×, compile-to-result1.31–1.34× CPython3.13.16. Variable control/compile
IQR visible; current ordinary CSV warm1.16–1.19× variable, text1.67–1.68× diagnostic.
All source/Ready/runtime/reference/golden/quiet/join/fixture/archive gates pass.
Guide marks these revisions separately, retaining older valid824fc6bb rows and
all adverse source pairs. Pending full source68d5bffe delivery; f88191f7 alone
is now fully green CI42426106105, cached. No producer/backend/publication or general
CPU-gain claim. Whole-script performance objective and independent redesign remain.

Combined corrected R71/R72 source68d5bffe full delivery is GREEN, CI42428096630:
Windows/Ubuntu Release actual11/10, original Stress both and installed core
consumers; cached terminal receipt is final. This qualifies the R73 measured
source, not any subsequent float-entry prototype. No unchanged qualification replay.

## Validated floating-entry reassessment

[R74/B430](results/2026-11-08-json-float-entry/README.md) revisits the old candidate
withdrawn under36.7%unchanged-control drift/no-retiming policy. Current source uses
the same original entry, so a bounded current confirmation is justified. Preserve
the complete older evidence; no component retiming. Replace invariant double.Parse
for lexer-validated default JSON floats with public Utf8Parser/complete consumption;
no fallback/new framework/cache/owner/dependency/unsafe mechanism.2,191semantic
goldens and257internal/1,160public per actual11/10 pass, including cancellation,
actual hooks/suspension/refusal/retained aliases/cleanup. Allocation neutral.
Four-job controlled pair115.362s/all gates: float elapsed−11.40–12.03%sync and
−10.05–14.08%async; integer−1.02to+0.61%, text−0.49to−1.05%. CoarseCPU favourable
but quantized; adverse desktop integer+7.75–12.57% and variable text stay visible.
Retain current simpler entry/whole-script benefit without claiming old causality,
universalCPU/CPython gain or objective completion. Fully green source68d5bffe is
the baseline only; new full delivery pending. Verified B424 redundant prototype
core links reclaimed with all immutable/local archives retained. B430 terminal/
archived/verified; publication postponed, broader roadmap remains active.

## Current text-storage gate without allocation capture (2026-11-08)

[R75](results/2026-11-08-text-storage-gate/README.md) preserves the original
staging-floor, Ready-list and diagnostic-socket failures. All started users joined,
owned fixtures unmounted and failure archives verified locally. No current type
attribution, timing or runtime prototype was obtained; stop this setup attempt.
These failures establish missing evidence, not a rejected or permanently closed
scalar/string design. Historical D36 allocation and R63 CPU evidence remain
separately revision-labelled. Current source still allocates token wrappers/arrays;
whole token storage is the next bounded representation assessment. Direct operand
indices/native calls already exist; widening operands alone leaves escaping boxes.
The source gate also found an uncancelled Unicode offset-cache construction loop;
its cancellation/rollback correction precedes any storage prototype.

R74's retained source33b6c191 subsequently passes full Windows/Ubuntu Release,
actual11/10, original Stress on both targets and installed core consumers,
CI42431587731. Its [terminal receipt](results/2026-11-08-json-float-entry/delivery-ci.json)
is cached; never requery. Historical pending-at-measurement state stays unchanged.
This receipt does not qualify subsequent Unicode-cache cancellation code.

## Completed token storage retained (2026-11-08)

[R76](results/2026-11-08-inline-utf8/README.md) retains compiler-managed short UTF-8
storage and direct reserved final writes for casing/split/CSV. No new owner, budget,
ledger/pool/tracing, native dependency or unsafe source code. Adopted buffers keep
capacity; stable tiny memory views are prepaid, with cold construction separated.
Source903eb346 passes162internal/415public per actual11/10. Complete-script
allocation text−9.4%, CSV−8–9%; B436 five-job141.346s controlled pair gives modest
async-text−2.3–3.3% and report elapsed benefit, CSV near baseline. Sync text is
inconclusive with11.59% unchanged-control spread. Float async+0.9–2.7% debt is
explicitly accepted, not explained or repaid by unrelated gains. CPU quantized/
mixed; no broad CPU/CPython/embedding-tail claim. Initial092/B435114.465s and
intermediate7eb/5b adverse desktop/source conditions all remain in the raw archive.
Tier1 hash already inlines its getter; no inlining attribute or causal claim.
Full retained-source delivery passed CI42439532114: Windows/Ubuntu actual11/10,
original Stress and installed core consumers. Publication postponed.

## Combined current-script checkpoint (2026-11-08)

[R77/B440](results/2026-11-08-inline-checkpoint/README.md) refreshes all eight
maintained scripts against CPython3.13.16 at source38f53c33/core903eb346:
26 jobs/539.66s, separate compilation/warm/compile-to-result and two explicitly
labeled numeric-string inputs. All source/Ready/golden/quiet/join/fixture/archive
gates pass. Remaining warm gaps include text1.65–1.67×, returned CSV1.33×,
report1.44–1.47× and file search1.64–1.66×; ordinary JSON0.99–1.00×.
No source-pair speedup, universal CPU/embedding latency or regression-debt claim.
Original B437/B439 staging failures and B438 pre-sample role typo/owned-mount
cleanup remain archived; bounded corrections retain sampling and OS free floor.

## Shared string receiver families retained (2026-11-08)

[R78](results/2026-11-08-string-receivers/README.md) migrates segmentation/search/
padding onto existing immutable ReceiverMethod declarations/borrowed operands.
Two providers, per-access receiver closures, immediate wrappers/general positional
packing and three duplicate arms disappear.48baseline callback cases reproduce
synchronous-host failures; genuine suspension/cancellation and ownership pass in
the migration. Measured045517ea passes161internal/406public per actual11/10.
Final63a7cc5e removes three unreachable duplicate arms and passes257public11;
final allocation−21.26%, controlled045 elapsed−14.5–16.8%sync/−14.3–14.7%async.
The final dead-arm cleanup was not VM-retimed. B441 five jobs141.343s passes all
source/Ready/golden/quiet/join/fixture/archive gates. Report control old spread6.76%
keeps its−6.74to+1.90% elapsed inconclusive. Original desktop textasync+21–25%,
reportasync+13–17% and final mixed controls remain; coarseCPU supports affected
direction, not universal CPU attribution. No per-name native-proof expansion or
performance-objective closure; remaining helper activation/storage gap stays open.

The [adjacent positional-only correction](results/2026-11-08-string-receivers/positional-correction.json)
uses the existing signature layout for eleven migrated methods, rejecting invalid
Python keywords that old declarations/tests accepted.251public per actual11/10
and43 independent CPython method/form/ordering observations pass. Argument effects,
terminal refusal and callback suspension remain covered. No new binder/option or
performance claim. Combined5b9d569a passes CI42443700988: Windows/Ubuntu actual
11/10, original Stress both targets and installed core consumers; terminal cached.

## Native workstate dispatch retained (2026-11-08)

[R79](results/2026-11-08-native-workstate/README.md) centralizes existing native
receiver calls and puts admitted exact list/tuple iteration before context
promotion. Duplicate ordinary native-call branches disappear; no new name marker,
backend/dispatcher/arena/pool/owner. Same-core complete helper/inline gate separates
~1.30MB invocation difference from~0.29MB fused-text difference. Source18b563e9
passes140internal/326public per actual11/10. Ordinary/guarded reports allocate
~42%less; B443 six jobs168.441s yields affected elapsed11.8–14.4%lower sync/
14.5–16.1%async, guarded10.5–12.2%lower. Native control−1.26to+2.84% is
inconclusive(current spread3.70%). CSV+0.25–1.92% elapsed is explicitly accepted
debt; reportasync near baseline, coarseCPU not universal attribution. Original
desktop adverse controls remain; no debt repayment or performance-objective
closure. All source/Ready/golden/quiet/join/fixture/archive gates pass. New tests
preserve native-loop adoption before true I/O, alias/result funding, nonallocating
cancellation and terminal refusal. Committed913ea550 is fully qualified by cached
CI42447193207: Windows/Ubuntu actual .NET11/10, original Stress and installed core
package consumers. This does not qualify subsequent runtime changes.

## Current execution attribution (2026-11-08)

[R80/B444 and R81](results/2026-11-08-execution-attribution/README.md) preserve
current-source CPU attribution and a same-core module/function workload contrast.
The profile has1,083guest samples/5.45CPU-s,49unknown. Prefix20.96% includes
inlined useful work;112samples map to loop/acquisition/check/dispatch blocks.
Diagnostic overrides preclude normal-settings timing claims. Split already
batches admission and string hashes already have the exact-native route.
Function-wrapped text/CSV allocate slightly more(<0.07%), with unstable desktop
elapsed and deliberately different namespace semantics. No speculative namespace,
numeric, call-arena or reservation redesign is selected from these observations.
All golden/ownership/cleanup and profile source/Ready/quiet/join/archive gates pass.
No runtime change, CPU improvement or performance-objective closure is claimed.

## Prepared member, iterator and branch consumers (2026-11-08)

[R82/B445](results/2026-11-08-prepared-consumers/README.md) completes existing
single-input forwarding for these consumers, removing adjacent pure leaf dispatch
and temporary publication. No argument view/arena/cache/owner/backend/option.
Allocation neutral; text module77→74, CSV83→79/helper17→16, report62→58 static
instructions.332internal/155public focused pass per actual11/10, including genuine
suspending bool/member/iterator callbacks and awaited cancellation/budget reuse.
Six controlled jobs167.554s: textsync elapsed−1.85to−0.13% with mixed coarseCPU;
textasync−4.74to−0.98% but old spread3.60%. Guarded−0.77to+1.76%, CSV−0.24to
+1.50%, report−4.02to+2.15%(candidate spread5.32%), arithmetic near baseline.
Retain consistent operand representation/removed execution work; no broad CPU
gain established. Possible CSV regression and earlier debt remain explicit.
Initial six-case rejection, stale copied control metadata/per-case snapshots,
wrong immutable launcher name and all adverse desktop controls are preserved.
All source/Ready/golden/quiet/join/fixture/archive gates pass. Full retained delivery
follows; no further per-opcode refinement series or performance-objective closure.

## Combined CPython checkpoint after receiver/workstate changes (2026-11-08)

[R83/B446](results/2026-11-08-runtime-checkpoint/README.md) refreshes production
91f15a3e/core4c072fed:26jobs539.117s, eight complete scripts with compilation,
warm and compile-to-result separated, plus ordinary rsplit/guarded text warm rows.
Compile-to-result ratios span about1.01×CPython(JSON) to1.63×(text). Ordinary
helper warm rows are2.12–2.23×; source/call/frame and text conversion contributions
remain unresolved. Variable native-text/report/CSV controls remain visible; earlier
numeric-string warm evidence and named-source guide are preserved. All source/
Ready/golden/quiet/join/fixture/archive gates pass and local archive is verified.
Full retained-source qualification is independent CI42451722109, not established
by this benchmark. No single-change CPU attribution, debt repayment or objective
closure. Next attribution must use the ordinary helper path, not the fused-helper
profile; binding/workspace reuse and inline presence bits already exist.

R82 source91f15a3e final delivery is fully green and cached as CI42451722109:
Windows/Ubuntu Release actual .NET11/.NET10, original Stress and installed core
consumers. This also qualifies the production inventory measured by R83; it does
not qualify subsequent runtime work. Publication remains postponed.

## Ordinary-helper CPU attribution (2026-11-08)

[R84/B447](results/2026-11-08-ordinary-helper-attribution/README.md) profiles the
complete rsplit report at qualified91f15a3e:1,107guest samples/5.58CPU-s,46unknown.
Prefix19.06% includes inlined work; no dominant binder allocation supports an arena,
value or memory redesign. Every raw sample/map/log and unknown remains. All source/
Ready/golden/quiet/join/fixture/archive gates pass; diagnostic overrides preclude
timing claims. One-case catalog/existing core archival preserve the OS floor without
evidence deletion. The concrete callable-representation inconsistency selects R85;
a separate source pair must establish useful magnitude.

## Canonical native receiver declarations (2026-11-08)

[R85/B448](results/2026-11-08-receiver-contract/README.md) removes typed no-argument
string/dictionary targets, the special LoadCallReceiver/name heuristic, redundant
member routing and text-semantics provider closures. One shared declaration serves
immediate/exposed/native-return calls; native-body proof/fusion stays unchanged.
Casefold/title complete helpers allocate45–52%less, with controlled elapsed22–24%
lower and agreeing coarseCPU direction.373internal/2046public per actual11/10 pass.
Existing public tests caught17 invalid-arity prototype failures; fixed signatures
and common no-argument diagnostics restore them unchanged. Original6fdf rsplit
desktop+6–7%motivated fixed-arity invocation within ReceiverMethod; originals remain.
Final aa4acebe controlled rsplit−0.90to+4.45%(candidate spread3.85%) is inconclusive;
CSV−0.38to+2.70%possible regression stays explicit, reportasync−4.14to−2.78%.
Earlier debt remains separate. All source/Ready/golden/quiet/join/fixture/archive
gates pass; six jobs168.102s. One identical worker companion library atomically
hardlinked, with all paths/bytes/evidence preserved. Retain this complete callable
consolidation; no universal CPU claim or whole-script objective closure. Full
platform/originalStress/installed source qualification follows the retained commit.

Full CI42457767068 later found exposed environment-view receiver rebinding and
unrelated Decimal diagnostic regressions. The [binding correction](results/2026-11-08-receiver-binding-correction/README.md)
reuses existing fixed-arity exposed wrappers and deletes the overbroad binder
override; original tests stay unchanged. Both actual targets pass focused checks,
and affected allocations remain45–52%lower. Desktop timing/control instability
is preserved; no new controlled elapsed confirmation is claimed. Corrected-source
full delivery is independent of the original failed run and B448 timings.

Corrected production7796a24f is fully qualified and cached as CI42459221409:
Windows/Ubuntu Release actual.NET11/10, original Stress and installed core-package
consumers. No subsequent prototype or newer measurement is qualified by it.

## Complete CSV-helper attribution (2026-11-08)

[R86/B449](results/2026-11-08-csv-helper-attribution/README.md) uses committed02c76e52,
before the exposed-callable correction:1,124guest samples/5.65CPU-s,79unknown.
Parsing, instruction/input/return transport, adoption and numeric dispatch all
contribute; binding/work construction is3.56%self/4.27%inclusive. This does not
support a binder arena or universal value rewrite. Current CSV already incorporates
funded raw-row transfer/shared keys/batched fields/in-place native compaction, so
the older bulk proposal cannot be revived unchanged. All identity/golden/quiet/
join/fixture/archive gates pass; existing B448 core archival reused, no evidence
deleted. Keep raw/JIT/map/unknown evidence; next source gate maps existing samples
to generated execution/operand work before selecting a coherent replacement.

## Closed-helper preparation source gate (2026-11-08)

[R87/R88](results/2026-11-08-helper-activation-gate/README.md) maps existing R86
samples to unique Tier1/method/code-size groups: prefix fetching/dispatch dominate
its samples; token/refusal checks are smaller. Input-handler JIT listing is absent.
Current same-function-scope helper/inline contrast has neutral text allocation,
diagnostic text elapsed−11.13to−15.96%, with unchanged role spreads2.43/7.18%.
CSV allocation+1.51–1.52%, mixed elapsed/21.08%role spread and protected continue
shape limit attribution; report14.77%spread retained. No causal binder/CPU claim.
All complete goldens/funding/image/cleanup gates pass; initial driver row-count
refusal is retained. Select consolidation of existing native-body method inventories
onto canonical declarations/shared frontend return facts, preserving fusion/proofs
and removing displaced enums/maps. No new engine/result-type catalog/proof markers;
separate correctness/source-pair validation is required before retaining a change.

## Canonical closed-method preparation (2026-11-08)

[R89/B450–B451](results/2026-11-08-native-method-facts/README.md) removes the duplicate
native method enums/map/indexed declaration array/accessors, reusing existing
canonical methods/frontend return facts. Six missing Boolean facts join the existing
catalog. Per-callsite prototype metadata is replaced by one native-node reference
and four generic result shapes. No new engine/catalog/per-name proof markers.
397internal/2165public per actual11/10 pass, three independent CPython cases.

Initial source and all desktop adverse/unstable controls remain. B450167.945s
shows affected elapsed13–18%lower, but fused control+2.52–8.29% with adverse CPU.
The existing fusion then drops its dead producer/type proof/empty argument scan,
preserving original span-check order. One refined B451168.406s: rsplit sync
−13.94–17.85%, async−16.06–19.25%, casefoldsync−11.14–14.17%, coarseCPU agrees.
Allocation neutral. Fused−3.77to+2.31% is inconclusive; old concern not reproduced,
no complete causal explanation. CSV+0.42–1.03%possible elapsed debt accepted,
coarseCPUflat. Reportinconclusive; earlier debt separate. All source/Ready/golden/
quiet/join/fixture/archive gates pass; no users remain. Exact baseline core reused
without evidence deletion. Retain coherent simplification/affected improvement;
full final-source delivery follows commit. Objective/publication remain open.

R89 productionac5aea97 is fully qualified and cached as CI42463155006:
Windows/Ubuntu Release actual.NET11/10, original Stress and installed core-package
consumers. This qualifies the measured production inventory, not later changes.

## Canonical-method CPython checkpoint (2026-11-08)

[R90/B452](results/2026-11-08-canonical-method-checkpoint/README.md) refreshes the
same26 maintained jobs at ac5aea97/core6a098198 in538.681s. Compilation/warm/
compile-to-result and I/O/validation/prior cleanup remain separate. Warm ordinary
rsplit1.80×CPython, guarded helper2.15×; other ordinary cases about1.00–1.67×,
with variable cases visible. The guarded case includes an isspace predicate;
different algorithms preclude attributing its entire gap to dispatch/fusion.
All source/Ready/golden/quiet/join/fixture/archive gates pass; no users remain.
Exact existing B451 core/archive reused after measured footprint/headroom proof,
without evidence deletion. Current guide names the measured source/qualification,
prior tables and adverse conditions remain linked. No single-change attribution,
debt repayment or whole-performance-objective closure.

The following source gate confirms ordinary integer formatting is already native
UTF-8 on11/stack-bridged10 and valid escaped JSON already uses UTF-8 CopyString.
Remaining large-integer JSON rendering is outside ordinary fixtures; builtin
slots have per-run owner/live-namespace identity. No speculative conversion or
global type-cache prototype follows. Next bounded design assessment considers
sharing existing composed-receiver preparation across ordinary/native execution,
with deletion of displaced native-only fusion machinery as a required outcome.

## Shared casing/split preparation (2026-11-08)

[R92/B453](results/2026-11-08-shared-cased-split/README.md) shares the existing
kernel through ordinary call metadata and native helpers, deleting both old
native-only fused variants and separate span/evaluation selection. Exact dynamic
guards precede effects; forwarded local bindings remain ordinary-visible.
Guarded full text allocation−12.74%; controlled elapsed sync−3.48to−4.84%,
async−6.78to−8.01%, coarseCPU agrees. Desktop async+68.76to82.65% remains adverse
evidence without a complete causal explanation. Fused/rsplit controls inconclusive;
CSV−0.26to+1.07% possible debt; small frontend allocation increase0.1–0.3% accepted,
frontend elapsed unstable. No fixed-percentage veto or whole-objective closure.
All source/Ready/golden/quiet/Closed/join/archive gates pass, no users/fixtures.
Focused semantics/ownership pass per actual11/10; full CI42467357131 subsequently
qualified committed05d81b83 on Windows/Ubuntu actual11/10, originalStress and
installed consumers. The terminal receipt is cached with R92; no repeat dispatch.
Bounded verified hardlinks preserved old core paths/bytes and all archives while
freeing27,848,704B. Failed local inventory indentation/analysis-clock selection
and early retrieval guard are recorded; no runtime failure was hidden or retried.

## Remaining frame/operand attribution (2026-11-08)

[R93/B454](results/2026-11-08-current-operand-attribution/README.md) closes the
bounded source gate without a runtime prototype: argument presence/workspace/local
reuse/context deferral already exist. Current complete rsplit script yields1092
guest CPU samples,51unknown(4.67%); prefix17.12%self/input6.04% remain visible with
inlining limits, not binder dominance. Current prefix maps uniquely; input listing
absent. Existing CSV parser already scans native chunks/in-place. No new arena,
fetch representation or parsing kernel is selected from these facts.

[R94](results/2026-11-08-allocation-selection/README.md) uses the existing unchanged
allocation collector for two bounded complete scripts. Sampled type association,
not exact bytes/CPU: PyString44.81/29.13%, BigInteger21.61/27.13% (text/CSV),
Boolean7.25%CSV. Output/Ready/zero-lost/Closed/join/fixture/funding gates pass;
original raw ticks retained. This supports assessing generic direct comparison
branches before a universal value replacement or new ownership subsystem. No
runtime change or speedup follows solely from this profile.

## Direct comparison branches (2026-11-08)

[R95/B455/B456](results/2026-11-08-direct-comparison-branches/README.md) consumes
native numeric condition truth without a result box/publication and uses one
operator enum through ordered comparison helpers. Predicate/name/forwarding/
invoker and obsolete primitive fallback plumbing disappear. Existing exact-number
ordering, general raw-result protocols, current cancellation, ownership and real
comparison/truth suspension remain. No public API or memory framework change.

Originalcde/B455 keeps affected gains and text elapsed+0.72to+6.58%/CPU+4.41to+7.46%.
One justified refinement gives comparison control its own opcode handler, restoring
the original ordinary value handler. Refined9d/B456 retains CSVsync−5–9%, async−10–16%
(variable candidate control), JSON−6–8%, returnedCSV−3–8%. Text+1.28to+3.50%elapsed/
CPU0to+2.90% remains explicit accepted debt; no unrelated gains repay it or complete
causal explanation is claimed. Report inconclusive. All original sources/cores/
failed conditions/adverse data remain; no further retiming to erase the debt.
Focused76public/61internal per actual11/10 and stronger6public close assertions pass.
Both source-pair gates/archive/local verification pass, no users/fixtures remain.
Committedcb2b015e subsequently passed full CI42472284713 on both platforms,
actual11/10, original Stress and installed consumers. This milestone does not close the performance
objective; continue the substantial roadmap with measured storage/transport costs.

## Direct operand references (2026-11-08)

[R96/B457](results/2026-11-08-direct-operand-transport/README.md) removes the24B
operand carrier/reader and runtime general-arity projection. Direct references
retain original leaf-read ordering and descriptor snapshots; actual async work
keeps required references. Synchronous metadata is borrowed. No replacement carrier,
pool, owner or public API. Existing62public/24internal checks peractual11/10 pass.
Allocation neutral. Controlled CSV/JSON elapsed favorable; text direction favorable
but old-source spread8.70% prevents precision. Report+0.88to+3.65%elapsed with mixed
CPU is inconclusive/possible debt. Retain removal with those limits; original
desktop adverse/failed migration conditions remain. R95 text debt is separate,
and no unmatched-source repayment or universal CPU claim follows.
All source/Ready/golden/quiet/Closed/join/archive/local verification gates pass;
no VM users/fixtures. Baseline cb2 subsequently fully qualified CI42472284713;
candidate delivery pending. The string-layout gate used existing64B samples and
unchanged source; no speculative cold union/layout change or new measurement.

[R97/B458](results/2026-11-08-direct-operand-checkpoint/README.md) refreshes the
unchanged26-job CPython checkpoint at committed94cb8963/cored6254b2e after
R92/R95/R96. Separate compile/warm/compile-to-result, identical sources/fixtures/
goldens/worker/protocol, normalGC/tiering, two same-source Lython controls and
CPython3.13.16. All gates/local archive verification pass; no remaining users.
Full source qualification is independently green/cached CI42475420182.
Updated primary guide and historical guide preserve every old
ratio/adverse case; external ratios do not establish causal gains or repay debt.

[R98](results/2026-11-08-shared-split-construction/README.md) shares explicit and
whitespace split final construction, removing temporary reverse lists/conversion/
copied arrays and duplicate forward growth. Original red tests demonstrate67KB
allocated before whole-result refusal; complete admission now precedes fragments.
Two complete delimiter reports allocate5.47%/8.91–8.92%less across both APIs;
unchanged whitespace/CSV/report allocation neutral. Desktop elapsed/control drift
and adverse originals remain diagnostic; no controlled CPU claim/new VM campaign.
Corrected150public/35internal peractual11/10 pass; full retained-boundary qualification pending.
Native UTF-8/owners/aliases/current cancellation use existing mechanisms. Initial
build failure and invalid immutable-identity oracle/source are preserved separately.
The initial R98 prototype2e232a4d then failed two real suspended __index__ cases:
both explicit directions returned despite admission-triggered cancellation after
awaited host I/O. Current context now passes explicitly into shared construction.
Corrected57e1ef6c reconfirms allocation; oldcore/source/results and red tests remain
separate. Corrected desktop report+24.6–28.7% and asyncCSV+15.7–26.6%, unstable
unchanged controls and all initial adverse results remain inconclusive/unexplained.
No controlled elapsed/CPU gain asserted; no new VM collection.

[R99/B459](results/2026-11-08-default-rsplit-preparation/README.md) lets default
zero-argument rsplit share the existing cased-word preparation, removing two compiler
restrictions and intermediate cased-line storage; no new runtime machinery.
Complete lower report allocation−16.46%, upper−11.88% across both APIs. Controlled
lower elapsed−14.19to−16.70%sync/−12.88to−13.44%async, upper−7.85to−11.77%sync;
coarseCPU agrees. Ordinary-split control variable/mixedCPU, CSV nearbaseline,
report−3.94to+2.69%/baseline5.77%spread inconclusive. Original desktop adverse and
49%async-baseline drift remain preserved.120public/35internal peractual11/10 pass,
including all29 whitespace scalars, real suspended custom methods/exact closes,
aliases/first refusal/cleanup and supplied-option/visible-local Python oracle.
B459167.959s all gates/archive/local verification pass; no users/fixtures. Full
retained-source delivery pending independently; old source debts remain distinct.

R98 sourcecbe88fef independently passes Windows/Ubuntu actual11/10,
originalStress and installed-core consumers, cached CI42477957586. This later
receipt does not change its allocation/diagnostic elapsed claims or qualify R99.

R100 source gate atc74c5802 finds common opportunities already delivered:
sum has an unboxed64-bit/float/general SumState; local imports compile/lower to
the executable engine; immutable builtin functions/signatures are shared.
Builtin exception values still own lazy name/NewSlot metadata and per-run bases
use the active State. Naive global sharing is not selected from source alone.
Public projection preserves CLR ingress types and returns guest BigInteger boxes;
a new scalar representation would need an explicit boundary contract and broad
numeric-consumer validation. Sampled boxing alone does not justify that redesign.
The next bounded investigation is one complete small configuration-script CPU
capture using the existing profiler/worker, to assess actual invocation setup.

[R101/B460](results/2026-11-08-config-invocation-attribution/README.md) closes one
bounded config-setup profile atc74c5802/core3fb37cc3:6.010wall/2.93guestCPU-s,
552samples/31unknown(5.62%). Inclusive factory3.44%, fullcontextctor6.34%,
JSON16.67%, comparisonfilehost49.64%; kernel leaves60.33%. Groups overlap; no
causal/normal-settings elapsed claim. No builtin-lazy/global-root/scalar redesign
selected. Decoder already has a completed-value loop; encoder still uses actual
async per-step helpers. Next bounded source design targets completed encoding,
preserving hooks/owners/chunks/current tokens. B460 all gates/archive/local
verification pass; no remaining users/fixtures/maps. Exact B459 core reused.

R99 sourcec74c5802 independently passes Windows/Ubuntu actual11/10, originalStress
and installed core consumers, cached CI42479543467. R101 attribution reused that
same production source/core; source qualification is separate from diagnostics.
The next selected milestone is bounded completed JSON encoding on its existing
frame stack, with exact chunks/hooks/token/ownership and measured source-pair gates.

## Unified JSON encoding (2026-11-08)

[R102/B461](results/2026-11-08-unified-json-encoding/README.md) retains one existing
frame traversal for eager and streamed output, deleting four recursive renderer
helpers and completing ordinary operations without per-step async state machines.
765public/171internal peractual11/10 pass. Private metadata temporary ownership
fix and original50+8accounting/threshold failures retained. Eager allocation+4.9%,
controlled sync elapsed+3.27to+5.01%/coarseCPU+6.25%: accepted renderer-consolidation
debt, not a speedup. Streamed elapsed−2.02to−4.70%sync/−3.28to−6.42%async with mixed/
coarseCPUnearzero. CSVcontrol+1.17to+8.77%/6.14%candidate spread and JSON/config
uncertainty preserved, as are desktop adverse/35.8%baseline drift. All full goldens/
quiet/join/archive gates pass,172.779s; no remaining users/fixtures. Full retained
source delivery pending separately. Future JSON changes must preserve one walker
and explicitly decide debt; the broader whole-script performance goal remains.

[R103/B462](results/2026-11-08-unified-json-checkpoint/README.md) updates the
CPython comparison at committed15c06863/core1674 after R98/R99/R102.28jobs,
581.936s, same26-job catalog plus two full JSON normalization
warm cases. Separate compile/warm/compile-to-result, unchanged workers/normal
runtime, isolated CPython3.13.16 and duplicated same-source Lython controls.
All source/golden/quiet/join/archive/local gates pass; no users/fixtures. Full
source qualification independently pendingCI42484943603. Primary guide updated; all prior
named-source tables and variable/adverse cases preserved. External ratios do
not repay eager source-pair debt or establish isolated CPU gains. R104 source gate
finds common lexical facts/final call storage/native UTF8 caches already shared;
no new speculative compiler/frame/ownership machinery selected. The broader
whole-script performance objective remains open.

Source15c06863 independently passes full Windows/Ubuntu actual.NET11/10, original
Stress and installed core consumers, cached CI42484943603. This closes R102
delivery and qualifies R103's measured source; it does not qualify later prototypes
or alter the accepted eager source-pair regression. No unchanged qualification replay.

## Native JSON constructor and call transport (2026-11-08)

[R105/B463](results/2026-11-08-native-json-construction/README.md) shares one
initializer across bound native module options, public construction and guest
assignment. Remove constructor dictionary/name-array and native keyword/method/
value transport; keep mutable attributes, genuine custom dispatch, managed call
guard and one R102 renderer.765public/171internal peractual11/10 pass. Eager
allocation−27.22%sync/−27.68%async, streamed−24.03/−23.85%. Controlled eager
elapsed−9.05to−9.93%sync/−9.06to−11.58%async, streamed sync−6.25to−7.00%;
coarseCPU agrees. Config elapsednearbaseline/coarseCPU+4.38to+5.70% possible debt,
CSVcontrol variable and JSONsummary possible small debt retained. Original
desktop+8.25%adverse/17.48%baseline drift remain. All gates/archive/local verification
pass,172.758s; no users/fixtures. New-source full delivery pending independently;
15c source's cached green does not qualify this prototype. No per-edit CPython replay,
old-debt repayment or broader objective closure.

R105 sourcea7bd4103 independently passes full Windows/Ubuntu actual.NET11/10,
originalStress and installed core consumers, cached CI42488128858. Controlled
gains/possible configCPU debt remain scoped to their recorded pair. This receipt
does not qualify the subsequent R106 conditional-helper prototype.

## Native acyclic return trees (2026-11-08)

[R106/B464](results/2026-11-08-native-conditional-helpers/README.md) replaces
single-block native-return selection and final-unselected-return spans with one
existing proof/evaluator for branch returns. Shared empty-list admission; no new
engine/control carrier/arena/runtime owner/cache/options.205public/42internal
peractual11/10 pass, independent Python and actual async truth/cancellation/refusal/
funding/cleanup/selected-span coverage. Warm allocation neutral; compiler allocation
+0.08–0.53%. Controlled guarded elapsed−12.82to−13.48%sync/−13.61to−15.93%async,
coarseCPU agrees. CSVruntime+2.71to+3.73%/CPU+4.65% retained debt, cause unresolved;
rsplit/JSON nearbaseline. Controlled CSVcompile−1.23to+0.58%; original Windows
+74–79% not reproduced on Ubuntu or changed-order Windows confirmation, but other
Windows compiler controls are adverse/variable. Windows compile timing remains
uncertain; all originals/confirmation and rejected two-case driver invocation kept.
Six jobs168.236s, all gates/archive/local verification pass; no users/fixtures.
Exact new-source full delivery pending, a7's green does not qualify this prototype.
No per-edit CPython replay, silent debt repayment or broader objective closure.

## Native return-tree construction refinement (2026-11-08)

[R106/B465](results/2026-11-08-native-conditional-helpers-final/README.md) retains
the return-tree proof with the unchanged literal-list factory borrowed through a
cold native helper. Initial general-list extraction and duplicate admission removed.
205public/42internal Release peractual11/10 pass. Warm allocation neutral; controlled
guarded elapsed−13–17% both APIs, coarse CPU supports the gain. Initial CSV +3%
does not recur (−0.12to−1.92%, CPU flat/lower); cause remains unresolved, original
source/data preserved. CSV compile+0.56to+1.48%, CPU mixed; JSON control variable.
Windows compiler uncertainty and initial compiler allocation+0.08–0.53% remain.
Six jobs168.190s/all gates/archive/local verification pass; no users/fixtures.
Exact retained-source full delivery is independent/pending; no publication or
completion of the broader performance objective.

## Current report attribution / bounded source gate (2026-11-08)

[R108/B466](results/2026-11-08-current-report-attribution/README.md) at7265538c:
1110guest samples/5.58CPU seconds,64unknown leaves. Formatting/JSON/dictionary work
material; call/frame inclusive84% contains useful callee work, leaf3.96%, context
constructors0.72%. No packaging-dominance claim or speculative runtime prototype.
Current direct record filling/token windows/completed parser/final-buffer formatting
already exist. Preserve R26's admission-reentry correctness deletion of the native
formatted evaluator; do not reintroduce upfront mutable proofs. Offline classifier
corrected actual call names/per-method deduplication; original/raw retained, no retime.
All gates/archive/local verification pass, no users/RAM. Full delivery independent;
next combined retained-source external checkpoint, no new framework/publication.

## Combined retained-source CPython checkpoint (2026-11-08)

[R109/B467](results/2026-11-08-json-conditional-checkpoint/README.md) at7265538c/
core1aa68ce9, unchanged28jobs/normalsettings/catalog/workers/full goldens,
581.729s. Separate compile/warm/compile-to-result, two independent
same-source Lython controls and CPython3.13.16. All gates/archive/local verification
pass, no users/fixtures. Full source qualification independently
pendingCI42496463028. Current guide updated, prior15c guide and all
variable/adverse observations preserved. No isolated CPU attribution, old-debt
repayment, publication or broader objective closure.

R106 exact-source delivery on7265538c passes/cached CI42496463028:
Windows/Ubuntu actual11/10 full Release, original Stress both and installed core
consumers. No runtime/source change or repeated earlier qualification. Focused
205public/42internal peractual11/10, initial67e/refined1aa raw observations and
uncertainties remain separate. [Receipt](results/2026-11-08-native-conditional-helpers-final/delivery-ci.json).

## Known-layout record value capacity (2026-11-08)

[Initial R110/B468](results/2026-11-08-json-record-capacity-initial/README.md)
uses full layout capacity at construction: wide allocation−11%, alternating+14.7%
and elapsed+4.31to+10.82%. One [retained refinement/B469](results/2026-11-08-json-record-capacity/README.md)
moves the existing capacity decision to actual ordered-prefix growth, removing
intermediate arrays without a new owner/cache/predictor/policy. Eight-record full
report replacement admissions21→7, initial reservations unchanged.897public/
193internal peractual11/10 pass, independent full Python shapes/order plus actual
async/cancellation/refusal/shared/retained ownership checks. Wide allocation−8.56–8.58%
both APIs; controlled elapsed−3.80to−10.29%sync/−6.05to−7.53%async, coarseCPU supports.
Alternating regression does not recur; allocation neutral, elapsed−1.59to−4.20%,
CPUnearflat. CSVcontrol8.67%baseline spread inconclusive, JSON/shortreport nearbaseline.
All original/adverse/desktop conditions preserved; no unrelated debt repayment.
170.447s/sixjobs/all gates/archive/local pass, no users/fixtures. Initial disk
preflight stopped before workers; actual wide evidence footprint+50% now uses32MiB
headroom with unchanged240MiBfloor, exact-byte hardlinks preserve all evidence.
Exact retained-source full delivery pending independently; primary CPython guide
continues to name726. Publication/regex unchanged; broader objective stays active.

R110 exact-source delivery05790317 passes/cached CI42502610937: Windows/Ubuntu actual11/10 full Release, original Stress both and installed core consumers. [Receipt](results/2026-11-08-json-record-capacity/delivery-ci.json). No runtime change or repeated qualification; initial/refined source pairs and uncertainties remain distinct.

## Integer-loop transport evidence (2026-11-08)

[R111/B470–B471](results/2026-11-08-integer-loop-attribution/README.md) at05790317/
core1310. Existing16384iteration loop short normal micro700.968µs versusCPython
596.612µs≈1.17x; oldcontrol713.390µs, lowIQR. Diagnostic, no quiet-after/general
multiplier/R110numeric gain claim. Initial CPU collector rejected an omitted
no-file digest before warmup; completed micro never repeated. Failure/maps/IPC
metadata archived, users ended/RAM unmounted; one corrected profile-only capture
passes allgates/archive/local.1170samples/5.92CPU seconds/63unknown5.38%, publication
287leaf24.53%, prefix23420%, range14612.48%, exactaddition11810.09%, input1089.23%.
Groups overlap; not all publication removable. Source next: defer compiler-name
lookup/classification until actual namespace/mirroring needs it, preserving all
cleanup/cell/global/synthetic/ownership/cancellation behavior. No tagged-value/frame
redesign from boxing alone. Original classifiers and failed stage/capture retained.

## Local publication tradeoff (2026-11-08)

[R112/B472](results/2026-11-08-local-publication/README.md) defers compiler-name
lookup/classification to real namespace branches; focused446public/132internal
peractual11/10 pass, allocation neutral. Six controlled jobs167.915s/all gates:
async loop modestly faster, sync loop/control spread inconclusive, representative
file scripts nearbaseline/control-limited, report CPU+1.42–3.84%possible debt.
Not retained: duplicated cold branch structure without convincing complete-script
benefit or allocation reduction. No fixed percentage veto/permanent design ban.
Exact fully qualified baseline restored; prototype binaries must not be reused
without a fresh build. Raw/source/binaries/adverse results archived and verified;
all users ended/owned RAM unmounted. No full CI replay for restored identical code.

## Integer helper proof, intermediate (2026-11-08)

[R113/B473](results/2026-11-08-integer-helper-prototype/README.md) broadens existing
Add-only proof to exact integer binary operators, canonical semantics. Focused
1524public/104internal peractual11/10; allocation neutral, compile+0.40–0.41%affected.
New complete CSV/JSON accounting scripts,168.664s/all gates: CSV−1.53to−5.78%sync/
−4.33to−6.28%async; JSON−3.54to−5.02%sync/−4.57to−6.67%async with supportingCPU.
CSVsyncCPU uncertain, reportCPU0to+6.25%possible debt. Intermediate, no full delivery
or CPython refresh. Source revealed native proof indices disable ordinary operand
forwarding even for failed guards; coherent next milestone makes those independent
using immutable source metadata in existing nodes, with compiled-storage tradeoff.
Disk preflight/alias-only failures/corrections preserved, no guard relaxation or
archive deletion; users joined/RAM unmounted/archive locally verified.

## Independent native proof, intermediate (2026-11-08)

[R114/B474](results/2026-11-08-independent-native-proof/README.md) removes runtime
source offsets/native-forwarding exclusion; immutable descriptor nodes32→72B,
ordinary instructions32B. Allocation neutral, incremental compiler+0.59–1.09%.
Eight jobs223.498s/all output/ownership/quiet/cleanup/archive gates. Integer scripts
improve, async floatCSV+4.14–6.29%adverse; text mildly faster, report uncertain.
Intermediate, no full qualification. R115 bounded correction shares primitive
integer/float/bool proof with compiler; canonical operations, custom protocols
ordinary and explicitly measured. Original limit metadata210 vs configured270/
terminal infinity retained; no claim of independently observed earlier live limit.

## Coherent primitive helper delivery (2026-11-08)

[R115/B475](results/2026-11-08-primitive-helper-delivery/README.md) retains the
combined shared primitive proof and independent descriptor/ordinary-forwarding
design.1526public/104internal peractual11/10,8jobs224.458s/allgates/archive/local.
IntegerCSV/JSON−3.5–5.2%, floatCSV−1.8–6.2%across APIs/supportingCPU; prior adverse
float pair preserved. Warm allocations neutral in incremental diagnostics, direct
compile+1.40–1.48%accounting/+0.59%text. Native nodes32to72B/instructions32B.
Custom async+1.28–3.36%/CPU+6.67%explicit debt; text controls variable, reportCPU
possible debt. Retain coherent simplification with those limits, full delivery
pending. Live270slimit captured; all historical conditions/intermediates remain.
Continue current complete text/custom CPU attribution and primary checkpoint,
not claim the performance objective complete or revive slower call carriers.

## Current complete-text CPU assessment (2026-11-08)

[R116/B476](results/2026-11-08-current-text-cpu/README.md), b1e17b64/core93e9,
1160samples/5.85CPU/67unknown5.78%, all gates/archive/local. Prefix21818.79%,
input69 5.95%, hash48 4.14%, split/casing97leaf8.36%, nativeproof7 0.60%,
binding/contexts0leaf. Monitor50leaf4.31%maps to actual allocation preflights;
fragment commit and dictionary replacement already avoid root resize. Direct
positional calls already suppress duplicate pre-frame proof; earlier generic
repeated-probe discussion was unverified for that measured route. No dominant
redundant phase established, no prototype selected. Keep required checks/budget
semantics, use bounded existing-PC/source analysis before any further redesign.
Next current CPython checkpoint, custom async debt remains explicit.

R115 exact-source deliveryb1e17b64 passes/cached CI42512127664: Windows/Ubuntu actual11/10 full Release, original Stress both and installed core consumers. [Receipt](results/2026-11-08-primitive-helper-delivery/delivery-ci.json). No changed timing, repeated old qualification, publication or implicit repair of custom async debt.

## Qualified primitive-source CPython checkpoint (2026-11-08)

[R117/B477](results/2026-11-08-primitive-cpython-checkpoint/README.md), qualified
b1e17b64/core93e9, CPython3.13.16,15jobs311.431s/allgates/archive/local. Five
unchanged scripts separate compile/warm/compile-to-result. Compile-to-result:
returnedCSV1.23–1.25x, CSV1.06–1.07x, text1.52x, JSON0.92–0.96xvariable,
report1.42–1.46xvariable. Warmtext1.58–1.65xvariable, CSV1.03–1.07xvariable;
all roles/adverse data retained, no isolated gain/old-debt repayment. Historical
config/search/manifest/extra variants remain726, not current-source claims.
No publication, new framework or unchanged source qualification replay.


## Flat executable lookup facts and shared record fields (2026-11-08)

[R119/B478](results/2026-11-08-record-field-resolution/README.md), qualified93e9
reference/refined19b8 candidate,8jobs224.323s/allgates/archive/localverified.
Complete native CSV/JSON elapsed−5.2–10.4% across source/control pairs, customCSV
−3.1–6.4% across APIs/supporting CPU. Warm allocation near neutral except custom
+0.575%; compile allocation unchanged. Text inconclusive, report CPU+1.53–6.25%
possible debt. Initial report+9.02%allocation explained by96B per-call cache;
refinement restricts field slots to reusable loop/comprehension sites. Actual
census corrects earlier false native-report inference. Retain flat existing
lookup storage/shared layouts, no new opcode/owner/framework; full delivery pending.
All initial/adverse source/core/raw retained. R118 guarded get/add/store source gate
selected no prototype, complexity/evidence decision, not a permanent closure.


## Remove cache acquisition for already-excluded receivers (2026-11-08)

[R120](results/2026-11-08-cache-eligibility/README.md) consolidates the existing
mutable/protocol receiver exclusion and removes unused per-call cache table
allocation/admission.24internal/214public peractual11/10 pass, same5complete-case
normal diagnostic/full goldens/ownership gates. CustomCSV allocation−1.71% across
APIs (~192KiB/4096rows), other allocation nearbaseline; no CPU claim. Adverse desktop
text/custom and drifting controls preserved. Frontend unchanged, no repeated compile
campaign. Retain; full new-source delivery at next coherent boundary, R119CI remains
independent. No new owner/cache framework/options or publication.

R119 exact-source d05b3359 passes/cached CI42519822299: Windows/Ubuntu actual11/10 full Release, original Stress both and installed core consumers. [Receipt](results/2026-11-08-record-field-resolution/delivery-ci.json). Later ca835/R121 source has separate status; no new timing, replay or publication.


## Execution-owned function lookup facts across calls (2026-11-08)

[R121](results/2026-11-08-function-lookup-lifetimes/README.md), retainedc617.
B4798jobs224.145s combined19b8 reference: report8.35%candidate spread and possible
asyncfloat+1–2%debt preserved. B480four-job194.529s confirmation isolates9ae->c617:
report−6.09–6.66%, primary validatedCSV−3.30–5.45%/supportingCPU, independently
CPython-checked inline±0.60%, asyncfloat variable−1.36..+1.08%. No netting pairs or
universal gain. Initialc54 frame inflation+8B/custom+0.871%allocation corrected by
one existing storage slot, widths72/120B restored; all initial/adverse/source/core
logs preserved. Warm/compile allocation neutral.83internal/2133public+15provider
callback checks peractual11/10 pass; full retained delivery pending. Existing
function execution lifetime owns facts, conservative retention/earlier denial
explicit; no new owner/ledger/framework/options, native guards/canonical behavior
preserved. B480offline assertion corrected, actual four-job evidence unchanged.


## Function-lookup-source CPython checkpoint (2026-11-08)

[R122/B481](results/2026-11-08-function-cpython-checkpoint/README.md), runtime6f76/
corec617, CPython3.13.16, 15jobs/311.685s/allgates/archive/local verified.
Compile-to-result validatedCSV1.17x, CSV1.01–1.03xvariable, text1.52–1.53x,
JSON0.91–0.96xvariable, report1.32–1.33x. Compile/warm/CPU/control separate;
not source-pair causal gains or repayment of earlier debts. Prior guide and
historical726cases retained. Optional dedup EPERM preserved, independent launch
space preflight passed; fresh idle audit17aliases unchanged/no temporarylinks.
Test-only fixture fixd97 leaves measured runtime unchanged; combined full delivery
pending. No new framework, unchanged producer qualification or publication.


## Complete helper/inline call attribution (2026-11-08)

[R123/B482](results/2026-11-08-helper-call-attribution/README.md),6f76/corec617,
two existing six-second CPU captures/helper1208samples6.08CPU57unknown4.72%,
inline1174/5.90/69unknown5.88%. Helper binding/workspace4.97%leaf, preparation4.14%,
tuple0.99%, context0; inclusive frame42% contains useful prefix work. Exacttuple
unpack and bound locals already borrow existing funded storage, CSV adoptsarrays.
No prototype or universal call-tax claim. Next source-gated ordinary fixed-shape
binding consolidation, not carrier/arena/evaluator/owner expansion. Originalspace
and symlink preflight refusals preserved; permitted-canonical5537792B exact-byte
recovery/post-audit17aliases pass. Owner exited1 aftercleanup for unsupported
two-map archive assumption; corrected metadata underfreshidlelease/no rerun,
samecaptures archived locallyverified. Full source qualification independent;
performance objective and regex active, no publication.


Combined R120/R121 runtime6f76/corec617 with typed fixture fixd97 is fully qualified:
cached CI42527042378success observed22:19:56UTC, Windows/Ubuntu actual11/10 full
Release, original stress both and installed-core consumers. [Receipt](results/2026-11-08-function-lookup-lifetimes/delivery-ci.json).
No old terminal query, retiming or producer qualification replay. Current R122 guide
updated from pending to named-source-qualified; later R124 working prototype remains
independent and unqualified. Publication postponed; performance objective active.


R123 compile-only inventory correction: original census used initialC54 path;
actual VM profile core gates verifiedC617. R124 frozen-reference hash preflight
rejected that old path before any comparison. Corrected current census C617 has
identical instructions/operations/cache/local/native properties and72/120Bframes,
not80/128B. [Correction](results/2026-11-08-helper-call-attribution/census-correction.json).
Original inventory preserved; no CPU/elapsed changes or rerun.


## Compile-owned named binding prototype held (2026-11-08)

[R124](results/2026-11-08-prepared-function-binding/README.md), qualifiedC617/
prototypeC4bb. Compiler-owned immutable-layout slots/shared default completion,
no runtime cache/owner/engine/options.81internal/41public peractual11/10 plus262
additional public11 pass. Warmallocation neutral, compilerallocation+0.199–0.254%.
B483fourjobs114.346s and B484two-job confirmation109.217s/allgates/archive/localpass.
Primary CSV candidate spread4.33%, no stable whole-script CPU gain; async text
confirmation improves but original desktop+6.4%/quiet adverse survives, reportCPU
0..+4%possible debt. Hold narrow form rather than promote extra compiler machinery
without established benefit; not fixed-threshold rejection, semantic failure or
permanent closure. Full source/core/raw/conditions preserved. Restore qualified
production and retain useful observable lifetime/semantic tests, continue broader
call/frame/frontend roadmap and independent regex. No third unchanged timing round
or publication; future source-specific evidence can justify revisiting the design.


R124 production restoration matches qualifiedd97; forced nonincremental builds and
5internal/6public targeted checks pass eachactual11/10. Nine independent observable
semantic/lifetime cases retained, two baseline controls; no runtime code change.
[Validation](results/2026-11-08-prepared-function-binding/restored-validation.json).
ScopeDirectiveFacts/LexicalScopePreparation already share prepared lexical facts;
apparent repeatedForFunction calls do not establish duplicate walks. Source gate
recorded in active plan; no new inventory/capture/prototype from that hypothesis.


## Text-read lifetime correctness (2026-11-09 Paris)

[R125/B485](results/2026-11-09-text-read-lifetimes/README.md), qualifiedC617 versus
final004e. Both reference regressions demonstrate premature342B refund while72B
raw input is borrowed during output admission. One native-read lifetime defers
raw/decoded/file retirement until actual copy/decoder/append/suspension users and
awaited cleanup finish. Reentry explicit, no next refill after retirement; first
refusal/cancellation wins over cleanup failure. No public policy/owner/ledger.
228internal/140public focused Release eachactual11/10,16new lifetime cases pass.
Five complete scripts/both APIs: allocation neutral within0.07%; original desktop
async text+5.73–7.98% and refined sync text+12.83–13.47%/report+24.60–40.91% preserved.
Source removed duplicate scanner poll after entry check. Controlled three-job
quiet confirmation86.526s/allgates: sync text−1.19..+2.62%variable, async text
−2.78..−0.22%, report−1.01..+0.89%; coarse CPU mixed, no gain claim. Original
adverse async result not reproduced there; not proof of universal neutrality.
Two space preflights refused before work; verified existing catalog reuse and
remote-only redundant inventory trimming preserve original local proof and floors.
Archive under lease verified locally, all users joined/RAM removed/terminal cached.
Full coherent delivery pending; roadmap and regex independent, publication postponed.


## Dispatch CLR-liveness policy removal (2026-11-09 Paris)

[R126](results/2026-11-09-dispatch-liveness/README.md), committedR125/core004e
versusworking85e: five forcedNoInlining attributes/comments/imports removed,17lines,
same canonical algorithms/operand owners/retirement. Thread/stack/empty-recursion
boundaries remain.189internal/229public +21internalStack/60publicStack eachactual
11/10 pass, matching probes built; no test/assertion/timeout changes. Allocation
neutral within0.064%, full goldens/balances/isolation/cleanup pass. Desktop sync
text−5.51..−6.04%/report−7.93..−8.96%, but JSON+5.69..+8.16%adverse survives;
async controls drift17.54%. No CPU-gain or universal-neutrality claim. Retain
development simplification with explicit JSON debt; one bounded quiet confirmation
of text/report/adverseJSON next, no unchanged desktop replay. Full coherent
platform/stress/package qualification pending; R125CI3109 is separate. Existing
linear runner/shared layouts/inline presence/borrowed locals/tuple/adopted CSV
correct source assumptions; no imagined duplicate phase or new VM. Publication
postponed; complete-script objective remains open.


R126 bounded quiet confirmation B486:3jobs87.001s/allgates/archive/localpass, no
users/RAM remain, terminal cached. Text elapsed−7.23..−2.16%/CPU−8.54..−1.25%,
but reference spread5.27% prevents a precise gain; report elapsed−4.15..+1.32%/
CPU0..+3.99%possible debt with reference4.50%; JSON−2.07..+0.94%, adverse desktop
elapsed not reproduced/coarseCPU mixed. Retain policy deletion for simplification
and focused correctness, no universal gain/neutrality claim or further unchanged
retiming. Offline25019methods/24797ILbodies/signatures/locals/exception metadata
identical, onlyfiveNoInlining bits removed; not JIT identity or full qualification.
Fresh idle exact-byte proof/persisted plan/two singleton compiler-assembly extents
linked to permitted canonical,14016512B recovered; all historical paths/bytes
preserved, no floor reduction/deletion. [Raw/proofs](results/2026-11-09-dispatch-liveness/README.md).
Full coherent delivery pending separately; publication postponed.


R125/3109 full delivery complete:cached CI42536319061success observed Nov9
00:08:01UTC, Windows/Ubuntu actual11/10 fullRelease/originalStressboth/installedcore.
[Receipt](results/2026-11-09-text-read-lifetimes/delivery-ci.json). Never requery
terminal run. LaterR126/9f2e remains separate, focused/quiet checked and full delivery
pending; no retrospective qualification or publication claim.


R126/9f2e source (core85e) full delivery complete:cached CI42538659198success
observed Nov9 00:35:33UTC, Windows/Ubuntu actual11/10 fullRelease/originalStressboth/
installedcore. [Receipt](results/2026-11-09-dispatch-liveness/delivery-ci.json).
Terminal cached, never requery. Later R127 declaration-layout prototype remains
independent; performance uncertainty/debts survive. Publication postponed.


## Shared function-declaration metadata (2026-11-09 Paris)

[R127/B487](results/2026-11-09-declaration-metadata/README.md), qualified9f2e/core85e
versus prototypeac6b. One declaration-owned flat signature replaces redundant role
arrays/lowered-parameter retention, per-shape keyword maps, repeated inferred-call
generator scans and synthetic lowered parameters used only for lexical names.
Captured flow states, mutable defaults/annotations, general binding and existing
admission/cleanup remain distinct. No registry, guard, owner, carrier or public API.
41internal/360public plus23internal/149public focused cases peractual11/10 pass.
Compilation allocation−0.10..−0.32%, warm allocation neutral within0.066%; no admitted
allowance reduction. Original desktop sync CSV+13..15%/text+21..22% preserved.
B487 six quiet jobs168.433s/allgates/archive/localpass: validated CSV+0.36..+1.06%,
CSVsummary−0.25..+1.61%, text−3.50..−0.91%, JSON−1.79..+1.43%, report−3.02..−0.05%,
async text−3.22..−1.00%. Coarse CPU mixed except supporting async text−2.70%.
Large desktop adverse timings do not reproduce; possible small CSV debt remains.
Retain coherent metadata simplification, no universal speedup/neutrality claim or
unchanged retiming selected. Full candidate delivery pending; broader objective open.
Space plan preceded exact-byte links for eight singleton compiler-library aliases,
56,131,584B recovered with historical paths/bytes/metadata preserved. Original
Windows argument-length launch failure and corrected stdin transport retained.
All users joined/RAM removed/archive verified locally; terminal cached. Publication postponed.


## Current compilation phase attribution (2026-11-09 Paris)

[R128/B488](results/2026-11-09-compile-phase/README.md), committed847/coreac6b.
Two existing compile-only6s samples,1172/1199active-worker samples: config/helper
parsing43.09/25.94%, abstract19.11/29.19%, executable18.52/18.77%, lexical7.25/8.42%,
lowering2.30/3.59%. Unknown leaf13.74/15.93%, frame/inlining limits explicit; shares
are useful work too, not removable taxes or elapsed improvements. No dominant
single duplicate phase supports a general rewrite. Allsource/quiet/golden/Closed/
zero-budget/join/RAM/archive/local gates pass; terminal cached, no production change.
Next selectedR129 consolidates actual string-literal staging within parsing:
source spans/canonical body decoder/lazy concatenation, not a new AST/value/backend.
Original raw/maps and phase classifier preserved. Publication postponed.


## Source-to-literal construction consolidation (2026-11-09 Paris)

[R129/B489](results/2026-11-09-literal-construction/README.md), committed847/coreac6b
versus prototype1be2. Borrowed token spans, canonical literal/body decoder, direct
unescaped materialization, lazy adjacent concatenation/formatted parts. Removes
ordinary token copy/two-builder staging and synthetic f-string triple delimiters;
AST/static strings and runtime UTF8 storage/owners unchanged.42internal/529public
focused peractual11/10 pass; no tests/assertions changed. Compilation allocation
−2.94..−9.89% (config−11408B), warm neutralwithin0.023%; admission estimate unchanged.
Desktop textcompile+13.35..+15.03%/JSON+3.13..+6.93%adverse and warm/control drift
preserved. B489 six quiet jobs167.303s/allgates/archive/localpass: compileconfig
−4.79..−3.66%/CPU−5.54..−5.00%, text−1.37..+0.03%, JSON−3.03..−0.15%, report
−4.24..−2.60%; completeconfig−3.35..−1.08%, supportingCPU. Warmtext+0.36..+1.01%
possible debt, no warm gain/universalneutrality claim. Original large adverse
compile timings do not reproduce. Retain complete staging removal; no unchanged
retiming selected. Allusersjoined/RAMremoved/persistentarchive locallyverified,
terminalcached. Full candidate delivery pending; publication postponed.


## Targeted agent-workload CPython refresh (2026-11-09 Paris)

[R130/B490](results/2026-11-09-agent-workloads/README.md), committed459/core1be2,
CPython3.13.16/.NET11RC3, same quietF4as_v7. Ninejobs193.226s/allsource/quiet/golden/
ownership/Closed/join/RAM/archive/localgates pass. Existing scripts/fixtures/goldens/
worker/protocol, three roles,5swarmup/7rotated150msrounds. No new framework or main
five-case replay. Compile-to-result config1.09–1.10xVariable (IQR3.95%), search
1.40–1.41x, two-small-file manifest1.47–1.48x. Separate warm ratios1.26–1.27x,
1.69–1.71x,1.56–1.59x; compile1.08–1.09x,.96–.97x,1.25–1.27x. Guide labels every
row6f76/4598; old historical baseline retained, no attribution/additive gain claim.
Full459 platform/stress/package qualification pending independently. Search's warm
gap despite competitive compilation justifies bounded current runtime/host-I/O
attribution next; ratio alone is not an interpreter tax. Publication postponed.


R127/847/coreac6b full delivery complete:cached CI42543394041success observed
Nov9 01:37:39UTC, Windows/Ubuntu full actual11/10, originalstressboth/installedcore.
[Receipt](results/2026-11-09-declaration-metadata/delivery-ci.json). Never requery.
LaterR129/459/core1be2 remains independent; measured uncertainty/debts survive.


## Current config/search runtime attribution (2026-11-09 Paris)

[R131/B491](results/2026-11-09-agent-runtime/README.md), source459/core1be2 afterB490.
Two6s whole-batch samples, not elapsed evidence. Actual guestconfig549/search875
samples, caller561/300, tiering23/55; raw all-thread CPU retained. Initial active
>.25s filter included searchTieredJIT; corrected offline using actual thread names,
original classifier/raw unchanged, no capture rerun. Sampling includes untimed
fixture reset/protocol and timed validation, separated from guest work. Guestconfig
host281/prefix129/builtininit13 samples; no dominant builtin redesign. Guestsearch
prefix326/nativecallprep109/eval104/guard74/host121/admission63; inner observed phase
shares include useful work and inlining, not removable taxes. Native guard/eval
leaf136/875=15.54%, phase178/875=20.34%. Source: consumed bound-local/constant leaves
remain full native Read nodes, visited recursively in proof and evaluation despite
ordinary engine's direct operand convention. R132 separately selects coherent native
input transport consolidation, retaining guard-before-effects/mutable reads/root
spans/cancellation and existing managed depth/owners. Allsource/quiet/golden/Closed/
zero-budget/join/RAM/archive/local gates pass; terminalcached. No new elapsed claim,
builtin global sharing, backend, arena, owner or general profiling campaign.


R129/459/core1be2 full delivery complete:cachedCI42545629905success,
Windows/Ubuntu fullactual11/10/originalstressboth/installedcore, terminal observation
in [receipt](results/2026-11-09-literal-construction/delivery-ci.json). Never requery.
Source-qualified R130 measurements name459; laterR132 is a separate prototype.


## Native helper direct inputs and fixed local prefix (2026-11-09 Paris)

[R132](results/2026-11-09-native-inputs/README.md), qualified459/core1be2 versus
final95fc. Native helper inputs reuse ordinary direct constant/local domains;
consumed leaves no longer allocate/recursively dispatch full Read nodes. Real roots/
mutable names keep metadata; old NativeValueIndex/NativeArgumentIndex plumbing removed.
Guard-before-effects, field re-reads, operation/member/root/return cancellation,
managed depth and borrowed funding remain. Fixed compiler local prefix published once.
No new frame, array, owner, cache, engine, option or syntax restriction. Each of4f7/
3afe/95fc passes87internal/394public peractual11/10 after source changes; no test changes.
Census wanted3→1/words2→1/charge9→4 nodes, ordinary instruction metadata/prefix counts
identical. Final compileallocation−.018..−1.18%affected, report+.014%small metadata;
warmwithin.038%. Original desktop text+57%/search+34%/control drift43.63% preserved.
B492 sixjobs169.786s: mixed/search−2.05..+1.43%/textCPU+1.25..5.33%/reportelapsed
+1.85..4.64%. Source refinement removes repeated interface Count for immutable prefix.
B493 sixjobs169.776s/allgates/archive/localpass: syncsearch−3.40..−2.45%, close controls;
text−.05..+.98%/CPU0..+5.35%, report−2.86..+.95%candidateSpread3.65%, numeric
−3.47..+1.32%spread3.64%, validatedCSV−5.65..+.69%reference/candidate~3%, asyncsearch
−4.96..+.87%candidateSpread5.53%. CPU includes wider reset/protocol boundary, coarse
ticks; no generalCPUgain/universalneutrality claim. Retain coherent representation
and bounded syncsearch elapsed benefit; explicit textCPU/debt/control uncertainty.
No further unchanged retiming selected, not permanent closure. Both archives verified,
all users joined/RAM removed/terminalcached; qualified catalog reused by owned SHA link,
no disk recovery/floor relaxation. Full candidate delivery pending; objective open.


## Canonical static call syntax (2026-11-09 Paris)

[R133/B494](results/2026-11-09-static-call-shape/README.md), c343/core95fc versus36fd.
One syntax-only ordinary-call partition replaces repeated static positional/keyword
and parallel inferred-value construction. No inferred binding-state cache; expanded
arguments remain current.47internal/206public peractual11/10 pass,3new state/expansion
regressions. Compileallocation−2.59..−4.14%, warmwithin.081%. Desktop configcompile
+15.74..+17.70% and large controls/adverseasyncJSON retained. Seven quiet jobs196.114s,
normalGC/tiering/fourprocesscontrols/allgates/archive/localpass: compiler elapsedCSV
−2.33..+.25%,text−1.41..+.32%,JSON−3.41..−.59%,report−.01..+.62%,config−2.09..+.04%.
Completeconfig−1.17..+2.33%with3.34%candidate spread; widerprocessCPU+3.17..+5.14%possible
debt, warmcontrol mixed. Retain construction/allocation removal; no generalCPUclaim
or unchanged retiming selected. Original evidence/space proofs preserved, allusers
joined/RAMremoved/terminalcached. Candidate full qualification batches later;
publication postponed and performance objective remains open.


R132/c343/core95fc full qualification complete: cachedCI42552506089success, observed
2026-11-09T03:17:40UTC. Windows/Ubuntu full actual.NET11/10, originalstressboth and
installed-core consumers pass; terminalcached, never query again. Later9db/R133
qualifies independently.
[Receipt](results/2026-11-09-native-inputs/delivery-ci.json).


## Current scalar/storage allocation attribution (2026-11-09 Paris)

[R134](results/2026-11-09-runtime-allocation/README.md),9db/core36fd. ExistingD36
allocation-tick collector unchanged, original complete text/validatedCSV,256warmups/
512capture jobs per script,120s ownership, normalGC/tiering/Windows11RC3.7170/18178
ticks/zero lost/allgoldens/Closed/joins/fixtures pass. Threshold-type associations,
not exact typebytes or CPU: textPyString35.13/BigInteger24.63%; CSVPyString26.56/
BigInteger23.66/Boolean9.05%. Boolean samples24B on the thread also allocating guest
PyString; all threads/raw/timestamp limits retained. No forcedGC/rundown/heapdump.
R135 selects native Boolean result construction before universal integer/value/VM
redesign; arbitrary rich protocol return objects and ownership/cancellation remain.
No production or elapsed/CPU claim for attribution.


## Native Boolean result storage and identity (2026-11-09 Paris)

[R135/B495](results/2026-11-09-native-booleans/README.md),9db/core36fd versus76d.
Typed RuntimeValue/two immutable CLRboolboxes replace66sourceconversions at core
comparison/truth/bitwise/predicate exits; validated567trees, emitted65fewerboxsites
(475→410 incl two one-timeboxes). Raw rich protocol returns remain. Canonical bool
identity keys fix is/id inconsistency across literals/host/module boxes; independent
CPython golden and original red tests retained.97internal/385public eachactual11/10,
new2APIcases, existingassertions unchanged. ActualwarmallocationCSV−9.09%,search
−11.54..−11.66%, controlswithin.022%, compileallocationneutral. Desktop report
+8..12.6/search+6..13.5/asynctext+14..33.7%adverse/controldrift preserved. Sixquiet
jobs169.328s/allgates/archive/localpass: CSV−.40..+1.79/search−1.79..+2.01/text
−2.23..+2.55/JSON−4.03..+1.65/report+.11..+2.44/asyncCSV−3.03..+.57%elapsed.
No generalCPUgain/neutrality; reportpossibledebt and variablecontrols remain.
Retain construction/allocation/correctness; no unchanged retiming selected, not
permanentclosure. No space recovery/floorrelaxation; allusersjoined/RAMremoved/
terminalcached. Six-case local catalog initially refused before users; condition
preserved, corrected to unchanged five-case driver. Candidate full qualification
batches now; publication postponed, performance objective open.


## Current three-script CPython delivery checkpoint (2026-11-09 Paris)

[R136/B496](results/2026-11-09-cpython-checkpoint/README.md),6d73/freshea65, CPython3.13.16.
Ninejobs188.968s/normalGCtiering/threeprocessroles/allidentity/quiet/golden/budget/
Closed/join/RAM/archive/localgates pass. Current completeCSV1.15–1.16xDiagnostic,
text1.44–1.45xDiagnostic,report1.35–1.38xVariable. Warm1.16–1.18/1.55–1.57/1.38–1.40x;
compile1.05–1.08/1.05–1.07/1.08xVariable. FreshReleaseworker22331/coreea65 from6d73;
24818runtimeILbodies/signatures/policies exactlymatch focused76d, source/PDBmetadata
differs. Worker source only defaultcatalog additions; external source/fixtures/
host/protocol/boundaries unchanged. Exactspaceproof2duplicates/allpathbytes preserved,
floors unchanged. Guideupdates3cases, prior459config/file and6f76CSV/JSONrowshistorical;
no across-checkpoint sourcegainattribution or regexqualification. FullUbuntu
qualification passes; Windows native-hash pre-cancellation IsCompleted assertion
failed, remaining stages skipped. Cached terminal evidence retained separately;
publication postponed, performance objective remains open.


## Native hash cancellation scheduling (2026-11-09 Paris)

[R137](results/2026-11-09-hash-cancellation/README.md) retains the original Windows
6d73 CI failure before cancellation was requested. The64MiB timed scan now arms
cancellation on its own hashing worker; its parent continuation is outside that
window. Same input/deadline/cache/attribution/refund assertions, actual users joined.
Two concurrent cases move to Stress;14small checks stay ordinary. Removing only
loop checks (entry check retained) fails both native cases; exact source restored,
nonincremental builds16/16pass eachactual11/10. No runtime change or speedup claim.
Full delivery remains incomplete pending the corrected Windows checkpoint.


## Closed nested invocation construction (2026-11-09 Paris)

[R138/B497](results/2026-11-09-closed-nested-helpers/README.md),6d73/coreea65 versus345f.
Remove lexical-parent barriers to existing native/deferred paths; capture facts
and promotion keep Python scoping, no new binder/arena/cache/owner. Complete current
CSV/text/report in ordinary main() wrappers, original JSON/search controls. Actual
warm allocation−38.67/46.5/28.5%bothAPIs, compileallocationneutral. Desktop async
report+2.25..10.25/search+6.8..7.6%adverse preserved. Six quiet jobs169.171s/allgates:
CSV−8.16..8.39/text−19.62..20.04/report−10.50..13.22/asyncReport−6.24..7.68%elapsed,
supporting widerCPU. Report candidatespread2.98%; searchreference2.49% remains
variable, JSONclose. Retain actual phase removal/alloc/affectedscriptbenefit; no
universal gain or CPython ratio.68internal/279public thenfinal36/76eachactual11/10,
independentPython/nestedcapacity/realasync/cancel/refusal/alias/depth/budget checks.
Terminalcached/localSHA/joins/RAMremoved, no floors relaxed. Full delivery batching;
publication postponed and broader performance objective open.


## Unresolved lexical names in native proofs (2026-11-09 Paris)

[R139a](results/2026-11-09-lexical-name-proof/README.md). Full CI42563792033/df3
fails two existing class-scope regressions eachWindows/Ubuntu actual11;4446internal/
16857public pass, later stages skipped. Removing the R138 lexical-parent barrier
exposed incomplete class-method facts: zero capture slots did not prove global
lookup. Native proof now declines LoadName; explicit LoadGlobal stays eligible.
Delete parent-depth/class/nonlocal heuristic; ordinary resolution/promotion retains
semantics. Existing failures reproduce locally35pass/two fail; combined active
R139 prototype+correction101public pass eachactual11/10. Corrected baseline and
candidate qualification/source pair still pending; no performance claim or erased
prior failure. Publication remains postponed.


## Explicit incoming lexical operands (2026-11-09 Paris)

[R139/B498](results/2026-11-09-lexical-operands/README.md),correcteda58/core0216
versusa063, native global proof fix on both sides. Work-state cell reader replaces
frame-only read/transfer; no incoming-cell eager context. Writes/missing fallback/
local-cell construction/special scopes keep contexts.606internal/323public each
actual11/10 after exact source restoration/nonincremental builds; baseline37class/
global regressions pass. WarmallocationCSV−38.67%,report−26.1%,text unchanged (explicit
split still promotes), compile0%. Original4e94asyncCSV+60..64/report+11.7..17.7%desktop
and corrected CSV+12.7..20.1/asyncCSV+12.1..14.4%adverse/control drift preserved.
Seven quiet jobs196.580s/allgates: CSV−7.31..7.93/asyncCSV−7.50..11.93/report−4.97..9.14/
asyncReport−5.67..7.65%elapsed, supportingCPU. Text−3.13..+4.49/search−8.15..+4.29
controls inconclusive; JSONclose. Retain phase removal/allocation/affectedbenefit,
no universal gain. Terminalcached/localSHA/joins/RAMremoved. Initial setup refuses
space before workers; fresh exact2+1duplicate proofs preserve everypath/byte/
metadata/alias, floors unchanged. CSV field storage already admitted once; no new
scope layer or automatic bulk-copy revival. Full corrected delivery batches;
publication postponed, broader performance objective remains open.


## Current original-text CPU attribution (2026-11-09 Paris)

[R140/B499](results/2026-11-09-text-runtime-attribution/README.md),23e-equivalent
frozena063, sourcehashproof/actualassemblymetadataa58 explicit. One unchanged text
script,6swarmup/6.153scapture/normalGCtiering/perf maps, namedguest6.06CPUseconds.
1205guest/44caller/26tiering samples,59guestunknown4.90%. Correct named-thread
classifier beforecapture; allraw/names/maps/CPU survive. Innermost observed shares:
VMprefix39.92/text22.07/dict10.04/admission6.64/scalars6.39/nativecall5.89%; guard+
eval2.16%, not dominant. Prefix246leaves20.41% includes inlined useful work; not
all removable dispatch. Integer boxing allocation does not prove CPUdominance.
Source gates confirm native hashes already stay native and CSV field storage
already admitted once. No speculative JIT/value rewrite or second capture selected.
Allidentity/golden/quiet/Closed/joins/RAM/archiveunderlease/localSHA pass; terminal
cached, no users/fixtures, fresh exactduplicate preserves everypath/byte/metadata,
floors unchanged. No elapsed/sourcepair/CPython or embedding-tail claim.


## Native hash stress scheduling window (2026-11-09 Paris)

[R141](results/2026-11-09-hash-stress-scheduling/README.md). CI42567067318/23e
fullWindows/Ubuntu actual11/10/package passes; Ubuntu Stressbothpasses. Windows
.NET11publicStress406passes, internal5pass/one hashfail, .NET10Stress skipped.
Timer fixture misses window; local.NET10 diagnostic completionToken=false even
with warmedtimer, original failure retained. Replace timer/pool with owned ready
canceller +256MiB native input/same32MiBheadroom; same10sdeadline/cache/attribution/
input/depth/refund assertions, no retries/ignored windows. Both users joined.
16native+6internalStress pass eachactual11/10. Remove only loopchecks, entryretained:
both nativecases fail eachframework, completionToken=true. Exactsource restored,
nonincrementalbuilds/finalchecks pass. No production/policy/API/performance change.
Corrected CI42573178034/source7aa637bf is cached terminal success: Windows/Ubuntu
actual11/10, installed core consumers and original Stress both. Original failures
remain; no production speedup or repeated unchanged qualification. Publication postponed.

## Shared generator compilation and retained storage (2026-11-09 Paris)

[R142/B500](results/2026-11-09-generator-storage/README.md): committed7aa/cored1c9
versus prototype2ce8. Reuse enclosing lexical compilation and executable work-state
construction. Delete eager isolated generator-definition code/tuple, blanket complete-
scope mirroring, argument namespace round trip and duplicate initializers; class and
generator-lambda bridges remain where needed. Capture delete-only nonlocals and
synchronize actual cells/live slots; keep suspended class namespaces. Final source
restores compile phase checks and removes the initializer with only fixture callers.
184internal/628public plus37compiler diagnostics per actual11/10; eight independent
Python goldens, real pending cleanup/refusal/cancellation and shared ownership retained.

Text warm allocation−15.1% both APIs; compile allocation+1.3–1.6%. Four quiet jobs/
114.323s: CSVelapsed−5.82–10.95%, text−11.60–12.08%, JSON−6.17–6.28%, supporting CPU.
CSV references differ5.24%; report control+1.17–4.54%elapsed/+0.39–4.44%CPU remains
unresolved debt. Original desktop JSON+6.39–8.03% and drifting controls remain. Retain
coherent deletion/affected benefit; no universal CPU/neutrality or final-binary timing
claim. All quiet/identity/output/funding/join/RAM/archive/localSHA gates pass. No space
recovery/floor change; terminal cached, no users. Full delivery batches; broader work open.

R142 delivery42577712653/source081052fa: both OS .NET11 public16,875pass; internal
4,448pass/four obsolete mirroring assertions fail. Later10/package/Stress skipped.
Test-only27353cfb removes that expectation and unused flags, keeping every source,
operand/mutation/alias/admission/cleanup assertion. Four failures reproduced;31
focused pass peractual11/10. Originalfailure/logs preserved; corrected delivery batches.

Corrected delivery42579357509/source27353cfb is cached terminal success on Windows/
Ubuntu, actual11/10, original Stress both and installed Release core consumers.
Production remains081052fa; R142 is fully qualified, without repeated timing.

## Current file-search call and frame attribution (2026-11-09 Paris)

[R143/B501](results/2026-11-09-search-runtime-attribution/README.md), fresh081052fa/
core3f8d, unchanged maintained search script and goldens. One6s wall capture after
6s execution warmup,4.842s recorded execution, normalGC/tiering and namedguest4.29CPU s.
843guest/328caller/53tiering samples,4.86%guest unknown. Innermost observed prefix31.67%,
host15.18%,native call9.49%,admission9.02%,guard7.83%,evaluation6.88%,scalar5.22%,context/
builtin initialization0.36%each. Caller reset230/host69/protocol27 separated. Useful
inlined/affix work remains inside these shares; no removable-cost/elapsed/CPython claim.
Source disproves new frame/argument/iterator construction phase for this helper.
Direct final UTF8 casing/one Unicode pass/fused final words/commit without resize
also already delivered. Source gate/capture complete; next bounded design gate is
shared helper proof/execution, preserving guards before effects and avoiding narrow
affix intrinsics, another guard VM/cache/arena or second capture from these counts.
Allquiet/identity/golden/Closed/join/maps/RAM/archive/localSHA gates pass; no space
recovery or floor change. Terminal cached/no users. Broader performance remains open.

## One executable callable engine (2026-11-09 Paris)

[R145/B502](results/2026-11-09-callable-consolidation/README.md), committed65ed6cf7;
measured081052fa/core3f8d versus source-identical delivery freezecce0. Existing
enclosing capture decisions prepare executable lambdas; namespace-backed and lazy
type consumers compile once before execution. Remove PyFunction, its mode and
177-line duplicate dispatcher, runtime lambda lowering/compilation and unused bodies.
Original prototype hash recursion, class-cell and lazy-preparation failures survive
with fixes. Nine independent Python goldens;357internal/1722public plus46compiler/
lambda checks eachactual11/10. Full delivery42586293361 runs independently.

Complete affected class-callable CSV/text/JSON: allocation−23–26%/−74–76%/−56–57%
across both APIs. Five quiet jobs/142.598s: elapsed−9.2–11.9%/−37.5–39.3%/−12.5–14.7%,
supporting CPU. Same source/input/golden per pair, not CPython or shape ratios.
Compile allocation+3.2–5.2% accepted as preparing formerly runtime-only code;
report control+0.05–1.12%elapsed/0–4.44%coarseCPU remains uncertain. Earlier R142
debt stays; desktop drift/adversity retained. All identity/output/quiet/join/RAM/
archive/localSHA gates pass. No floor change/space recovery; terminal cached/no users.
No universal/startup/pending-I/O claim. Broader performance roadmap remains active.

First R145 delivery42586293361 is cached terminal failure on both OS:109 internal
raw-module setup cases and one public lazy-alias/super classification regression.
Keep real annotation scopes separate from module reads; migrate three fixture
families to executable entry with an explicit live namespace where needed. Preserve
all semantic/refusal/ownership/cleanup assertions. Five additional Python goldens;
200internal/752public focused checks eachactual11/10. Later delivery gates were
skipped; corrected delivery batches. Original measured65ed source remains labelled,
with no timing/neutrality claim for the correction or repeated unchanged campaign.

## Current representative CPython checkpoint (2026-11-09 Paris)

[R146/B503](results/2026-11-09-cpython-callable-checkpoint/README.md), fresh committed
65ed6cf7/core082a, unchanged R136 worker/adapter/catalog/protocol. Twelve jobs/250.847s:
main CSV/text/JSON-report/search complete ratios1.13–1.17×/1.49–1.51×/1.35×/1.34–1.35×;
warm1.15–1.17×/1.56–1.60×/1.37–1.38×/1.61–1.63×. Variable controls stay explicit.
Compile roughly1.05–1.09×, search0.95×; no individual optimization attribution.
Search input2049physical lines, exits1025; fix label without changing bytes/work.
All quiet/output/ownership/join/RAM/archive/localSHA gates pass. Initial staging
catalog-link and continuation-script errors precede timing, with cleanup/original
logs preserved; success reverified exact identity and idle lease. No floor change/
space recovery; terminal cached/no users. Full65ed CI found the separately corrected
lazy-scope defect; no relabelling as58d3 or final-binary timing claim. Older guide/
all displaced rows remain. Broader performance and independent regex work continue.

## Current general operand transport gate (2026-11-09 Paris)

[R147](results/2026-11-09-operand-transport-assessment/README.md), compile-only census
of five maintained scripts and corrected58d3 source. All listed modules/helpers
nonmirrored. Input forwarding and general producer/local-result forwarding already
remove ordinary temporary publication/store dispatch. Retained store instructions
carry pending cleanup continuation, original terminal check/span/publication order.
No duplicate phase selected; no extra capture/timing/dispatch/arena. Static counts
are not CPU shares. Direct finalUTF8/bulkASCII/fusedsplit/coarsecounter admission
also already exist. Gate complete, no permanent design ban; broader roadmap open.

Corrected R145 delivery42588611410/source58d3ec0f is cached terminal success:
Windows/Ubuntu actual11/10, original Stress both and installed Release core consumers.
Qualification complete; preserve the first failure/correction and original65ed
timing sources. No unchanged retiming. Publication postponed; broader roadmap active.

## Filename-filter asynchronous iteration (2026-11-09 Paris)

[R148](results/2026-11-09-wildcard-iteration/README.md), committed46e3746e. Original
pending file-backed generator fails under RunAsync because fnmatch.filter pulls it
synchronously. Use the existing cursor's active API for acquisition, pulls and
awaited disposal; keep native UTF-8 matching. 305 focused public checks eachactual11/10,
four independent Python goldens, including user iteration protocols, gated cleanup,
retained identity, terminal refusal and budget reuse. No speedup claim. Full delivery
batches with the next coherent runtime milestone; R145 qualification remains source
labelled. Publication postponed; broader performance and regex priorities remain open.

## Direct native callback argument transport (2026-11-09 Paris)

[R149/B504](results/2026-11-09-callback-arguments/README.md), fresh46e3746e/coreDEDD85
versus source freeze6417B97A. Existing binders accept short managed spans and own
values before suspension. Remove three thread-local pools/six rent-clear-return
helpers, async forwarders and duplicate binding bodies; ordinary map stops allocating
per-record argument arrays/reservations. Wide/dynamic callables retain owned storage.
No public API/framework/pool. Actual11/10:60internal/1636public each, five new cases,
two callback and five complete-script independent Python goldens. Pending default
lookup/gated cancellation cleanup and existing overlap/refusal/ownership pass.

Same-source map-based CSV/text/JSON warm allocation−6.8/−14.1/−4.7% across both APIs;
compile allocation unchanged. Original desktop controls vary up to16.5%, elapsed
inconclusive. Five quiet jobs142.204s/four roles: CSVelapsed−2.8–3.2% (coarse CPU
mixed), text−3.0–6.6% (supporting CPU), JSON−1.5–+2.7% mixed. Reportcontrol−0.6–3.9%,
search−2.6–+2.9%; attribution/general CPU uncertainty remains. Retain deletion and
allocation benefits without a fixed-percentage veto or another unchanged campaign.
All quiet/identity/output/join/RAM/archive/localSHA gates pass; no space/floor change,
terminal cached/no users. Original pre-timing invalid CSV transform retained. Full
delivery batches with R148; broader performance and independent regex work remain open.

## Maintained JSON-report CPU attribution (2026-11-09 Paris)

[R150/B505](results/2026-11-09-json-runtime-attribution/README.md), committedafb11d97/
core6417B97A verified unchanged from the fresh R149 freeze. Original maintained JSON
script/input/goldens, common worker/adapter. One6s capture/6.083s recorded execution,
6.03guestCPU s,1201guest/29caller/17tiering samples,5.66%unknown guest leaves. Observed
innermost prefix40.47%,JSON19.90%,dictionary7.83%,admission6.91%,nativecall6.74%,text6.41%,
managed cancellation/stack4.33%. Prefix includes useful inline work and parent
assignment for unknown/truncated leaves; no removable-cost/elapsed/CPython claim.

Instructions already compact/immutable/read by readonly reference. Plain JSON strings
already skip backend reparsing/scratch; record keys/completed-value loops share work;
default JSON calls skip general binding. Early binding facts must not be cached as
final class-cell facts. No new phase deletion selected from inclusive counts; no
second capture/new dispatcher/arena/cache or permanent design veto. Original offline
classifier omitted JSON method names; preserve it and corrected owner classification
of the same raw stacks. All quiet/identity/golden/Closed/join/maps/RAM/archive/localSHA
gates pass, no space/floor change, terminal cached/no users. Broader roadmap remains.

R148/R149 delivery42598266368/sourceafb11d97 is cached terminal success: Windows
and Ubuntu actual.NET11/10, original Stress both and installed Release core consumers.
Qualification complete; retain measured source pairs and JSON/control uncertainty.
No unchanged retiming. Publication postponed; broader performance roadmap continues.

## One borrowed callable entry (2026-11-09 Paris)

[R151/B506](results/2026-11-09-borrowed-call-entry/README.md), qualifiedafb11d97/core6417
versus sourcefreeze21CF1118. Extend existing ICallable with consume-before-suspension
borrows; invocation-private fallback arrays clear after actual completion. Constructors/
wide calls retain owned storage. Remove three executable packet pools/lifecycles,
two callback type switches, completed general/JSON async forwarding and six pool-only
fixture regions; retain semantic/lifetime assertions and private builtin hierarchy.
No new interface/framework/public option/dependency. Five packet-release failures
uncovered/fixed in code; original assertions unchanged. 706internal/1614public each
actual11/10, five independent whole-script Python goldens, all ownership/cleanup pass.

Five quiet jobs141.919s: JSONsync−1.0–1.8%elapsed, async−5.7–10.6% with supporting
CPU. Ordinary allocation/compile unchanged. Constructor control allocation+2.5%,
elapsed−0.9–+0.7%; accept owned-array cost without another pool/redesign. Text/map
controls mixed, current process spreads5.0/6.8%; regression uncertainty remains for
next substantial checkpoint, not neutrality. Retain desktop async adversity/controls;
no universal/pending-I/O/CPython claim, unchanged second campaign or percentage veto.

Initial setup rejected headroom before any root/service/fixture/timing. Fresh idle
lease/hash/single-alias/owner/parent checks and persisted pre-mutation plan precede
removing only two redundant remote compressed archives, both byte-identical in Git,
raw trees/receipts retained. Identical staged source/payload/floors on retry; no timed
work restarted. All quiet/identity/output/join/RAM/archive/localSHA gates pass,
terminal cached/no users. Full delivery next; broader performance/regex work active.

R151 delivery42605184549/source99844959 is cached terminal success: Windows/Ubuntu
actual.NET11/10, original Stress both and installed Release core consumers. Original
packet-lifetime failures/fix and all adverse/control uncertainty survive; no unchanged
qualification/retiming. Publication postponed; broader performance roadmap continues.


## R160 — current config CPU attribution and delivery qualification

[One current-source capture](results/2026-11-09-config-runtime-attribution/README.md)
separates545guest samples/2.90CPU seconds from567caller/reset/protocol,38tiering.
Mediated filesystem51.74%, JSON9.91%, builtin initialization2.20%; inclusive prefix
and unknowns remain explicit. No builtin sharing/cache redesign or CPU-gain claim.
Original pre-work identity-shape failure/correction preserved. All ownership/quiet/
join/RAM/archive/localSHA gates pass. R157 now fully qualified by cached green
CI42627090614/source038f7fb4, Windows/Ubuntu actual11/10, original Stress both and
installed consumers; [receipt](results/2026-11-09-managed-whitespace-search/delivery-ci.json).
No unchanged replay. Next distinct source gate is maintained validated CSV.


## R161 — default argparse input correction

New handoff integrated into ignored PLAN and removed. Default/None parse_args and
parse_known_args skip program name, explicit lists retain all elements; current
exposed sys.argv mutations observed without importing another capability. Frontend
now accepts None consistently with runtime. Module-registry lookup uses existing
async protocols under RunAsync. Four old fixtures corrected to the documented
complete host argv contract; direct sys.argv assertions remain unchanged.
201focused public regressions each actual .NET11/10 pass, including existing
cancellation, first-refusal, retained-default/alias and awaited-cleanup scenarios.
Eight new cases cover both APIs; four independent CPython3.13.2 scenarios agree.
The empty-argv control supplies prog explicitly, isolating argument parsing from
CPython's constructor indexing argv[0]. Initial frontend rejection and old fixture
failures preserved privately. No performance claim; full qualification is batched.


## R162 — validated-CSV CPU/source gate

[One usable capture](results/2026-11-09-csv-runtime-attribution/README.md) retains
1,210guest samples, parser16.03%, scalar7.11%, binding4.21%, setup3.97%, actual
array adoption1.74%; prefix includes useful work. Existing slot/array/frame/UTF-8
mechanisms confirmed, no new binder/cache/arena or header-proof deletion. R163
comparison consolidation selected before prototyping; gains unmeasured.
Original oversized warmup failed before sampling, killed/joined worker and temporary
maps/RAM archived under owned cleanup/localSHA. Bounded128/64-request correction
preserves20s guard/6s targets and all gates; no design-failure inference. Unused stale
original catalog label preserved, corrected exact identity independently verified.
R161 ordinary Windows/Ubuntu net11/package CI42634762709 is cached GREEN; focused
actualnet10 also passes. Full manual net10/originalStress remains batched.


## R163 — exact scalar comparison consolidation

[Source pair and original adversity](results/2026-11-09-scalar-comparisons/README.md)
retain the shared exact number/string entry and remove executable string detour.
Native UTF-8 operators now check current token in bounded chunks; custom/raw/async
protocols retained. Final65internal/306public eachactual11/10 pass, including new
active-cancellationStress, first refusal and cleanup; twelve Python string pairs agree.
One quiet4job115.042s: CSVsync elapsed−2.10to3.74% but CPU mixed; asyncCSV elapsed
−4.53to6.08%, coarseCPU−6.90% supports retention. Search control favorable, report
baseline spread5.37% inconclusive; no universal claim. Original monolithic desktop
allocation flat, control spread27.87% and adverse asyncsearch/report retained.
Source refinement before quiet timing is distinct, not an established JIT explanation
or final allocation result. Proven space deficit recovered two exact redundant
remote compressed copies only; canonical/raw/core evidence survives. All quiet/
identity/golden/ownership/join/RAM/archive/localSHA gates pass. Full delivery pending,
publication postponed. R164 addresses separate structural whole-span string kernels.

## R177A — post-frame native entry remains useful

[The deleted shortcut assessment](results/2026-11-10-framed-native-entry/README.md)
was withdrawn after quiet B521: affected complete-handler text CPU +5.08–8.62%
sync/+13.46% async, allocation flat. Original contrary desktop observations and
rejected six-case setup remain visible. 369 internal/96 public cases pass on each
actual target; three Python semantic regressions remain. All three production
files were restored exactly. No unchanged platform/package qualification repeat
or permanent veto on a materially different shared-kernel design follows.

## R178 — consume completed JSON storage

[Terminal JSON transfer](results/2026-11-10-json-terminal-storage/README.md) removes
two final copies using existing ownership. A complete 3,072-record JSONL manifest
allocates 3.04%/3.03% less through sync/async APIs. Adverse async desktop elapsed
remains inconclusive with moving controls; no CPU claim. Public peak checks show
both reduced overlap and increased retained spare capacity. Reused iterator chunks
still copy. 193 internal/861 public cases pass each actual target; full combined
delivery qualification remains pending. Original fixture/setup errors survive.

## R179 — remove the closed-helper handler cliff

[Caller-handler eligibility](results/2026-11-10-handler-native-calls/README.md)
removes needless binding/frame construction from existing closed helper calls.
Affected complete text allocation−47.31% sync/−47.29% async; quiet B522 elapsed/CPU
roughly18–22% lower. Ordinary calls already use this path; no universal gain claim.
Report/Unicode controls are adverse and unresolved, justifying bounded confirmation.
79 focused public cases each actual target and six Python controls pass, including
real pending protocols under handlers/cancellation/joined cleanup. Full combined
delivery qualification remains pending; all adverse/raw evidence is retained.

## R181 — current representative CPython checkpoint

[Fifteen jobs at ee448715-equivalent source](results/2026-11-10-current-cpython-checkpoint/README.md)
complete in409.135s, with independent compile/warm/compile-to-result lanes and all
identity/golden/ownership/quiet/join/archive/SHA gates passing. Warm text1.62–1.69×
and search about1.58× CPython remain material. Variable report/other controls stay
visible; no single-change speedup or statistical significance follows. Exact frozen
source/binaries, boundary, prior guide and original local-analysis correction are
retained. The isolated operand prototype is absent; publication stays postponed.

## R180 — shared instruction-prefix implementation withdrawn

[The isolated prototype](results/2026-11-10-shared-prefix-assessment/README.md)
removes native expression preparation/value graph, argument-source mutation and
recursive evaluation. A shared-record cache admission/reentry defect was reproduced
and corrected before quiet timing. Useful CSV-helper allocation−42.95% contrasts
with quiet CSV elapsed+1.29–2.23%/coarseCPU flat and text elapsed/CPU roughly+38–52%.
All adverse controls/original sources remain. No runtime promotion or CPU claim;
B527 attributes substantial work to both proof and prefix; a bounded fixed-fact/
branch-storage refinement is isolated, with no new CPU claim.
The main runtime stays qualified; independent record semantic cases survive.

[Fixed facts and branch storage](results/2026-11-10-shared-prefix-assessment/fixed-facts/README.md)
pass64internal/135public per actual target but quiet B528 text CPU remains
+29-37%sync/+37-53%async. The current implementation is withdrawn, source delta
and frozen binaries preserved, private worktree retired. CSV allocation-42.95%
does not justify whole-text regression. Main keeps its qualified native shortcut.
This is not completion of the whole-script performance objective; R182 will use
production CSV/search attribution to select a different coherent design.

## R182 — production CSV attribution, search setup gap

[The partial owner](results/2026-11-10-ordinary-runtime-attribution/README.md)
preserves1221guest CSV samples/6.14CPU-s: VM prefix47.01%, parser14.41%,
binding4.50%, activation4.42%, context initialization1sample. Useful work is
included; no removable-cost or speedup estimate. Search warmup hit a fixed
iteration cap before capture. Original/recovery failures and verified cleanup
survive; no repeated samples. Consolidating existing instruction-loop routing
is selected for bounded source implementation; another context-allocation
project is not justified by this evidence. Publication stays postponed.

## R183 — invocation-only routing consolidation

[The isolated implementation](results/2026-11-10-invocation-routing/README.md)
retains direct synchronous completion and native shortcuts while consolidating
existing context operations.1381focused public cases eachactual11/10 pass.
Warm/compile allocation flat; quiet B530 source858c4ac9 supports modest text/search
CPU improvement but Unicode/report controls remain adverse/inconclusive. One
larger-batch confirmation follows. Original desktop adversity/setup mistakes
and all raw data survive. No promotion, universal speedup or final qualification.

[R183 confirmation](results/2026-11-10-invocation-routing/confirmation/README.md)
completes111.356s with all gates; larger-batch Unicode/report/text ranges overlap
zero. Withdraw routing-only code: added dispatch/completion protocol does not
eliminate sufficient actual work.1381public/42internal focused eachtarget pass;
source/core/all adversity remain, no unchanged full qualification repeat. Next
source gate removes actual owned binding work for closed named/default helpers
through the shared binder, retaining native kernels and dynamic async protocols.

## R184 — shared binding destination and closed named/default entry

[The retained source pair](results/2026-11-10-shared-call-binding/README.md)
uses one Span/presence fill for owned binding and small closed native helpers,
removing workspace/activation on eligible named/default calls. Existing positional
borrows, wide signatures and genuinely suspending defaults keep their routes.
Focused Release checks pass 715 public / 82 internal per actual .NET 11/10.
An actual runtime-defined helper runs without additional admission and retains
funded inputs. B532 completes three whole-script jobs in 112.099 seconds, all
identity/output/ownership/quiet/join/unmount/archive/local-SHA checks passing.
Keyword-default search elapsed falls 15.7–21.5%, wider CPU 15.4–18.8%; allocation
is effectively flat. Text/CSV controls remain inconclusive with possible small
debt. Desktop search +5.46–8.31% and compilation uncertainty survive. This is not
a new CPython row or completion of the broader performance objective. Full
retained-source delivery checks follow; publication remains postponed.

## R185 — cleared storage for unbound locals and cells

[The retained migration](results/2026-11-10-unbound-storage/README.md) removes the
UninitializedLocal object, per-call Array.Fill and synthetic empty class-name entry.
Guest None remains PyNone. The first 15 unbound-name failures and read-order
correction are preserved. Corrected focused checks pass 3124 public / 432 internal
per actual target; three independent Python goldens also pass against the prior
runtime. Main promotion passes 123 selected public checks per target after fresh
nonincremental builds. B533 finishes 112.706 seconds with all ownership/output/
quiet/join/archive checks passing: affected CSV CPU −1.22–4.71%, elapsed −0.28–4.91%.
Search/text controls overlap zero; desktop search +9–19% remains unexplained.
Allocation is flat, no general CPU gain/equivalence claim. Full delivery pending.

[Bulk CSV reassessment](results/2026-11-10-record-construction-assessment/README.md)
compares B74's exact patch with current source. Its opportunity was revised and
retained, then extended by row adoption/shared keys. Header revalidation protects
real admission reentry; adding list-version machinery is not justified by this
narrow gate. No repeated prototype, timing or permanent record-design veto.

R185 source3a413c59 is fully qualified: Windows/Ubuntu, actual.NET11/10,
original Stress and installed Release consumers all pass. [Cached GREEN42761050500](results/2026-11-10-unbound-storage/qualification/delivery-ci-status.json).
This closes delivery without replacing any measured source or adverse observation.

## R186 — combined-source CPython checkpoint

[Nine complete jobs](results/2026-11-10-combined-cpython-checkpoint/README.md)
finish247.200seconds, all identity/output/balance/quiet/join/unmount/archive/localSHA
checks pass. Qualified3a413c59-equivalent frozen26d43e66 versusCPython3.13.16;
all564production sources verified with explicit checkout-EOL normalization.
Compile/warm/compile-to-result stay separate. Current warm CSV1.09–1.11×,
text1.64–1.71×, keyword-default search1.51–1.52×. Text/complete-search controls
remain variable. Keyword-default search is explicitly a different script from
the older literal-prefix row. No individual-change gain or equivalence claim.
Unchanged older rows remain historical; publication stays held.

## R187 — immutable dev64 compatibility

[Supplied0.5.0-dev.64](results/2026-11-10-regex-dev64/README.md), package revision
9af7141f, passes86internal/524public focused checks per actual target on current
qualified runtime. Actual loaded DLLs match supplied package bytes; exact dev59
bridge, normal0.4 pin and provider source stay unchanged. The623 documented member
names are unchanged; three producer API gaps remain. This establishes development
semantic/contract compatibility, not final adapter safety/performance qualification.
No sibling edits/builds, producer republishing or speculative pin promotion.

## R194 — continuous instruction run with cold exception routing

[Both isolated variants](results/2026-11-10-continuous-prefix/README.md) are
withdrawn; the qualified runtime remains unchanged. Focused semantics,
cancellation and ownership checks pass on actual .NET 11/10. The jumped
stack-home disappears, but generated code/frame size grows and allocation stays
flat. The four-process quiet confirmation retains affected-script regressions,
unstable identical controls and early tiering activity. It does not establish a
general CPU gain or a permanent veto on dispatch redesigns.

Original/refined source, binaries, JIT output, desktop adversity and raw VM
observations are preserved. Run-only clocks and the reset/validation/disposal
envelope are reported separately; wider process CPU includes shared JIT/GC.
Workers are joined, fixtures removed and archives SHA-verified. The private
worktree was removed after all source deltas were verified against that evidence.

## R195 — report construction and compilation attribution

[Partial attribution](results/2026-11-10-report-construction-attribution/README.md)
preserves a completed report-runtime capture and a compilation capture whose
collector misidentified the active threads. Compilation continued on three
thread-pool threads; offline selection uses their recorded CPU deltas. Original
failure, killed-worker exit and unknown/malformed samples remain visible.
Configuration captures and final qualification gates are missing. No elapsed or
ownership-delivery claim follows. All users are joined and raw evidence survives
the owned RAM cleanup. Source remains the qualified runtime.


## R269–R271 — iterative JSON, shared annotation demand and reader windows

2026-11-12. [R269](results/2026-11-12-json-depth/README.md) removes the obsolete
JSON decoder depth policy; [R270](results/2026-11-12-annotation-demand/README.md)
removes duplicate frontend demand preparation. Both pass full Windows/Ubuntu,
actual.NET11/10, original stress and installed Release consumers. No substantial
whole-script CPU claim follows from their correctness/allocation results.

[R271](results/2026-11-12-reader-windows/README.md) removes non-inline per-line
payload copies by sharing immutable admitted windows, reusing existing string
storage fields. Focused semantics/ownership/cancellation and six complete scripts
pass. Search allocation−18%, text/JSON−5%; controlled search warm elapsed−2..−6%.
Longer confirmation retains a small report elapsed cost(+0.6..+2.8%) explicitly;
JSON/captured-text CPU remains uncertain. All adverse/original failed conditions
survive. Earlier general-window and ordered-prefix designs are not replayed.
Conservative full-window command/result retention can deny earlier. Full delivery
is tracked from the retained-source commit; the broad CPU objective remains open.


## R272 — current captured-text cost and segment-entry withdrawal

[One retained-image CPU assessment](results/2026-11-12-captured-text-costs/README.md)
identifies actual native dictionary-loop, text-preparation and guard/evaluation
work, not a duplicate frame. Two scoped-entry forms each pass742 focused checks
and six complete scripts; allocations stay flat and affected desktop warm results
remain adverse. The cached-values form and lifetime-only refinement are both
[withdrawn with original data](results/2026-11-12-captured-text-costs/segment-entry-withdrawn/README.md).
All568 qualified production files restored/nonincrementally rebuilt;80 restored
checks pass. No quiet CPU or permanent design conclusion follows. No dormant
prototype remains; the whole-script performance objective continues.


## R273 — native semantic operation facts

[Representation deletion](results/2026-11-12-native-operation-facts/README.md)
removes embedded ordinary instructions, physical positions, redundant opcode and
third input from native nodes. Same guard/evaluator; no controller/solver/option.
Final node72→48B, affected script compilation−0.06..−0.18%, warm allocation flat.
1,032focused actual-target checks and six complete scripts pass. First56B form,
all desktop adversity and the staging-preflight correction survive. B627 finishes
four jobs184.637seconds; moving6–8%reference controls leave CPU uncertain. Retained
for the actual representation simplification/storage result, without a speedup
claim; full delivery tracked separately. The whole-script objective stays open.

R273 full delivery is GREEN on exact cb8f59c3: Windows/Ubuntu, actual .NET 11/10,
original stress and installed Release consumers. All 568 frozen production files
match with explicit newline normalization. [Archived delivery](results/2026-11-12-native-operation-facts/delivery/README.md).
CPU uncertainty remains; this does not qualify the later range prototype.

## R274 — prepared cased-word ranges

[Source pair and bounded comparison](results/2026-11-12-prepared-word-ranges/README.md)
replaces private consumption's second whitespace scan with prepared ranges, using
the existing scanner and operand-stack retirement. One private cache implementation
serves function and range buffers; active loans remain funded through shutdown.
Eager word admission and publication before refund reentry remain explicit.
730 actual-target focused checks and six complete scripts/both APIs/compile/fresh
pass. Allocation flat; desktop elapsed mixed. B628 three jobs finish152.867s:
captured Run elapsed−5.44..−0.97%, Async−4.94..−3.26%, but report/control and CPU
movement limit attribution. Retain complete-stage removal, no substantial CPU or
CPython-ratio claim. Full qualification follows at this source boundary. Original
failed build/test assertions, desktop adversity, stale raw-purpose prose and disk
assessment survive. Only rebuildable NuGet HTTP downloads were cleared; no evidence
or images retired. The broader performance goal remains open.

R274 first delivery43541e78 ends with Ubuntu all-target/stress/consumer green and
one Windows public numeric allocation-window failure (141336>131071). Original
condition and unresolved cause survive in [first delivery](results/2026-11-12-prepared-word-ranges/first-delivery/README.md).
The fixture now measures at its existing first-refusal snapshot with unchanged
bound and explicit large requested storage; cleanup/terminal semantics remain.
A deliberately displaced eager power preflight is detected, then exact production
bytes are restored/nonincrementally rebuilt.29focused cases pass per actual target;
all568production hashes match. Full correction qualification follows without retiming.

## R275 — config/report source gate

[Four bounded profiles](results/2026-11-12-config-report-costs/README.md) reuse
existing config and UTF8/UTF16 report fixtures/fixed workers;23.725sampled seconds.
Caller/guest/GC CPU and all unknown/raw stacks remain explicit. No demonstrated
dominant duplicate frontend/builtin graph or frame construction stage; keep prior
simplifications. UTF16 already uses native UTF8, but manual scalar emission and a
separate character-count/sizing pass remain. Select R276 bulk managed UTF16
transcoding with shared count/sizing, exact semantics/preflight/cancellation and
one affected/control comparison. No CPU forecast from leaf shares. Preserve B629
pre-sampling filename failure and B630 corrected-path confirmation; no runtime edit
or permanent design rejection follows. Existing patch evidence needs no replay.

R274 correction1011ed0b is fully GREEN43254088804 on both OSes/actual targets,
stress and installed consumers. All568frozen production sources match.
[Archived delivery](results/2026-11-12-prepared-word-ranges/delivery/README.md).
No arithmetic runtime change, retiming or erased first failure. R276 stays separate.

## R283 — shared-allowance frequency source gate

[One untimed pass](results/2026-11-12-admission-frequency/README.md) over three
complete scripts/both APIs observes6229text/13398CSV/14425report reservation
resizes per command, with identical API counts and full output/balance checks.
The source already combines record/word preflights. Counts and prior monitor
samples do not prove removable CPU or duplicate funding. No governor-wide funding
cache is selected: it would add a balance, reentry/headroom/refusal rules and
retention changes. No new allocator, policy, runtime option or ownership layer;
the private framework-selection/build failures remain alongside the diagnostic.

## R282 — committed-source CPython checkpoint

[Five targeted jobs](results/2026-11-12-captured-cpython-checkpoint/README.md) on
committed9bca1337/frozene4cc65b2 complete in238.674s, with all source/image/golden/
ownership/quiet/join/unmount/archive gates passing. Captured text warm1.63–1.74×,
compile0.83–0.84× and compile-to-result1.44–1.54× CPython3.13.16; UTF8report
warm1.30–1.33× and compile-to-result1.23–1.29×. Moving controls/IQR remain visible.
These are external ratios, not source-pair gains or release qualification. [Full
delivery43295336048 passes](results/2026-11-12-captured-slot-ownership/delivery/README.md) independently; the broad performance objective remains
open. Original guide/rows and all raw observations survive; publication held.

## R281 — authoritative captured slots

[The representation migration](results/2026-11-12-captured-slot-ownership/README.md)
removes sparse LocalCells arrays, owner-slot backpointers, active-frame cache writes,
generator cell-to-slot resynchronization and duplicate transport. Captured locals
hold their shared cell in the existing workspace. Initial three generic-function
failures expose namespace handover's dependency on the old cache; seeding before
mirroring corrects it, with all failed checks retained.7458focused actual-target
checks and an overlapping3683public checks per target pass; seven scripts/both APIs/
compile/fresh pass, whole allocation effectively flat. B638five jobs264.498s shows
capturedRun elapsed−4..−6%/CPU−2.6..−4.5%; async/control uncertainty and initial
report adversity remain. Same-source B639two jobs139.022s narrows CSVAsync elapsed
debt to+0.4..2.3%, with CPU uncertain; report remains variable and initial CPU
adversity is not reproduced. Retain the mechanism removal/affectedRun benefit,
accept that explicit debt; [full delivery passes](results/2026-11-12-captured-slot-ownership/delivery/README.md) separately on9bca1337. No CPython
extrapolation or closure of the broader whole-script objective; publication held.

## R280 — native accumulation boundary deletion withdrawn

[Both measured forms](results/2026-11-12-native-accumulation-boundaries/README.md)
remove checks/span transport only from bounded callback-free native arithmetic;
per-word stack protection and actual admission/protocol/unwind boundaries remain.
Each passes1826focused actual-target checks and seven complete scripts/both APIs/
compile/fresh, allocation flat. B636capturedRun+3..6%elapsed/+2..5%CPU is adverse;
kernel-only B637refinement is inconclusive with moving controls. Withdraw both
limited deletions, preserve all original/refined sources/conditions, and restore
qualifiedb3369e3e nonincrementally with focused checks. No dormant prototype,
quasi-fuel counter or further unchanged timing. This does not permanently reject
boundary consolidation or complete the larger performance objective.

## R279 — JSON tokens over existing immutable storage

The [retained source migration](results/2026-11-12-json-existing-windows/README.md)
removes same-owner plain-token arrays/copies using R271's existing representation.
R54/R60 original global-layout/accessor costs and adverse data remain; no new
layout/hash policy, owner, ledger, parser, pool or option is introduced. Full
backing/header stays funded after source-shell cleanup; small native aliases can
retain large input until the bounded command ends. Other-owner/unowned, escaped
and inline text copies. CLR result projection can end the guest lifetime safely.
2290focused actual-target checks and seven complete scripts/both APIs/compile/fresh
pass. Report allocation−4.68%UTF8 /−4.46..4.49%UTF16; other allocations effectively
flat. Desktop async report adversity and failed fixture/launcher/compile conditions
are retained. B634four jobs208.984s and targeted B635three143.230s pass all identity/
golden/zero-balance exit/quiet/join/unmount/archive gates; terminal cached. UTF8
report elapsed improves with modest supporting CPU. UTF16/JSON/CSV CPU remains
uncertain; small control/retention debt is accepted, no equivalence or extrapolated
CPython claim. [Full delivery passes onb3369e3e](results/2026-11-12-json-existing-windows/delivery/README.md) at the retained-source checkpoint. This
milestone does not complete the substantial whole-script performance objective.

## R277 — qualified selected CPython checkpoint

Qualified41bd869b completes seven existing jobs in328.130s: captured-text warm/
fresh, UTF16report warm/fresh, UTF8warm control, config compile/fresh. Captured
warm1.66–1.68×CPython; UTF16warm1.45–1.49×/fresh1.37–1.41×; UTF8warm1.30–1.32×;
config compile1.07–1.08×/fresh1.11–1.12×. Captured fresh1.46–1.55× retains5.19%
Lython process spread. All identity/golden/quiet/zero-balance worker exit/join/RAM
cleanup/archive gates pass; terminal cached. The original launcher self-match
failure occurred before staging/workers and survives with its correction. No
cross-collection attribution or new timing needed for this bounded decision.
[Raw evidence and boundaries](results/2026-11-12-utf16-cpython-checkpoint/README.md).
The external gaps remain a product priority; this checkpoint does not finish it.

## R278 source gate — shared prefix form not selected

The proposed managed span/inline frame view, array-backed retained continuation,
funded live-state promotion and native graph deletion match the already implemented
R268 migration. Its [positional refinement](results/2026-11-12-ordered-helper-prototype/positional-refinement/README.md)
removed ordinary argument packaging but still regressed native search22–26% with
stable controls. Paired profiles identified recurring entry/instruction dispatch;
the removed guard/evaluator also performed useful execution. No new production
prototype or quiet-VM timing is warranted for the same form. This is a decision on
this proposal, not a permanent ban on coherent call/frame redesigns. A different
design needs to show which recurring work disappears before another migration.
Current executable locals/operands already share an array, the binder accepts
spans and deferred contexts already support incoming closure cells. Source facts
must not present these as new improvements. [Source map and offline attribution](results/2026-11-12-redesign-source-gate/README.md)
preserve the bounded decision and its limitations. No public contract changed.

## R276 — bulk managed UTF16 output

[Both forms and bounded confirmation](results/2026-11-12-bulk-utf16/README.md)
replace manual scalar emission with bounded span transcoding and delete three
dormant helpers. First sizing/count fusion shows UTF8 control elapsed+2..+5%;
withdraw that branch and restore common UTF8 files exactly. Refined source changes
one production file, passes686actual-target checks and seven complete scripts/both
APIs/fresh; allocation flat. B632three jobs153.958s: UTF16 elapsed−1..−8%Run/
−2..−5%Async, supporting AsyncCPU; sync CPU uncertain. UTF8elapsed−.36..+2.11%,
CPU0..+1.25%is accepted small debt/uncertainty, not equivalence. First/adverse and
moving desktop controls survive. Retain the smaller encoder; full delivery follows.
No new pool/policy/backend/unsafe code or inferred CPython ratio. Whole-script
objective stays open; coherent external checkpoint and larger engine source proof
remain separate deliverables.

## R284 — explicit lexical cell storage withdrawn

+[The prototype and all original conditions](results/2026-11-12-lexical-cell-storage-withdrawn/README.md)
+reduce each cell48→40B but leave whole-script allocation effectively flat. Explicit
+namespace promotion adds machinery;14316focused actual-target checks and seven
+scripts/both APIs/compile/fresh pass. B641five jobs275.929s shows CSV Run
++1.28..+3.46% elapsed/+1.70..+4.98% CPU, while captured text remains uncertain.
+Withdraw the source, retain the independent alias regression and all favorable/
+adverse desktop and VM data.568sources restore to qualified production; forced
+nonincremental build and438focused target checks pass. No dormant promotion hooks
+or repeated full qualification. The substantial performance objective stays open.
+
## R285/R286 — result ownership gate and bounded local attribution

+[The source gate](results/2026-11-12-private-iteration-source-gate/README.md) finds
+that the private cursor transfers a real range loan through native, ordinary and
+protected/suspending returns. A caller-only side table would add an incomplete
+protocol; no local carrier prototype is selected. The broader alternative remains
+open. [Three current-core Windows disk traces and one existing-memory-host
+correction](results/2026-11-12-local-script-attribution/README.md) retain all goldens,
+balances, thread stacks and original failures. Sampled wall time does not establish
+CPU dominance; unmanaged waits and apparent MapCaseRune concentration remain
+unresolved. No speedup or Linux/CPython inference, no repeated full qualification.
+The next actual source prototype is ordinary tuple storage, preserving Python
+objects and aliases rather than adding a private return/unpack convention.
+
## R287/R288 — tuple storage withdrawn; complete-helper design gate

+[Three tuple forms and all original conditions](results/2026-11-12-inline-tuple-prototype/README.md)
+measure initial48→72B/refined64B headers. The eager adopted-array refinement fails
+deepcopy; corrected code preserves that backing and passes8222focused actual-target
+checks plus seven scripts/both APIs/compile/fresh. CSV allocation−2.42%, but
+B642four jobs169.406s demonstrates no reliable targeted CPU benefit; Run includes
+possible adversity. Withdraw the storage tag/header/index/admission complexity,
+preserve all results, restore568sources and force a nonincremental build plus
+focused checks. No new full qualification or closure of the whole-script objective.
+[R288 exact code facts](results/2026-11-12-complete-helper-source-gate/README.md)
+show13ordinary CSV helper instructions/8slots/0statement fallbacks. Repeated local
+use and concrete returns need one ordered effect/transition contract, rather than
+duplicated initializers or premature field-type proof. No automatic larger
+compiler migration or replay of the slower generic-prefix/arena forms.
+
## R289 — ordered final-slot migration, first controlled comparison

[First source/checks/B643](results/2026-11-12-ordered-final-slots/README.md) removes
the native graph/proof/recursive evaluator,287 production lines. Both targets and
seven full-script/phase controls pass. CSV improves but native search+12–14% and
captured text+4–8% remain adverse with supporting CPU; warm allocation is flat.
Retain all controls/first fixture/build/disk failures. One bounded lifetime/cache/
obsolete-metadata refinement is active, not a retained speedup or completed goal.
Qualified production remains9bca1337; no platform/package replay or publication.

## R290 — optional adapter capability refresh, immutable dev78

[New bounded package observation](results/2026-11-13-regex-provider-capability/README.md)
finds a material adoption detail: funded Compile ignores AssumeValid on both actual
TFMs, although the unfunded overload honors it; ownership returns to0. Preserve
package hashes/raw property checks; no timing or adapter promotion claim. Three
precise final-adapter API gaps remain, not a stop on core/runtime development.

R289's [refined B644](results/2026-11-12-ordered-final-slots/README.md) improves CSV
Run10–11%/Async13–15% with supporting CPU, allocation flat; original native
regression shrinks but moving controls leave debt uncertain. Captured/report
cross zero. A demonstrated private-return cancellation cleanup gap is corrected
after the measured freeze;3288focused checks pass actual targets. Prepare the
coherent delivery candidate and targeted final-source confirmation, preserving
all first/refined results. Full delivery is not established by these timings.

The same [dev78 evidence](results/2026-11-13-regex-provider-capability/README.md)
also qualifies the existing adapter-local development bridge with AssumeValid
selected through the working overload, preserving conservative compilation funding.
88internal/534public checks pass each actual target; loaded producer hashes,
29normal files,570production sources and normal core binary are verified unchanged.
No normal promotion, backend speed claim, core dependency or publication follows.

R289 is [fully delivered on2d91eb1a](results/2026-11-12-ordered-final-slots/delivery/README.md),
GREEN43327264124,346 production lines removed. Exact final source improves CSV
compile+run9–11% with supporting CPU; current CPython ratio0.92–0.95×. B645's
three warm rows/compilation complete before a private unsupported fresh-lane request;
its missing normal end gate and all raw conditions remain explicit. B646 corrects
only that lane in60.276s, all gates pass. Native-search1–6% debt is accepted with
uncertainty; guarded controls remain variable. Current performance guide/source
proof are refreshed, not extrapolated. Publication held; broader objective open.


## R292 — current captured-text CPU source gate

[Qualified2d91 CPU capture](results/2026-11-13-captured-current-cpu/README.md):
1,518 selected samples /7.64 guest CPU seconds. Dictionary loop17.39%, cased-range
preparation9.09%, instruction prefix7.77%; inlined useful work is included and no
removable shares or speedup are inferred. Original collector/archive failures,
all raw frames and the exact-byte recovery are preserved. No samples repeated.
Next bounded gate assesses direct per-record range/storage construction with
observable admission and retained-alias semantics; no new runtime prototype yet.


## R293 — direct range construction

[Shared existing construction buffers](results/2026-11-13-direct-range-construction/README.md)
remove scratch reservations/counters and the private inventory copy (12 net production
lines). All affected/ownership/cancellation checks and seven-script boundaries pass.
B648 six-job quiet comparison completes320.712s with all custody gates. Allocation
is flat. Captured compile-to-result improves3.61–9.06% elapsed /4.37–7.67% wider CPU;
warm results cross zero and adverse/moving CSV/search controls remain explicit.
Retain the common ownership simplification; do not claim universal CPU improvement.
Minimum/idle capacity can deny earlier. Full delivery remains pending separately.


## R294 — complete report CPU gate

[1,477 current-source CPU samples](results/2026-11-13-report-cpu-source-gate/README.md)
retain10.16% unknown leaves and all raw maps/stacks. Existing UTF8/default numeric
formatting and JSON windows are confirmed, not proposed again. Formatting
instruction/operand construction is a follow-on source gate; shared builder refund
publication is a separate focused ownership hypothesis to reproduce first. Original
B649 decoding/forced-close failure and corrected B650 normal shutdown/quiet/balance
gates survive. No timing comparison or removable-CPU share is claimed.


## R295 — publish builder ownership before refund

[Four demonstrated retained-funding/cleanup failures](results/2026-11-13-builder-refund-ownership/README.md)
are corrected in the existing byte-builder transaction. New/empty ownership is
published before external refunds; only unpublished allocation rolls back.134internal/
704public per actual target and all seven scripts/both APIs pass. No new mechanism
or CPU claim; full qualification of this later source remains a separate delivery gate.


## R296 — formatted helpers in final slots (2026-11-13)

The actual three-field report helper now shares ordinary formatting operations in
the native final slots, with once-only transfer at custom protocols. No separate
formatter/controller is introduced. Focused target checks and all seven complete
scripts/four phase boundaries pass. Allocation is flat. B651 finishes351.629s with
all quiet/ownership/golden/join/archive gates: UTF8/UTF16 warm improves5.8–9.1% /
6.5–8.0%, report compile-to-result4.9–8.3%, supporting wider CPU. CSV is stable;
compilation/captured/search uncertainty and all adverse controls remain. Retain
the source change, pending full delivery and affected CPython refresh. Original
fixture/summarizer failures are preserved; no valid VM rows replayed. The broader
performance objective remains open. [Exact source, all results and limits](results/2026-11-13-formatted-helper-slots/README.md).


R296 external checkpoint B652 completes281.260s with all identity/output/ownership/
quiet/join/archive gates. Committed31d65e01/e34cacaf reports are1.23–1.28× CPython
warm; UTF8 compile-to-result1.18–1.22×, UTF16 fresh1.22–1.26×. Compilation remains
separate, roughly1.07–1.08×. Six report rows refresh the maintained guide, retaining
all earlier/adverse observations and process/IQR variability. Full delivery runs
independently; no unrelated script or async speed claim follows. [Raw/current/older
rows](results/2026-11-13-formatted-helper-slots/README.md#cpython-checkpoint--b652).


## R297 — current complete config-edit CPU source gate

One bounded current31d65e01/e34cacaf capture has737 samples/3.87guest CPU seconds,
63.23% unknown leaves (mostly kernel). Host resolve/open nearest frames total45.73%;
builtin construction3.12%. Useful/inlined work and unknown stacks limit attribution;
no setup-dominance, CPU-free sharing or speedup is established. No shared-type
prototype is selected. Mutable per-run namespace/hierarchy/descriptor caches would
make blind sharing unsafe for ownership. The source gate additionally confirms an
independent object-slot owner alias bug, trackedR298. Original staging/offline-path
failures survive; the completed capture passes all output/ownership/quiet/join/
archive gates, without repeating valid rows. [Evidence and limits](results/2026-11-13-config-cpu-source-gate/README.md).


## R298 — builtin object slot owner aliases

The config source review confirms that assigning object.__init__ to another class
changes the original slot's __objclass__. A public probe disagrees with CPython,
and both API regression cases fail on the compiled baseline. Sixteen native slots
now retain the first defining owner, matching function binding; no new abstraction
or representation. The full fifteen-slot alias/wrapper golden matches isolated
CPython;627 public semantic/descriptor/cancellation/ownership checks pass per actual
target, including repeated shared-budget cleanup. The invalid earlier no-test
attempt is retained/excluded. No speedup or current-HEAD CPython timing is claimed.
[Original failures, exact source and validation](results/2026-11-13-builtin-slot-owner/README.md).


B654 refreshes only three stale captured-text phases at the same qualified
31d65e01/e34cacaf report checkpoint.173.802s, all gates/joins/archive pass; warm
1.61–1.64× CPython, fresh1.50–1.51×, compilation0.81–0.82×, tight controls. No
internal dominance or individual-change attribution follows. Older/adverse rows
remain, laterR298 excluded. [Full evidence](results/2026-11-13-captured-cpython-checkpoint/README.md).


## R299 — immutable dev.79 adapter bridge

The existing isolated bridge independently qualifies new producer891cabbf/dev.79
on consumerd413392c:88internal/534public per actual.NET11/10, exact loaded package
identities,29 normal files/571 core sources unchanged. No normal pin/API/dependency
promotion or backend speed claim. Final single-match owners, nativeUTF8 names and
token-independent preparation remain separate; source unchanged funded forwarding
is not a new successful capability probe. [Exact copies and validation](results/2026-11-13-regex-dev79-bridge/README.md).

R298 d413392c/d7b2ddaf full Windows/Ubuntu actual.NET11/10 ordinary, original stress and installed Release consumers are GREEN43354417890. [Cached source-matched delivery](results/2026-11-13-builtin-slot-owner/delivery/README.md). No performance or publication claim.

## R300 — whole-line cased-word pipeline withdrawn

[Complete source/ownership migration and controlled evidence](results/2026-11-13-cased-word-pipeline/README.md)
replace per-word casing, private inline word storage/cutoff and range capacity
metadata with one cased string and ordinary final UTF8 ranges. Existing windows
fund long aliases through unwind; both eager/private consumers migrate together.
154internal/408public and3affected original stress cases pass per actual target;
seven scripts/four boundaries pass outputs/balances/cleanup. Four fixture failures
from an exact reservation-size trigger and all original logs remain preserved.

Allocation rises29.1%warm/25.1%fresh. Quiet B655 completes266.593s/all gates:
captured warm+10.4–16.2%elapsed/+10.8–16.3%widerCPU, fresh+11.8–14.3%elapsed/
+10.9–14.8%CPU. Control uncertainty/adverse report rows remain explicit.
Withdraw the complete runtime prototype; all571production sources exactly restore
qualifiedd413392c. Keep independent long-word ownership and size-independent
refund-reentry regressions (34cases/target after nonincremental build). Generated
Git-version metadata changes the rebuilt binary identity; the failed private hash
expectation and valid source/build/test proof remain visible. No unchanged full
qualification replay, speedup claim, dormant code or permanent design veto.
The whole-script objective continues through the complete call/frame/operand gate.

## R301 — shared temporary reservation ownership correction

[Complete-call source gate and reproduced callback failures](results/2026-11-13-temporary-owner-reentry/README.md)
confirm bound/local arrays, linear callers, native completion, lazy namespaces and
operand forwarding already exist. Do not add a duplicate controller from that premise.
Temporary shrink refunds before publishing its own reduced balance: reentrant
disposal steals16B from an independent96B reservation. Growth can complete after
its owner ends. Three before-fix failures reproduce; first-refusal coverage passes.

The existing transaction now publishes balances before refunds and returns unused
growth before refusing to continue a disposed owner. Five corrected cases,854internal/
216public per actual target and seven complete-script warm diagnostics pass.
Allocation is flat; moving desktop controls retain uncertainty, no CPU claim.
Original overlapping-build/fixture conditions and all logs survive. Full delivery
is batched from the retained-source commit; broader performance work remains open.

R301 runtimefc2531b8 / source-pair core6b468bf1 is fully qualified by terminal
GREEN43364639355: Windows/Ubuntu actual .NET11/.NET10 ordinary, original stress
and installed Release package consumers. [Cached full delivery/source proof](results/2026-11-13-temporary-owner-reentry/delivery/README.md).
All 571 frozen inputs match the commit; later invoice catalog/tests are excluded.
No CPU improvement or unchanged full qualification replay is claimed.

## R302 — complete invoice workload and composed-call source gate

[One maintained CSV invoice script](results/2026-11-13-invoice-workload/README.md)
fills arithmetic/composed-helper coverage alongside the existing validation/count
and bulk-sum scripts. Independent complete-file goldens agree under Lython and
isolated CPython on empty/small/large compile/warm/fresh smoke. Both target
frameworks pass100focused public cases, including sync/async shared-budget cleanup.
Original4MiB new-fixture refusal survives; the finite large invoice uses16MiB.

Compiler inspection confirms pricing/rounding use12/6final slots, both native-
eligible. User-function calls nevertheless promote the native caller before
rounding. This concrete context/storage elimination hypothesis needs bounded
attribution and a decision before a runtime prototype. No timing ratio, speedup,
new backend/arena/value wrapper or producer dependency is introduced.
