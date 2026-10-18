# Correct split count semantics and suspension, 2026-10-18 UTC

`str.split` now converts `maxsplit` once before separator validation. Guest index
lookup uses the existing type-MRO coercion helper, including awaited descriptor
binding and callbacks under `RunAsync`. Instance shadows and ordinary attribute
hooks do not override the implicit index slot. Native counts complete without a
new async state machine.

Counts within signed 64-bit bounds work, including magnitudes beyond Int32;
out-of-range values raise `OverflowError`. Representable counts normalize to the
managed kernel's domain. Existing UTF-8 split kernels, capacity preflights and
owners allocate for actual subject parts, rather than the requested count.
Duplicate Int32 conversion branches and a redundant post-binding arity check
are removed. The normal signature binder remains authoritative.

Baseline is immutable **636c1664e53028c2bed373f8900c3f6c12f36a51**. The
[frozen correction](candidate.patch) changes one runtime segmentation file and
focused public regressions. Shared index helpers/validators, splitting/storage
kernels, frontend checks and executable call markers remain unchanged.

## Demonstrated failures and correction

Six [baseline probes](split-limit-probes.json) show four discrepancies on each
API: count effects occur too late, separator errors incorrectly win over index
errors, large representable counts are rejected, and overflow raises ValueError.
The simple native and pure-index controls match CPython.

The [119-case baseline](baseline-split.trx) passes 101 and fails 18. Ten failures
are actual delayed-host cases across direct, exposed, keyword, expanded and
descriptor forms. Their cancellation variants explicitly report that host stat
cannot run synchronously. The [initial correction](corrected-split.trx) passes
all 119 cases; no async-host failure is inferred solely from source inspection.

The [final 124-case selection](final-split-v2.trx) also covers retained counted
split results across another execution, terminal denial inside the index hook,
actual Unicode whitespace and receiver rebinding. These additions/final variants
were not separately executed on the baseline. Scalar baseline tests stop at the
first failing API; the separate six-probe matrix establishes both APIs.

## Validation and limits

All **1,011 distinct final focused Release cases** pass: 124 scalar/split, 176
surrounding string/lifetime, 496 binding, 89 ownership/admission, 62 complete-job/
cancellation and 64 white-box cases. This includes 58 complete CSV/text/JSON/file
jobs. All [30 split source goldens](python-goldens.json) independently match
isolated CPython 3.13.2. [Validation](validation.json) records receipts and hashes.

Delayed callbacks/descriptors suspend and cancel without replay. Count conversion
retains the evaluated receiver through rebinding; retained results remain charged.
Admission refusal still fails through a guest finally return and caller catch,
preserves earlier file effects and returns allowance only after cleanup. Original
Stress scales, containment assertions and timeouts remain unchanged.

SPEC/MEMORY describe the corrected ordering, count domain and lifetime contract.
No allocation, elapsed-time, complete-job or CPython speedup is measured or
claimed. Two-positional direct split dispatch remains separate planned work,
requiring a new frozen allocation/work comparison on this corrected behavior.
Other string/bytes methods and the planning-only regex migration are outside
this correction.
