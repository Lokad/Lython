# Direct string separator calls, 2026-10-18 UTC

Ordinary `text.split(separator)` now uses existing operand storage instead of
creating a receiver-bound method and packaging/binding its arguments. It shares
separator validation and the existing UTF-8 split implementation with general
calls. The former split-only zero-argument delegate is removed. General exposed,
keyword, expanded and custom-receiver calls remain supported; two-argument
maxsplit calls retain the general route. No new binder, storage mechanism or
public API is introduced.

| Native loop, 128 calls | Before bytes | After bytes | Saved per call |
| --- | ---: | ---: | ---: |
| Explicit separator, synchronous | 125,440 | 91,648 | 264 |
| Explicit separator, async API without suspension | 121,320 | 91,624 | 232 |
| Explicit None, synchronous | 125,440 | 91,648 | 264 |
| Explicit None, async API without suspension | 121,320 | 91,624 | 232 |
| Omitted argument, synchronous control | 91,648 | 91,648 | 0 |
| Omitted argument, async control | 91,624 | 91,624 | 0 |
| Exposed method, synchronous control | 121,352 | 121,352 | 0 |
| Exposed method, async control | 121,328 | 121,328 | 0 |

These are native execution allocations, including result storage and loop work,
not isolated binder allocations, elapsed-time improvements or CPython timing.
Each role used four warmups and eight captured samples with 128 distinct Unicode
receivers. All eligible samples fell below every baseline sample, and all omitted/
exposed control samples were identical. The fixed gate passed without repetition.
No whole-job capture, profile or long lane was run for this change.

## Source and measurement boundary

Baseline is immutable **217b388064fa644253843208ad90176f1e7cc977**; its runtime
source equals qualified `73deb8dc`. Candidate is the frozen [seven-file patch](candidate.patch)
on that baseline, plus focused test extensions. [Validation](validation.json)
records all production hashes and process/TRX receipts. The split-only delegate
and its dispatch case disappear; the inline bound separator body moves into one
shared scalar helper. Existing call frames, execution checks and exception
translation wrap the direct calls. UTF-8 splitting, result ownership, capacity
preflights, maxsplit conversion and general binding implementations are unchanged.

The temporary [diagnostic recipe](diagnostic.cs) runs inside the white-box
assembly on Windows 11, Intel Core i7-14700KF / 28 logical processors, .NET SDK
10.0.300-preview.0.26177.108 / runtime 10.0.13, ordinary JIT/GC. A completed
ValueTask and unchanged managed thread are required around
`GC.GetAllocatedBytesForCurrentThread`. Compilation, context construction,
fixture strings and reporting are outside the allocation interval. Distinct
receivers prevent repeatedly accessing one receiver from hiding packaging.
Native async samples do not measure actual suspension. Per-owner cleanup returns
the shared allowance to zero. The diagnostic was removed afterward.

[Baseline](baseline-diagnostic.trx), [candidate](candidate-diagnostic.trx),
[comparison](comparison.json) and process receipts preserve all eight native
series and **88 exact work records**. Both APIs keep identical results/errors,
messages, fuel counts, failure attribution and source spans across comma, None,
empty and invalid separators at eleven fuel limits. No timing claim follows.

## Focused validation

All **1,020 distinct final Release cases** pass: 149 white-box, 720 public semantics,
89 ownership/admission and 62 complete-job/cancellation cases. Existing scales,
assertions and timeouts remain. The 62 include 58 complete CSV/text/JSON/file jobs;
original Stress coverage remains separate from this focused iteration.

Coverage includes Unicode and multichar separators, empty/None/invalid values,
fresh lists and retained aliases, exposed/keyword/expanded/custom methods,
receiver rebinding and single evaluation. Suspended receiver/argument cases
validate success, cancellation and terminal admission without replay, premature
refund or unawaited cleanup. Existing shared-budget, retained-result/cache and
preflight tests pass. All 16 public examples independently match isolated
CPython 3.13.2 with `-I -S -X utf8`; [goldens](python-goldens.json) retain that
semantic check, separately from the primary external timing baseline.

This does not measure a reduction of the complete text job's CPython gap.
The authoritative [performance table](../../../BENCHMARKS.md) keeps that immutable
comparison separate. Exact tiny-budget thresholds, physical heap/RSS and
immediate reclamation after arbitrary reference withdrawal are not claimed.
