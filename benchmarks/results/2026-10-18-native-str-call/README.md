# Ordinary native str calls, 2026-10-18 UTC

Executable single-positional calls to the actual builtin str now borrow exact
integer/string operands through the existing InvokeSingleInput helper. Both
APIs reuse the same rendering expression as the ordinary builtin. This avoids
argument packaging, binding and a one-element bound array for that call form.
The existing text-summary reports call str(counts[word]) in their output loops.
No renderer, binder, type metadata, owner, cache or pool is introduced/replaced.

Baseline: `7cb7e8ec89e592067398d7b2754fde340adf8667`; candidate: that revision
plus the [three-file patch](candidate.patch). One frozen pair, two warmups,
eight samples and eight calls/sample. No retry or post-capture production tuning.

| Native warm call, either API | Before allocation | After allocation | Saved per call |
| --- | ---: | ---: | ---: |
| str(integer) | 488 B | 456 B | 32 B |
| str(wide integer) | 776 B | 744 B | 32 B |
| Alias of str(integer) | 496 B | 464 B | 32 B |
| str(string) | 344 B | 312 B | 32 B |

All 14 control allocation arrays are identical: expanded calls, float, bytes,
codec, zero-argument, custom object and shadowed callable, under both APIs.
All work/funding/source/output arrays and 216 fuel plus 36 admission records
match exactly, including errors/spans, first refusal, denied sizes and publication.
String input identity remains. Prepared context/input/code are outside the
measurement, as are compilation, startup scheduling, projection and host I/O.
Pure async calls must complete on the measured thread. Owners return allowance
to zero and frames/depth/temporary reservations clear.

This is an allocation-only diagnostic. No elapsed, compile-to-result, CPython,
whole-job or host-latency gain is claimed. The shared immutable operation is
ordinary process metadata; existing conservative runtime headroom remains.
The integer rendering/UTF-8 copying/scratch preflights and output admission are
unchanged. General codecs, keywords, custom protocols, containers and shadowed
functions retain their existing routes and asynchronous behavior.

All 366 distinct focused Release checks pass:63 white-box,151 public rendering/
protocol/codec cases,90 shared-budget/owner/refusal cases and62 complete-job/
cancellation cases (58 CSV/text/JSON/file jobs). Existing tests exercise wide
integer preflights, argument effects/fuel/spans, actual descriptor/protocol I/O,
cancellation, attributed refusal, retained results, caches and managed stack.
Eight public probe programs match CPython3.13.2 through both APIs, including
alias identity/rebinding, expanded/native controls, codecs and retained strings.
Temporary diagnostic removed;no permanent architecture tests added. Original
Stress scales, assertions and timeouts remain.

Two separate existing compatibility failures remain visible: CPython accepts
str(object=12) and str(object=bytes,encoding='utf-8');Lython rejects the object
keyword. Both failures reproduce through both APIs using the immutable baseline
Release package,whose library SHA256 is recorded in goldens.json. The runtime
signature remains unchanged by this patch. PLAN.md prioritizes the correction;
these cases are excluded from the eight matching-program claim.

Local Windows10.0.26200 X64,i7-14700KF/28 logical processors,68.4GB;
SDK10.0.300-preview.0.26177.108/runtime10.0.13,ordinary test-process settings.
No quiet-machine timing claim. [Scope](measurement-scope.json),
[source screen](source-screen.json), [diagnostic](diagnostic.cs),
[baseline](baseline.trx), [candidate](candidate.trx), [comparison](comparison.json),
[public/CPython probes including failures](goldens.json) and
[validation](validation.json) preserve the evidence. Package/platform qualification
is recorded separately in PLAN.md. Stop at this transport form;regex planning only.
