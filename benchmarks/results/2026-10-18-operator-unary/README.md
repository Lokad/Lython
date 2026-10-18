# Share unary protocol dispatch, 2026-10-18 UTC

`operator.neg`, `operator.pos` and `operator.invert` now use the same unary
dispatcher as Python syntax, through both public execution APIs. Previously,
their direct numeric helpers rejected guest hooks; positive/negative complex
operands also failed. Six bounded probes on immutable **21a2dd91d8242609cbc79863c949c1f6cccf7980**
confirm five discrepancies on each API and one passing integer control against
isolated CPython. [Probe records](operator-unary-probes.json) preserve the inputs
and results; they measure correctness, not performance.

The [two-file runtime correction](candidate.patch) changes the three registrations
and the existing unary lookup twins. Numeric kernels and callable signatures are
unchanged. Binding already admits one argument, so the registrations also lose
three redundant arity wrappers and nested per-call lambdas. No new dispatcher,
binder, cache, owner, reservation or execution mode is introduced.

## Type slots and genuine suspension

The initial [registration-only correction](registration-only.patch) exposed a
shared lookup defect: instance attributes could shadow unary special methods.
The existing unary helpers now read the operand type's MRO and use the existing
descriptor binding functions. Implicit lookup bypasses the instance dictionary,
`__getattribute__` and `__getattr__`. Async execution awaits descriptor binding
before invoking the callback. Ordinary unary syntax, `abs` and complex conversion
share these helpers; their relevant controls remain in the validation selection.
Binary lookup is unchanged.

Delayed-host tests exercise all three callbacks, plus a descriptor whose `__get__`
performs actual host I/O. They verify suspension/cancellation, exactly-once effects
and accounting cleanup. Retained hook results stay funded across another execution;
disposal returns allowance to the compiled baseline and then zero. Denied
allocations still fail the command through a guest `finally` return and caller
catch, while preserving earlier file effects and awaited native cleanup.

## Validation and limits

The [baseline 74-case run](baseline-unary.trx) passes 53 and fails 21 new protocol,
complex, suspension, refusal and retained-result regressions. The
[registration-only run](corrected-unary.trx) passes 73 and fails the instance-shadow
case. The final selection adds descriptor/getattribute controls and extends type
lookup coverage: [all 77 scalar/protocol cases pass](final-unary.trx). Those added
controls were not separately executed on the baseline.

All **1,071 distinct final focused Release cases** pass: 77 scalar/protocol,
665 operator/binding/numeric, 67 descriptor/complex/integer, 89 ownership/admission,
62 complete-job/cancellation and 111 white-box cases. This includes 58 complete
CSV/text/JSON/file jobs. Original Stress scales, assertions and timeouts remain
unchanged. [Validation](validation.json) records process receipts and hashes.
All [36 source goldens](python-goldens.json) independently match isolated CPython
3.13.2 with `-I -S -X utf8`.

There is no timing, allocation comparison or speedup claim. Primitive `abs` call
packaging remains a separate planned task with its own fixed allocation/control
gate. Core dependencies and the planning-only regex migration are unchanged.
