# Unicode formatting and ownership-wrapper cleanup, 2026-10-19 UTC

Python string width/precision and one-character fills now handle supplementary
characters correctly. Baseline93b4b172 under-pads astral text, splits a surrogate
pair during precision (escaping as CLR EncoderFallbackException), and rejects
supplementary fill. Four independent programs reproduce all three defects under
both APIs. The corrected shared formatter matches31 CPython3.13.2 programs under
both APIs (62 runs), including every original failure and invalid-format controls.

| Case | Before | Corrected |
| --- | --- | --- |
| Quoted café/emoji, width12 | 11 Python characters | 12 characters |
| format('😀z', '.1s') | CLR EncoderFallbackException | 😀 |
| format('x', '🧡>3') | ValueError | 🧡🧡x |

The implementation reuses PyString's existing character count and slices only at
valid boundaries. A scalar fill plus the existing unspecified marker replaces
nullable UTF-16 char without growing that four-byte field. BMP padding keeps its
bulk constructor; supplementary padding uses managed string.Create with bounded
execution checkpoints. Centering no longer constructs an unused full padding
buffer. Existing UTF-8 expansion admission precedes allocation. No new budget,
owner, cache, regex dependency or formatting framework is added.

A separate preceding commit d212c3fa removes inert OwnJoinItem and21 calls in five
files. It performed no ownership work. Its [five-file patch](identity.patch)
retains real funding, preflights, result helpers and per-value managed admission;
the recursive core is byte-identical. The stale str.format ownership comment is
corrected. All24 native allocation/work/funding/output arrays and248 fuel/admission
records match exactly across that mechanical change. It claims no speedup.

Correctness baseline:d212c3fa96f3dc366266ef63c4162268dc9e4b68;candidate:that
revision plus the [shared formatter/complex marker patch](candidate.patch).
One initial diagnostic attempt failed six normative padding assertions, revealing
the baseline bug;it is preserved. The mechanical comparison snapshots that known
three-space result for equivalence only. The permanent corrected expectation is
four spaces, validated against CPython. No assertion is relaxed to qualify a bug.

The short native diagnostic prepares code/context/input outside measurement:
two warmups,eight samples,eight calls/sample. All24 allocation/small-work arrays
remain identical after the correctness change;all18 unaffected rows match fully.
Correct extra padding increases measured retained charges by1B/call for f-strings
or2B for method/lowered joins;partial-denial charges can also increase. All248
fuel/admission outcomes,error kinds/spans and work match on these small inputs.
Large supplementary padding adds cooperative checkpoints,covered separately.

There is no new baseline measurement for the correctness change:the mechanical
candidate is reused. A superseded manual-width-scan draft capture is retained
privately;the final implementation reuses existing string length instead. Scope
records both attempts;no elapsed timing,retiming campaign or speedup claim.

All739 distinct focused Release cases pass:134 white-box,453 public formatting/
protocol,90 ownership/refusal and62 complete-job/cancellation cases (58 complete
CSV/text/JSON/file jobs). Thirty new cases cover Unicode semantics,invalid fills,
funding/cleanup,large-fill refusal before publication and cooperative fuel. G03
managed stack guards,deep-repr recovery,protocol I/O/cancellation and retained
owners remain. Original Stress scales/assertions/timeouts are unchanged and no
fresh Stress pass is claimed. Temporary diagnostic removed.

Local Windows10.0.26200 X64,i7-14700KF/28 logical processors,68.4GB;
SDK10.0.300-preview.0.26177.108/runtime10.0.13. Allocation capture excludes
compilation,startup scheduling,projection and host I/O;pure async completes on
the measured thread. No elapsed/CPython/complete-job gain is established.
[SPEC](../../../SPEC.md) states character semantics. Package/platform delivery
is recorded in the private plan;regex remains planning-only.

[Identity scope](identity-scope.json), [identity diagnostic](identity-diagnostic.cs),
[identity baseline](identity-baseline.trx), [identity candidate](identity-candidate.trx),
[identity comparison](identity-comparison.json), [initial failures](initial-failures.trx),
[correctness scope](measurement-scope.json), [diagnostic](diagnostic.cs),
[final samples](candidate.trx), [comparison](comparison.json),
[baseline probes](baseline-probes.json), [candidate probes](candidate-probes.json)
and [validation](validation.json) preserve the evidence.
