# Complete helper construction — R288 source gate

2026-11-12. Read-only compilation of the existing seven complete scripts against
qualified9bca1337/e4cc65b2 and the existing optional adapter. No runtime prototype,
timing or new qualification. The R287 tuple-storage experiment is independent.

| Existing helper | Native body prepared | Executable slots | Statement fallbacks |
| --- | --- | ---: | ---: |
| Search/wanted, both search scripts | yes | 6 | 0 |
| CSV/validated_record | no | 8 | 0 |
| Captured text/words_from_line | yes | 5 | 0 |
| JSON/record_amount | yes | 5 | 0 |
| Report/render, both encodings | no | 6 | 0 |

All these helpers have unmirrored ordinary executable scopes. CSV has three locals
(record,minimum,amount), thirteen flattened instructions and no lowered statement
fallback. It binds/executes ordinary shared workspace; there is no second local
array or unavoidable lexical-frame allocation to delete. Its local value is reused
after a branch, then two values form the actual observable return tuple. Native
preparation only forwards one immediately reloaded local; later reads decline it,
and nonempty MakeTuple is excluded. Merely extending tuple shape eligibility does
not complete this helper.

Prepared NativeBody presence is not runtime hit counts. The actual JSON helper
returns record['amount'] directly; it does not call int, and a root subscript
can complete under the existing native guard. Do not invent a coverage gap for it.
The CSV helper does call int on a computed dictionary field. In a proposed expanded
body, Subscript validates dictionary/key but leaves computed shape Other, whereas
BuiltinUnary needs an actual argument or accepted computed shape. Extending local/
tuple construction therefore also needs a coherent guard/value policy for this
computed input. This is a source design requirement, not current native hit counts
or an attribution of the full CPython gap.

**The direct local/tuple-node extension is insufficient.** A single-assignment
local must evaluate once and remain available to later branches/return; substituting
its expression repeats conversion, admission and error effects. Guarding every later
dictionary field before execution is also insufficient: int parsing/formatting can
admit storage, and host reentry can change a later field/type. An actual later
protocol can require asynchronous host I/O. Whole-call restart after those effects
is forbidden. Current native entry/proof/evaluation is valuable (R268 replacement
measured slower), but extending its eligibility must preserve actual ordered reads
and a complete transition to the ordinary executable state when required.

The larger design must identify the code it removes: recurring ordinary operand/
instruction transport for complete eligible helpers, plus the current limited
single-load forwarder and any displaced native-state code. Preserve exact Python
tuples/defaults/aliases/branch and error order; no new private result convention,
second dispatcher, cache, general type solver or replay. Keep shared admission,
first refusal, cancellation, managed stack and funded pending cleanup. Read-only
facts do not prove that a larger promotion scheme is simpler or faster.

This bounded gate closes with a precise obstacle to the proposed simple expansion,
not a failed design or permanent architecture veto. Continue design around one
ordered call state, or choose a materially different source-backed stage. Require
an explicit removal argument before implementation; compare complete CSV/JSON and
search/captured/report controls, compilation and fresh boundaries if selected.
Do not count another isolated storage experiment as completing the performance goal.

[Exact executable facts](facts.json), [unchanged script sources](sources.json),
[inspection sources/build and analysis](diagnostic.tar.xz). No repeated platform,
stress/package run or producer-version probing. Publication remains postponed.
