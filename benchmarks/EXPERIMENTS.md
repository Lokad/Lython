# Benchmark experimental history

Historical records preserved on 2026-10-12. Revision-specific findings below
must not be read as measurements of current HEAD. See [BENCHMARKS.md](../BENCHMARKS.md)
for the authoritative comparison and [COMPARISON.md](COMPARISON.md) for methods.
The original narratives and evidence links follow unchanged. Collector-first
ownership experiments below are historical and superseded by governed memory;
their reproduction sources are evidence and are outside the supported runtime.

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
[measurement identities](../BENCHMARKS.md#which-code-and-machine-were-measured).

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
