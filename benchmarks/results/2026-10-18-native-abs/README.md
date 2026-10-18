# Native absolute-value calls, 2026-10-18 UTC

Ordinary executable `abs(value)` now reads an exact builtin integer, float or
boolean directly from existing operand storage. Exact shared-builtin identity
guards the path; saved builtin aliases qualify, replaced callables do not.
The existing single-input wrapper preserves interpreter admission, execution
checks, exception translation and cleanup. The numeric kernel is unchanged.
Complex/Decimal/timedelta, guest hooks, expanded/keyword and wrong-arity calls
keep their existing routes. No binder, cache, owner or execution mode is added.

Baseline is immutable **2a268ae82cbbcc2526a0c9eb8f05bc900e598f3c**; candidate is
the [frozen four-file runtime patch](candidate.patch), with permanent regressions.
It bypasses bound argument-array creation for eligible calls; small outer
argument arrays were already pooled. The general binder remains for other calls.

## Allocation comparison

| Route | Sync before / after | Async before / after | Saved per call |
| --- | ---: | ---: | ---: |
| Integer or boolean | 12,768 / 8,672 B | 12,744 / 8,648 B | 32 B |
| Float | 10,720 / 6,624 B | 10,696 / 6,600 B | 32 B |
| Expanded call control | 112,096 / 112,096 B | 121,288 / 121,288 B | 0 B |
| Protocol control | 8,672 / 8,672 B | 8,648 / 8,648 B | 0 B |

Totals are per **128 calls**, including the native loop and checksum. One
baseline and one candidate capture each use four warmups/eight samples. All
retained samples are flat; every primitive route allocates less and controls
match exactly. [Fixed gate](comparison.json), [baseline](baseline-native.trx),
[candidate](candidate-native.trx) and [diagnostic source](diagnostic.cs) preserve
the evidence. The synthetic guest-protocol control isolates dispatch; real
suspension/cancellation is checked separately through the public API.

This is an internal executable-loop allocation diagnostic in an existing context,
excluding frontend compilation, invocation setup, public projection and worker
scheduling. The local machine is Windows x64 build 26200, i7-14700KF, 28 logical
processors, with .NET host 10.0.13 / SDK 10.0.300-preview.0.26177.108.
[Environment](environment.json) records the details. This is not a quiet-machine
elapsed comparison. No elapsed-time, complete-job or CPython speedup is claimed.

## Semantic and containment checks

All **132 fuel/value/identity/error/span records** match, including positive
integer identity, signed zero, exhaustion and invalid operands. All **1,078
distinct final focused Release cases** pass, including 80 scalar/protocol cases,
113 white-box cases, 89 ownership/admission cases and 58 complete CSV/text/JSON/
file jobs. All [37 scalar source goldens](python-goldens.json) match isolated
CPython 3.13.2; this is semantic validation, not a performance baseline.

New controls cover native arguments produced after genuine suspension and
cancellation, negative large-integer admission before assignment, and positive/
negative retained integer results across another execution. Previous target
values and first refusal attribution survive denial. Cleanup returns allowance
to baseline; existing terminal-refusal, stack and protocol controls remain.
[Validation](validation.json) records hashes and process receipts.

The temporary diagnostic is removed from the test project. Existing call-frame,
binding and ownership implementations, numeric allocation preflights and core
dependencies remain unchanged. Regex migration remains planning only.
