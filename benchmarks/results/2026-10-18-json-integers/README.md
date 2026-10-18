# JSON integer value/key emission, 2026-10-18 UTC

JSON now admits major integer-conversion work before creating decimal CLR text,
including values and keys in default dumps, explicit encoding and iterencode.
Twenty-four baseline refusal cases allocate 44,088–81,136 bytes before denial;
the fixed cases stay below 16 KiB, with a maximum of 8,616 bytes in the initial
candidate capture. Input, first failure origin, cleanup and retained prefixes
are checked. **Elapsed speedup and CPython timing are unmeasured.**

Baseline: `9eb065eb1fd72234713c90e6c5879c7b25053484`. Candidate: that revision plus
the [four-file patch](candidate.patch), with canonical identities in
[validation](validation.json). These are diagnostic source identities, not a
measurement of an unspecified later HEAD.

| Native operation / allocated bytes per 128 calls | Baseline | Candidate |
| --- | ---: | ---: |
| Default dumps, integer `42` | 63,488 | 63,488 |
| Default dumps, integer `123456` | 69,632 | 64,512 |
| Default dumps, signed-64 maximum | 91,136 | 82,944 |
| Default dumps, integer `2**80` | 94,208 | 98,304 |
| Default dumps, float `1.5` | 67,584 | 67,584 |
| Default dumps, key `42` / null value | 105,472 | 105,472 |
| Default dumps, key `123456` / null value | 111,616 | 106,496 |
| Default dumps, signed-64 maximum key / null value | 133,120 | 124,928 |
| Default dumps, key `2**80` / null value | 154,624 | 158,720 |
| Default dumps, float key `1.5` / null value | 109,568 | 109,568 |
| Explicit encode, integer `123456` | 100,352 | 95,232 |
| Explicit encode, key `123456` / null value | 142,336 | 137,216 |
| Iterencode drained, integer `123456` | 126,976 | 121,856 |
| Iterencode drained, key `123456` / null value | 263,168 | 258,048 |

The table gives medians; [before](allocation-before.json) and
[after](allocation-after.json) preserve all eight samples per case. Large-value
cases add a 32-byte existing temporary-reservation object per operation. Small
positive and float controls show no allocation improvement. The byte counts
do not establish complete-script latency, compilation cost or a CPython ratio.

The [temporary fixture](allocation-diagnostic.cs) invokes already loaded native
JSON callables in fresh funded contexts. Context, input, call arguments and
callable loading are outside the thread-allocation counter. The counter covers
128 operations, encoder iterator creation/draining where applicable, retained
results/chunks and consumption of UTF-8 lengths. Last output and cleanup to zero
are checked. This excludes public invocation setup/scheduling, executable
dispatch, compilation, startup and host I/O. Four warmups precede eight samples.
No GC was requested. The fixture was removed before final Release builds.

The active Windows development workstation supplied these allocation samples:
Intel Core i7-14700KF, 20 cores / 28 logical processors, Windows build 26200,
x64. [Toolchain](dotnet-info.txt) records SDK 10.0.300-preview.0.26177.108 /
host 10.0.13; validation preserves assembly/source hashes and process receipts.
This is not an Azure-VM elapsed capture. No extra VM/full lane or elapsed
qualification campaign was run for this bounded conversion/admission change.

Eager and iterator writers now share integer emission. Signed-64 values format
into 20 managed UTF-16 characters and append to the existing builder. Larger
values reserve the existing rough ordinary-rendering estimate before BCL
formatting, holding it through append. Numeric keys use the same path and fund
builder growth before appending. The escaper borrows a character span with its
existing code-unit loop/checkpoints. The old bool-returning key converter,
unused skip argument and duplicate caller guard are removed. Sorting and key
validation still occur in their original order; callbacks and chunk boundaries
remain unchanged. No new serializer, builder, owner, cache, pool or unsafe/native
mechanism is introduced.

Conservative working/key-growth admission can reject earlier. Temporary
conversion storage is relinquished after use; retained chunks/results keep
their existing owners. Private conversions do not accumulate intermediate
Python strings. Framework-pool retention still needs host runtime headroom;
see the [memory contract](../../../MEMORY.md).

The [failed baseline](refusal-baseline.trx),
[frozen numeric-key controls](guard-baseline.trx) and
[final regressions](guard-candidate.trx) preserve failure/cleanup and exact
fuel/span/chunk behavior. All 78 preceding white-box JSON cases and their
`JSON_` records remain unchanged. Seventeen public cases (both APIs) also match
isolated [CPython 3.13.2 goldens](cpython-oracle.json), including signed limits,
large magnitudes, numeric key sorting, skipkeys and bool/float/None controls.
Append-refusal tests release already admitted conversion work; retained numeric
chunks keep funding through iterator completion and another shared-budget run.

753 distinct focused Release checks cover JSON semantics/ownership/callbacks,
command-failing admission, stack protection and complete CSV/text/JSON/file
goldens. Counts and receipts are in validation. Full platform/package checks
are batched at delivery. No assertions, deadlines or Stress scales were relaxed.
