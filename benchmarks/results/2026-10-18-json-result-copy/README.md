# Small JSON result conversion, 2026-10-18 UTC

Eager JSON serialization and iterator chunks now share one finalizer. For results
up to 128 UTF-16 units, bounded managed spans replace the intermediate CLR string;
the existing UTF-8 factory admits and copies the retained result. Larger outputs
keep the existing funded UTF-16 copy. Escaping, traversal, hooks, chunks, fuel,
output growth accounting and ownership are unchanged. No new builder, encoder,
pool, owner, configuration or memory infrastructure is introduced.

Baseline: `32867face7e702ef6b31e2a4486a0c2ce97c9c03`. Candidate: that revision plus
the [frozen patch](candidate.patch), including the permanent admission and retained
result regressions. This is not a measurement of an unspecified later HEAD.
The [scope](scope.json) and allocation gate were frozen before production changes.

| Route / result | Bytes saved per call, Run | Bytes saved per call, RunAsync | Status |
| --- | ---: | ---: | --- |
| dumps, short ASCII / Unicode | 40 | 40 | All samples improve |
| dumps, escaped Unicode | 72 | 72 | All samples improve |
| dumps, 128 UTF-16 units / astral boundary | 280 | 280 | All samples improve |
| JSONEncoder.encode / scalar iterencode | 40 | 40 | All samples improve |
| Container iterencode | 120 | 120 | All samples improve |
| dumps / iterencode, 129 UTF-16 units | 0 | 0 | Control arrays identical |

One native before/after pair uses 4 warmups, 8 samples and 128 calls per sample,
20 routes in total. [Comparison](comparison.json), [diagnostic](diagnostic.cs),
[baseline raw TRX](baseline-native.trx), [candidate raw TRX](candidate-native.trx)
and process receipts retain the evidence. All 560 byte/chunk/error/message/span/
callback/fuel records match exactly. The native callbacks complete immediately;
real suspended callbacks and cancellation are covered by existing public checks.

Local Windows x64 build26200, Intel Core i7-14700KF (28 logical processors),
.NET SDK10.0.300-preview.0.26177.108/runtime10.0.13. Unchanged configuration from
earlier that day is retained in [environment](environment.json), with its original
capture timestamp. This boundary measures warm expression allocation using the
same context, excluding compilation, setup, scheduling, startup and public
projection. No elapsed-time, complete-job, CPython speedup or total pending-I/O
allocation claim. No retiming or complete-configuration comparison was performed.

The fixed spans total 640 bytes and do not escape the synchronous leaf. The
existing funded UTF-8 factory still admits metadata and result bytes before heap
allocation. Removing the small heap temporary can change tiny admission points;
retained aliases and pending users keep their existing ownership. The large-output
fallback and output builder remain funded. See [MEMORY.md](../../../MEMORY.md).

Validation: 788 distinct focused Release cases, including 58 complete CSV/text/
JSON/file jobs, managed stack admission, Unicode/chunks/hooks/cycles, actual
suspension/cancellation, terminal refusal and shared-owner cleanup. Twelve new
small-result admission cases pass on baseline and candidate; six new public
retained-result cases pass on candidate. Their full names, filters, receipt and
TRX hashes are in [validation](validation.json). No stress scales, assertions or
timeouts were relaxed. The temporary native diagnostic was removed. Cross-platform
and explicit Release consumer qualification is recorded in the delivery plan;
regex remains a separate plan-only migration.
