# str keyword/default correction, 2026-10-18 UTC

str now accepts object= and preserves omitted argument information through the
existing presence binder. Missing object plus validly typed encoding/errors
produces empty text;explicit None remains distinct. Errors-only byte decoding
uses UTF-8. Empty bytes validate codec-name types without looking up codec names.
The earlier array-length heuristic lost omitted slots and rejected valid calls.

A private str specialization inherits existing builtin type metadata and uses
existing ArgumentPresence hooks. The private base becomes inheritable;no new
fields, public API, binder options, cache or ownership subsystem is added. Old
Str/StrAsync suffix inference and the incorrect positional-only restriction are
removed. Codec decoding borrows PyBytes.Memory instead of copying input before
its existing preflights. Ordinary native call transport remains unchanged.

Baseline:`b7d67a60d231318ea4799ce8ef7bbf9cfa6f99ea`;candidate:that revision
plus the [three-file patch](candidate.patch). A finite 30-program matrix first
found 14 mismatches under each API. Expanded public regressions run against the
immutable baseline package:17 pass/24 fail out of41. All41 pass on candidate,
including keyword descriptor/body I/O and cancellation with owner cleanup.
Thirty-seven public programs match CPython3.13.2 through both APIs (74 runs),
covering aliases, expansion, __new__, metadata, identity and custom conversion.
Error-kind/effect controls do not claim every CPython message is identical.

One extra probe incorrectly assumed builtin-str subclassing was supported.
Candidate and immutable baseline both explicitly reject builtin class bases.
That unchanged subset boundary is preserved in the evidence and excluded from
the37 matching programs. No class-storage redesign or runtime tuning followed.

One short frozen allocation pair reuses the native str diagnostic:two warmups,
eight samples,eight calls/sample. Native integer/wide/alias/string arrays are
unchanged,retaining the preceding32B/call saving. All work/funding/source/output
arrays and252 existing fuel/admission records match exactly. General calls now
use the existing presence binder's full three-slot array:

| Existing native call form, either API | Allocation change per call |
| --- | ---: |
| Direct integer/wide/string or alias | 0 B |
| Expanded integer,float,bytes repr,custom object | +16 B |
| Zero arguments | +48 B |
| Two-byte UTF-8 codec call | -24 B |
| Guest shadowed callable | 0 B |

The larger general-call array is an explicit correctness tradeoff. The codec
row includes removal of a private input-byte copy. Do not infer latency or whole-
job gains. Prepared code/context/input,compilation,startup scheduling,projection
and host I/O are excluded. Pure async values complete on the measured thread;
frames/depth/temporary reservations clear and owners return allowance to zero.
There is one capture per revision,with no recapture or production tuning.

All440 distinct focused Release checks pass:63 white-box,41 new regressions,
151 existing rendering/protocol/codec,33 builtin metadata/isolation,90 shared-
budget/owner/refusal and62 complete-job/cancellation cases (58 complete CSV/text/
JSON/file jobs). Managed stack,first refused admission,wide integer preflights,
retained aliases/caches and awaited cleanup remain. Original Stress scales,
assertions and timeouts stay unchanged. Temporary diagnostic removed.
[SPEC](../../../SPEC.md) and [MEMORY](../../../MEMORY.md) state the contracts.

Local Windows10.0.26200 X64,i7-14700KF/28 logical processors,68.4GB;
SDK10.0.300-preview.0.26177.108/runtime10.0.13. Allocation only,no elapsed or quiet-
machine claim. [Scope](measurement-scope.json), [source screen](source-screen.json),
[diagnostic](diagnostic.cs), [baseline samples](baseline.trx),
[candidate samples](candidate.trx), [comparison](comparison.json),
[baseline probes](baseline-probes.json), [candidate/CPython probes](candidate-probes.json),
[baseline regressions](baseline-regressions.trx), [candidate regressions](candidate-regressions.trx)
and [validation](validation.json) preserve the evidence. Baseline library identity
is recorded separately. Platform/package delivery is recorded in PLAN.md.
Stop at str binding/defaults;regex remains planning-only.
