# Compatibility baseline for lowered dispatch

Two static-analysis false positives were found while preparing the next dispatch
experiment and fixed before performance collection. Local `type` declarations
now bind their names without analyzing lazy values/bounds as immediate reads.
Deferred function and lambda bodies no longer freeze captured mutable collection
contents, including lists inside tuples; function summaries use the same rule.
Known errors in collections created locally still produce diagnostics.

For example, this was rejected during compilation despite being valid when
`read()` runs later:

```python
items=[]
def read(): return items[0]
items.append(7)
print(read())
```

Similarly, reading a locally declared alias produced an unassigned-local error.
These are compatibility fixes; the runtime and benchmark harness are unchanged.

Thirty-four independent regression checks are retained: ten white-box checkpoint/
control-flow boundaries, eight class-method effect/scope cases, and sixteen reduced
deferred-binding cases, including four provable-error controls. Original production
passes 107 of 119 selected checks and fails twelve; the later complete sixteen-case
reduced set passes four controls and fails twelve positive cases. Initial receipts
also preserve a C# fixture compile error, two incorrectly expected flow returns and
the original two public false rejections; the fixtures were corrected without
changing production first. All 239 focused public checks pass after the fixes.

Frozen source `88e23251` passes all **9,335 Debug tests (1,537 white / 7,798 public)**,
after explicitly building the matching probe. Twenty complete trusted snippets
match isolated CPython in both sync and async modes: forty comparisons in total.

The direct synchronous-dispatch candidate `d1a1c599` remains isolated. It routes
lowered statements, expressions and executable fallback statements through the
existing synchronous actions; all 121 focused boundary/effect checks pass. Full
candidate/VM gates and separately declared short timing/allocation/native
collections remain required before deciding integration. No candidate speed or
allocation benefit is inferred from source or tests. Attribute handling and
eligible executable class-method bodies remain further design work.

**No performance timing or full lanes run in this preparation step.** Routine
performance rounds retain seconds-long micros and a 30-second external collection/
cleanup cap. Ten-minute lanes remain infrequent, separately declared milestones.
Further qualification, original CSV/pipeline causes and external Utf8Regex remain
pending. Raw receipts and frozen assemblies are retained privately; hashes and
counts are in [evidence.json](evidence.json).
